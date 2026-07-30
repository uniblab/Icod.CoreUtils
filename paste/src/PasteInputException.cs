namespace Icod.CoreUtils.Paste;

/// <summary>Reports a paste input failure together with its user-facing source name.</summary>
internal sealed class PasteInputException : IOException {
	/// <summary>Initializes an input exception.</summary>
	/// <param name="displayName">The user-facing source name.</param>
	/// <param name="innerException">The original input exception.</param>
	internal PasteInputException( string displayName, Exception innerException )
		: base( innerException?.Message, innerException ) {
		this.DisplayName = displayName ?? throw new ArgumentNullException( nameof( displayName ) );
	}

	/// <summary>Gets the user-facing source name.</summary>
	internal string DisplayName { get; }
}
