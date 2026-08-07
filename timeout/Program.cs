namespace Icod.CoreUtils.Timeout;

/// <summary>Provides the executable entry point for <c>timeout</c>.</summary>
public static class Program {
	/// <summary>Runs <c>timeout</c>.</summary>
	public static Task<int> Main(
		string[] args
	) => Command.RunAsync(
		args,
		forwardHostSignals: true
	);
}
