// Ported to .NET by Timothy J. Bruce <uniblab@hotmail.com>

namespace Icod.CoreUtils.StdBuf;

using System.Globalization;
using System.Numerics;
using Icod.Processes;

/// <summary>
/// Implements GNU-compatible standard-stream buffering control for child processes.
/// </summary>
public static class Command {
	private const int InternalFailureExitCode = 125;
	private const string ProgramName = "stdbuf";
	private const string Version = "9.11";

	/// <summary>
	/// Runs <c>stdbuf</c> synchronously.
	/// </summary>
	public static int Run(
		string[] args,
		TextReader? stdin = null,
		TextWriter? stdout = null,
		TextWriter? stderr = null,
		IProcessExecutor? processExecutor = null,
		IStdBufPlatform? platform = null,
		Func<string, string?>? environmentVariableProvider = null,
		CancellationToken cancellationToken = default,
		bool replaceCurrentProcess = false
	) => RunAsync(
		args,
		stdin,
		stdout,
		stderr,
		processExecutor,
		platform,
		environmentVariableProvider,
		cancellationToken,
		replaceCurrentProcess
	).GetAwaiter().GetResult();

	/// <summary>
	/// Runs <c>stdbuf</c> asynchronously.
	/// </summary>
	public static async Task<int> RunAsync(
		string[] args,
		TextReader? stdin = null,
		TextWriter? stdout = null,
		TextWriter? stderr = null,
		IProcessExecutor? processExecutor = null,
		IStdBufPlatform? platform = null,
		Func<string, string?>? environmentVariableProvider = null,
		CancellationToken cancellationToken = default,
		bool replaceCurrentProcess = false
	) {
		ArgumentNullException.ThrowIfNull( args );
		_ = stdin;
		stdout ??= Console.Out;
		stderr ??= Console.Error;
		processExecutor ??= SystemProcessExecutor.Instance;
		platform ??= SystemStdBufPlatform.Instance;
		environmentVariableProvider ??= Environment.GetEnvironmentVariable;

		var modes = new Dictionary<char, string>();
		var index = 0;
		while ( index < args.Length ) {
			var token = args[ index ];
			if ( "--" == token ) {
				index++;
				break;
			}
			if ( "-" == token || !token.StartsWith( '-' ) ) {
				break;
			}

			int? terminalStatus;
			if ( token.StartsWith( "--", StringComparison.Ordinal ) ) {
				terminalStatus = ParseLongOption(
					args,
					ref index,
					modes,
					stdout,
					stderr
				);
			} else {
				terminalStatus = ParseShortOption(
					args,
					ref index,
					modes,
					stderr
				);
			}
			if ( terminalStatus.HasValue ) {
				return terminalStatus.Value;
			}
		}

		if ( index >= args.Length ) {
			return FailWithHelp(
				stderr,
				"missing operand"
			);
		}
		if ( 0 == modes.Count ) {
			return FailWithHelp(
				stderr,
				"you must specify a buffering mode option"
			);
		}

		if ( !platform.TryGetPreloadConfiguration(
			out var preload,
			out var unsupportedReason
		) ) {
			stderr.WriteLine(
				$"{ProgramName}: standard-stream buffering control is unsupported: {unsupportedReason}"
			);
			return InternalFailureExitCode;
		}

		var command = args[ index ];
		var argumentZero = ( replaceCurrentProcess && !OperatingSystem.IsWindows() )
			? command
			: null
		;
		var options = new ProcessRunOptions( command ) {
			ArgumentZero = argumentZero,
			ReplaceCurrentProcess = replaceCurrentProcess,
			ResolveExecutable = true,
			ReturnLaunchFailureResult = true
		};
		for ( var argumentIndex = index + 1; argumentIndex < args.Length; argumentIndex++ ) {
			options.Arguments.Add( args[ argumentIndex ] );
		}

		foreach ( var mode in modes ) {
			options.EnvironmentVariables[ $"_STDBUF_{char.ToUpperInvariant( mode.Key )}" ] = mode.Value;
		}

		var inheritedPreload = environmentVariableProvider( preload.EnvironmentVariable );
		options.EnvironmentVariables[ preload.EnvironmentVariable ] = string.IsNullOrEmpty( inheritedPreload )
			? preload.LibraryPath
			: string.Concat(
				inheritedPreload,
				preload.Separator,
				preload.LibraryPath
			);

		var result = await processExecutor.RunAsync(
			options,
			cancellationToken
		).ConfigureAwait( false );
		if ( ProcessTerminationKind.LaunchFailed == result.Termination.Kind ) {
			var detail = string.IsNullOrWhiteSpace( result.Termination.Message )
				? "the child process could not be started"
				: result.Termination.Message;
			stderr.WriteLine(
				$"{ProgramName}: failed to run command '{command}': {detail}"
			);
		}
		return result.Termination.ToPortableExitCode();
	}

