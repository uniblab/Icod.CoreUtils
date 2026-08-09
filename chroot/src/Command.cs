// Original behavior/reference: GNU coreutils 9.11
// Ported to .NET by Timothy J. Bruce <uniblab@hotmail.com>

namespace Icod.CoreUtils.ChRoot;

/// <summary>Implements GNU Coreutils 9.11 <c>chroot</c>.</summary>
public static class Command {
	private const int Success = 0;
	private const int InternalFailure = 125;
	private const string VersionText = "chroot (Icod.CoreUtils) 9.11";
	private const string HelpText = """
Usage: chroot [OPTION]... NEWROOT [COMMAND [ARG]...]
Run COMMAND with root directory set to NEWROOT.

      --groups=G_LIST        specify supplementary groups as g1,g2,..,gN
      --userspec=USER:GROUP  specify user and group (ID or name) to use
      --skip-chdir           do not change working directory to '/'
      --help                 display this help and exit
      --version              output version information and exit

If no command is given, run '"$SHELL" -i' (default: '/bin/sh -i').
Exit status 125 means chroot itself failed, 126 means COMMAND was found but
could not be invoked, and 127 means COMMAND could not be found.
""";

	/// <summary>Runs <c>chroot</c> synchronously.</summary>
	/// <param name="args">Command-line arguments.</param>
	/// <param name="stdin">Optional standard input retained for compatibility with historical callers.</param>
	/// <param name="stdout">Optional standard output writer.</param>
	/// <param name="stderr">Optional diagnostic writer.</param>
	/// <returns>The process exit status.</returns>
	public static int Run(
		string[] args,
		TextReader? stdin = null,
		TextWriter? stdout = null,
		TextWriter? stderr = null
	) {
		ArgumentNullException.ThrowIfNull( args );
		return RunAsync( args, stdin, stdout, stderr ).GetAwaiter().GetResult();
	}

	/// <summary>Runs <c>chroot</c> with an injectable platform implementation.</summary>
	/// <param name="args">Command-line arguments.</param>
	/// <param name="stdin">Optional standard input retained for compatibility with historical callers.</param>
	/// <param name="stdout">Optional standard output writer.</param>
	/// <param name="stderr">Optional diagnostic writer.</param>
	/// <param name="platform">Optional root-changing platform implementation.</param>
	/// <param name="environmentVariableProvider">Optional environment-variable provider.</param>
	/// <param name="cancellationToken">Cancellation token observed before irreversible host operations.</param>
	/// <returns>A task whose result is the process exit status.</returns>
	public static async Task<int> RunAsync(
		string[] args,
		TextReader? stdin = null,
		TextWriter? stdout = null,
		TextWriter? stderr = null,
		IChrootPlatform? platform = null,
		Func<string, string?>? environmentVariableProvider = null,
		CancellationToken cancellationToken = default
	) {
		ArgumentNullException.ThrowIfNull( args );
		_ = stdin;
		stdout ??= Console.Out;
		stderr ??= Console.Error;
		environmentVariableProvider ??= Environment.GetEnvironmentVariable;

		var parsed = ParseArguments( args );
		if ( null != parsed.Error ) {
			await stderr.WriteLineAsync( parsed.Error ).ConfigureAwait( false );
			await stderr.WriteLineAsync( "Try 'chroot --help' for more information." ).ConfigureAwait( false );
			return InternalFailure;
		}
		if ( parsed.ShowHelp ) {
			await stdout.WriteAsync( NormalizeLineEndings( HelpText ) ).ConfigureAwait( false );
			return Success;
		}
		if ( parsed.ShowVersion ) {
			await stdout.WriteLineAsync( VersionText ).ConfigureAwait( false );
			return Success;
		}
		if ( null == parsed.RootDirectory ) {
			await stderr.WriteLineAsync( "chroot: missing operand" ).ConfigureAwait( false );
			await stderr.WriteLineAsync( "Try 'chroot --help' for more information." ).ConfigureAwait( false );
			return InternalFailure;
		}

		var activePlatform = platform ?? SystemChrootPlatform.Instance;
		if ( !activePlatform.IsSupported ) {
			await stderr.WriteLineAsync( $"chroot: {activePlatform.UnsupportedReason}" ).ConfigureAwait( false );
			return InternalFailure;
		}
		if ( parsed.SkipChdir && !activePlatform.IsCurrentRoot( parsed.RootDirectory ) ) {
			await stderr.WriteLineAsync( "chroot: option --skip-chdir only permitted if NEWROOT is old '/'" ).ConfigureAwait( false );
			await stderr.WriteLineAsync( "Try 'chroot --help' for more information." ).ConfigureAwait( false );
			return InternalFailure;
		}

		var command = parsed.Command.ToArray();
		if ( 0 == command.Length ) {
			var shell = environmentVariableProvider( "SHELL" );
			if ( string.IsNullOrEmpty( shell ) ) {
				shell = "/bin/sh";
			}
			command = [ shell, "-i" ];
		}
		var request = new ChrootExecutionRequest(
			parsed.RootDirectory,
			command,
			parsed.UserSpec,
			parsed.GroupsSpec,
			parsed.SkipChdir
		);
		try {
			cancellationToken.ThrowIfCancellationRequested();
			var result = await activePlatform.ExecuteAsync( request, cancellationToken ).ConfigureAwait( false );
			if ( !string.IsNullOrEmpty( result.Diagnostic ) ) {
				await stderr.WriteLineAsync( result.Diagnostic ).ConfigureAwait( false );
			}
			return result.ExitCode;
		} catch ( OperationCanceledException ) when ( cancellationToken.IsCancellationRequested ) {
			return InternalFailure;
		} catch ( Exception exception ) {
			await stderr.WriteLineAsync( $"chroot: {exception.Message}" ).ConfigureAwait( false );
			return InternalFailure;
		}
	}

