namespace Icod.CoreUtils.Who;

using System.Net;
using System.Net.Sockets;
using Icod.CommandFramework.CommandLine;
using Icod.CommandFramework.Diagnostics;
using Icod.CoreUtils.Shared.Platform;

/// <summary>Implements the <c>who</c> command.</summary>
public static class Command {
	private const string ProgramName = "who";
	private const string Version = "who (Icod.CoreUtils) 1.0";

	/// <summary>
	/// Executes <c>who</c> synchronously with optional standard-stream substitution.
	/// </summary>
	/// <remarks>
	/// This compatibility entry point blocks on the TAP implementation. A <see langword="null"/> text stream selects the corresponding <see cref="Console"/> stream; caller-supplied streams remain caller-owned.
	/// </remarks>
	/// <param name="args">The command-line arguments, excluding the executable name.</param>
	/// <param name="stdin">The text reader to use as standard input, or <see langword="null"/> to use <see cref="Console.In"/>.</param>
	/// <param name="stdout">The text writer to use as standard output, or <see langword="null"/> to use <see cref="Console.Out"/>.</param>
	/// <param name="stderr">The text writer to use as standard error, or <see langword="null"/> to use <see cref="Console.Error"/>.</param>
	/// <returns>The GNU-compatible process exit status: zero for successful command execution and nonzero for a usage or operational failure.</returns>
	public static int Run( string[] args, TextReader? stdin = null, TextWriter? stdout = null, TextWriter? stderr = null ) =>
		RunAsync( args, stdin, stdout, stderr ).GetAwaiter().GetResult();

	/// <summary>
	/// Executes <c>who</c> asynchronously with optional injected standard streams.
	/// </summary>
	/// <remarks>
	/// A <see langword="null"/> text stream selects the corresponding <see cref="Console"/> stream. Caller-supplied streams remain caller-owned.
	/// </remarks>
	/// <param name="args">The command-line arguments, excluding the executable name.</param>
	/// <param name="stdin">The text reader to use as standard input, or <see langword="null"/> to use <see cref="Console.In"/>.</param>
	/// <param name="stdout">The text writer to use as standard output, or <see langword="null"/> to use <see cref="Console.Out"/>.</param>
	/// <param name="stderr">The text writer to use as standard error, or <see langword="null"/> to use <see cref="Console.Error"/>.</param>
	/// <param name="cancellationToken">The token used to cancel parsing, platform queries, and asynchronous I/O.</param>
	/// <returns>The GNU-compatible process exit status: zero for successful command execution and nonzero for a usage or operational failure.</returns>
	public static Task<int> RunAsync(
		string[] args,
		TextReader? stdin = null,
		TextWriter? stdout = null,
		TextWriter? stderr = null,
		CancellationToken cancellationToken = default
	) => RunAsync(
		args ?? Array.Empty<string>(),
		new CommandContext(
			ProgramName,
			stdin ?? Console.In,
			stdout ?? Console.Out,
			stderr ?? Console.Error,
			cancellationToken: cancellationToken
		)
	);

