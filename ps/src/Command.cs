// Ported to .NET by Timothy J. Bruce <uniblab@hotmail.com>

namespace Icod.ProcPs.Ps;

using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using Icod.ProcPs.Shared;

/// <summary>Resolves ProcPs account operands in both name-to-id and id-to-name directions.</summary>
public interface IProcPsAccountResolver : IProcAccountResolver {
	/// <summary>Resolves a user identifier to a display name.</summary>
	bool TryGetUserName( uint id, out string name );
	/// <summary>Resolves a group identifier to a display name.</summary>
	bool TryGetGroupName( uint id, out string name );
}

/// <summary>Provides host account resolution for the <c>ps</c> presentation engine.</summary>
public sealed class SystemProcPsAccountResolver : IProcPsAccountResolver {
	/// <summary>Gets the singleton system resolver.</summary>
	public static SystemProcPsAccountResolver Instance { get; } = new();

	/// <inheritdoc />
	public bool TryResolveUser( string text, out uint id ) {
		ArgumentNullException.ThrowIfNull( text );
		return SystemProcAccountResolver.Instance.TryResolveUser( text, out id );
	}

	/// <inheritdoc />
	public bool TryResolveGroup( string text, out uint id ) {
		ArgumentNullException.ThrowIfNull( text );
		return SystemProcAccountResolver.Instance.TryResolveGroup( text, out id );
	}

	/// <inheritdoc />
	public bool TryGetUserName( uint id, out string name ) => TryResolveUnixName( "/etc/passwd", id, out name );

	/// <inheritdoc />
	public bool TryGetGroupName( uint id, out string name ) => TryResolveUnixName( "/etc/group", id, out name );

	private static bool TryResolveUnixName( string path, uint id, out string name ) {
		if ( OperatingSystem.IsWindows() ) {
			name = string.Empty;
			return false;
		}
		try {
			foreach ( var line in File.ReadLines( path ) ) {
				if ( string.IsNullOrEmpty( line ) || '#' == line[ 0 ] ) {
					continue;
				}
				var fields = line.Split( ':' );
				if ( 3 > fields.Length ) {
					continue;
				}
				if ( uint.TryParse( fields[ 2 ], NumberStyles.None, CultureInfo.InvariantCulture, out var candidate ) && id == candidate ) {
					name = fields[ 0 ];
					return true;
				}
			}
		} catch ( IOException ) {
		} catch ( UnauthorizedAccessException ) {
		}
		name = string.Empty;
		return false;
	}
}

/// <summary>Implements the procps-ng 4.0.6 <c>ps</c> process-reporting command.</summary>
public static class Command {
	private const int Success = 0;
	private const int Failure = 1;
	private const int Cancelled = 130;
	private const int DefaultWidth = 80;
	private static readonly Encoding Utf8 = new UTF8Encoding( false );
	private static readonly IReadOnlyDictionary<string, ProcReportFieldDefinition> FieldCatalog = ProcReportFieldCatalog.Aliases;
	private static readonly string[] DefaultFields = [ "pid", "tty", "time", "comm" ];
	private static readonly string[] BsdDefaultFields = [ "pid", "tty", "stat", "time", "command" ];
	private static readonly string[] ThreadFields = [ "pid", "lwp", "tgid", "nlwp", "tty", "time", "comm" ];
	private static readonly string[] FullFields = [ "user", "pid", "ppid", "c", "stime", "tty", "time", "cmd" ];
	private static readonly string[] FullExtraFields = [ "user", "pid", "ppid", "c", "sz", "rss", "psr", "stime", "tty", "time", "cmd" ];
	private static readonly string[] LongFields = [ "f", "state", "uid", "pid", "ppid", "c", "pri", "ni", "addr", "sz", "wchan", "tty", "time", "cmd" ];
	private static readonly string[] JobsFields = [ "pid", "pgid", "sid", "tty", "time", "cmd" ];
	private static readonly string[] UserFields = [ "user", "pid", "pcpu", "pmem", "vsz", "rss", "tty", "stat", "start", "time", "command" ];
	private static readonly string[] MemoryFields = [ "pid", "tty", "stat", "time", "vsz", "rss", "pmem", "command" ];
	private const string Usage = """

Usage:
 ps [options]
Selection:
 -A, -e                    select all processes
 -a                        select processes with a terminal, except session leaders
 a                         lift the current-user restriction
 x                         include processes without a controlling terminal
 -d                        select all processes except session leaders
 -N, --deselect            invert the selection
 -p, --pid PIDLIST         select process IDs
 -q, --quick-pid PIDLIST   select process IDs and preserve the supplied order
 --ppid PIDLIST            select parent process IDs
 -g GROUPLIST            select sessions (numeric) or effective groups (named)
 --pgroup PIDLIST          select process-group IDs
 --group GROUPLIST         select effective groups
 -s, --sid PIDLIST         select session IDs
 -t, --tty TTYLIST         select terminals
 -u, --user USERLIST       select effective users
 -U, --User USERLIST       select real users
 -G, --Group GROUPLIST     select real groups
 -C, --command LIST        select short command names
 r                         restrict selection to running tasks
Output:
 L                         list format specifiers
 -o, --format FORMAT       user-defined output format
 -f, -F, -l                full, extra-full, or long format
 j, l, u, v                BSD jobs, long, user, or virtual-memory format
 --sort SPEC               sort by comma-separated [+|-]field keys
 --forest, -H, f           show the process hierarchy
 -L, -T, -m, H, m          show threads where the provider can enumerate them
 e                         append the environment to the command
 c                         show the short command name instead of arguments
 --headers, --no-headers   force or suppress headings
 --cols N, --columns N     set output width
 --width N                 set output width
 w                         widen output; repeat for unlimited width
 --personality NAME        select linux, posix, bsd, sunos4, digital, hp, or aix
 --help                    display this help and exit
 --version                 output version information and exit
""";

	/// <summary>Runs <c>ps</c> synchronously.</summary>
	/// <param name="args">Command-line arguments.</param>
	/// <param name="stdout">Optional output writer.</param>
	/// <param name="stderr">Optional error writer.</param>
	/// <returns>The process exit status.</returns>
	public static int Run( string[] args, TextWriter? stdout = null, TextWriter? stderr = null ) {
		ArgumentNullException.ThrowIfNull( args );
		using var output = new MemoryStream();
		using var error = new MemoryStream();
		var status = RunAsync( args, stdout: output, stderr: error ).GetAwaiter().GetResult();
		( stdout ?? Console.Out ).Write( Utf8.GetString( output.ToArray() ) );
		( stderr ?? Console.Error ).Write( Utf8.GetString( error.ToArray() ) );
		return status;
	}

	/// <summary>Runs <c>ps</c> asynchronously over injectable ProcPs providers.</summary>
	/// <param name="args">Command-line arguments.</param>
	/// <param name="stdout">Destination for standard output.</param>
	/// <param name="stderr">Destination for standard error.</param>
	/// <param name="processProvider">Optional process provider.</param>
	/// <param name="metricsProvider">Optional system-metric provider.</param>
	/// <param name="supplementProvider">Optional provider for elapsed time, environment, and lightweight tasks.</param>
	/// <param name="accountResolver">Optional user/group resolver.</param>
	/// <param name="currentProcessIdProvider">Optional current-process-id source for deterministic tests.</param>
	/// <param name="environment">Optional personality environment.</param>
	/// <param name="nowProvider">Optional wall-clock source for deterministic start-time formatting.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>The process exit status.</returns>
	public static async Task<int> RunAsync(
		string[] args,
		Stream? stdout = null,
		Stream? stderr = null,
		IProcProcessProvider? processProvider = null,
		IProcSystemMetricsProvider? metricsProvider = null,
		IProcMatchSupplementProvider? supplementProvider = null,
		IProcPsAccountResolver? accountResolver = null,
		Func<int>? currentProcessIdProvider = null,
		IReadOnlyDictionary<string, string?>? environment = null,
		Func<DateTimeOffset>? nowProvider = null,
		CancellationToken cancellationToken = default
	) {
		ArgumentNullException.ThrowIfNull( args );
		var hostEnvironment = environment ?? ReadPersonalityEnvironment();
		var parsed = ParseArguments( args, hostEnvironment, accountResolver ?? SystemProcPsAccountResolver.Instance );
		if ( null != parsed.Error ) {
			await WriteLineAsync( stderr, $"ps: {parsed.Error}", cancellationToken ).ConfigureAwait( false );
			return Failure;
		}
		if ( parsed.ShowHelp ) {
			await WriteAsync( stdout, NormalizeLineEndings( Usage ), cancellationToken ).ConfigureAwait( false );
			return Success;
		}
		if ( parsed.ShowVersion ) {
			await WriteLineAsync( stdout, "ps from procps-ng 4.0.6", cancellationToken ).ConfigureAwait( false );
			return Success;
		}
		if ( parsed.ShowFieldList ) {
			foreach ( var field in FieldCatalog.Values.GroupBy( value => value.Name, StringComparer.Ordinal ).Select( group => group.First() ).OrderBy( value => value.Name, StringComparer.Ordinal ) ) {
				await WriteLineAsync( stdout, $"{field.Name,-16} {field.Header}", cancellationToken ).ConfigureAwait( false );
			}
			return Success;
		}
		try {
			cancellationToken.ThrowIfCancellationRequested();
			var processes = processProvider ?? SystemProcProcessProvider.Instance;
			var collection = await processes.GetProcessesAsync( cancellationToken ).ConfigureAwait( false );
			var currentProcessId = currentProcessIdProvider?.Invoke() ?? Environment.ProcessId;
			var current = collection.Processes.FirstOrDefault( process => process.ProcessId == currentProcessId );
			var selected = SelectProcesses( collection.Processes, parsed, current );
			var needSupplements = parsed.ShowThreads || parsed.IncludeEnvironment || FieldsNeedSupplements( parsed.Fields );
			var candidates = await BuildCandidatesAsync(
				selected,
				needSupplements,
				parsed.ShowThreads,
				supplementProvider,
				cancellationToken
			).ConfigureAwait( false );
			if ( parsed.QuickProcessIds.Count > 0 ) {
				candidates = OrderQuick( candidates, parsed.QuickProcessIds );
			} else if ( parsed.Forest ) {
				candidates = OrderForest( candidates );
			} else if ( 0 < parsed.SortKeys.Count ) {
				candidates = SortCandidates( candidates, parsed.SortKeys );
			}
			ProcSystemSnapshot? system = null;
			if ( FieldsNeedMetrics( parsed.Fields ) ) {
				system = await ( metricsProvider ?? SystemProcSystemMetricsProvider.Instance ).GetSnapshotAsync( cancellationToken ).ConfigureAwait( false );
			}
			var now = nowProvider?.Invoke() ?? DateTimeOffset.Now;
			await RenderAsync( candidates, parsed, system, accountResolver ?? SystemProcPsAccountResolver.Instance, now, stdout, cancellationToken ).ConfigureAwait( false );
			return Success;
		} catch ( OperationCanceledException ) {
			return Cancelled;
		} catch ( PlatformNotSupportedException exception ) {
			await WriteLineAsync( stderr, $"ps: {exception.Message}", CancellationToken.None ).ConfigureAwait( false );
			return Failure;
		} catch ( Exception exception ) when ( exception is IOException or UnauthorizedAccessException or InvalidOperationException ) {
			await WriteLineAsync( stderr, $"ps: {exception.Message}", CancellationToken.None ).ConfigureAwait( false );
			return Failure;
		}
	}

