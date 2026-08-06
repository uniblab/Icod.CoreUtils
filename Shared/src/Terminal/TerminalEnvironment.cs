namespace Icod.CoreUtils.Shared.Terminal;

/// <summary>
/// Captures the process-environment inputs used by directory-listing and
/// <c>dircolors</c> presentation policy.
/// </summary>
public sealed class TerminalEnvironmentSnapshot {
	/// <summary>
	/// Initializes a terminal-environment snapshot.
	/// </summary>
	/// <param name="term">The <c>TERM</c> value.</param>
	/// <param name="colorTerm">The <c>COLORTERM</c> value.</param>
	/// <param name="columns">The <c>COLUMNS</c> value.</param>
	/// <param name="lines">The <c>LINES</c> value.</param>
	/// <param name="shell">The <c>SHELL</c> value.</param>
	/// <param name="quotingStyle">The <c>QUOTING_STYLE</c> value.</param>
	public TerminalEnvironmentSnapshot(
		string? term,
		string? colorTerm,
		string? columns,
		string? lines,
		string? shell,
		string? quotingStyle
	) {
		this.Term = Normalize( term );
		this.ColorTerm = Normalize( colorTerm );
		this.Columns = Normalize( columns );
		this.Lines = Normalize( lines );
		this.Shell = Normalize( shell );
		this.QuotingStyle = Normalize( quotingStyle );
		this.TerminalNames = CreateTerminalNames( this.Term );
	}

	/// <summary>Gets the normalized <c>TERM</c> value.</summary>
	public string? Term {
		get;
	}

	/// <summary>Gets the normalized <c>COLORTERM</c> value.</summary>
	public string? ColorTerm {
		get;
	}

	/// <summary>Gets the normalized <c>COLUMNS</c> value.</summary>
	public string? Columns {
		get;
	}

	/// <summary>Gets the normalized <c>LINES</c> value.</summary>
	public string? Lines {
		get;
	}

	/// <summary>Gets the normalized <c>SHELL</c> value.</summary>
	public string? Shell {
		get;
	}

	/// <summary>Gets the normalized <c>QUOTING_STYLE</c> value.</summary>
	public string? QuotingStyle {
		get;
	}

	/// <summary>
	/// Gets the terminal name supplied by <c>TERM</c> as a zero- or one-item
	/// list suitable for later <c>dircolors</c> selector evaluation.
	/// </summary>
	public IReadOnlyList<string> TerminalNames {
		get;
	}

	/// <summary>
	/// Captures the recognized values from an injectable environment provider.
	/// </summary>
	/// <param name="provider">The environment provider.</param>
	/// <returns>An immutable terminal-environment snapshot.</returns>
	/// <exception cref="ArgumentNullException">
	/// <paramref name="provider"/> is <see langword="null"/>.
	/// </exception>
	public static TerminalEnvironmentSnapshot Capture(
		IEnvironmentVariableProvider provider
	) {
		ArgumentNullException.ThrowIfNull( provider );
		return new TerminalEnvironmentSnapshot(
			provider.GetValue( "TERM" ),
			provider.GetValue( "COLORTERM" ),
			provider.GetValue( "COLUMNS" ),
			provider.GetValue( "LINES" ),
			provider.GetValue( "SHELL" ),
			provider.GetValue( "QUOTING_STYLE" )
		);
	}

	/// <summary>
	/// Parses a positive decimal dimension from an environment value.
	/// </summary>
	/// <param name="value">The candidate value.</param>
	/// <param name="dimension">The parsed positive dimension.</param>
	/// <returns><see langword="true"/> when parsing succeeded.</returns>
	public static bool TryParsePositiveDimension(
		string? value,
		out int dimension
	) {
		return int.TryParse(
			value,
			System.Globalization.NumberStyles.None,
			System.Globalization.CultureInfo.InvariantCulture,
			out dimension
		) && ( 0 < dimension );
	}

	private static string? Normalize(
		string? value
	) {
		if ( string.IsNullOrWhiteSpace( value ) ) {
			return null;
		}
		return value.Trim();
	}

	private static IReadOnlyList<string> CreateTerminalNames(
		string? term
	) {
		return null is term
			? Array.Empty<string>()
			: Array.AsReadOnly( new[] { term! } );
	}
}