	/// <summary>
	/// Executes <c>who</c> asynchronously using a complete shared command context.
	/// </summary>
	/// <remarks>
	/// The context carries text and optional binary standard streams, centralized diagnostics, and cancellation. The command does not dispose caller-owned standard streams.
	/// </remarks>
	/// <param name="args">The command-line arguments, excluding the executable name.</param>
	/// <param name="context">The command context that supplies standard streams, diagnostics, and cancellation.</param>
	/// <param name="provider">The injectable login-record provider; <see langword="null"/> selects the system implementation when supported by this overload.</param>
	/// <returns>The GNU-compatible process exit status: zero for successful command execution and nonzero for a usage or operational failure.</returns>
	/// <exception cref="ArgumentNullException"><paramref name="context"/> is <see langword="null"/>.</exception>
	public static async Task<int> RunAsync( string[] args, CommandContext context, ILoginRecordProvider? provider = null ) {
		ArgumentNullException.ThrowIfNull( context );
		provider ??= SystemLoginRecordProvider.Instance;
		var parser = CreateParser(
			new OptionDefinition( "all", 'a', new[] { "all" } ),
			new OptionDefinition( "boot", 'b', new[] { "boot" } ),
			new OptionDefinition( "dead", 'd', new[] { "dead" } ),
			new OptionDefinition( "heading", 'H', new[] { "heading" } ),
			new OptionDefinition( "login", 'l', new[] { "login" } ),
			new OptionDefinition( "lookup", null, new[] { "lookup" } ),
			new OptionDefinition( "me", 'm' ),
			new OptionDefinition( "process", 'p', new[] { "process" } ),
			new OptionDefinition( "count", 'q', new[] { "count" } ),
			new OptionDefinition( "runlevel", 'r', new[] { "runlevel" } ),
			new OptionDefinition( "short", 's', new[] { "short" } ),
			new OptionDefinition( "time", 't', new[] { "time" } ),
			new OptionDefinition( "mesg", 'T', new[] { "mesg", "message", "writable" } ),
			new OptionDefinition( "mesg-w", 'w' ),
			new OptionDefinition( "users", 'u', new[] { "users" } ),
			new OptionDefinition( "help", null, new[] { "help" } ),
			new OptionDefinition( "version", null, new[] { "version" } )
		);
		try {
			var result = parser.Parse( args );
			if ( await WriteParseErrorsAsync( result, context ).ConfigureAwait( false ) ) {
				return CommandExitCodes.Failure;
			}
			if ( result.HasOption( "help" ) ) {
				await WriteHelpAsync( context ).ConfigureAwait( false );
				return CommandExitCodes.Success;
			}
			if ( result.HasOption( "version" ) ) {
				await context.StandardOutput.WriteLineAsync(
					Version.AsMemory(),
					context.CancellationToken
				).ConfigureAwait( false );
				return CommandExitCodes.Success;
			}
			if ( 2 < result.Operands.Count ) {
				await context.Diagnostics.ErrorAsync(
					$"extra operand '{result.Operands[ 2 ]}'",
					context.CancellationToken
				).ConfigureAwait( false );
				return CommandExitCodes.Failure;
			}
			if ( !provider.IsSupported ) {
				await context.Diagnostics.ErrorAsync(
					"login records are not supported on this platform",
					context.CancellationToken
				).ConfigureAwait( false );
				return CommandExitCodes.Failure;
			}

			var fileName = ( 1 == result.Operands.Count )
				? result.Operands[ 0 ]
				: null
			;
			var currentTerminalOnly = result.HasOption( "me" ) || 2 == result.Operands.Count;
			var currentTerminal = ( currentTerminalOnly )
				? await provider.GetStandardInputTerminalLineAsync(
					context.CancellationToken
				).ConfigureAwait( false )
				: null
			;
			if ( result.HasOption( "count" ) ) {
				var users = new List<string>();
				await foreach ( var record in provider.ReadAsync( fileName, context.CancellationToken ).ConfigureAwait( false ) ) {
					if (
						currentTerminalOnly
						&& !string.Equals( record.Line, currentTerminal, StringComparison.Ordinal )
					) {
						continue;
					}
					if (
						LoginRecordType.UserProcess == record.Type
						&& !string.IsNullOrEmpty( record.User )
					) {
						users.Add( record.User );
					}
				}
				await context.StandardOutput.WriteLineAsync(
					string.Join( ' ', users ).AsMemory(),
					context.CancellationToken
				).ConfigureAwait( false );
				await context.StandardOutput.WriteLineAsync(
					$"# users={users.Count}".AsMemory(),
					context.CancellationToken
				).ConfigureAwait( false );
				return CommandExitCodes.Success;
			}

			var includeIdle = result.HasOption( "users" ) || result.HasOption( "all" );
			var includeMesg = result.HasOption( "mesg" ) || result.HasOption( "mesg-w" ) || result.HasOption( "all" );
			if ( result.HasOption( "heading" ) ) {
				await context.StandardOutput.WriteLineAsync(
					CreateHeading( includeIdle, includeMesg ).AsMemory(),
					context.CancellationToken
				).ConfigureAwait( false );
			}
			await foreach ( var record in provider.ReadAsync( fileName, context.CancellationToken ).ConfigureAwait( false ) ) {
				if (
					currentTerminalOnly
					&& !string.Equals( record.Line, currentTerminal, StringComparison.Ordinal )
				) {
					continue;
				}
				if ( !ShouldInclude( record.Type, result ) ) {
					continue;
				}
				var host = ( result.HasOption( "lookup" ) )
					? await ResolveHostAsync(
						record.Host,
						context.CancellationToken
					).ConfigureAwait( false )
					: record.Host
				;
				var line = FormatRecord(
					record with {
						Host = host
					},
					includeIdle,
					includeMesg
				);
				await context.StandardOutput.WriteLineAsync(
					line.AsMemory(),
					context.CancellationToken
				).ConfigureAwait( false );
			}
			return CommandExitCodes.Success;
		} catch ( OperationCanceledException ) {
			return CommandExitCodes.Canceled;
		} catch ( Exception ex ) when ( ex is IOException or UnauthorizedAccessException ) {
			await context.Diagnostics.ErrorAsync(
				ex.Message,
				context.CancellationToken
			).ConfigureAwait( false );
			return CommandExitCodes.Failure;
		}
	}

