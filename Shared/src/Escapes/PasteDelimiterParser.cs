namespace Icod.CoreUtils.Shared.Escapes;

using System.Buffers;
using System.Text;
using Icod.CommandFramework.Delimiters;

/// <summary>Parses the GNU <c>paste --delimiters</c> escape language into a separator cycle.</summary>
public static class PasteDelimiterParser {

	/// <summary>Parses one delimiter-list argument.</summary>
	/// <param name="value">The managed delimiter-list argument.</param>
	/// <param name="encoding">The stateless encoding used for ordinary Unicode scalars; deterministic UTF-8 is used by default.</param>
	/// <returns>A parsed cycle or a structured error result.</returns>
	public static PasteDelimiterParseResult Parse(
		string value,
		Encoding? encoding = null
	) {
		ArgumentNullException.ThrowIfNull( value );
		encoding ??= new UTF8Encoding(
			encoderShouldEmitUTF8Identifier: false,
			throwOnInvalidBytes: true
		);
		var separators = new List<ByteSeparator>();
		var diagnostics = new List<EscapeDiagnostic>();
		if ( 0 == value.Length ) {
			separators.Add( ByteSeparator.Empty );
			return new PasteDelimiterParseResult(
				new SeparatorCycle( separators ),
				diagnostics
			);
		}
		for ( var index = 0; index < value.Length; index++ ) {
			if ( '\\' != value[index] ) {
				if ( !TryEncodeRune( value, ref index, encoding, out var bytes ) ) {
					diagnostics.Add( InvalidScalar( index ) );
					return new PasteDelimiterParseResult( null, diagnostics );
				}
				separators.Add( new ByteSeparator( bytes ) );
				continue;
			}
			EscapeSequenceScanner.TryRead(
				value,
				ref index,
				out var sequence
			);
			if ( value.Length <= index ) {
				diagnostics.Add(
					new EscapeDiagnostic(
						EscapeDiagnosticCode.TrailingBackslash,
						EscapeDiagnosticSeverity.Error,
						sequence.BackslashOffset,
						1,
						"The delimiter list ends with an unescaped backslash."
					)
				);
				return new PasteDelimiterParseResult( null, diagnostics );
			}
			var current = value[index];
			switch ( current ) {
				case '0': separators.Add( ByteSeparator.Empty ); break;
				case 'b': separators.Add( Single( (byte)'\b' ) ); break;
				case 'f': separators.Add( Single( (byte)'\f' ) ); break;
				case 'n': separators.Add( Single( (byte)'\n' ) ); break;
				case 'r': separators.Add( Single( (byte)'\r' ) ); break;
				case 't': separators.Add( Single( (byte)'\t' ) ); break;
				case 'v': separators.Add( Single( (byte)'\v' ) ); break;
				case '\\': separators.Add( Single( (byte)'\\' ) ); break;
				default:
					if ( !TryEncodeRune( value, ref index, encoding, out var bytes ) ) {
						diagnostics.Add( InvalidScalar( index ) );
						return new PasteDelimiterParseResult( null, diagnostics );
					}
					separators.Add( new ByteSeparator( bytes ) );
					break;
			}
		}
		return new PasteDelimiterParseResult(
			new SeparatorCycle( separators ),
			diagnostics
		);
	}

	private static ByteSeparator Single( byte value ) => new( new[] { value } );

	private static EscapeDiagnostic InvalidScalar( int index ) => new(
		EscapeDiagnosticCode.InvalidUnicodeScalar,
		EscapeDiagnosticSeverity.Error,
		index,
		1,
		"The delimiter list contains an invalid UTF-16 scalar sequence."
	);

	private static bool TryEncodeRune(
		string value,
		ref int index,
		Encoding encoding,
		out byte[] bytes
	) {
		var status = Rune.DecodeFromUtf16(
			value.AsSpan( index ),
			out var rune,
			out var consumed
		);
		if ( OperationStatus.Done != status ) {
			bytes = [];
			return false;
		}
		index += consumed - 1;
		bytes = encoding.GetBytes( rune.ToString() );
		return true;
	}

}