	private static int? ParseLongOption(
		string[] args,
		ref int index,
		IDictionary<char, string> modes,
		TextWriter stdout,
		TextWriter stderr
	) {
		var token = args[ index ];
		var body = token[ 2.. ];
		var equalsIndex = body.IndexOf( '=' );
		var hasInlineValue = 0 <= equalsIndex;
		var optionName = hasInlineValue
			? body[ ..equalsIndex ]
			: body;
		var inlineValue = hasInlineValue
			? body[ ( equalsIndex + 1 ).. ]
			: null;

		var resolved = ResolveLongOption( optionName );
		if ( null == resolved ) {
			return FailWithHelp(
				stderr,
				$"unrecognized option '{token}'"
			);
		}
		if ( "help" == resolved || "version" == resolved ) {
			if ( hasInlineValue ) {
				return FailWithHelp(
					stderr,
					$"option '--{resolved}' doesn't allow an argument"
				);
			}
			if ( "help" == resolved ) {
				PrintUsage( stdout );
			} else {
				PrintVersion( stdout );
			}
			return 0;
		}

		string value;
		if ( hasInlineValue ) {
			value = inlineValue!;
		} else {
			if ( index + 1 >= args.Length ) {
				return FailWithHelp(
					stderr,
					$"option '--{resolved}' requires an argument"
				);
			}
			index++;
			value = args[ index ];
		}

		var option = resolved switch {
			"input" => 'i',
			"output" => 'o',
			"error" => 'e',
			_ => throw new InvalidOperationException( "Resolved an unknown stdbuf option." )
		};
		var failure = SetMode(
			option,
			value,
			modes,
			stderr
		);
		index++;
		return failure;
	}

	private static int? ParseShortOption(
		string[] args,
		ref int index,
		IDictionary<char, string> modes,
		TextWriter stderr
	) {
		var token = args[ index ];
		if ( 2 > token.Length ) {
			return null;
		}
		var option = token[ 1 ];
		if ( 'i' != option && 'o' != option && 'e' != option ) {
			return FailWithHelp(
				stderr,
				$"invalid option -- '{option}'"
			);
		}

		string value;
		if ( 2 < token.Length ) {
			value = token[ 2.. ];
		} else {
			if ( index + 1 >= args.Length ) {
				return FailWithHelp(
					stderr,
					$"option requires an argument -- '{option}'"
				);
			}
			index++;
			value = args[ index ];
		}

		var failure = SetMode(
			option,
			value,
			modes,
			stderr
		);
		index++;
		return failure;
	}

	private static int? SetMode(
		char option,
		string rawMode,
		IDictionary<char, string> modes,
		TextWriter stderr
	) {
		var mode = rawMode.TrimStart();
		if ( 'i' == option && "L" == mode ) {
			return FailWithHelp(
				stderr,
				"line buffering standard input is meaningless"
			);
		}
		if ( "L" == mode ) {
			modes[ option ] = "L";
			return null;
		}
		if ( !TryParseBufferSize(
			mode,
			out var bytes
		) ) {
			stderr.WriteLine(
				$"{ProgramName}: invalid mode '{mode}'"
			);
			return InternalFailureExitCode;
		}
		modes[ option ] = bytes.ToString( CultureInfo.InvariantCulture );
		return null;
	}

	private static bool TryParseBufferSize(
		string text,
		out BigInteger bytes
	) {
		bytes = BigInteger.Zero;
		if ( string.IsNullOrEmpty( text ) ) {
			return false;
		}

		var digitStart = '+' == text[ 0 ]
			? 1
			: 0;
		if ( digitStart >= text.Length ) {
			return false;
		}
		var digitEnd = digitStart;
		while ( digitEnd < text.Length && char.IsAsciiDigit( text[ digitEnd ] ) ) {
			digitEnd++;
		}
		if ( digitEnd == digitStart ) {
			return false;
		}
		if ( !BigInteger.TryParse(
			text[ digitStart..digitEnd ],
			NumberStyles.None,
			CultureInfo.InvariantCulture,
			out var value
		) ) {
			return false;
		}

		var suffix = text[ digitEnd.. ];
		if ( !TryGetMultiplier(
			suffix,
			out var multiplier
		) ) {
			return false;
		}
		var result = value * multiplier;
		var maximum = Environment.Is64BitProcess
			? new BigInteger( ulong.MaxValue )
			: new BigInteger( uint.MaxValue );
		if ( result > maximum ) {
			return false;
		}
		bytes = result;
		return true;
	}

