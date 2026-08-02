namespace Icod.CoreUtils.Shared.FileSystem.Modes;

/// <summary>
/// Identifies a mode-expression parse failure.
/// </summary>
public enum FileModeParseErrorCode {
	/// <summary>No parse error.</summary>
	None = 0,
	/// <summary>The expression was empty.</summary>
	Empty = 1,
	/// <summary>An octal operand contains a non-octal digit.</summary>
	InvalidNumericDigit = 2,
	/// <summary>An octal operand exceeds portable mode bit octal 7777.</summary>
	NumericValueOutOfRange = 3,
	/// <summary>A symbolic clause contains an invalid subject.</summary>
	InvalidSubject = 4,
	/// <summary>A symbolic clause does not contain an operation.</summary>
	MissingOperator = 5,
	/// <summary>A symbolic operation contains an invalid permission.</summary>
	InvalidPermission = 6,
	/// <summary>A permission-copy letter is combined with other permission letters.</summary>
	InvalidPermissionCopy = 7,
	/// <summary>A comma introduced an empty clause.</summary>
	EmptyClause = 8
}

/// <summary>
/// Describes the result of parsing a mode expression.
/// </summary>
public sealed class FileModeParseResult {
	/// <summary>
	/// Initializes a structured parse result.
	/// </summary>
	/// <param name="expression">The parsed expression, when successful.</param>
	/// <param name="errorCode">The parse failure category.</param>
	/// <param name="errorOffset">The zero-based failure offset.</param>
	/// <param name="message">The user-facing diagnostic.</param>
	internal FileModeParseResult(
		FileModeExpression? expression,
		FileModeParseErrorCode errorCode,
		int errorOffset,
		string? message
	) {
		Expression = expression;
		ErrorCode = errorCode;
		ErrorOffset = errorOffset;
		Message = message;
	}

	/// <summary>Gets the parsed expression when parsing succeeded.</summary>
	public FileModeExpression? Expression { get; }

	/// <summary>Gets the parse-error code.</summary>
	public FileModeParseErrorCode ErrorCode { get; }

	/// <summary>Gets the zero-based error offset, or -1 when parsing succeeded.</summary>
	public int ErrorOffset { get; }

	/// <summary>Gets a user-facing parse diagnostic.</summary>
	public string? Message { get; }

	/// <summary>Gets whether parsing succeeded.</summary>
	public bool Succeeded => Expression is not null;
}

/// <summary>
/// Parses GNU numeric, operator-numeric, and symbolic mode expressions.
/// </summary>
public static class FileModeParser {
	/// <summary>
	/// Parses one mode expression.
	/// </summary>
	/// <param name="text">The expression text.</param>
	/// <returns>The structured parse result.</returns>
	public static FileModeParseResult Parse( string? text ) {
		if ( string.IsNullOrEmpty( text ) ) {
			return Failure( FileModeParseErrorCode.Empty, 0, "The mode expression is empty." );
		}

		if ( IsAllDecimalDigits( text ) ) {
			return ParseAbsoluteNumeric( text );
		}

		var clauses = new List<FileModeClause>();
		var clauseStart = 0;
		while ( clauseStart <= text.Length ) {
			var comma = text.IndexOf( ',', clauseStart );
			var clauseEnd = comma < 0 ? text.Length : comma;
			if ( clauseEnd == clauseStart ) {
				return Failure(
					FileModeParseErrorCode.EmptyClause,
					clauseStart,
					"A mode expression contains an empty clause."
				);
			}

			var clauseText = text[ clauseStart..clauseEnd ];
			var clauseResult = ParseClause( clauseText, clauseStart );
			if ( clauseResult.Error is not null ) {
				return clauseResult.Error;
			}
			clauses.Add( clauseResult.Clause! );

			if ( comma < 0 ) {
				break;
			}
			clauseStart = comma + 1;
		}

		return Success( new FileModeExpression( clauses ) );
	}

	/// <summary>
	/// Parses one mode expression and throws <see cref="FormatException"/> when invalid.
	/// </summary>
	/// <param name="text">The expression text.</param>
	/// <returns>The parsed mode expression.</returns>
	public static FileModeExpression ParseRequired( string text ) {
		var result = Parse( text );
		if ( !result.Succeeded ) {
			throw new FormatException( result.Message );
		}
		return result.Expression!;
	}

	private static FileModeParseResult ParseAbsoluteNumeric( string text ) {
		if ( !TryParseOctal( text, out var value, out var invalidOffset ) ) {
			return invalidOffset >= 0
				? Failure(
					FileModeParseErrorCode.InvalidNumericDigit,
					invalidOffset,
					"A numeric mode may contain only octal digits from 0 through 7."
				)
				: Failure(
					FileModeParseErrorCode.NumericValueOutOfRange,
					0,
					"A numeric mode exceeds octal 7777."
				);
		}
		return Success( new FileModeExpression( value, text.Length ) );
	}