	private static ParsedArguments ParseArguments( string[] args, IReadOnlyDictionary<string, string?> environment, IProcPsAccountResolver accountResolver ) {
		ArgumentNullException.ThrowIfNull( args );
		ArgumentNullException.ThrowIfNull( environment );
		ArgumentNullException.ThrowIfNull( accountResolver );
		var result = new ParsedArguments {
			Personality = ProcPersonalityResolver.ResolveEnvironment( environment )
		};
		for ( var index = 0; index < args.Length; index++ ) {
			var argument = args[ index ];
			if ( int.TryParse( argument, NumberStyles.None, CultureInfo.InvariantCulture, out var operandPid ) && 0 < operandPid ) {
				result.ProcessIds.Add( operandPid );
				result.HasExplicitSelection = true;
				continue;
			}
			if ( 1 < argument.Length && '-' == argument[ 0 ] && int.TryParse( argument[ 1.. ], NumberStyles.None, CultureInfo.InvariantCulture, out var negativeFormPid ) && 0 < negativeFormPid ) {
				result.ProcessIds.Add( negativeFormPid );
				result.HasExplicitSelection = true;
				continue;
			}
			if ( TryParseBsdOperandOption( args, ref index, argument, result, accountResolver ) ) {
				continue;
			}
			if ( "--" == argument ) {
				if ( index + 1 < args.Length ) {
					result.Fail( $"garbage option: {args[ index + 1 ]}" );
				}
				break;
			}
			if ( argument.StartsWith( "--", StringComparison.Ordinal ) ) {
				ParseLongOption( args, ref index, argument, result, accountResolver );
				continue;
			}
			if ( argument.StartsWith( '-' ) && 1 < argument.Length ) {
				ParseUnixOptions( args, ref index, argument, result, accountResolver );
				continue;
			}
			ParseBsdOptions( argument, result );
		}
		if ( null != result.Error || result.ShowHelp || result.ShowVersion || result.ShowFieldList ) {
			return result;
		}
		if ( 0 < result.QuickProcessIds.Count && ( 0 < result.SortKeys.Count || result.Forest || result.HasNonQuickSelection() ) ) {
			result.Fail( "-q/--quick-pid is incompatible with other selection options, sorting, and forest output" );
			return result;
		}
		if ( !result.CustomFormat ) {
			ApplyPresetFormat( result );
		}
		if ( 0 == result.Fields.Count ) {
			result.AddFields( DefaultFields );
		}
		if ( result.SecurityFormat && !result.Fields.Any( field => ProcReportFieldKind.SecurityLabel == field.Definition.Kind ) ) {
			result.PrependField( "label" );
		}
		return result;
	}

	private static void ParseLongOption( string[] args, ref int index, string argument, ParsedArguments result, IProcPsAccountResolver accountResolver ) {
		ArgumentNullException.ThrowIfNull( args );
		ArgumentNullException.ThrowIfNull( argument );
		ArgumentNullException.ThrowIfNull( result );
		ArgumentNullException.ThrowIfNull( accountResolver );
		var equals = argument.IndexOf( '=' );
		var name = ( 0 <= equals )
			? argument[ ..equals ]
			: argument
		;
		var attached = ( 0 <= equals )
			? argument[ ( equals + 1 ).. ]
			: null
		;
		switch ( name ) {
			case "--help":
				if ( null != attached && !IsHelpSection( attached ) ) {
					result.Fail( $"unknown help section '{attached}'" );
				} else {
					result.ShowHelp = true;
				}
				break;
			case "--version":
				if ( null != attached ) {
					result.Fail( "option '--version' doesn't allow an argument" );
				} else {
					result.ShowVersion = true;
				}
				break;
			case "--all":
				result.SelectAll = true;
				result.HasExplicitSelection = true;
				break;
			case "--deselect":
				result.Invert = true;
				break;
			case "--pid":
				ParseIdentifierOption( args, ref index, attached, name, result.ProcessIds, result );
				result.HasExplicitSelection = true;
				break;
			case "--quick-pid":
				ParseOrderedIdentifierOption( args, ref index, attached, name, result.QuickProcessIds, result );
				result.HasExplicitSelection = true;
				break;
			case "--ppid":
				ParseIdentifierOption( args, ref index, attached, name, result.ParentIds, result );
				result.HasExplicitSelection = true;
				break;
			case "--pgroup":
				ParseIdentifierOption( args, ref index, attached, name, result.ProcessGroupIds, result );
				result.HasExplicitSelection = true;
				break;
			case "--group":
				ParseAccountOption( args, ref index, attached, name, result.EffectiveGroupIds, result, accountResolver, false );
				result.HasExplicitSelection = true;
				break;
			case "--sid":
				ParseIdentifierOption( args, ref index, attached, name, result.SessionIds, result );
				result.HasExplicitSelection = true;
				break;
			case "--tty":
				ParseStringOption( args, ref index, attached, name, result.Terminals, result );
				result.HasExplicitSelection = true;
				break;
			case "--user":
				ParseAccountOption( args, ref index, attached, name, result.EffectiveUserIds, result, accountResolver, true );
				result.HasExplicitSelection = true;
				break;
			case "--User":
				ParseAccountOption( args, ref index, attached, name, result.RealUserIds, result, accountResolver, true );
				result.HasExplicitSelection = true;
				break;
			case "--Group":
				ParseAccountOption( args, ref index, attached, name, result.RealGroupIds, result, accountResolver, false );
				result.HasExplicitSelection = true;
				break;
			case "--command":
				ParseStringOption( args, ref index, attached, name, result.CommandNames, result );
				result.HasExplicitSelection = true;
				break;
			case "--format":
				if ( TryTakeValue( args, ref index, attached, name, result, out var format ) ) {
					ParseFormat( format!, result );
				}
				break;
			case "--sort":
				if ( TryTakeValue( args, ref index, attached, name, result, out var sort ) ) {
					ParseSort( sort!, result );
				}
				break;
			case "--forest":
				result.Forest = true;
				break;
			case "--context":
				result.SecurityFormat = true;
				break;
			case "--headers":
				result.HeaderMode = HeaderMode.Show;
				break;
			case "--no-heading":
			case "--no-headers":
				result.HeaderMode = HeaderMode.Hide;
				break;
			case "--cols":
			case "--columns":
			case "--width":
				if ( TryTakeValue( args, ref index, attached, name, result, out var width ) ) {
					ParseWidth( width!, result );
				}
				break;
			case "--personality":
				if ( TryTakeValue( args, ref index, attached, name, result, out var personality ) ) {
					if ( !ProcPersonalityResolver.TryParse( personality, out var parsed ) ) {
						result.Fail( $"unknown personality '{personality}'" );
					} else {
						result.Personality = parsed;
					}
				}
				break;
			default:
				result.Fail( $"unknown option {name}" );
				break;
		}
	}

	private static void ParseUnixOptions( string[] args, ref int index, string argument, ParsedArguments result, IProcPsAccountResolver accountResolver ) {
		ArgumentNullException.ThrowIfNull( args );
		ArgumentNullException.ThrowIfNull( argument );
		ArgumentNullException.ThrowIfNull( result );
		ArgumentNullException.ThrowIfNull( accountResolver );
		for ( var optionIndex = 1; optionIndex < argument.Length; optionIndex++ ) {
			var option = argument[ optionIndex ];
			switch ( option ) {
				case 'A':
				case 'e':
					result.SelectAll = true;
					result.HasExplicitSelection = true;
					break;
				case 'a':
					result.SelectTerminalProcesses = true;
					result.HasExplicitSelection = true;
					break;
				case 'd':
					result.SelectExceptSessionLeaders = true;
					result.HasExplicitSelection = true;
					break;
				case 'N':
					result.Invert = true;
					break;
				case 'p':
					ParseUnixValue( args, ref index, argument, ref optionIndex, option, result.ProcessIds, result );
					result.HasExplicitSelection = true;
					break;
				case 'q':
					ParseUnixOrderedValue( args, ref index, argument, ref optionIndex, option, result.QuickProcessIds, result );
					result.HasExplicitSelection = true;
					break;
				case 'g':
					ParseUnixSessionOrGroupValue( args, ref index, argument, ref optionIndex, option, result, accountResolver );
					result.HasExplicitSelection = true;
					break;
				case 's':
					ParseUnixValue( args, ref index, argument, ref optionIndex, option, result.SessionIds, result );
					result.HasExplicitSelection = true;
					break;
				case 't':
					ParseUnixStringValue( args, ref index, argument, ref optionIndex, option, result.Terminals, result );
					result.HasExplicitSelection = true;
					break;
				case 'u':
					ParseUnixAccountValue( args, ref index, argument, ref optionIndex, option, result.EffectiveUserIds, result, accountResolver, true );
					result.HasExplicitSelection = true;
					break;
				case 'U':
					ParseUnixAccountValue( args, ref index, argument, ref optionIndex, option, result.RealUserIds, result, accountResolver, true );
					result.HasExplicitSelection = true;
					break;
				case 'G':
					ParseUnixAccountValue( args, ref index, argument, ref optionIndex, option, result.RealGroupIds, result, accountResolver, false );
					result.HasExplicitSelection = true;
					break;
				case 'C':
					ParseUnixStringValue( args, ref index, argument, ref optionIndex, option, result.CommandNames, result );
					result.HasExplicitSelection = true;
					break;
				case 'o':
					if ( TryTakeUnixValue( args, ref index, argument, ref optionIndex, option, result, out var format ) ) {
						ParseFormat( format!, result );
					}
					break;
				case 'f':
					result.FullFormat = true;
					break;
				case 'F':
					result.FullExtraFormat = true;
					break;
				case 'l':
					result.LongFormat = true;
					break;
				case 'j':
					result.JobsFormat = true;
					break;
				case 'M':
					result.SecurityFormat = true;
					break;
				case 'V':
					result.ShowVersion = true;
					break;
				case 'H':
					result.Forest = true;
					break;
				case 'L':
				case 'T':
				case 'm':
					result.ShowThreads = true;
					break;
				case 'w':
					result.Widen();
					break;
				case 'Z':
					result.SecurityFormat = true;
					break;
				case 'h':
					result.HeaderMode = HeaderMode.Hide;
					break;
				default:
					result.Fail( $"unknown option -{option}" );
					return;
			}
		}
	}

