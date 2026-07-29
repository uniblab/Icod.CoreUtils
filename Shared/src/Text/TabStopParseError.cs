namespace Icod.CoreUtils.Shared.Text;

/// <summary>Describes the first deterministic error found while parsing tab stops.</summary>
public sealed class TabStopParseError {
	/// <summary>Initializes a new instance of the <see cref="TabStopParseError"/> class.</summary>
	/// <param name="code">The error code.</param>
	/// <param name="message">The invariant diagnostic message.</param>
	/// <param name="specificationIndex">The zero-based specification index, or minus one when none was supplied.</param>
	/// <param name="characterIndex">The zero-based character index within the specification, or minus one when no single character caused the error.</param>
	/// <param name="token">The offending token, when one exists.</param>
	internal TabStopParseError(
		TabStopParseErrorCode code,
		string message,
		int specificationIndex,
		int characterIndex,
		string? token
	) {
		this.Code = code;
		this.Message = message;
		this.SpecificationIndex = specificationIndex;
		this.CharacterIndex = characterIndex;
		this.Token = token;
	}

	/// <summary>Gets the zero-based character index within the offending specification, or minus one when unavailable.</summary>
	public int CharacterIndex {
		get;
	}

	/// <summary>Gets the stable error code.</summary>
	public TabStopParseErrorCode Code {
		get;
	}

	/// <summary>Gets the invariant diagnostic message.</summary>
	public string Message {
		get;
	}

	/// <summary>Gets the zero-based offending specification index, or minus one when none was supplied.</summary>
	public int SpecificationIndex {
		get;
	}

	/// <summary>Gets the offending token, when one exists.</summary>
	public string? Token {
		get;
	}
}
