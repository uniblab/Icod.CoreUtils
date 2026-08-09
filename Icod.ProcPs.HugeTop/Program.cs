// Ported to .NET by Timothy J. Bruce <uniblab@hotmail.com>

namespace Icod.ProcPs.HugeTop;
/// <summary>Provides the procps-ng compatible <c>hugetop [options]</c> executable entry point.</summary>
public static class Program {
	/// <summary>Runs the <c>hugetop</c> command.</summary>
	/// <param name="args">Command-line arguments.</param>
	/// <returns>A task whose result is the process exit status.</returns>
	public static Task<int> Main( string[] args ) {
		ArgumentNullException.ThrowIfNull( args );
		return Command.RunAsync(
			args,
			stdout: Console.OpenStandardOutput(),
			stderr: Console.OpenStandardError()
		);
	}
}
