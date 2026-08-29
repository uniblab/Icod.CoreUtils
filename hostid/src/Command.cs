namespace Icod.CoreUtils.HostId;

using Icod.Host;

/// <summary>Provides the command boundary for GNU-compatible host identification.</summary>
public static class Command {
	private const string VersionText = "hostid (Icod CoreUtils) 0.1.0";

	/// <summary>Runs <c>hostid</c> synchronously.</summary>
	/// <param name="args">The command-line arguments.</param>
	/// <param name="stdin">The standard-input reader, which is not used.</param>
	/// <param name="stdout">The standard-output writer.</param>
	/// <param name="stderr">The standard-error writer.</param>
	/// <param name="provider">The host-identifier provider.</param>
	/// <returns>The process exit code.</returns>
	public static int Run(
		string[] args,
		TextReader? stdin = null,
		TextWriter? stdout = null,
		TextWriter? stderr = null,
		IHostIdentifierProvider? provider = null
	) => RunAsync(
		args,
		stdin,
		stdout,
		stderr,
		provider
	).GetAwaiter().GetResult();

	/// <summary>Runs <c>hostid</c> asynchronously.</summary>
	/// <param name="args">The command-line arguments.</param>
	/// <param name="stdin">The standard-input reader, which is not used.</param>
	/// <param name="stdout">The standard-output writer.</param>
	/// <param name="stderr">The standard-error writer.</param>
	/// <param name="provider">The host-identifier provider.</param>
	/// <param name="cancellationToken">A cancellation token.</param>
	/// <returns>The process exit code.</returns>
	public static async Task<int> RunAsync(
		string[] args,
		TextReader? stdin = null,
		TextWriter? stdout = null,
		TextWriter? stderr = null,
		IHostIdentifierProvider? provider = null,
		CancellationToken cancellationToken = default
	) {
		ArgumentNullException.ThrowIfNull( args );
		_ = stdin;
		stdout ??= Console.Out;
		stderr ??= Console.Error;

		HostIdOptions options;
		try {
			options = HostIdOptions.Parse( args );
		} catch ( HostIdUsageException exception ) {
			await WriteUsageErrorAsync( stderr, exception.Message ).ConfigureAwait( false );
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
		try {
			var observation = await provider.GetHostIdentifierAsync( cancellationToken ).ConfigureAwait( false );
			if ( !observation.IsAvailable ) {
				await stderr.WriteLineAsync(
					string.Concat(
						"hostid: cannot determine host identifier: ",
						observation.Message ?? "the provider did not return an identifier"
					)
				).ConfigureAwait( false );
				return 1;
			}
			await stdout.WriteLineAsync( observation.GetRequiredValue().Hexadecimal ).ConfigureAwait( false );
			return 0;
		} catch ( OperationCanceledException ) when ( cancellationToken.IsCancellationRequested ) {
			await stderr.WriteLineAsync( "hostid: operation canceled" ).ConfigureAwait( false );
			return 1;
		} catch ( Exception exception ) {
			await stderr.WriteLineAsync(
				string.Concat( "hostid: cannot determine host identifier: ", exception.Message )
			).ConfigureAwait( false );
			return 1;
		}
	}

	private static async Task WriteUsageErrorAsync( TextWriter stderr, string message ) {
		await stderr.WriteLineAsync(
			string.Concat( "hostid: ", message )
		).ConfigureAwait( false );
		await stderr.WriteLineAsync(
			"Try 'hostid --help' for more information."
		).ConfigureAwait( false );
	}

	private static readonly string HelpText = """
Usage: hostid [OPTION]
Print the numeric identifier (in hexadecimal) for the current host.

      --help        display this help and exit
      --version     output version information and exit
""" + Environment.NewLine;
}

/// <summary>Represents parsed <c>hostid</c> command-line options.</summary>
public sealed record HostIdOptions {
	/// <summary>Gets whether help was requested.</summary>
	public bool Help { get; private init; }

	/// <summary>Gets whether version information was requested.</summary>
	public bool Version { get; private init; }

	/// <summary>Parses command-line options.</summary>
	/// <param name="args">The command-line arguments.</param>
	/// <returns>The parsed options.</returns>
	/// <exception cref="HostIdUsageException">An option or operand is invalid.</exception>
	public static HostIdOptions Parse( IReadOnlyList<string> args ) {
		ArgumentNullException.ThrowIfNull( args );
		var help = false;
		var version = false;
		var optionParsing = true;
		foreach ( var argument in args ) {
			if ( optionParsing && argument == "--" ) {
				optionParsing = false;
				continue;
			}
			if ( optionParsing && argument == "--help" ) {
				help = true;
				continue;
			}
			if ( optionParsing && argument == "--version" ) {
				version = true;
				continue;
			}
			if ( optionParsing && argument.StartsWith( '-' ) ) {
				throw new HostIdUsageException(
					string.Concat( "unrecognized option '", argument, "'" )
				);
			}
			throw new HostIdUsageException(
				string.Concat( "extra operand '", argument, "'" )
			);
		}
		return new HostIdOptions { Help = help, Version = version };
	}
}

/// <summary>Reports invalid <c>hostid</c> command-line usage.</summary>
public sealed class HostIdUsageException : Exception {
	/// <summary>Initializes a usage exception.</summary>
	/// <param name="message">The diagnostic message.</param>
	public HostIdUsageException( string message ) : base( message ) { }
}