	private static bool ShouldInclude( LoginRecordType type, OptionParseResult result ) {
		if ( result.HasOption( "count" ) ) {
			return LoginRecordType.UserProcess == type;
		}
		var all = result.HasOption( "all" );
		var hasExplicitSelection = all
			|| result.HasOption( "boot" )
			|| result.HasOption( "dead" )
			|| result.HasOption( "login" )
			|| result.HasOption( "process" )
			|| result.HasOption( "runlevel" )
			|| result.HasOption( "time" )
			|| result.HasOption( "users" );
		if ( !hasExplicitSelection ) {
			return LoginRecordType.UserProcess == type;
		}
		return type switch {
			LoginRecordType.BootTime => all || result.HasOption( "boot" ),
			LoginRecordType.DeadProcess => all || result.HasOption( "dead" ),
			LoginRecordType.LoginProcess => all || result.HasOption( "login" ),
			LoginRecordType.InitProcess => all || result.HasOption( "process" ),
			LoginRecordType.RunLevel => all || result.HasOption( "runlevel" ),
			LoginRecordType.OldTime or LoginRecordType.NewTime => all || result.HasOption( "time" ),
			LoginRecordType.UserProcess => all || result.HasOption( "users" ),
			_ => false
		};
	}

	private static string CreateHeading( bool includeIdle, bool includeMesg ) {
		var name = ( includeMesg )
			? "NAME     S"
			: "NAME    "
		;
		return ( includeIdle )
			? $"{name} LINE         TIME             IDLE          PID COMMENT"
			: $"{name} LINE         TIME             COMMENT"
		;
	}

	private static string FormatRecord( LoginRecord record, bool includeIdle, bool includeMesg ) {
		var timestamp = record.Timestamp.ToLocalTime().ToString( "yyyy-MM-dd HH:mm", System.Globalization.CultureInfo.InvariantCulture );
		return record.Type switch {
			LoginRecordType.UserProcess => FormatUser( record, timestamp, includeIdle, includeMesg ),
			LoginRecordType.BootTime => $"         system boot  {timestamp}",
			LoginRecordType.RunLevel => $"         run-level {RunLevel( record.ProcessId )}  {timestamp}                   last={PreviousRunLevel( record.ProcessId )}",
			LoginRecordType.LoginProcess => $"LOGIN    {record.Line,-12} {timestamp}               {record.ProcessId,5} id={record.Id}",
			LoginRecordType.InitProcess => $"         {record.Line,-12} {timestamp}               {record.ProcessId,5} id={record.Id}",
			LoginRecordType.DeadProcess => $"         {record.Line,-12} {timestamp}               {record.ProcessId,5} id={record.Id} term={record.TerminationStatus} exit={record.ExitStatus}",
			LoginRecordType.OldTime => $"         clock old    {timestamp}",
			LoginRecordType.NewTime => $"         clock new    {timestamp}",
			_ => string.Empty
		};
	}

	private static string FormatUser( LoginRecord record, string timestamp, bool includeIdle, bool includeMesg ) {
		var status = ( includeMesg )
			? $" {MessageStatus( record.Line )}"
			: string.Empty
		;
		var comment = ( string.IsNullOrEmpty( record.Host ) )
			? string.Empty
			: $" ({record.Host})"
		;
		if ( includeIdle ) {
			return $"{record.User,-8}{status} {record.Line,-12} {timestamp} {IdleTime( record.Line ),-12} {record.ProcessId,5}{comment}";
		}
		return $"{record.User,-8}{status} {record.Line,-12} {timestamp}{comment}";
	}

