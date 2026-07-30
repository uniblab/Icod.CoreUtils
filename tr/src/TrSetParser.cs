namespace Icod.CoreUtils.Tr;

using System.Text;
using Icod.CoreUtils.Shared.Escapes;

/// <summary>Contains the result of parsing one <c>tr</c> set expression.</summary>
internal sealed class TrSetParseResult {
	/// <summary>Initializes a set-expression parse result.</summary>
	/// <param name="expression">The parsed expression, or <see langword="null"/> after failure.</param>
	/// <param name="diagnostics">Low-level escape warnings and errors.</param>
	/// <param name="error">The command-specific grammar error, or <see langword="null"/>.</param>
	public TrSetParseResult(
		TrSetExpression? expression,
		IReadOnlyList<EscapeDiagnostic> diagnostics,
		string? error
	) {
		this.Expression = expression;
		this.Diagnostics = diagnostics ?? throw new ArgumentNullException( nameof( diagnostics ) );
		this.Error = error;
	}

	/// <summary>Gets the parsed expression.</summary>
	public TrSetExpression? Expression { get; }

	/// <summary>Gets low-level escape diagnostics.</summary>
	public IReadOnlyList<EscapeDiagnostic> Diagnostics { get; }

	/// <summary>Gets the command-specific grammar error.</summary>
	public string? Error { get; }

	/// <summary>Gets whether parsing completed without an error.</summary>
	public bool IsSuccess => null != this.Expression && null == this.Error
		&& !this.Diagnostics.Any( value => EscapeDiagnosticSeverity.Error == value.Severity );
}

/// <summary>Parses ranges, classes, equivalence classes, and repeats over Shared escape parsing.</summary>
internal static class TrSetParser {
	private const ulong MaximumRepeatCount = ulong.MaxValue - 1;

	private static readonly IReadOnlyDictionary<string, TrCharacterClass> CharacterClasses =
		new Dictionary<string, TrCharacterClass>( StringComparer.Ordinal ) {
			["alnum"] = TrCharacterClass.Alnum,
			["alpha"] = TrCharacterClass.Alpha,
			["blank"] = TrCharacterClass.Blank,
			["cntrl"] = TrCharacterClass.Cntrl,
			["digit"] = TrCharacterClass.Digit,
			["graph"] = TrCharacterClass.Graph,
			["lower"] = TrCharacterClass.Lower,
			["print"] = TrCharacterClass.Print,
			["punct"] = TrCharacterClass.Punct,
			["space"] = TrCharacterClass.Space,
			["upper"] = TrCharacterClass.Upper,
			["xdigit"] = TrCharacterClass.XDigit
		};

	/// <summary>Parses a complete set expression.</summary>
	/// <param name="value">The managed command-line operand.</param>
	/// <returns>The parse result.</returns>
	public static TrSetParseResult Parse( string value ) {
		ArgumentNullException.ThrowIfNull( value );
		var escaped = TrByteEscapeParser.Parse( value );
		if ( !escaped.IsSuccess ) {
			return new TrSetParseResult( null, escaped.Diagnostics, "invalid byte escape in set expression" );
		}
		var elements = new List<TrSetElement>();
		var bytes = escaped.Bytes;
		var index = 0;
		while ( index < bytes.Count ) {
			if ( TryParseBracketConstruct( bytes, ref index, elements, out var error ) ) {
				if ( null != error ) {
					return new TrSetParseResult( null, escaped.Diagnostics, error );
				}
				continue;
			}
			if ( index + 2 < bytes.Count
				&& !bytes[index + 1].WasEscaped
				&& bytes[index + 1].Value == (byte)'-' ) {
				var first = bytes[index].Value;
				var last = bytes[index + 2].Value;
				if ( last < first ) {
					return new TrSetParseResult(
						null,
						escaped.Diagnostics,
						string.Concat(
							"range endpoints of '",
							Printable( first ),
							"-",
							Printable( last ),
							"' are in reverse order"
						)
					);
				}
				elements.Add( TrSetElement.Range( first, last ) );
				index += 3;
				continue;
			}
			elements.Add( TrSetElement.Literal( bytes[index].Value ) );
			index++;
		}
		return new TrSetParseResult( new TrSetExpression( elements ), escaped.Diagnostics, null );
	}

	private static bool TryParseBracketConstruct(
		IReadOnlyList<EscapedByte> bytes,
		ref int index,
		List<TrSetElement> elements,
		out string? error
	) {
		error = null;
		if ( !Matches( bytes, index, '[' ) || index + 2 >= bytes.Count ) {
			return false;
		}
		if ( Matches( bytes, index + 1, ':' ) ) {
			var close = FindDelimitedClose( bytes, index + 2, ':' );
			if ( 0 <= close ) {
				if ( close == index + 2 ) {
					error = "missing character class name '[::]'";
					return true;
				}
				var name = AsAscii( bytes, index + 2, close - index - 2, allowEscaped: true );
				if ( null != name && CharacterClasses.TryGetValue( name, out var characterClass ) ) {
					elements.Add( TrSetElement.Class( characterClass ) );
					index = close + 2;
					return true;
				}
				if ( IsRepeatFallback( bytes, index + 2 )
					&& TryParseRepeatConstruct( bytes, ref index, elements, out error ) ) {
					return true;
				}
				error = string.Concat( "invalid character class '[:", name ?? "?", ":]'" );
				return true;
			}
		}
		if ( Matches( bytes, index + 1, '=' ) ) {
			var close = FindDelimitedClose( bytes, index + 2, '=' );
			if ( 0 <= close ) {
				var length = close - index - 2;
				if ( 0 == length ) {
					error = "missing equivalence class character '[==]'";
					return true;
				}
				if ( 1 == length ) {
					elements.Add( TrSetElement.Equivalence( bytes[index + 2].Value ) );
					index = close + 2;
					return true;
				}
				if ( IsRepeatFallback( bytes, index + 2 )
					&& TryParseRepeatConstruct( bytes, ref index, elements, out error ) ) {
					return true;
				}
				error = "equivalence class operand must be a single byte";
				return true;
			}
		}
		return TryParseRepeatConstruct( bytes, ref index, elements, out error );
	}