	private static void ParseBsdOptions( string argument, ParsedArguments result ) {
		ArgumentNullException.ThrowIfNull( argument );
		ArgumentNullException.ThrowIfNull( result );
		if ( 0 == argument.Length ) {
			return;
		}
		foreach ( var option in argument ) {
			switch ( option ) {
				case 'a':
					result.BsdAllUsers = true;
					result.Personality = ProcPersonality.Bsd;
					break;
				case 'x':
					result.BsdIncludeNoTerminal = true;
					result.Personality = ProcPersonality.Bsd;
					break;
				case 'u':
					result.UserFormat = true;
					result.Personality = ProcPersonality.Bsd;
					break;
				case 'l':
					result.LongFormat = true;
					result.Personality = ProcPersonality.Bsd;
					break;
				case 'j':
					result.JobsFormat = true;
					result.Personality = ProcPersonality.Bsd;
					break;
				case 'L':
					result.ShowFieldList = true;
					result.Personality = ProcPersonality.Bsd;
					break;
				case 'v':
					result.MemoryFormat = true;
					result.Personality = ProcPersonality.Bsd;
					break;
				case 'r':
					result.RunningOnly = true;
					break;
				case 'f':
					result.Forest = true;
					break;
				case 'e':
					result.IncludeEnvironment = true;
					break;
				case 'c':
					result.CommandNameOnly = true;
					break;
				case 'H':
				case 'm':
					result.ShowThreads = true;
					break;
				case 'w':
					result.Widen();
					break;
				case 'T':
					result.CurrentTerminalOnly = true;
					result.HasExplicitSelection = true;
					break;
				case 'Z':
					result.SecurityFormat = true;
					break;
				case 'V':
					result.ShowVersion = true;
					break;
				case 'h':
					result.HeaderMode = HeaderMode.Hide;
					break;
				default:
					result.Fail( $"unknown BSD option {option}" );
					return;
			}
		}
	}

	private static void ApplyPresetFormat( ParsedArguments result ) {
		ArgumentNullException.ThrowIfNull( result );
		IEnumerable<string> fields;
		if ( result.FullExtraFormat ) {
			fields = FullExtraFields;
		} else if ( result.FullFormat ) {
			fields = FullFields;
		} else if ( result.UserFormat ) {
			fields = UserFields;
		} else if ( result.MemoryFormat ) {
			fields = MemoryFields;
		} else if ( result.JobsFormat ) {
			fields = JobsFields;
		} else if ( result.LongFormat ) {
			fields = LongFields;
		} else if ( result.ShowThreads ) {
			fields = ThreadFields;
		} else if ( ProcPersonality.Bsd == result.Personality ) {
			fields = BsdDefaultFields;
		} else {
			fields = DefaultFields;
		}
		result.AddFields( fields );
	}

	private static void ParseFormat( string text, ParsedArguments result ) {
		ArgumentNullException.ThrowIfNull( text );
		ArgumentNullException.ThrowIfNull( result );
		if ( !result.CustomFormat ) {
			result.Fields.Clear();
			result.CustomFormat = true;
		}
		foreach ( var token in SplitFormat( text ) ) {
			var equals = token.IndexOf( '=' );
			var nameAndWidth = ( 0 <= equals )
				? token[ ..equals ]
				: token
			;
			var header = ( 0 <= equals )
				? token[ ( equals + 1 ).. ]
				: null
			;
			var name = nameAndWidth;
			int? width = null;
			var colon = nameAndWidth.LastIndexOf( ':' );
			if ( 0 < colon ) {
				name = nameAndWidth[ ..colon ];
				if ( !int.TryParse( nameAndWidth[ ( colon + 1 ).. ], NumberStyles.None, CultureInfo.InvariantCulture, out var parsedWidth ) || 0 >= parsedWidth ) {
					result.Fail( $"invalid width in format specifier '{token}'" );
					return;
				}
				width = parsedWidth;
			}
			if ( !FieldCatalog.TryGetValue( name, out var definition ) ) {
				result.Fail( $"unknown user-defined format specifier '{name}'" );
				return;
			}
			result.Fields.Add( new SelectedField( definition, header, width ) );
		}
	}