	private static char MessageStatus( string line ) {
		if ( string.IsNullOrEmpty( line ) ) {
			return '?';
		}
		try {
			var path = System.IO.Path.Combine( "/dev", line );
			if ( !File.Exists( path ) ) {
				return '?';
			}
			if ( OperatingSystem.IsWindows() ) {
				return '?';
			}
			var mode = File.GetUnixFileMode( path );
			return ( 0 != ( mode & ( UnixFileMode.GroupWrite | UnixFileMode.OtherWrite ) ) )
				? '+'
				: '-'
			;
		} catch ( Exception ex ) when ( ex is IOException or UnauthorizedAccessException or PlatformNotSupportedException ) {
			return '?';
		}
	}

	private static string IdleTime( string line ) {
		if ( string.IsNullOrEmpty( line ) ) {
			return "?";
		}
		try {
			var path = System.IO.Path.Combine( "/dev", line );
			if ( !File.Exists( path ) ) {
				return "?";
			}
			var idle = DateTime.UtcNow - File.GetLastAccessTimeUtc( path );
			if ( idle < TimeSpan.FromMinutes( 1 ) ) {
				return ".";
			}
			if ( idle >= TimeSpan.FromDays( 1 ) ) {
				return "old";
			}
			return $"{(int)idle.TotalHours:00}:{idle.Minutes:00}";
		} catch ( Exception ex ) when ( ex is IOException or UnauthorizedAccessException ) {
			return "?";
		}
	}

	private static char RunLevel( int processId ) => PrintableRunLevel( processId & 0xff );
	private static char PreviousRunLevel( int processId ) => PrintableRunLevel( ( processId >> 8 ) & 0xff );
	private static char PrintableRunLevel( int value ) => ( 0 == value )
		? 'N'
		: ( char )value
	;

	private static async Task<string> ResolveHostAsync( string host, CancellationToken cancellationToken ) {
		if ( string.IsNullOrEmpty( host ) ) {
			return host;
		}
		var separator = host.IndexOf( ':' );
		var lookupName = ( 0 <= separator )
			? host[ ..separator ]
			: host
		;
		try {
			var entry = await Dns.GetHostEntryAsync( lookupName ).WaitAsync( cancellationToken ).ConfigureAwait( false );
			return ( 0 <= separator )
				? entry.HostName + host[ separator.. ]
				: entry.HostName
			;
		} catch ( Exception ex ) when ( ex is SocketException or ArgumentException ) {
			return host;
		}
	}

	private static async Task WriteHelpAsync( CommandContext context ) {
		const string text = """
Usage: who [OPTION]... [ FILE | ARG1 ARG2 ]
Print information about users who are currently logged in.

  -a, --all         same as -b -d --login -p -r -t -T -u
  -b, --boot        time of last system boot
  -d, --dead        print dead processes
  -H, --heading     print line of column headings
  -l, --login       print system login processes
      --lookup      canonicalize hostnames via DNS
  -m                only hostname and user associated with stdin
  -p, --process     print active processes spawned by init
  -q, --count       all login names and number of users logged on
  -r, --runlevel    print current runlevel
  -s, --short       print only name, line, and time (default)
  -t, --time        print last system clock change
  -T, -w, --mesg   add user's message status as +, - or ?
      --message     same as -T
      --writable    same as -T
  -u, --users       list users logged in, including idle time
      --help        display this help and exit
      --version     output version information and exit
""";
		await context.StandardOutput.WriteAsync(
			text.ReplaceLineEndings( Environment.NewLine ).AsMemory(),
			context.CancellationToken
		).ConfigureAwait( false );
	}
	private static OptionParser CreateParser( params OptionDefinition[] options ) => new(
		options,
		new OptionParserSettings {
			AllowLongOptionAbbreviations = true,
			Ordering = OptionOrdering.Permute
		}
	);
	private static async Task<bool> WriteParseErrorsAsync( OptionParseResult result, CommandContext context ) {
		if ( result.IsSuccess ) {
			return false;
		}
		foreach ( var error in result.Errors ) {
			await context.StandardError.WriteLineAsync(
				OptionDiagnosticFormatter.Format( context.ProgramName, error ).AsMemory(),
				context.CancellationToken
			).ConfigureAwait( false );
		}
		return true;
	}
}