	private static bool TryGetMultiplier(
		string suffix,
		out BigInteger multiplier
	) {
		if ( 0 == suffix.Length ) {
			multiplier = BigInteger.One;
			return true;
		}

		var exponent = suffix[ 0 ] switch {
			'k' or 'K' => 1,
			'M' => 2,
			'G' => 3,
			'T' => 4,
			'P' => 5,
			'E' => 6,
			'Z' => 7,
			'Y' => 8,
			'R' => 9,
			'Q' => 10,
			_ => 0
		};
		if ( 0 == exponent ) {
			multiplier = BigInteger.Zero;
			return false;
		}

		var rest = suffix[ 1.. ];
		if ( 0 == rest.Length ) {
			multiplier = BigInteger.Pow(
				new BigInteger( 1024 ),
				exponent
			);
			return true;
		}
		if ( "B" == rest ) {
			multiplier = BigInteger.Pow(
				new BigInteger( 1000 ),
				exponent
			);
			return true;
		}
		if ( "iB" == rest ) {
			multiplier = BigInteger.Pow(
				new BigInteger( 1024 ),
				exponent
			);
			return true;
		}
		multiplier = BigInteger.Zero;
		return false;
	}

	private static string? ResolveLongOption(
		string prefix
	) {
		if ( string.IsNullOrEmpty( prefix ) ) {
			return null;
		}
		string? match = null;
		foreach ( var candidate in new[] { "input", "output", "error", "help", "version" } ) {
			if ( !candidate.StartsWith(
				prefix,
				StringComparison.Ordinal
			) ) {
				continue;
			}
			if ( null != match ) {
				return null;
			}
			match = candidate;
		}
		return match;
	}

	private static int FailWithHelp(
		TextWriter stderr,
		string message
	) {
		stderr.WriteLine(
			$"{ProgramName}: {message}"
		);
		stderr.WriteLine(
			$"Try '{ProgramName} --help' for more information."
		);
		return InternalFailureExitCode;
	}

	private static void PrintUsage(
		TextWriter stdout
	) {
		stdout.WriteLine( "Usage: stdbuf OPTION... COMMAND [ARG]..." );
		stdout.WriteLine( "Run COMMAND with modified buffering for its standard streams." );
		stdout.WriteLine();
		stdout.WriteLine( "  -i, --input=MODE   adjust standard input stream buffering" );
		stdout.WriteLine( "  -o, --output=MODE  adjust standard output stream buffering" );
		stdout.WriteLine( "  -e, --error=MODE   adjust standard error stream buffering" );
		stdout.WriteLine( "      --help         display this help and exit" );
		stdout.WriteLine( "      --version      output version information and exit" );
		stdout.WriteLine();
		stdout.WriteLine( "MODE 'L' requests line buffering (invalid for standard input)." );
		stdout.WriteLine( "MODE '0' requests unbuffered I/O." );
		stdout.WriteLine( "Otherwise MODE is a byte count; K/M/G/... are powers of 1024," );
		stdout.WriteLine( "KB/MB/GB/... are powers of 1000, and KiB/MiB/GiB/... are binary prefixes." );
		stdout.WriteLine();
		stdout.WriteLine( "Active buffering control is available on supported Linux ELF targets." );
	}

	private static void PrintVersion(
		TextWriter stdout
	) {
		stdout.WriteLine(
			$"{ProgramName} (Icod.CoreUtils) {Version}"
		);
	}
}

/// <summary>
/// Describes the native preload path used to install the stdio buffering shim in a child.
/// </summary>
public readonly record struct StdBufPreloadConfiguration(
	string EnvironmentVariable,
	string LibraryPath,
	string Separator
);

/// <summary>
/// Provides the platform-specific preload capability required by <c>stdbuf</c>.
/// </summary>
public interface IStdBufPlatform {
	/// <summary>
	/// Resolves the native preload configuration for a child process.
	/// </summary>
	bool TryGetPreloadConfiguration(
		out StdBufPreloadConfiguration configuration,
		out string unsupportedReason
	);
}

/// <summary>
/// Resolves the repository-owned Linux ELF preload shim used by <c>stdbuf</c>.
/// </summary>
public sealed class SystemStdBufPlatform : IStdBufPlatform {
	private const string NativeShimName = "libicodstdbuf.so";

	/// <summary>Gets the shared system platform provider.</summary>
	public static SystemStdBufPlatform Instance {
		get;
	} = new();

	private SystemStdBufPlatform() {
	}

	/// <inheritdoc />
	public bool TryGetPreloadConfiguration(
		out StdBufPreloadConfiguration configuration,
		out string unsupportedReason
	) {
		configuration = default;
		if ( !OperatingSystem.IsLinux() ) {
			unsupportedReason = "the native preload implementation is supported only on Linux ELF targets";
			return false;
		}

		var libraryPath = System.IO.Path.GetFullPath(
			System.IO.Path.Combine(
				AppContext.BaseDirectory,
				NativeShimName
			)
		);
		if ( !File.Exists( libraryPath ) ) {
			unsupportedReason = $"the native preload shim '{libraryPath}' is unavailable";
			return false;
		}
		if ( libraryPath.Contains( ':' ) || libraryPath.Any( char.IsWhiteSpace ) ) {
			unsupportedReason = "the native preload shim path cannot contain ':' or whitespace";
			return false;
		}

		configuration = new StdBufPreloadConfiguration(
			"LD_PRELOAD",
			libraryPath,
			":"
		);
		unsupportedReason = string.Empty;
		return true;
	}
}
