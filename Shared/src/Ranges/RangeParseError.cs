namespace Icod.CoreUtils.Shared.Ranges;

/// <summary>Describes one deterministic range-list parsing failure.</summary>
public sealed class RangeParseError {

	/// <summary>Initializes a range parsing error.</summary>
	/// <param name="code">The stable error category.</param>
	/// <param name="characterIndex">The zero-based source character index.</param>
	/// <param name="token">The offending source token.</param>
	/// <param name="message">A command-neutral explanatory message.</param>
	public RangeParseError(
		RangeParseErrorCode code,
		int characterIndex,
		string token,
		string message
	) {
		if ( !Enum.IsDefined( code ) ) {
			throw new ArgumentOutOfRangeException( nameof( code ) );
		}
		ArgumentNullException.ThrowIfNull( token );
		ArgumentNullException.ThrowIfNull( message );
		if ( characterIndex < 0 ) {
			throw new ArgumentOutOfRangeException( nameof( characterIndex ) );
		}
		this.Code = code;
		this.CharacterIndex = characterIndex;
		this.Token = token;
		this.Message = message;
	}

	/// <summary>Gets the stable error category.</summary>
	public RangeParseErrorCode Code { get; }

	/// <summary>Gets the zero-based source character index.</summary>
	public int CharacterIndex { get; }

	/// <summary>Gets the offending source token.</summary>
	public string Token { get; }

	/// <summary>Gets the command-neutral explanatory message.</summary>
	public string Message { get; }

}
