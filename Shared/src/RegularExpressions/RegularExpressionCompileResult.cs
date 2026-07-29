namespace Icod.CoreUtils.Shared.RegularExpressions;

/// <summary>Represents the controlled result of compiling a regular expression.</summary>
public sealed class RegularExpressionCompileResult {
	private RegularExpressionCompileResult(
		ICompiledRegularExpression? expression,
		RegularExpressionDiagnostic? diagnostic
	) {
		Expression = expression;
		Diagnostic = diagnostic;
	}

	/// <summary>Gets whether compilation succeeded.</summary>
	public bool IsSuccess => Expression is not null;

	/// <summary>Gets the compiled expression when compilation succeeded.</summary>
	public ICompiledRegularExpression? Expression { get; }

	/// <summary>Gets the deterministic diagnostic when compilation failed.</summary>
	public RegularExpressionDiagnostic? Diagnostic { get; }

	/// <summary>Creates a successful compilation result.</summary>
	/// <param name="expression">The compiled expression.</param>
	/// <returns>A successful result.</returns>
	public static RegularExpressionCompileResult Succeeded( ICompiledRegularExpression expression ) {
		ArgumentNullException.ThrowIfNull( expression );
		return new( expression, null );
	}

	/// <summary>Creates a failed compilation result.</summary>
	/// <param name="diagnostic">The deterministic compile diagnostic.</param>
	/// <returns>A failed result.</returns>
	public static RegularExpressionCompileResult Failed( RegularExpressionDiagnostic diagnostic ) {
		ArgumentNullException.ThrowIfNull( diagnostic );
		return new( null, diagnostic );
	}
}