	private static void ParseSort( string text, ParsedArguments result ) {
		ArgumentNullException.ThrowIfNull( text );
		ArgumentNullException.ThrowIfNull( result );
		foreach ( var raw in text.Split( ',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries ) ) {
			var descending = '-' == raw[ 0 ];
			var key = ( '-' == raw[ 0 ] || '+' == raw[ 0 ] )
				? raw[ 1.. ]
				: raw
			;
			if ( 0 == key.Length || !FieldCatalog.TryGetValue( key, out var definition ) ) {
				result.Fail( $"unknown sort specifier '{key}'" );
				return;
			}
			result.SortKeys.Add( new SortKey( definition.Kind, descending ) );
		}
	}

	private static IReadOnlyList<ProcProcessSnapshot> SelectProcesses(
		IReadOnlyList<ProcProcessSnapshot> processes,
		ParsedArguments options,
		ProcProcessSnapshot? current
	) {
		ArgumentNullException.ThrowIfNull( processes );
		ArgumentNullException.ThrowIfNull( options );
		IEnumerable<ProcProcessSnapshot> selected;
		if ( 0 < options.QuickProcessIds.Count ) {
			var set = options.QuickProcessIds.ToHashSet();
			selected = processes.Where( process => set.Contains( process.ProcessId ) );
		} else if ( options.SelectAll ) {
			selected = processes;
		} else if ( options.HasExplicitSelection ) {
			selected = processes.Where( process => MatchesExplicitSelection( process, options, current ) );
		} else {
			selected = processes.Where( process => MatchesDefaultSelection( process, options, current ) );
		}
		if ( options.RunningOnly ) {
			selected = selected.Where( process => process.State.HasValue && ProcProcessState.Running == process.State.Value );
		}
		if ( options.Invert ) {
			var selectedIds = selected.Select( process => process.ProcessId ).ToHashSet();
			selected = processes.Where( process => !selectedIds.Contains( process.ProcessId ) );
		}
		return selected.ToArray();
	}

	private static bool MatchesDefaultSelection( ProcProcessSnapshot process, ParsedArguments options, ProcProcessSnapshot? current ) {
		ArgumentNullException.ThrowIfNull( process );
		ArgumentNullException.ThrowIfNull( options );
		if ( options.BsdAllUsers ) {
			if ( options.BsdIncludeNoTerminal ) {
				return true;
			}
			return process.Terminal.HasValue && !( process.SessionId.HasValue && process.SessionId.Value == process.ProcessId );
		}
		var userMatches = null == current || !current.EffectiveUserId.HasValue
			|| ( process.EffectiveUserId.HasValue && process.EffectiveUserId.Value == current.EffectiveUserId.Value );
		if ( !userMatches ) {
			return false;
		}
		if ( options.BsdIncludeNoTerminal ) {
			return true;
		}
		if ( null == current || !current.Terminal.HasValue ) {
			return process.Terminal.HasValue;
		}
		return SameTerminal( process.Terminal, current.Terminal );
	}

	private static bool MatchesExplicitSelection( ProcProcessSnapshot process, ParsedArguments options, ProcProcessSnapshot? current ) {
		ArgumentNullException.ThrowIfNull( process );
		ArgumentNullException.ThrowIfNull( options );
		var anyCriterion = false;
		var matched = false;
		if ( 0 < options.ProcessIds.Count ) {
			anyCriterion = true;
			matched |= options.ProcessIds.Contains( process.ProcessId );
		}
		if ( 0 < options.ParentIds.Count ) {
			anyCriterion = true;
			matched |= process.ParentProcessId.HasValue && options.ParentIds.Contains( process.ParentProcessId.Value );
		}
		if ( 0 < options.ProcessGroupIds.Count ) {
			anyCriterion = true;
			matched |= process.ProcessGroupId.HasValue && options.ProcessGroupIds.Contains( process.ProcessGroupId.Value );
		}
		if ( 0 < options.SessionIds.Count ) {
			anyCriterion = true;
			matched |= process.SessionId.HasValue && options.SessionIds.Contains( process.SessionId.Value );
		}
		if ( 0 < options.EffectiveUserIds.Count ) {
			anyCriterion = true;
			matched |= process.EffectiveUserId.HasValue && options.EffectiveUserIds.Contains( process.EffectiveUserId.Value );
		}
		if ( 0 < options.RealUserIds.Count ) {
			anyCriterion = true;
			matched |= process.RealUserId.HasValue && options.RealUserIds.Contains( process.RealUserId.Value );
		}
		if ( 0 < options.EffectiveGroupIds.Count ) {
			anyCriterion = true;
			matched |= process.EffectiveGroupId.HasValue && options.EffectiveGroupIds.Contains( process.EffectiveGroupId.Value );
		}
		if ( 0 < options.RealGroupIds.Count ) {
			anyCriterion = true;
			matched |= process.RealGroupId.HasValue && options.RealGroupIds.Contains( process.RealGroupId.Value );
		}
		if ( 0 < options.Terminals.Count ) {
			anyCriterion = true;
			matched |= MatchesTerminal( process.Terminal, options.Terminals );
		}
		if ( 0 < options.CommandNames.Count ) {
			anyCriterion = true;
			matched |= process.CommandName.HasValue && options.CommandNames.Contains( process.CommandName.Value );
		}
		if ( options.SelectTerminalProcesses ) {
			anyCriterion = true;
			matched |= process.Terminal.HasValue && !( process.SessionId.HasValue && process.SessionId.Value == process.ProcessId );
		}
		if ( options.CurrentTerminalOnly ) {
			anyCriterion = true;
			matched |= null != current && SameTerminal( process.Terminal, current.Terminal );
		}
		if ( options.SelectExceptSessionLeaders ) {
			anyCriterion = true;
			matched |= !( process.SessionId.HasValue && process.SessionId.Value == process.ProcessId );
		}
		return anyCriterion && matched;
	}

	private static bool MatchesTerminal( ProcObservedValue<ProcTerminalInfo> terminal, IReadOnlySet<string> selectors ) {
		ArgumentNullException.ThrowIfNull( terminal );
		ArgumentNullException.ThrowIfNull( selectors );
		if ( !terminal.HasValue ) {
			return selectors.Contains( "?" ) || selectors.Contains( "-" );
		}
		var value = NormalizeTerminal( terminal.Value );
		return selectors.Contains( value ) || selectors.Contains( $"/dev/{value}" );
	}

	private static bool SameTerminal( ProcObservedValue<ProcTerminalInfo> left, ProcObservedValue<ProcTerminalInfo> right ) {
		ArgumentNullException.ThrowIfNull( left );
		ArgumentNullException.ThrowIfNull( right );
		if ( !left.HasValue || !right.HasValue ) {
			return false;
		}
		return string.Equals( NormalizeTerminal( left.Value ), NormalizeTerminal( right.Value ), StringComparison.Ordinal );
	}

	private static async Task<IReadOnlyList<ProcMatchCandidate>> BuildCandidatesAsync(
		IReadOnlyList<ProcProcessSnapshot> processes,
		bool needSupplements,
		bool includeThreads,
		IProcMatchSupplementProvider? supplementProvider,
		CancellationToken cancellationToken
	) {
		ArgumentNullException.ThrowIfNull( processes );
		if ( needSupplements ) {
			return await ( supplementProvider ?? SystemProcMatchSupplementProvider.Instance )
				.GetCandidatesAsync( processes, includeThreads, cancellationToken )
				.ConfigureAwait( false );
		}
		return processes.Select( process => new ProcMatchCandidate(
			process,
			new ProcMatchSupplement {
				ThreadGroupId = process.ProcessId
			}
		) ).ToArray();
	}

	private static IReadOnlyList<ProcMatchCandidate> OrderQuick( IReadOnlyList<ProcMatchCandidate> candidates, IReadOnlyList<int> processIds ) {
		ArgumentNullException.ThrowIfNull( candidates );
		ArgumentNullException.ThrowIfNull( processIds );
		var byId = candidates.GroupBy( candidate => candidate.Process.ProcessId ).ToDictionary( group => group.Key, group => group.ToArray() );
		var result = new List<ProcMatchCandidate>();
		foreach ( var processId in processIds ) {
			if ( byId.TryGetValue( processId, out var values ) ) {
				result.AddRange( values );
			}
		}
		return result;
	}

	private static IReadOnlyList<ProcMatchCandidate> SortCandidates( IReadOnlyList<ProcMatchCandidate> candidates, IReadOnlyList<SortKey> keys ) {
		ArgumentNullException.ThrowIfNull( candidates );
		ArgumentNullException.ThrowIfNull( keys );
		var result = candidates.ToArray();
		Array.Sort( result, ( left, right ) => CompareCandidates( left, right, keys ) );
		return result;
	}

	private static int CompareCandidates( ProcMatchCandidate left, ProcMatchCandidate right, IReadOnlyList<SortKey> keys ) {
		ArgumentNullException.ThrowIfNull( left );
		ArgumentNullException.ThrowIfNull( right );
		ArgumentNullException.ThrowIfNull( keys );
		foreach ( var key in keys ) {
			var comparison = CompareField( left.Process, right.Process, key.Kind );
			if ( 0 != comparison ) {
				return ( key.Descending )
					? -comparison
					: comparison
				;
			}
		}
		return left.Process.ProcessId.CompareTo( right.Process.ProcessId );
	}

	private static int CompareField( ProcProcessSnapshot left, ProcProcessSnapshot right, ProcReportFieldKind kind ) {
		ArgumentNullException.ThrowIfNull( left );
		ArgumentNullException.ThrowIfNull( right );
		return kind switch {
			ProcReportFieldKind.Pid => left.ProcessId.CompareTo( right.ProcessId ),
			ProcReportFieldKind.ParentPid => CompareObserved( left.ParentProcessId, right.ParentProcessId ),
			ProcReportFieldKind.ProcessGroup => CompareObserved( left.ProcessGroupId, right.ProcessGroupId ),
			ProcReportFieldKind.Session => CompareObserved( left.SessionId, right.SessionId ),
			ProcReportFieldKind.EffectiveUserId => CompareObserved( left.EffectiveUserId, right.EffectiveUserId ),
			ProcReportFieldKind.RealUserId => CompareObserved( left.RealUserId, right.RealUserId ),
			ProcReportFieldKind.EffectiveGroupId => CompareObserved( left.EffectiveGroupId, right.EffectiveGroupId ),
			ProcReportFieldKind.RealGroupId => CompareObserved( left.RealGroupId, right.RealGroupId ),
			ProcReportFieldKind.Nice => CompareObserved( left.NiceValue, right.NiceValue ),
			ProcReportFieldKind.Threads => CompareObserved( left.ThreadCount, right.ThreadCount ),
			ProcReportFieldKind.ResidentMemory => CompareObserved( left.ResidentMemoryBytes, right.ResidentMemoryBytes ),
			ProcReportFieldKind.VirtualMemory => CompareObserved( left.VirtualMemoryBytes, right.VirtualMemoryBytes ),
			ProcReportFieldKind.Command => CompareObservedString( left.CommandName, right.CommandName ),
			_ => left.ProcessId.CompareTo( right.ProcessId )
		};
	}

	private static IReadOnlyList<ProcMatchCandidate> OrderForest( IReadOnlyList<ProcMatchCandidate> candidates ) {
		ArgumentNullException.ThrowIfNull( candidates );
		var byParent = candidates
			.GroupBy( candidate => ( candidate.Process.ParentProcessId.HasValue )
			? candidate.Process.ParentProcessId.Value
			: int.MinValue
		)
			.ToDictionary( group => group.Key, group => group.OrderBy( candidate => candidate.Process.ProcessId ).ToArray() );
		var ids = candidates.Select( candidate => candidate.Process.ProcessId ).ToHashSet();
		var roots = candidates
			.Where( candidate => !candidate.Process.ParentProcessId.HasValue || !ids.Contains( candidate.Process.ParentProcessId.Value ) )
			.OrderBy( candidate => candidate.Process.ProcessId )
			.ToArray();
		var result = new List<ProcMatchCandidate>();
		var visited = new HashSet<int>();
		foreach ( var root in roots ) {
			AppendTree( root, 0, byParent, visited, result );
		}
		foreach ( var candidate in candidates.OrderBy( candidate => candidate.Process.ProcessId ) ) {
			if ( !visited.Contains( candidate.Process.ProcessId ) ) {
				AppendTree( candidate, 0, byParent, visited, result );
			}
		}
		return result;
	}

	private static void AppendTree(
		ProcMatchCandidate candidate,
		int depth,
		IReadOnlyDictionary<int, ProcMatchCandidate[]> byParent,
		ISet<int> visited,
		ICollection<ProcMatchCandidate> result
	) {
		ArgumentNullException.ThrowIfNull( candidate );
		ArgumentNullException.ThrowIfNull( byParent );
		ArgumentNullException.ThrowIfNull( visited );
		ArgumentNullException.ThrowIfNull( result );
		if ( !visited.Add( candidate.Process.ProcessId ) ) {
			return;
		}
		result.Add( candidate );
		if ( byParent.TryGetValue( candidate.Process.ProcessId, out var children ) ) {
			foreach ( var child in children ) {
				AppendTree( child, depth + 1, byParent, visited, result );
			}
		}
	}

	private static int GetForestDepth( ProcProcessSnapshot process, IReadOnlyList<ProcMatchCandidate> candidates ) {
		ArgumentNullException.ThrowIfNull( process );
		ArgumentNullException.ThrowIfNull( candidates );
		var byId = candidates.ToDictionary( candidate => candidate.Process.ProcessId, candidate => candidate.Process );
		var depth = 0;
		var current = process;
		var visited = new HashSet<int> { current.ProcessId };
		while ( current.ParentProcessId.HasValue && byId.TryGetValue( current.ParentProcessId.Value, out var parent ) && visited.Add( parent.ProcessId ) ) {
			depth++;
			current = parent;
		}
		return depth;
	}

	private static async Task RenderAsync(
		IReadOnlyList<ProcMatchCandidate> candidates,
		ParsedArguments options,
		ProcSystemSnapshot? system,
		IProcPsAccountResolver accountResolver,
		DateTimeOffset now,
		Stream? stdout,
		CancellationToken cancellationToken
	) {
		ArgumentNullException.ThrowIfNull( candidates );
		ArgumentNullException.ThrowIfNull( options );
		ArgumentNullException.ThrowIfNull( accountResolver );
		var showHeader = HeaderMode.Show == options.HeaderMode
			|| ( HeaderMode.Default == options.HeaderMode && options.Fields.Any( field => null == field.HeaderOverride || 0 < field.HeaderOverride.Length ) );
		if ( showHeader ) {
			var header = string.Join( " ", options.Fields.Select( field => PadField( field.Header, field.Width, field.Definition.RightAligned ) ) ).TrimEnd();
			await WriteLineAsync( stdout, LimitWidth( header, options.Width ), cancellationToken ).ConfigureAwait( false );
		}
		foreach ( var candidate in candidates ) {
			cancellationToken.ThrowIfCancellationRequested();
			var forestDepth = ( options.Forest )
				? GetForestDepth( candidate.Process, candidates )
				: 0
			;
			var context = new RenderContext( candidate, system, accountResolver, now, options, forestDepth );
			var values = new string[ options.Fields.Count ];
			for ( var index = 0; index < options.Fields.Count; index++ ) {
				var field = options.Fields[ index ];
				var value = FormatField( field.Definition.Kind, context );
				values[ index ] = ( index + 1 == options.Fields.Count )
					? value
					: PadField( value, field.Width, field.Definition.RightAligned )
				;
			}
			var line = string.Join( " ", values ).TrimEnd();
			await WriteLineAsync( stdout, LimitWidth( line, options.Width ), cancellationToken ).ConfigureAwait( false );
		}
	}

	private static string FormatField( ProcReportFieldKind kind, RenderContext context ) {
		ArgumentNullException.ThrowIfNull( context );
		var process = context.Candidate.Process;
		return kind switch {
			ProcReportFieldKind.Pid => process.ProcessId.ToString( CultureInfo.InvariantCulture ),
			ProcReportFieldKind.ThreadId => process.ProcessId.ToString( CultureInfo.InvariantCulture ),
			ProcReportFieldKind.ThreadGroupId => context.Candidate.Supplement.ThreadGroupId.ToString( CultureInfo.InvariantCulture ),
			ProcReportFieldKind.ParentPid => FormatObserved( process.ParentProcessId ),
			ProcReportFieldKind.ProcessGroup => FormatObserved( process.ProcessGroupId ),
			ProcReportFieldKind.Session => FormatObserved( process.SessionId ),
			ProcReportFieldKind.EffectiveUserId => FormatObserved( process.EffectiveUserId ),
			ProcReportFieldKind.RealUserId => FormatObserved( process.RealUserId ),
			ProcReportFieldKind.EffectiveGroupId => FormatObserved( process.EffectiveGroupId ),
			ProcReportFieldKind.RealGroupId => FormatObserved( process.RealGroupId ),
			ProcReportFieldKind.EffectiveUserName => FormatAccountName( process.EffectiveUserId, context.AccountResolver, true ),
			ProcReportFieldKind.RealUserName => FormatAccountName( process.RealUserId, context.AccountResolver, true ),
			ProcReportFieldKind.EffectiveGroupName => FormatAccountName( process.EffectiveGroupId, context.AccountResolver, false ),
			ProcReportFieldKind.RealGroupName => FormatAccountName( process.RealGroupId, context.AccountResolver, false ),
			ProcReportFieldKind.Terminal => FormatTerminal( process.Terminal ),
			ProcReportFieldKind.State => FormatState( process.State ),
			ProcReportFieldKind.Stat => FormatStat( process ),
			ProcReportFieldKind.Nice => FormatObserved( process.NiceValue ),
			ProcReportFieldKind.Priority => FormatPriority( process ),
			ProcReportFieldKind.Threads => FormatObserved( process.ThreadCount ),
			ProcReportFieldKind.ResidentMemory => FormatKilobytes( process.ResidentMemoryBytes ),
			ProcReportFieldKind.VirtualMemory => FormatKilobytes( process.VirtualMemoryBytes ),
			ProcReportFieldKind.SizePages => FormatSizePages( process.VirtualMemoryBytes ),
			ProcReportFieldKind.Command => FormatCommand( context ),
			ProcReportFieldKind.CommandName => FormatCommandName( context ),
			ProcReportFieldKind.Environment => FormatEnvironment( context.Candidate.Supplement.Environment ),
			ProcReportFieldKind.Elapsed => FormatElapsed( context.Candidate.Supplement.Elapsed, false ),
			ProcReportFieldKind.ElapsedSeconds => FormatElapsed( context.Candidate.Supplement.Elapsed, true ),
			ProcReportFieldKind.CpuTime => FormatCpuTime( process, context.System, context.Candidate.Supplement.Elapsed ),
			ProcReportFieldKind.CpuPercent => FormatCpuPercent( process, context.System, context.Candidate.Supplement.Elapsed ),
			ProcReportFieldKind.MemoryPercent => FormatMemoryPercent( process, context.System ),
			ProcReportFieldKind.Start => FormatStart( context.Candidate.Supplement.Elapsed, context.Now, false ),
			ProcReportFieldKind.StartLong => FormatStart( context.Candidate.Supplement.Elapsed, context.Now, true ),
			ProcReportFieldKind.Cgroup => FormatCgroup( process.Container ),
			ProcReportFieldKind.Container => FormatContainer( process.Container ),
			ProcReportFieldKind.NamespacePid => FormatNamespacePid( process.NamespaceProcessIds ),
			ProcReportFieldKind.IpcNamespace => FormatNamespace( process.Namespaces, "ipc" ),
			ProcReportFieldKind.MountNamespace => FormatNamespace( process.Namespaces, "mnt" ),
			ProcReportFieldKind.NetNamespace => FormatNamespace( process.Namespaces, "net" ),
			ProcReportFieldKind.PidNamespace => FormatNamespace( process.Namespaces, "pid" ),
			ProcReportFieldKind.UserNamespace => FormatNamespace( process.Namespaces, "user" ),
			ProcReportFieldKind.UtsNamespace => FormatNamespace( process.Namespaces, "uts" ),
			ProcReportFieldKind.SecurityLabel => FormatSecurityLabel( context.Candidate.Supplement.SecurityLabel ),
			ProcReportFieldKind.SignalBlocked => FormatLinuxStatusField( context.Candidate.Supplement.LinuxStatusFields, "SigBlk" ),
			ProcReportFieldKind.SignalCaught => FormatLinuxStatusField( context.Candidate.Supplement.LinuxStatusFields, "SigCgt" ),
			ProcReportFieldKind.SignalIgnored => FormatLinuxStatusField( context.Candidate.Supplement.LinuxStatusFields, "SigIgn" ),
			ProcReportFieldKind.SignalPending => FormatLinuxStatusField( context.Candidate.Supplement.LinuxStatusFields, "SigPnd" ),
			ProcReportFieldKind.CapabilityInheritable => FormatLinuxStatusField( context.Candidate.Supplement.LinuxStatusFields, "CapInh" ),
			ProcReportFieldKind.CapabilityPermitted => FormatLinuxStatusField( context.Candidate.Supplement.LinuxStatusFields, "CapPrm" ),
			ProcReportFieldKind.CapabilityEffective => FormatLinuxStatusField( context.Candidate.Supplement.LinuxStatusFields, "CapEff" ),
			ProcReportFieldKind.CapabilityBounding => FormatLinuxStatusField( context.Candidate.Supplement.LinuxStatusFields, "CapBnd" ),
			ProcReportFieldKind.CapabilityAmbient => FormatLinuxStatusField( context.Candidate.Supplement.LinuxStatusFields, "CapAmb" ),
			ProcReportFieldKind.Unsupported => "-",
			_ => "-"
		};
	}

	private static string FormatTerminal( ProcObservedValue<ProcTerminalInfo> terminal ) {
		ArgumentNullException.ThrowIfNull( terminal );
		return ( terminal.HasValue )
			? NormalizeTerminal( terminal.Value )
			: "?"
		;
	}

	private static string FormatState( ProcObservedValue<ProcProcessState> state ) {
		ArgumentNullException.ThrowIfNull( state );
		return ( state.HasValue )
			? StateCode( state.Value )
			: "?"
		;
	}

	private static string FormatCgroup( ProcObservedValue<ProcContainerInfo> container ) {
		ArgumentNullException.ThrowIfNull( container );
		return ( container.HasValue )
			? container.Value.CgroupPath
			: "-"
		;
	}

	private static string FormatSecurityLabel( ProcObservedValue<string> label ) {
		ArgumentNullException.ThrowIfNull( label );
		if ( !label.HasValue || string.IsNullOrWhiteSpace( label.Value ) ) {
			return "-";
		}
		return label.Value;
	}

	private static string FormatLinuxStatusField( ProcObservedValue<IReadOnlyDictionary<string, string>> status, string field ) {
		ArgumentNullException.ThrowIfNull( status );
		ArgumentException.ThrowIfNullOrWhiteSpace( field );
		if ( !status.HasValue || !status.Value.TryGetValue( field, out var value ) || string.IsNullOrWhiteSpace( value ) ) {
			return "-";
		}
		return value;
	}

	private static string FormatCommand( RenderContext context ) {
		ArgumentNullException.ThrowIfNull( context );
		var process = context.Candidate.Process;
		var command = ( context.Options.CommandNameOnly )
			? FormatCommandName( context )
			: ( process.CommandLineArguments.HasValue && 0 < process.CommandLineArguments.Value.Count )
				? string.Join( " ", process.CommandLineArguments.Value )
				: FormatCommandName( context )
		;
		if ( context.Options.Forest && 0 < context.ForestDepth ) {
			command = string.Concat( new string( ' ', context.ForestDepth * 2 ), "\\_ ", command );
		}
		if ( context.Options.IncludeEnvironment && context.Candidate.Supplement.Environment.HasValue && 0 < context.Candidate.Supplement.Environment.Value.Count ) {
			command = string.Concat( command, " ", string.Join( " ", context.Candidate.Supplement.Environment.Value ) );
		}
		return command;
	}

	private static string FormatCommandName( RenderContext context ) {
		ArgumentNullException.ThrowIfNull( context );
		return ( context.Candidate.Process.CommandName.HasValue )
			? context.Candidate.Process.CommandName.Value
			: "-"
		;
	}

	private static string FormatAccountName( ProcObservedValue<uint> id, IProcPsAccountResolver resolver, bool user ) {
		ArgumentNullException.ThrowIfNull( id );
		ArgumentNullException.ThrowIfNull( resolver );
		if ( !id.HasValue ) {
			return "-";
		}
		string name;
		var resolved = ( user )
			? resolver.TryGetUserName( id.Value, out name )
			: resolver.TryGetGroupName( id.Value, out name )
		;
		return ( resolved )
			? name
			: id.Value.ToString( CultureInfo.InvariantCulture )
		;
	}

	private static string FormatStat( ProcProcessSnapshot process ) {
		ArgumentNullException.ThrowIfNull( process );
		var state = ( process.State.HasValue )
			? StateCode( process.State.Value )
			: "?"
		;
		if ( process.NiceValue.HasValue ) {
			if ( 0 < process.NiceValue.Value ) {
				state += "N";
			} else if ( 0 > process.NiceValue.Value ) {
				state += "<";
			}
		}
		if ( process.ThreadCount.HasValue && 1 < process.ThreadCount.Value ) {
			state += "l";
		}
		if ( process.SessionId.HasValue && process.SessionId.Value == process.ProcessId ) {
			state += "s";
		}
		return state;
	}

	private static string FormatPriority( ProcProcessSnapshot process ) {
		ArgumentNullException.ThrowIfNull( process );
		if ( !process.NiceValue.HasValue ) {
			return "-";
		}
		return ( 20 + process.NiceValue.Value ).ToString( CultureInfo.InvariantCulture );
	}

	private static string FormatKilobytes( ProcObservedValue<ulong> bytes ) {
		ArgumentNullException.ThrowIfNull( bytes );
		return ( bytes.HasValue )
			? ( bytes.Value / 1024UL ).ToString( CultureInfo.InvariantCulture )
			: "-"
		;
	}

	private static string FormatSizePages( ProcObservedValue<ulong> bytes ) {
		ArgumentNullException.ThrowIfNull( bytes );
		if ( !bytes.HasValue ) {
			return "-";
		}
		var pageSize = (ulong)Math.Max( Environment.SystemPageSize, 1 );
		return ( ( bytes.Value + pageSize - 1UL ) / pageSize ).ToString( CultureInfo.InvariantCulture );
	}

	private static string FormatEnvironment( ProcObservedValue<IReadOnlyList<string>> environment ) {
		ArgumentNullException.ThrowIfNull( environment );
		return ( environment.HasValue )
			? string.Join( " ", environment.Value )
			: "-"
		;
	}

	private static string FormatElapsed( ProcObservedValue<TimeSpan> elapsed, bool secondsOnly ) {
		ArgumentNullException.ThrowIfNull( elapsed );
		if ( !elapsed.HasValue ) {
			return "-";
		}
		if ( secondsOnly ) {
			return Math.Max( 0L, (long)elapsed.Value.TotalSeconds ).ToString( CultureInfo.InvariantCulture );
		}
		return FormatDuration( elapsed.Value );
	}

	private static string FormatCpuTime( ProcProcessSnapshot process, ProcSystemSnapshot? system, ProcObservedValue<TimeSpan> elapsed ) {
		ArgumentNullException.ThrowIfNull( process );
		ArgumentNullException.ThrowIfNull( elapsed );
		var seconds = CpuSeconds( process, system );
		if ( !seconds.HasValue ) {
			return "-";
		}
		return FormatDuration( TimeSpan.FromSeconds( seconds.Value ) );
	}

	private static string FormatCpuPercent( ProcProcessSnapshot process, ProcSystemSnapshot? system, ProcObservedValue<TimeSpan> elapsed ) {
		ArgumentNullException.ThrowIfNull( process );
		ArgumentNullException.ThrowIfNull( elapsed );
		var seconds = CpuSeconds( process, system );
		if ( !seconds.HasValue || !elapsed.HasValue || 0.0 >= elapsed.Value.TotalSeconds ) {
			return "0.0";
		}
		return ( 100.0 * seconds.Value / elapsed.Value.TotalSeconds ).ToString( "0.0", CultureInfo.InvariantCulture );
	}

	private static double? CpuSeconds( ProcProcessSnapshot process, ProcSystemSnapshot? system ) {
		ArgumentNullException.ThrowIfNull( process );
		if ( !process.UserCpuTicks.HasValue || !process.SystemCpuTicks.HasValue ) {
			return null;
		}
		var total = process.UserCpuTicks.Value + process.SystemCpuTicks.Value;
		if ( ProcObservationSource.DotNetProcessApi == process.UserCpuTicks.Source ) {
			return total / (double)TimeSpan.TicksPerSecond;
		}
		var ticksPerSecond = EstimateClockTicksPerSecond( system );
		if ( !ticksPerSecond.HasValue || 0.0 >= ticksPerSecond.Value ) {
			return null;
		}
		return total / ticksPerSecond.Value;
	}

	private static double? EstimateClockTicksPerSecond( ProcSystemSnapshot? system ) {
		if ( null == system || !system.Cpu.HasValue || !system.Uptime.HasValue || 0.0 >= system.Uptime.Value.Uptime.TotalSeconds ) {
			return null;
		}
		var processors = Math.Max( Environment.ProcessorCount, 1 );
		var estimate = system.Cpu.Value.Total / system.Uptime.Value.Uptime.TotalSeconds / processors;
		if ( 0.0 >= estimate ) {
			return null;
		}
		return Math.Max( 1.0, Math.Round( estimate ) );
	}

	private static string FormatMemoryPercent( ProcProcessSnapshot process, ProcSystemSnapshot? system ) {
		ArgumentNullException.ThrowIfNull( process );
		if ( !process.ResidentMemoryBytes.HasValue || null == system || !system.Memory.HasValue || !system.Memory.Value.TotalBytes.HasValue || 0UL == system.Memory.Value.TotalBytes.Value ) {
			return "0.0";
		}
		return ( 100.0 * process.ResidentMemoryBytes.Value / system.Memory.Value.TotalBytes.Value ).ToString( "0.0", CultureInfo.InvariantCulture );
	}

	private static string FormatStart( ProcObservedValue<TimeSpan> elapsed, DateTimeOffset now, bool longForm ) {
		ArgumentNullException.ThrowIfNull( elapsed );
		if ( !elapsed.HasValue ) {
			return "-";
		}
		var start = now - elapsed.Value;
		if ( longForm ) {
			return start.ToString( "ddd MMM dd HH:mm:ss yyyy", CultureInfo.InvariantCulture );
		}
		return ( 24.0 > elapsed.Value.TotalHours )
			? start.ToString( "HH:mm", CultureInfo.InvariantCulture )
			: start.ToString( "MMMdd", CultureInfo.InvariantCulture )
		;
	}

	private static string FormatContainer( ProcObservedValue<ProcContainerInfo> container ) {
		ArgumentNullException.ThrowIfNull( container );
		if ( !container.HasValue ) {
			return "-";
		}
		return container.Value.ContainerId ?? container.Value.Runtime ?? container.Value.CgroupPath;
	}

	private static string FormatNamespacePid( ProcObservedValue<IReadOnlyList<int>> processIds ) {
		ArgumentNullException.ThrowIfNull( processIds );
		return ( processIds.HasValue )
			? string.Join( ",", processIds.Value )
			: "-"
		;
	}

	private static string FormatNamespace( ProcObservedValue<IReadOnlyDictionary<string, ProcNamespaceInfo>> namespaces, string kind ) {
		ArgumentNullException.ThrowIfNull( namespaces );
		ArgumentNullException.ThrowIfNull( kind );
		if ( !namespaces.HasValue || !namespaces.Value.TryGetValue( kind, out var value ) ) {
			return "-";
		}
		return ( value.Identifier.HasValue )
			? value.Identifier.Value.ToString( CultureInfo.InvariantCulture )
			: value.LinkTarget
		;
	}

	private static string StateCode( ProcProcessState state ) => state switch {
		ProcProcessState.Running => "R",
		ProcProcessState.Sleeping => "S",
		ProcProcessState.DiskSleep => "D",
		ProcProcessState.Stopped => "T",
		ProcProcessState.TracingStop => "t",
		ProcProcessState.Zombie => "Z",
		ProcProcessState.Dead => "X",
		ProcProcessState.Idle => "I",
		ProcProcessState.Waking => "W",
		ProcProcessState.Parked => "P",
		_ => "?"
	};

	private static string FormatDuration( TimeSpan value ) {
		if ( TimeSpan.Zero > value ) {
			value = TimeSpan.Zero;
		}
		var hours = checked( (long)value.TotalHours );
		if ( 0 < value.Days ) {
			return string.Format( CultureInfo.InvariantCulture, "{0}-{1:00}:{2:00}:{3:00}", value.Days, value.Hours, value.Minutes, value.Seconds );
		}
		return string.Format( CultureInfo.InvariantCulture, "{0:00}:{1:00}:{2:00}", hours, value.Minutes, value.Seconds );
	}

	private static bool FieldsNeedSupplements( IReadOnlyList<SelectedField> fields ) {
		ArgumentNullException.ThrowIfNull( fields );
		return fields.Any( field => field.Definition.Kind is
			ProcReportFieldKind.Elapsed or
			ProcReportFieldKind.ElapsedSeconds or
			ProcReportFieldKind.CpuPercent or
			ProcReportFieldKind.Start or
			ProcReportFieldKind.StartLong or
			ProcReportFieldKind.Environment or
			ProcReportFieldKind.SecurityLabel or
			ProcReportFieldKind.SignalBlocked or
			ProcReportFieldKind.SignalCaught or
			ProcReportFieldKind.SignalIgnored or
			ProcReportFieldKind.SignalPending or
			ProcReportFieldKind.CapabilityInheritable or
			ProcReportFieldKind.CapabilityPermitted or
			ProcReportFieldKind.CapabilityEffective or
			ProcReportFieldKind.CapabilityBounding or
			ProcReportFieldKind.CapabilityAmbient
		);
	}

	private static bool FieldsNeedMetrics( IReadOnlyList<SelectedField> fields ) {
		ArgumentNullException.ThrowIfNull( fields );
		return fields.Any( field => field.Definition.Kind is ProcReportFieldKind.CpuTime or ProcReportFieldKind.CpuPercent or ProcReportFieldKind.MemoryPercent );
	}

	private static bool TryParseBsdOperandOption( string[] args, ref int index, string argument, ParsedArguments result, IProcPsAccountResolver accountResolver ) {
		ArgumentNullException.ThrowIfNull( args );
		ArgumentNullException.ThrowIfNull( argument );
		ArgumentNullException.ThrowIfNull( result );
		ArgumentNullException.ThrowIfNull( accountResolver );
		if ( "t" == argument ) {
			result.CurrentTerminalOnly = true;
			result.HasExplicitSelection = true;
			return true;
		}
		if ( argument is not ( "p" or "q" or "U" or "o" or "k" ) ) {
			return false;
		}
		if ( index + 1 >= args.Length ) {
			result.Fail( $"option '{argument}' requires an argument" );
			return true;
		}
		var value = args[ ++index ];
		switch ( argument ) {
			case "p":
				ParseIdentifiers( value, result.ProcessIds, result );
				result.HasExplicitSelection = true;
				break;
			case "q":
				ParseOrderedIdentifiers( value, result.QuickProcessIds, result );
				result.HasExplicitSelection = true;
				break;
			case "U":
				ParseAccounts( value, result.EffectiveUserIds, result, accountResolver, true );
				result.HasExplicitSelection = true;
				break;
			case "o":
				ParseFormat( value, result );
				break;
			case "k":
				ParseSort( value, result );
				break;
		}
		result.Personality = ProcPersonality.Bsd;
		return true;
	}

	private static void ParseUnixSessionOrGroupValue(
		string[] args,
		ref int index,
		string argument,
		ref int optionIndex,
		char option,
		ParsedArguments result,
		IProcPsAccountResolver resolver
	) {
		ArgumentNullException.ThrowIfNull( args );
		ArgumentNullException.ThrowIfNull( argument );
		ArgumentNullException.ThrowIfNull( result );
		ArgumentNullException.ThrowIfNull( resolver );
		if ( !TryTakeUnixValue( args, ref index, argument, ref optionIndex, option, result, out var value ) ) {
			return;
		}
		var tokens = SplitList( value! ).ToArray();
		if ( tokens.All( token => uint.TryParse( token, NumberStyles.None, CultureInfo.InvariantCulture, out _ ) ) ) {
			ParseIdentifiers( value!, result.SessionIds, result );
			return;
		}
		ParseAccounts( value!, result.EffectiveGroupIds, result, resolver, false );
	}

	private static bool IsHelpSection( string section ) {
		ArgumentException.ThrowIfNullOrWhiteSpace( section );
		return section is "simple" or "s" or "list" or "l" or "output" or "o" or "threads" or "t" or "misc" or "m" or "all" or "a";
	}

	private static void ParseIdentifierOption( string[] args, ref int index, string? attached, string name, ISet<int> destination, ParsedArguments result ) {
		ArgumentNullException.ThrowIfNull( args );
		ArgumentNullException.ThrowIfNull( name );
		ArgumentNullException.ThrowIfNull( destination );
		ArgumentNullException.ThrowIfNull( result );
		if ( TryTakeValue( args, ref index, attached, name, result, out var value ) ) {
			ParseIdentifiers( value!, destination, result );
		}
	}

	private static void ParseOrderedIdentifierOption( string[] args, ref int index, string? attached, string name, ICollection<int> destination, ParsedArguments result ) {
		ArgumentNullException.ThrowIfNull( args );
		ArgumentNullException.ThrowIfNull( name );
		ArgumentNullException.ThrowIfNull( destination );
		ArgumentNullException.ThrowIfNull( result );
		if ( TryTakeValue( args, ref index, attached, name, result, out var value ) ) {
			ParseOrderedIdentifiers( value!, destination, result );
		}
	}

	private static void ParseStringOption( string[] args, ref int index, string? attached, string name, ISet<string> destination, ParsedArguments result ) {
		ArgumentNullException.ThrowIfNull( args );
		ArgumentNullException.ThrowIfNull( name );
		ArgumentNullException.ThrowIfNull( destination );
		ArgumentNullException.ThrowIfNull( result );
		if ( TryTakeValue( args, ref index, attached, name, result, out var value ) ) {
			ParseStrings( value!, destination );
		}
	}

	private static void ParseAccountOption(
		string[] args,
		ref int index,
		string? attached,
		string name,
		ISet<uint> destination,
		ParsedArguments result,
		IProcPsAccountResolver resolver,
		bool user
	) {
		ArgumentNullException.ThrowIfNull( args );
		ArgumentNullException.ThrowIfNull( name );
		ArgumentNullException.ThrowIfNull( destination );
		ArgumentNullException.ThrowIfNull( result );
		ArgumentNullException.ThrowIfNull( resolver );
		if ( TryTakeValue( args, ref index, attached, name, result, out var value ) ) {
			ParseAccounts( value!, destination, result, resolver, user );
		}
	}

	private static void ParseUnixValue( string[] args, ref int index, string argument, ref int optionIndex, char option, ISet<int> destination, ParsedArguments result ) {
		if ( TryTakeUnixValue( args, ref index, argument, ref optionIndex, option, result, out var value ) ) {
			ParseIdentifiers( value!, destination, result );
		}
	}

	private static void ParseUnixOrderedValue( string[] args, ref int index, string argument, ref int optionIndex, char option, ICollection<int> destination, ParsedArguments result ) {
		if ( TryTakeUnixValue( args, ref index, argument, ref optionIndex, option, result, out var value ) ) {
			ParseOrderedIdentifiers( value!, destination, result );
		}
	}

	private static void ParseUnixStringValue( string[] args, ref int index, string argument, ref int optionIndex, char option, ISet<string> destination, ParsedArguments result ) {
		if ( TryTakeUnixValue( args, ref index, argument, ref optionIndex, option, result, out var value ) ) {
			ParseStrings( value!, destination );
		}
	}

	private static void ParseUnixAccountValue(
		string[] args,
		ref int index,
		string argument,
		ref int optionIndex,
		char option,
		ISet<uint> destination,
		ParsedArguments result,
		IProcPsAccountResolver resolver,
		bool user
	) {
		if ( TryTakeUnixValue( args, ref index, argument, ref optionIndex, option, result, out var value ) ) {
			ParseAccounts( value!, destination, result, resolver, user );
		}
	}

	private static bool TryTakeUnixValue( string[] args, ref int index, string argument, ref int optionIndex, char option, ParsedArguments result, out string? value ) {
		ArgumentNullException.ThrowIfNull( args );
		ArgumentNullException.ThrowIfNull( argument );
		ArgumentNullException.ThrowIfNull( result );
		if ( optionIndex + 1 < argument.Length ) {
			value = argument[ ( optionIndex + 1 ).. ];
			optionIndex = argument.Length;
			return true;
		}
		if ( index + 1 >= args.Length ) {
			result.Fail( $"option requires an argument -- '{option}'" );
			value = null;
			return false;
		}
		value = args[ ++index ];
		optionIndex = argument.Length;
		return true;
	}

	private static bool TryTakeValue( string[] args, ref int index, string? attached, string option, ParsedArguments result, out string? value ) {
		ArgumentNullException.ThrowIfNull( args );
		ArgumentNullException.ThrowIfNull( option );
		ArgumentNullException.ThrowIfNull( result );
		if ( null != attached ) {
			value = attached;
			return true;
		}
		if ( index + 1 >= args.Length ) {
			result.Fail( $"option '{option}' requires an argument" );
			value = null;
			return false;
		}
		value = args[ ++index ];
		return true;
	}

	private static void ParseIdentifiers( string text, ISet<int> destination, ParsedArguments result ) {
		ArgumentNullException.ThrowIfNull( text );
		ArgumentNullException.ThrowIfNull( destination );
		ArgumentNullException.ThrowIfNull( result );
		foreach ( var token in SplitList( text ) ) {
			if ( !int.TryParse( token, NumberStyles.None, CultureInfo.InvariantCulture, out var value ) || 0 > value ) {
				result.Fail( $"invalid process ID list: {text}" );
				return;
			}
			destination.Add( value );
		}
	}

	private static void ParseOrderedIdentifiers( string text, ICollection<int> destination, ParsedArguments result ) {
		ArgumentNullException.ThrowIfNull( text );
		ArgumentNullException.ThrowIfNull( destination );
		ArgumentNullException.ThrowIfNull( result );
		foreach ( var token in SplitList( text ) ) {
			if ( !int.TryParse( token, NumberStyles.None, CultureInfo.InvariantCulture, out var value ) || 0 > value ) {
				result.Fail( $"invalid process ID list: {text}" );
				return;
			}
			destination.Add( value );
		}
	}

	private static void ParseStrings( string text, ISet<string> destination ) {
		ArgumentNullException.ThrowIfNull( text );
		ArgumentNullException.ThrowIfNull( destination );
		foreach ( var token in SplitList( text ) ) {
			destination.Add( token );
		}
	}

	private static void ParseAccounts( string text, ISet<uint> destination, ParsedArguments result, IProcPsAccountResolver resolver, bool user ) {
		ArgumentNullException.ThrowIfNull( text );
		ArgumentNullException.ThrowIfNull( destination );
		ArgumentNullException.ThrowIfNull( result );
		ArgumentNullException.ThrowIfNull( resolver );
		foreach ( var token in SplitList( text ) ) {
			uint id;
			var success = ( user )
				? resolver.TryResolveUser( token, out id )
				: resolver.TryResolveGroup( token, out id )
			;
			if ( !success ) {
				result.Fail( $"user/group name does not exist: {token}" );
				return;
			}
			destination.Add( id );
		}
	}

	private static void ParseWidth( string text, ParsedArguments result ) {
		ArgumentNullException.ThrowIfNull( text );
		ArgumentNullException.ThrowIfNull( result );
		if ( !int.TryParse( text, NumberStyles.None, CultureInfo.InvariantCulture, out var value ) || 1 > value ) {
			result.Fail( $"invalid width: {text}" );
			return;
		}
		result.Width = value;
	}

	private static IEnumerable<string> SplitList( string text ) {
		ArgumentNullException.ThrowIfNull( text );
		return text.Split( new[] { ',', ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries );
	}

	private static IEnumerable<string> SplitFormat( string text ) {
		ArgumentNullException.ThrowIfNull( text );
		if ( 0 <= text.IndexOf( ',' ) ) {
			return text.Split( ',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries );
		}
		return text.Split( new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries );
	}

	private static string NormalizeTerminal( ProcTerminalInfo terminal ) {
		ArgumentNullException.ThrowIfNull( terminal );
		if ( string.IsNullOrWhiteSpace( terminal.Name ) ) {
			return "?";
		}
		return ( terminal.Name.StartsWith( "/dev/", StringComparison.Ordinal ) )
			? terminal.Name[ 5.. ]
			: terminal.Name
		;
	}

	private static string PadField( string value, int width, bool rightAligned ) {
		ArgumentNullException.ThrowIfNull( value );
		if ( value.Length >= width ) {
			return value;
		}
		return ( rightAligned )
			? value.PadLeft( width )
			: value.PadRight( width )
		;
	}

	private static string LimitWidth( string value, int? width ) {
		ArgumentNullException.ThrowIfNull( value );
		if ( !width.HasValue || value.Length <= width.Value ) {
			return value;
		}
		return value[ ..width.Value ];
	}

	private static string FormatObserved<T>( ProcObservedValue<T> observed ) where T : IFormattable {
		ArgumentNullException.ThrowIfNull( observed );
		return ( observed.HasValue )
			? observed.Value.ToString( null, CultureInfo.InvariantCulture )
			: "-"
		;
	}

	private static int CompareObserved<T>( ProcObservedValue<T> left, ProcObservedValue<T> right ) where T : IComparable<T> {
		ArgumentNullException.ThrowIfNull( left );
		ArgumentNullException.ThrowIfNull( right );
		if ( left.HasValue && right.HasValue ) {
			return left.Value.CompareTo( right.Value );
		}
		if ( left.HasValue ) {
			return -1;
		}
		return ( right.HasValue )
			? 1
			: 0
		;
	}

	private static int CompareObservedString( ProcObservedValue<string> left, ProcObservedValue<string> right ) {
		ArgumentNullException.ThrowIfNull( left );
		ArgumentNullException.ThrowIfNull( right );
		if ( left.HasValue && right.HasValue ) {
			return string.Compare( left.Value, right.Value, StringComparison.Ordinal );
		}
		if ( left.HasValue ) {
			return -1;
		}
		return ( right.HasValue )
			? 1
			: 0
		;
	}

	private static IReadOnlyDictionary<string, string?> ReadPersonalityEnvironment() => new Dictionary<string, string?>( StringComparer.Ordinal ) {
		[ "PS_PERSONALITY" ] = Environment.GetEnvironmentVariable( "PS_PERSONALITY" ),
		[ "CMD_ENV" ] = Environment.GetEnvironmentVariable( "CMD_ENV" )
	};

	private static string NormalizeLineEndings( string value ) {
		ArgumentNullException.ThrowIfNull( value );
		var normalized = value.Replace( "\r\n", "\n", StringComparison.Ordinal ).Replace( "\r", "\n", StringComparison.Ordinal );
		return ( "\n" == Environment.NewLine )
			? normalized
			: normalized.Replace( "\n", Environment.NewLine, StringComparison.Ordinal )
		;
	}

	private static async Task WriteLineAsync( Stream? stream, string text, CancellationToken cancellationToken ) {
		ArgumentNullException.ThrowIfNull( text );
		await WriteAsync( stream, string.Concat( text, Environment.NewLine ), cancellationToken ).ConfigureAwait( false );
	}

	private static async Task WriteAsync( Stream? stream, string text, CancellationToken cancellationToken ) {
		ArgumentNullException.ThrowIfNull( text );
		if ( null == stream ) {
			return;
		}
		var bytes = Utf8.GetBytes( text );
		await stream.WriteAsync( bytes.AsMemory(), cancellationToken ).ConfigureAwait( false );
	}

	private enum HeaderMode {
		Default,
		Show,
		Hide
	}

	private sealed class SelectedField {
		public ProcReportFieldDefinition Definition { get; }
		public string? HeaderOverride { get; }
		public int? ExplicitWidth { get; }
		public string Header => this.HeaderOverride ?? this.Definition.Header;
		public int Width => Math.Max( this.ExplicitWidth ?? this.Definition.Width, this.Header.Length );

		public SelectedField( ProcReportFieldDefinition definition, string? headerOverride = null, int? explicitWidth = null ) {
			ArgumentNullException.ThrowIfNull( definition );
			if ( explicitWidth.HasValue && 0 >= explicitWidth.Value ) {
				throw new ArgumentOutOfRangeException( nameof( explicitWidth ) );
			}
			this.Definition = definition;
			this.HeaderOverride = headerOverride;
			this.ExplicitWidth = explicitWidth;
		}
	}

	private sealed class SortKey {
		public ProcReportFieldKind Kind { get; }
		public bool Descending { get; }

		public SortKey( ProcReportFieldKind kind, bool descending ) {
			this.Kind = kind;
			this.Descending = descending;
		}
	}

	private sealed class RenderContext {
		public ProcMatchCandidate Candidate { get; }
		public ProcSystemSnapshot? System { get; }
		public IProcPsAccountResolver AccountResolver { get; }
		public DateTimeOffset Now { get; }
		public ParsedArguments Options { get; }
		public int ForestDepth { get; }

		public RenderContext( ProcMatchCandidate candidate, ProcSystemSnapshot? system, IProcPsAccountResolver accountResolver, DateTimeOffset now, ParsedArguments options, int forestDepth ) {
			ArgumentNullException.ThrowIfNull( candidate );
			ArgumentNullException.ThrowIfNull( accountResolver );
			ArgumentNullException.ThrowIfNull( options );
			if ( 0 > forestDepth ) {
				throw new ArgumentOutOfRangeException( nameof( forestDepth ) );
			}
			this.Candidate = candidate;
			this.System = system;
			this.AccountResolver = accountResolver;
			this.Now = now;
			this.Options = options;
			this.ForestDepth = forestDepth;
		}
	}

	private sealed class ParsedArguments {
		public string? Error { get; private set; }
		public bool ShowHelp { get; set; }
		public bool ShowVersion { get; set; }
		public bool ShowFieldList { get; set; }
		public bool SelectAll { get; set; }
		public bool SelectTerminalProcesses { get; set; }
		public bool SelectExceptSessionLeaders { get; set; }
		public bool HasExplicitSelection { get; set; }
		public bool Invert { get; set; }
		public bool BsdAllUsers { get; set; }
		public bool BsdIncludeNoTerminal { get; set; }
		public bool RunningOnly { get; set; }
		public bool CurrentTerminalOnly { get; set; }
		public bool ShowThreads { get; set; }
		public bool Forest { get; set; }
		public bool IncludeEnvironment { get; set; }
		public bool CommandNameOnly { get; set; }
		public bool FullFormat { get; set; }
		public bool FullExtraFormat { get; set; }
		public bool LongFormat { get; set; }
		public bool JobsFormat { get; set; }
		public bool UserFormat { get; set; }
		public bool MemoryFormat { get; set; }
		public bool SecurityFormat { get; set; }
		public bool CustomFormat { get; set; }
		public ProcPersonality Personality { get; set; }
		public HeaderMode HeaderMode { get; set; }
		public int? Width { get; set; } = DefaultWidth;
		public HashSet<int> ProcessIds { get; } = [];
		public List<int> QuickProcessIds { get; } = [];
		public HashSet<int> ParentIds { get; } = [];
		public HashSet<int> ProcessGroupIds { get; } = [];
		public HashSet<int> SessionIds { get; } = [];
		public HashSet<uint> EffectiveUserIds { get; } = [];
		public HashSet<uint> RealUserIds { get; } = [];
		public HashSet<uint> EffectiveGroupIds { get; } = [];
		public HashSet<uint> RealGroupIds { get; } = [];
		public HashSet<string> Terminals { get; } = new( StringComparer.Ordinal );
		public HashSet<string> CommandNames { get; } = new( StringComparer.Ordinal );
		public List<SelectedField> Fields { get; } = [];
		public List<SortKey> SortKeys { get; } = [];
		private int WidenCount { get; set; }

		public void AddFields( IEnumerable<string> names ) {
			ArgumentNullException.ThrowIfNull( names );
			foreach ( var name in names ) {
				if ( !FieldCatalog.TryGetValue( name, out var definition ) ) {
					throw new InvalidOperationException( $"Internal ps field '{name}' is not registered." );
				}
				this.Fields.Add( new SelectedField( definition ) );
			}
		}


		public void PrependField( string name ) {
			ArgumentException.ThrowIfNullOrWhiteSpace( name );
			if ( !FieldCatalog.TryGetValue( name, out var definition ) ) {
				throw new InvalidOperationException( $"Internal ps field '{name}' is not registered." );
			}
			this.Fields.Insert( 0, new SelectedField( definition ) );
		}

		public bool HasNonQuickSelection() => this.SelectAll
			|| this.SelectTerminalProcesses
			|| this.BsdAllUsers
			|| this.BsdIncludeNoTerminal
			|| this.RunningOnly
			|| this.SelectExceptSessionLeaders
			|| this.CurrentTerminalOnly
			|| 0 < this.ProcessIds.Count
			|| 0 < this.ParentIds.Count
			|| 0 < this.ProcessGroupIds.Count
			|| 0 < this.SessionIds.Count
			|| 0 < this.EffectiveUserIds.Count
			|| 0 < this.RealUserIds.Count
			|| 0 < this.EffectiveGroupIds.Count
			|| 0 < this.RealGroupIds.Count
			|| 0 < this.Terminals.Count
			|| 0 < this.CommandNames.Count;

		public void Widen() {
			this.WidenCount++;
			if ( 1 == this.WidenCount ) {
				this.Width = 132;
			} else {
				this.Width = null;
			}
		}

		public void Fail( string error ) {
			ArgumentException.ThrowIfNullOrWhiteSpace( error );
			this.Error ??= error;
		}
	}
}