	private static (FileModeClause? Clause, FileModeParseResult? Error) ParseClause(
		string text,
		int sourceOffset
	) {
		if ( TryParseOperatorNumericClause( text, sourceOffset, out var numericClause, out var numericError ) ) {
			return (numericClause, numericError);
		}

		var index = 0;
		var subjects = FileModeSubject.None;
		while ( index < text.Length && text[ index ] is 'u' or 'g' or 'o' or 'a' ) {
			subjects |= text[ index ] switch {
				'u' => FileModeSubject.User,
				'g' => FileModeSubject.Group,
				'o' => FileModeSubject.Other,
				'a' => FileModeSubject.All,
				_ => FileModeSubject.None
			};
			index++;
		}
		var omitted = subjects == FileModeSubject.None;
		if ( omitted ) {
			subjects = FileModeSubject.All;
		}

		if ( index >= text.Length ) {
			return (
				null,
				Failure(
					FileModeParseErrorCode.MissingOperator,
					sourceOffset + index,
					"A symbolic mode clause must contain '+', '-', or '='."
				)
			);
		}
		if ( !TryGetOperator( text[ index ], out _ ) ) {
			var invalidSubject = text[ index ] is not ('r' or 'w' or 'x' or 'X' or 's' or 't');
			return (
				null,
				invalidSubject
					? Failure(
						FileModeParseErrorCode.InvalidSubject,
						sourceOffset + index,
						"A symbolic mode clause contains an invalid subject."
					)
					: Failure(
						FileModeParseErrorCode.MissingOperator,
						sourceOffset + index,
						"A symbolic mode clause must contain '+', '-', or '='."
					)
			);
		}

		var operations = new List<FileModeOperation>();
		while ( index < text.Length ) {
			if ( !TryGetOperator( text[ index ], out var operation ) ) {
				return (
					null,
					Failure(
						FileModeParseErrorCode.InvalidPermission,
						sourceOffset + index,
						"A symbolic mode operation contains an invalid permission."
					)
				);
			}
			index++;
			var permissionsStart = index;
			while ( index < text.Length && !TryGetOperator( text[ index ], out _ ) ) {
				if ( text[ index ] is not ('r' or 'w' or 'x' or 'X' or 's' or 't' or 'u' or 'g' or 'o') ) {
					return (
						null,
						Failure(
							FileModeParseErrorCode.InvalidPermission,
							sourceOffset + index,
							"A symbolic mode operation contains an invalid permission."
						)
					);
				}
				index++;
			}

			var permissions = text[ permissionsStart..index ];
			var copyCount = permissions.Count( static character => character is 'u' or 'g' or 'o' );
			if ( copyCount > 0 && permissions.Length != 1 ) {
				return (
					null,
					Failure(
						FileModeParseErrorCode.InvalidPermissionCopy,
						sourceOffset + permissionsStart,
						"A permission-copy operation must contain exactly one of 'u', 'g', or 'o'."
					)
				);
			}
			operations.Add( new FileModeOperation( operation, permissions, null ) );
		}

		return (new FileModeClause( subjects, omitted, operations ), null);
	}

	private static bool TryParseOperatorNumericClause(
		string text,
		int sourceOffset,
		out FileModeClause? clause,
		out FileModeParseResult? error
	) {
		clause = null;
		error = null;
		if ( text.Length < 2 || !TryGetOperator( text[ 0 ], out var operation ) ) {
			return false;
		}
		if ( !IsAllDecimalDigits( text.AsSpan( 1 ) ) ) {
			return false;
		}
		if ( !TryParseOctal( text[ 1.. ], out var value, out var invalidOffset ) ) {
			error = invalidOffset >= 0
				? Failure(
					FileModeParseErrorCode.InvalidNumericDigit,
					sourceOffset + 1 + invalidOffset,
					"An operator-numeric mode may contain only octal digits from 0 through 7."
				)
				: Failure(
					FileModeParseErrorCode.NumericValueOutOfRange,
					sourceOffset + 1,
					"An operator-numeric mode exceeds octal 7777."
				);
			return true;
		}
		clause = new FileModeClause(
			FileModeSubject.All,
			false,
			new[] { new FileModeOperation( operation, string.Empty, value ) }
		);
		return true;
	}

	private static bool TryParseOctal( string text, out int value, out int invalidOffset ) {
		value = 0;
		invalidOffset = -1;
		for ( var index = 0; index < text.Length; index++ ) {
			var character = text[ index ];
			if ( character is < '0' or > '7' ) {
				invalidOffset = index;
				return false;
			}
			if ( value > 0x0fff / 8 ) {
				return false;
			}
			value = (value * 8) + (character - '0');
			if ( value > 0x0fff ) {
				return false;
			}
		}
		return true;
	}

	private static bool IsAllDecimalDigits( string text ) => IsAllDecimalDigits( text.AsSpan() );

	private static bool IsAllDecimalDigits( ReadOnlySpan<char> text ) {
		foreach ( var character in text ) {
			if ( character is < '0' or > '9' ) {
				return false;
			}
		}
		return text.Length > 0;
	}

	private static bool TryGetOperator( char character, out FileModeOperator operation ) {
		operation = character switch {
			'+' => FileModeOperator.Add,
			'-' => FileModeOperator.Remove,
			'=' => FileModeOperator.Assign,
			_ => FileModeOperator.Add
		};
		return character is '+' or '-' or '=';
	}

	private static FileModeParseResult Success( FileModeExpression expression ) {
		return new FileModeParseResult( expression, FileModeParseErrorCode.None, -1, null );
	}

	private static FileModeParseResult Failure(
		FileModeParseErrorCode errorCode,
		int errorOffset,
		string message
	) {
		return new FileModeParseResult( null, errorCode, errorOffset, message );
	}
}
