namespace Icod.CoreUtils.Shared.RegularExpressions;

/// <summary>Compiles GNU basic regular expressions with a fully managed leftmost-longest engine.</summary>
public sealed class GnuBasicRegularExpressionProvider : IRegularExpressionProvider {
	private readonly IRegularExpressionCharacterClassProvider characterClassProvider;

	/// <summary>Initializes a provider using current-culture Unicode classification and collation.</summary>
	public GnuBasicRegularExpressionProvider() : this( UnicodeRegularExpressionCharacterClassProvider.CurrentCulture ) {
	}

	/// <summary>Initializes a provider with an injectable character classification and collation policy.</summary>
	/// <param name="characterClassProvider">The character provider.</param>
	public GnuBasicRegularExpressionProvider( IRegularExpressionCharacterClassProvider characterClassProvider ) {
		ArgumentNullException.ThrowIfNull( characterClassProvider );
		this.characterClassProvider = characterClassProvider;
	}

	/// <summary>Gets a provider backed by the culture current when the property is read.</summary>
	public static GnuBasicRegularExpressionProvider Default => new();

	/// <inheritdoc/>
	public RegularExpressionCompileResult Compile(
		string pattern,
		RegularExpressionOptions? options = null,
		CancellationToken cancellationToken = default
	) {
		ArgumentNullException.ThrowIfNull( pattern );
		cancellationToken.ThrowIfCancellationRequested();
		options ??= new RegularExpressionOptions();
		ArgumentOutOfRangeException.ThrowIfNegativeOrZero( options.MaximumNestingDepth );
		ArgumentOutOfRangeException.ThrowIfNegativeOrZero( options.MaximumMatchStates );
		var parser = new GnuBasicRegularExpressionParser(
			pattern,
			options,
			characterClassProvider,
			cancellationToken
		);
		var parseResult = parser.Parse();
		var expression = parseResult.Expression;
		if ( expression is null ) {
			return RegularExpressionCompileResult.Failed( parseResult.Diagnostic! );
		}
		return RegularExpressionCompileResult.Succeeded(
			new GnuBasicCompiledRegularExpression(
				pattern,
				expression,
				parseResult.CaptureCount,
				options,
				characterClassProvider
			)
		);
	}

	/// <inheritdoc/>
	public ValueTask<RegularExpressionCompileResult> CompileAsync(
		string pattern,
		RegularExpressionOptions? options = null,
		CancellationToken cancellationToken = default
	) => ValueTask.FromResult( Compile( pattern, options, cancellationToken ) );
}
