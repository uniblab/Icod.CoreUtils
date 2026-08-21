namespace Icod.CoreUtils.Tty;

using Icod.CommandFramework.Terminal;

/// <summary>Implements GNU-compatible terminal-name reporting for standard input.</summary>
public static class Command {
	private const int TerminalExitCode = 0;
	private const int NonTerminalExitCode = 1;
	private const int UsageExitCode = 2;
	private const int WriteFailureExitCode = 3;
	private const int IndeterminateExitCode = 4;
	private const string VersionText = "tty (Icod CoreUtils) 0.1.0";

	/// <summary>Runs <c>tty</c> synchronously.</summary>
	/// <param name="args">The command-line arguments.</param>
	/// <param name="stdout">The standard-output writer.</param>
	/// <param name="stderr">The standard-error writer.</param>
	/// <param name="provider">The terminal-control provider.</param>
	/// <returns>The process exit code.</returns>
	public static int Run(
		string[] args,
		TextWriter? stdout = null,
		TextWriter? stderr = null,
		ITerminalControlProvider? provider = null
	) => RunAsync( args, stdout, stderr, provider ).GetAwaiter().GetResult();

	/// <summary>Runs <c>tty</c> asynchronously.</summary>
	/// <param name="args">The command-line arguments.</param>
	/// <param name="stdout">The standard-output writer.</param>
	/// <param name="stderr">The standard-error writer.</param>
	/// <param name="provider">The terminal-control provider.</param>
	/// <param name="cancellationToken">A cancellation token.</param>
	/// <returns>The process exit code.</returns>
	public static async Task<int> RunAsync(
		string[] args,
		TextWriter? stdout = null,
		TextWriter? stderr = null,
		ITerminalControlProvider? provider = null,
		CancellationToken cancellationToken = default
	) {
		ArgumentNullException.ThrowIfNull( args );
		stdout ??= Console.Out;
		stderr ??= Console.Error;

		TtyOptions options;
		try {
			options = TtyOptions.Parse( args );
		} catch ( TtyUsageException exception ) {
			return await WriteUsageErrorAsync( stderr, exception.Message ).ConfigureAwait( false );
		}

		try {
			if ( options.Help ) {
				await stdout.WriteAsync( HelpText ).ConfigureAwait( false );
				return TerminalExitCode;
			}
			if ( options.Version ) {
				await stdout.WriteLineAsync( VersionText ).ConfigureAwait( false );
				return TerminalExitCode;
			}
		} catch ( Exception exception ) when ( IsWriteFailure( exception ) ) {
			return WriteFailureExitCode;
		}

		provider ??= SystemTerminalControlProvider.Instance;
		TerminalControlResult<TerminalEndpointObservation> result;
		try {
			cancellationToken.ThrowIfCancellationRequested();
			result = provider.Observe( TerminalEndpoint.StandardInput );
		} catch ( OperationCanceledException ) when ( cancellationToken.IsCancellationRequested ) {
			return await WriteIndeterminateAsync( stderr, options.Silent, "operation canceled" ).ConfigureAwait( false );
		} catch ( Exception exception ) {
			return await WriteIndeterminateAsync( stderr, options.Silent, exception.Message ).ConfigureAwait( false );
		}

		if ( !result.IsAvailable ) {
			return await WriteIndeterminateAsync(
				stderr,
				options.Silent,
				result.Message ?? "terminal identity is unavailable"
			).ConfigureAwait( false );
		}

		var observation = result.GetRequiredValue();
		if ( !observation.IsTerminal ) {
			if ( !options.Silent ) {
				try {
					await stdout.WriteLineAsync( "not a tty" ).ConfigureAwait( false );
				} catch ( Exception exception ) when ( IsWriteFailure( exception ) ) {
					return WriteFailureExitCode;
				}
			}
			return NonTerminalExitCode;
		}

		if ( string.IsNullOrWhiteSpace( observation.Pathname ) ) {
			return await WriteIndeterminateAsync(
				stderr,
				options.Silent,
				"terminal name is unavailable"
			).ConfigureAwait( false );
		}

		if ( !options.Silent ) {
			try {
				await stdout.WriteLineAsync( observation.Pathname ).ConfigureAwait( false );
			} catch ( Exception exception ) when ( IsWriteFailure( exception ) ) {
				return WriteFailureExitCode;
			}
		}
		return TerminalExitCode;
	}

	private static bool IsWriteFailure( Exception exception ) {
		return exception is IOException or ObjectDisposedException;
	}

	private static async Task<int> WriteUsageErrorAsync( TextWriter stderr, string message ) {
		try {
			await stderr.WriteLineAsync( string.Concat( "tty: ", message ) ).ConfigureAwait( false );
			await stderr.WriteLineAsync( "Try 'tty --help' for more information." ).ConfigureAwait( false );
		} catch ( Exception exception ) when ( IsWriteFailure( exception ) ) {
			return WriteFailureExitCode;
		}
		return UsageExitCode;
	}

	private static async Task<int> WriteIndeterminateAsync(
		TextWriter stderr,
		bool silent,
		string message
	) {
		if ( silent ) {
			return IndeterminateExitCode;
		}
		try {
			await stderr.WriteLineAsync( string.Concat( "tty: ", message ) ).ConfigureAwait( false );
		} catch ( Exception exception ) when ( IsWriteFailure( exception ) ) {
			return WriteFailureExitCode;
		}
		return IndeterminateExitCode;
	}

	private static readonly string HelpText = """
Usage: tty [OPTION]...
Print the file name of the terminal connected to standard input.

  -s, --silent, --quiet   print nothing, only return an exit status
      --help              display this help and exit
      --version           output version information and exit
""" + Environment.NewLine;
}

/// <summary>Represents parsed <c>tty</c> command-line options.</summary>
public sealed record TtyOptions {
	/// <summary>Gets whether status-only output was requested.</summary>
	public bool Silent { get; private init; }

	/// <summary>Gets whether help was requested.</summary>
	public bool Help { get; private init; }

	/// <summary>Gets whether version information was requested.</summary>
	public bool Version { get; private init; }

	/// <summary>Parses command-line options.</summary>
	/// <param name="args">The command-line arguments.</param>
	/// <returns>The parsed options.</returns>
	/// <exception cref="TtyUsageException">An option or operand is invalid.</exception>
	public static TtyOptions Parse( IReadOnlyList<string> args ) {
		ArgumentNullException.ThrowIfNull( args );
		var silent = false;
		var help = false;
		var version = false;
		var parsingOptions = true;
		foreach ( var argument in args ) {
			if ( parsingOptions && argument == "--" ) {
				parsingOptions = false;
				continue;
			}
			if ( parsingOptions && argument is "-s" or "--silent" or "--quiet" ) {
				silent = true;
				continue;
			}
			if ( parsingOptions && argument == "--help" ) {
				help = true;
				continue;
			}
			if ( parsingOptions && argument == "--version" ) {
				version = true;
				continue;
			}
			if ( parsingOptions && argument.StartsWith( '-' ) ) {
				throw new TtyUsageException( string.Concat( "unrecognized option '", argument, "'" ) );
			}
			throw new TtyUsageException( string.Concat( "extra operand '", argument, "'" ) );
		}
		return new TtyOptions { Silent = silent, Help = help, Version = version };
	}
}

/// <summary>Reports invalid <c>tty</c> command-line usage.</summary>
public sealed class TtyUsageException : Exception {
	/// <summary>Initializes a usage exception.</summary>
	/// <param name="message">The diagnostic message.</param>
	public TtyUsageException( string message ) : base( message ) { }
}
