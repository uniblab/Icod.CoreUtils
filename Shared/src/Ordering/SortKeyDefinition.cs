namespace Icod.CoreUtils.Shared.Ordering;

/// <summary>Identifies a field and optional character position in a GNU sort-key specification.</summary>
public sealed class SortKeyPosition {
	/// <summary>Initializes a sort-key position.</summary>
	/// <param name="fieldNumber">The one-based field number.</param>
	/// <param name="characterOffset">The explicit character offset, or <see langword="null"/> for the endpoint default.</param>
	/// <param name="skipLeadingBlanks">Whether leading blanks are skipped at this endpoint.</param>
	public SortKeyPosition(
		int fieldNumber,
		int? characterOffset,
		bool skipLeadingBlanks
	) {
		ArgumentOutOfRangeException.ThrowIfNegativeOrZero( fieldNumber );
		if ( 0 > characterOffset ) {
			throw new ArgumentOutOfRangeException( nameof( characterOffset ) );
		}
		this.FieldNumber = fieldNumber;
		this.CharacterOffset = characterOffset;
		this.SkipLeadingBlanks = skipLeadingBlanks;
	}

	/// <summary>Gets the one-based field number.</summary>
	public int FieldNumber { get; }

	/// <summary>Gets the explicit character offset, or <see langword="null"/> for the endpoint default.</summary>
	/// <remarks>An end offset of zero denotes the end of the field. A start offset is always positive.</remarks>
	public int? CharacterOffset { get; }

	/// <summary>Gets whether leading blanks are skipped at this endpoint.</summary>
	public bool SkipLeadingBlanks { get; }
}

/// <summary>Represents the syntax shared by GNU <c>-k</c> sort-key specifications.</summary>
public sealed class SortKeyDefinition {
	/// <summary>Initializes a parsed sort-key definition.</summary>
	/// <param name="start">The inclusive start endpoint.</param>
	/// <param name="end">The inclusive end endpoint, or <see langword="null"/> for the end of the record.</param>
	/// <param name="options">The normalized key comparison option letters other than endpoint-specific <c>b</c>.</param>
	public SortKeyDefinition(
		SortKeyPosition start,
		SortKeyPosition? end,
		string options
	) {
		ArgumentNullException.ThrowIfNull( start );
		ArgumentNullException.ThrowIfNull( options );
		this.Start = start;
		this.End = end;
		this.Options = options;
	}

	/// <summary>Gets the inclusive start endpoint.</summary>
	public SortKeyPosition Start { get; }

	/// <summary>Gets the inclusive end endpoint, or <see langword="null"/> for the end of the record.</summary>
	public SortKeyPosition? End { get; }

	/// <summary>Gets normalized key comparison option letters other than endpoint-specific <c>b</c>.</summary>
	public string Options { get; }
}

/// <summary>Identifies a deterministic sort-key parsing failure.</summary>
public enum SortKeyParseErrorCode {
	/// <summary>No error occurred.</summary>
	None,

	/// <summary>The specification was empty.</summary>
	EmptySpecification,

	/// <summary>A field number was missing.</summary>
	MissingFieldNumber,

	/// <summary>A numeric component was invalid or outside the supported range.</summary>
	InvalidNumber,

	/// <summary>A start character offset was zero.</summary>
	InvalidStartCharacterOffset,

	/// <summary>An unsupported option letter was present.</summary>
	UnknownOption,

	/// <summary>More than one endpoint separator was present.</summary>
	MultipleEndpointSeparators,

	/// <summary>The end endpoint after a comma was empty.</summary>
	MissingEndPosition
}

/// <summary>Describes a complete sort-key parse operation.</summary>
/// <param name="IsSuccess">Whether parsing succeeded.</param>
/// <param name="Definition">The parsed definition when successful.</param>
/// <param name="ErrorCode">The structured error code.</param>
/// <param name="ErrorOffset">The zero-based source offset of the failure.</param>
/// <param name="ErrorMessage">The controlled failure message.</param>
public sealed record SortKeyParseResult(
	bool IsSuccess,
	SortKeyDefinition? Definition,
	SortKeyParseErrorCode ErrorCode,
	int ErrorOffset,
	string? ErrorMessage
) {
	/// <summary>Creates a successful parse result.</summary>
	/// <param name="definition">The parsed definition.</param>
	/// <returns>The successful result.</returns>
	public static SortKeyParseResult Succeeded( SortKeyDefinition definition ) {
		ArgumentNullException.ThrowIfNull( definition );
		return new( true, definition, SortKeyParseErrorCode.None, -1, null );
	}

	/// <summary>Creates an unsuccessful parse result.</summary>
	/// <param name="errorCode">The structured error code.</param>
	/// <param name="errorOffset">The zero-based source offset.</param>
	/// <param name="errorMessage">The controlled failure message.</param>
	/// <returns>The unsuccessful result.</returns>
	public static SortKeyParseResult Failed(
		SortKeyParseErrorCode errorCode,
		int errorOffset,
		string errorMessage
	) {
		if ( SortKeyParseErrorCode.None == errorCode ) {
			throw new ArgumentException( "A failure must have a nonzero error code.", nameof( errorCode ) );
		}
		ArgumentOutOfRangeException.ThrowIfNegative( errorOffset );
		ArgumentException.ThrowIfNullOrWhiteSpace( errorMessage );
		return new( false, null, errorCode, errorOffset, errorMessage );
	}
}