	private static ParsedArguments ParseArguments( IReadOnlyList<string> args ) {
		ArgumentNullException.ThrowIfNull( args );
		var parsed = new ParsedArguments();
		var index = 0;
		var optionsEnded = false;
		while ( index < args.Count ) {
			var token = args[ index ];
			if ( optionsEnded || "-" == token || !token.StartsWith( "-", StringComparison.Ordinal ) ) {
				break;
			}
			if ( "--" == token ) {
				optionsEnded = true;
				index++;
				break;
			}
			if ( !token.StartsWith( "--", StringComparison.Ordinal ) ) {
				parsed.Error = $"chroot: invalid option -- '{token[ 1.. ]}'";
				return parsed;
			}
			var equals = token.IndexOf( '=' );
			var option = token;
			string? attached = null;
			if ( 0 <= equals ) {
				option = token[ ..equals ];
				attached = token[ ( equals + 1 ).. ];
			}
			switch ( option ) {
				case "--groups":
					if ( null == attached ) {
						if ( index + 1 >= args.Count ) {
							parsed.Error = "chroot: option '--groups' requires an argument";
							return parsed;
						}
						index++;
						attached = args[ index ];
					}
					parsed.GroupsSpec = attached;
					break;
				case "--userspec":
					if ( null == attached ) {
						if ( index + 1 >= args.Count ) {
							parsed.Error = "chroot: option '--userspec' requires an argument";
							return parsed;
						}
						index++;
						attached = args[ index ];
					}
					if ( attached.EndsWith( ":", StringComparison.Ordinal ) && 0 < attached.Length ) {
						attached = attached[ ..^1 ];
					}
					parsed.UserSpec = attached;
					break;
				case "--skip-chdir":
					if ( null != attached ) {
						parsed.Error = "chroot: option '--skip-chdir' doesn't allow an argument";
						return parsed;
					}
					parsed.SkipChdir = true;
					break;
				case "--help":
					if ( null != attached ) {
						parsed.Error = "chroot: option '--help' doesn't allow an argument";
						return parsed;
					}
					parsed.ShowHelp = true;
					return parsed;
				case "--version":
					if ( null != attached ) {
						parsed.Error = "chroot: option '--version' doesn't allow an argument";
						return parsed;
					}
					parsed.ShowVersion = true;
					return parsed;
				default:
					parsed.Error = $"chroot: unrecognized option '{token}'";
					return parsed;
			}
			index++;
		}
		if ( parsed.ShowHelp || parsed.ShowVersion ) {
			return parsed;
		}
		if ( index >= args.Count ) {
			return parsed;
		}
		parsed.RootDirectory = args[ index ];
		index++;
		for ( ; index < args.Count; index++ ) {
			parsed.Command.Add( args[ index ] );
		}
		return parsed;
	}

	private static string NormalizeLineEndings( string value ) {
		ArgumentNullException.ThrowIfNull( value );
		return value.Replace( "\r\n", "\n", StringComparison.Ordinal ).Replace( "\n", Environment.NewLine, StringComparison.Ordinal );
	}

	private sealed class ParsedArguments {
		public string? RootDirectory { get; set; }
		public string? UserSpec { get; set; }
		public string? GroupsSpec { get; set; }
		public bool SkipChdir { get; set; }
		public bool ShowHelp { get; set; }
		public bool ShowVersion { get; set; }
		public string? Error { get; set; }
		public List<string> Command { get; } = [];
	}
}
