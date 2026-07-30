namespace Icod.CoreUtils.Shared.RegularExpressions;

/// <summary>Compiles GNU Emacs regular expressions with the Shared fully managed leftmost-longest engine.</summary>
public sealed class GnuEmacsRegularExpressionProvider : IRegularExpressionProvider {
	private readonly IRegularExpressionCharacterClassProvider characterClassProvider;

	/// <summary>Initializes a provider using current-culture Unicode classification and collation.</summary>
	public GnuEmacsRegularExpressionProvider() : this( UnicodeRegularExpressionCharacterClassProvider.CurrentCulture ) {
	}

	/// <summary>Initializes a provider with an injectable character classification and collation policy.</summary>
	/// <param name="characterClassProvider">The character provider.</param>
	public GnuEmacsRegularExpressionProvider(
		IRegularExpressionCharacterClassProvider characterClassProvider
	) {
		ArgumentNullException.ThrowIfNull( characterClassProvider );
		this.characterClassProvider = characterClassProvider;
	}

	/// <summary>Gets a provider backed by the culture current when the property is read.</summary>
	public static GnuEmacsRegularExpressionProvider Default => new();

	/// <inheritdoc/>
	public RegularExpressionCompileResult Compile(
		string pattern,
		RegularExpressionOptions? options = null,
		CancellationToken cancellationToken = default
	) {
		ArgumentNullException.ThrowIfNull( pattern );
		cancellationToken.ThrowIfCancellationRequested();
		var effective = ( options ?? new RegularExpressionOptions() ) with {
			Syntax = GnuRegularExpressionSyntax.Emacs,
			AllowEmptyRanges = true,
			AllowInvalidRepetitionOperators = true
		};
		ArgumentOutOfRangeException.ThrowIfNegativeOrZero( effective.MaximumNestingDepth );
		ArgumentOutOfRangeException.ThrowIfNegativeOrZero( effective.MaximumMatchStates );
		var parser = new GnuBasicRegularExpressionParser(
			pattern,
			effective,
			this.characterClassProvider,
			cancellationToken
		);
		var parseResult = parser.Parse();
		if ( null == parseResult.Expression ) {
			return RegularExpressionCompileResult.Failed( parseResult.Diagnostic! );
		}
		return RegularExpressionCompileResult.Succeeded(
			new GnuBasicCompiledRegularExpression(
				pattern,
				parseResult.Expression,
				parseResult.CaptureCount,
				effective,
				this.characterClassProvider
			)
		);
	}

	/// <inheritdoc/>
	public ValueTask<RegularExpressionCompileResult> CompileAsync(
		string pattern,
		RegularExpressionOptions? options = null,
		CancellationToken cancellationToken = default
	) => ValueTask.FromResult( this.Compile( pattern, options, cancellationToken ) );
}