	private static bool IsRepeatFallback( IReadOnlyList<EscapedByte> bytes, int index ) {
		if ( !Matches( bytes, index, '*' ) ) {
			return false;
		}
		for ( var current = index + 1; current < bytes.Count; current++ ) {
			var item = bytes[current];
			if ( item.WasEscaped || item.Value is < (byte)'0' or > (byte)'9' ) {
				return Matches( bytes, current, ']' );
			}
		}
		return false;
	}

	private static bool TryParseRepeatConstruct(
		IReadOnlyList<EscapedByte> bytes,
		ref int index,
		List<TrSetElement> elements,
		out string? error
	) {
		error = null;
		if ( index + 3 >= bytes.Count || !Matches( bytes, index + 2, '*' ) ) {
			return false;
		}
		var close = FindUnescaped( bytes, index + 3, ']' );
		if ( close < 0 || ContainsEscapedByte( bytes, index + 3, close - index - 3 ) ) {
			return false;
		}
		var countText = AsAscii( bytes, index + 3, close - index - 3, allowEscaped: false );
		if ( null == countText || countText.Any( value => value is < '0' or > '9' ) ) {
			error = "invalid repeat count in bracket expression";
			return true;
		}
		if ( !TryParseRepeatCount( countText, out var count, out var indefinite ) ) {
			error = "invalid repeat count in bracket expression";
			return true;
		}
		elements.Add( TrSetElement.Repeat( bytes[index + 1].Value, count, indefinite ) );
		index = close + 1;
		return true;
	}

	private static int FindDelimitedClose(
		IReadOnlyList<EscapedByte> bytes,
		int start,
		char delimiter
	) {
		for ( var index = start; index + 1 < bytes.Count; index++ ) {
			if ( Matches( bytes, index, delimiter ) && Matches( bytes, index + 1, ']' ) ) {
				return index;
			}
		}
		return -1;
	}

	private static int FindUnescaped( IReadOnlyList<EscapedByte> bytes, int start, char value ) {
		for ( var index = start; index < bytes.Count; index++ ) {
			if ( Matches( bytes, index, value ) ) {
				return index;
			}
		}
		return -1;
	}

	private static bool Matches( IReadOnlyList<EscapedByte> bytes, int index, char value ) =>
		0 <= index && index < bytes.Count && !bytes[index].WasEscaped && bytes[index].Value == (byte)value;

	private static string? AsAscii(
		IReadOnlyList<EscapedByte> bytes,
		int start,
		int length,
		bool allowEscaped
	) {
		var builder = new StringBuilder( length );
		for ( var index = 0; index < length; index++ ) {
			var item = bytes[start + index];
			if ( ( !allowEscaped && item.WasEscaped ) || 127 < item.Value ) {
				return null;
			}
			builder.Append( (char)item.Value );
		}
		return builder.ToString();
	}

	private static bool ContainsEscapedByte(
		IReadOnlyList<EscapedByte> bytes,
		int start,
		int length
	) {
		for ( var index = 0; index < length; index++ ) {
			if ( bytes[start + index].WasEscaped ) {
				return true;
			}
		}
		return false;
	}

	private static bool TryParseRepeatCount( string value, out ulong count, out bool indefinite ) {
		count = 0;
		indefinite = string.IsNullOrEmpty( value );
		if ( indefinite ) {
			return true;
		}
		var numberBase = value.Length > 1 && value[0] == '0' ? 8UL : 10UL;
		try {
			foreach ( var character in value ) {
				var digit = (ulong)( character - '0' );
				if ( digit >= numberBase ) {
					return false;
				}
				count = checked( count * numberBase + digit );
			}
		} catch ( OverflowException ) {
			return false;
		}
		if ( MaximumRepeatCount < count ) {
			return false;
		}
		indefinite = 0 == count;
		return true;
	}

	private static string Printable( byte value ) => value switch {
		(byte)'\\' => "\\\\",
		(byte)'\a' => "\\a",
		(byte)'\b' => "\\b",
		(byte)'\f' => "\\f",
		(byte)'\n' => "\\n",
		(byte)'\r' => "\\r",
		(byte)'\t' => "\\t",
		(byte)'\v' => "\\v",
		>= 32 and <= 126 => ((char)value).ToString(),
		_ => string.Concat( "\\", Convert.ToString( value, 8 ).PadLeft( 3, '0' ) )
	};
}
