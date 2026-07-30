namespace Icod.CoreUtils.Shared.Ranges;

/// <summary>Contains either a parsed range set or a structured parsing error.</summary>
public sealed class RangeParseResult {

	private RangeParseResult(
		RangeSet? value,
		RangeParseError? error
	) {
		this.Value = value;
		this.Error = error;
	}

	/// <summary>Gets whether parsing succeeded.</summary>
	public bool IsSuccess => null != this.Value;

	/// <summary>Gets the parsed range set when parsing succeeded.</summary>
	public RangeSet? Value { get; }

	/// <summary>Gets the structured error when parsing failed.</summary>
	public RangeParseError? Error { get; }

	/// <summary>Creates a successful result.</summary>
	/// <param name="value">The parsed range set.</param>
	/// <returns>A successful parsing result.</returns>
	public static RangeParseResult Succeeded( RangeSet value ) {
		ArgumentNullException.ThrowIfNull( value );
		return new RangeParseResult( value, null );
	}

	/// <summary>Creates a failed result.</summary>
	/// <param name="error">The structured parsing error.</param>
	/// <returns>A failed parsing result.</returns>
	public static RangeParseResult Failed( RangeParseError error ) {
		ArgumentNullException.ThrowIfNull( error );
		return new RangeParseResult( null, error );
	}

}
