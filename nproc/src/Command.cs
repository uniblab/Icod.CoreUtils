namespace Icod.CoreUtils.NProc;

using Icod.CommandFramework.Host;
using System.Globalization;

/// <summary>Provides the command boundary for GNU-compatible processor counting.</summary>
public static class Command {
	private const string VersionText = "nproc (Icod CoreUtils) 0.1.0";

	/// <summary>Runs <c>nproc</c> synchronously.</summary>
	/// <param name="args">The command-line arguments.</param>
	/// <param name="stdin">The standard-input reader, which is not used.</param>
	/// <param name="stdout">The standard-output writer.</param>
	/// <param name="stderr">The standard-error writer.</param>
	/// <param name="provider">The processor-resource provider.</param>
	/// <param name="environment">The OpenMP environment reader.</param>
	/// <returns>The process exit code.</returns>
	public static int Run(
		string[] args,
		TextReader? stdin = null,
		TextWriter? stdout = null,
		TextWriter? stderr = null,
		IProcessorResourceProvider? provider = null,
		INProcEnvironment? environment = null
	) => RunAsync(
		args,
		stdin,
		stdout,
		stderr,
		provider,
		environment
	).GetAwaiter().GetResult();

	/// <summary>Runs <c>nproc</c> asynchronously.</summary>
	/// <param name="args">The command-line arguments.</param>
	/// <param name="stdin">The standard-input reader, which is not used.</param>
	/// <param name="stdout">The standard-output writer.</param>
	/// <param name="stderr">The standard-error writer.</param>
	/// <param name="provider">The processor-resource provider.</param>
	/// <param name="environment">The OpenMP environment reader.</param>
	/// <param name="cancellationToken">A cancellation token.</param>
	/// <returns>The process exit code.</returns>
	public static async Task<int> RunAsync(
		string[] args,
		TextReader? stdin = null,
		TextWriter? stdout = null,
		TextWriter? stderr = null,
		IProcessorResourceProvider? provider = null,
		INProcEnvironment? environment = null,
		CancellationToken cancellationToken = default
	) {
		ArgumentNullException.ThrowIfNull( args );
		_ = stdin;
		stdout ??= Console.Out;
		stderr ??= Console.Error;

		NProcOptions options;
		try {
			options = NProcOptions.Parse( args );
		} catch ( NProcUsageException exception ) {
			await stderr.WriteLineAsync(
				string.Concat( "nproc: ", exception.Message )
			).ConfigureAwait( false );
			await stderr.WriteLineAsync(
				"Try 'nproc --help' for more information."
			).ConfigureAwait( false );
			return 1;
		}

		if ( options.Help ) {
			await stdout.WriteAsync( HelpText ).ConfigureAwait( false );
			return 0;
		}
		if ( options.Version ) {
			await stdout.WriteLineAsync( VersionText ).ConfigureAwait( false );
			return 0;
		}

		provider ??= SystemHostResourceProvider.Instance;
		environment ??= SystemNProcEnvironment.Instance;
		try {
			var snapshot = await provider.GetProcessorResourcesAsync( cancellationToken ).ConfigureAwait( false );
			var decision = NProcPolicy.Calculate( snapshot, options, environment );
			await stdout.WriteLineAsync(
				decision.ProcessorCount.ToString( CultureInfo.InvariantCulture )
			).ConfigureAwait( false );
			return 0;
		} catch ( OperationCanceledException ) when ( cancellationToken.IsCancellationRequested ) {
			await stderr.WriteLineAsync( "nproc: operation canceled" ).ConfigureAwait( false );
			return 1;
		} catch ( Exception exception ) {
			await stderr.WriteLineAsync(
				string.Concat( "nproc: cannot determine processor count: ", exception.Message )
			).ConfigureAwait( false );
			return 1;
		}
	}

	private static readonly string HelpText = """
Usage: nproc [OPTION]...
Print the number of processing units available to the current process,
which may be less than the number of online processors.

      --all       print the number of installed processors
      --ignore=N  if possible, exclude N processing units
      --help      display this help and exit
      --version   output version information and exit
""" + Environment.NewLine;
}
