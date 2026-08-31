namespace Icod.CoreUtils.Env;

using Icod.Processes;

/// <summary>
/// Represents parsed GNU <c>env</c> command-line options.
/// </summary>
public sealed class EnvOptions {
	/// <summary>Gets the native argument zero override.</summary>
	public string? ArgumentZero { get; internal set; }

	/// <summary>Gets whether processing diagnostics are written to standard error.</summary>
	public bool Debug { get; internal set; }

	/// <summary>Gets whether the inherited environment is discarded.</summary>
	public bool IgnoreEnvironment { get; internal set; }

	/// <summary>Gets whether signal handling should be listed before invocation.</summary>
	public bool ListSignalHandling { get; internal set; }

	/// <summary>Gets whether environment output is NUL terminated.</summary>
	public bool NullOutput { get; internal set; }

	/// <summary>Gets the remaining NAME=VALUE and COMMAND operands.</summary>
	public IReadOnlyList<string> Operands => this._operands;

	/// <summary>Gets the launch-time signal changes.</summary>
	public ProcessLaunchSignalPolicy SignalPolicy { get; } = new();

	/// <summary>Gets environment-variable names requested for removal.</summary>
	public IReadOnlyList<string> UnsetNames => this._unsetNames;

	/// <summary>Gets the requested child working directory.</summary>
	public string? WorkingDirectory { get; internal set; }

	/// <summary>Gets whether help was requested.</summary>
	public bool ShowHelp { get; internal set; }

	/// <summary>Gets whether version information was requested.</summary>
	public bool ShowVersion { get; internal set; }

	private static readonly string[] LongOptionNames = [
		"--argv0",
		"--ignore-environment",
		"--null",
		"--unset",
		"--chdir",
		"--default-signal",
		"--ignore-signal",
		"--block-signal",
		"--list-signal-handling",
		"--debug",
		"--split-string",
		"--help",
		"--version"
	];
	private readonly List<string> _operands = [];
	private readonly List<string> _unsetNames = [];

	/// <summary>Parses GNU <c>env</c> options, including recursive <c>-S</c> expansion.</summary>
	public static EnvOptions Parse(
		IEnumerable<string> arguments,
		ProcessEnvironment originalEnvironment,
		IProcessSignalProvider signalProvider
	) {
		ArgumentNullException.ThrowIfNull( arguments );
		ArgumentNullException.ThrowIfNull( originalEnvironment );
		ArgumentNullException.ThrowIfNull( signalProvider );
		var options = new EnvOptions();
		var work = arguments.ToList();
		var index = 0;
		while ( index < work.Count ) {
			var token = work[ index ];
			if ( "--" == token ) {
				options._operands.AddRange( work.Skip( index + 1 ) );
				return options;
			}
			if ( "-" == token || token.Length < 2 || '-' != token[ 0 ] ) {
				options._operands.AddRange( work.Skip( index ) );
				return options;
			}
			if ( token.StartsWith( "--", StringComparison.Ordinal ) ) {
				if ( ParseLongOption( options, work, ref index, originalEnvironment, signalProvider ) ) {
					if ( options.ShowHelp || options.ShowVersion ) {
						return options;
					}
					continue;
				}
				throw new EnvUsageException( $"unrecognized option '{token}'" );
			}
			if ( ParseShortOptions( options, work, ref index, originalEnvironment, signalProvider ) ) {
				continue;
			}
		}
		return options;
	}

	private static bool ParseLongOption(
		EnvOptions options,
		List<string> work,
		ref int index,
		ProcessEnvironment originalEnvironment,
		IProcessSignalProvider signalProvider
	) {
		var token = work[ index ];
		var equals = token.IndexOf( '=' );
		var prefix = 0 <= equals ? token[ ..equals ] : token;
		var attached = 0 <= equals ? token[ ( equals + 1 ).. ] : null;
		var name = ResolveLongOption(
			prefix,
			out var ambiguous
		);
		if ( null == name ) {
			if ( ambiguous ) {
				throw new EnvUsageException(
					$"option '{prefix}' is ambiguous"
				);
			}
			return false;
		}
		switch ( name ) {
			case "--help":
				RequireNoAttachedValue( name, attached );
				options.ShowHelp = true;
				index++;
				return true;
			case "--version":
				RequireNoAttachedValue( name, attached );
				options.ShowVersion = true;
				index++;
				return true;
			case "--ignore-environment":
				RequireNoAttachedValue( name, attached );
				options.IgnoreEnvironment = true;
				index++;
				return true;
			case "--null":
				RequireNoAttachedValue( name, attached );
				options.NullOutput = true;
				index++;
				return true;
			case "--debug":
				RequireNoAttachedValue( name, attached );
				options.Debug = true;
				index++;
				return true;
			case "--list-signal-handling":
				RequireNoAttachedValue( name, attached );
				options.ListSignalHandling = true;
				index++;
				return true;
			case "--argv0":
				options.ArgumentZero = GetRequiredValue( work, ref index, name, attached );
				return true;
			case "--chdir":
				options.WorkingDirectory = GetRequiredValue( work, ref index, name, attached );
				return true;
			case "--unset":
				options._unsetNames.Add( GetRequiredValue( work, ref index, name, attached ) );
				return true;
			case "--split-string": {
				var split = GetRequiredValue( work, ref index, name, attached );
				RestartWithSplitString( work, ref index, split, originalEnvironment );
				return true;
			}
			case "--default-signal":
				ApplySignalDisposition( options.SignalPolicy, attached, ProcessSignalLaunchDisposition.Default, signalProvider );
				ApplySignalMask( options.SignalPolicy, attached, false, signalProvider );
				index++;
				return true;
			case "--ignore-signal":
				ApplySignalDisposition( options.SignalPolicy, attached, ProcessSignalLaunchDisposition.Ignored, signalProvider );
				index++;
				return true;
			case "--block-signal":
				ApplySignalMask( options.SignalPolicy, attached, true, signalProvider );
				index++;
				return true;
			default:
				return false;
		}
	}

	private static string? ResolveLongOption(
		string prefix,
		out bool ambiguous
	) {
		ambiguous = false;
		string? match = null;
		foreach ( var candidate in LongOptionNames ) {
			if ( !candidate.StartsWith(
				prefix,
				StringComparison.Ordinal
			) ) {
				continue;
			}
			if ( candidate == prefix ) {
				return candidate;
			}
			if ( null != match ) {
				ambiguous = true;
				return null;
			}
			match = candidate;
		}
		return match;
	}

	private static bool ParseShortOptions(
		EnvOptions options,
		List<string> work,
		ref int index,
		ProcessEnvironment originalEnvironment,
		IProcessSignalProvider signalProvider
	) {
		var token = work[ index ];
		for ( var offset = 1; offset < token.Length; offset++ ) {
			var option = token[ offset ];
			switch ( option ) {
				case 'i': options.IgnoreEnvironment = true; break;
				case '0': options.NullOutput = true; break;
				case 'v': options.Debug = true; break;
				case 'a':
				case 'C':
				case 'u':
				case 'S': {
					var attached = offset + 1 < token.Length ? token[ ( offset + 1 ).. ] : null;
					string value;
					if ( null != attached ) {
						value = attached;
						index++;
					} else {
						if ( index + 1 >= work.Count ) {
							throw new EnvUsageException( $"option requires an argument -- '{option}'" );
						}
						value = work[ index + 1 ];
						index += 2;
					}
					if ( 'a' == option ) options.ArgumentZero = value;
					else if ( 'C' == option ) options.WorkingDirectory = value;
					else if ( 'u' == option ) options._unsetNames.Add( value );
					else RestartWithSplitString( work, ref index, value, originalEnvironment );
					return true;
				}
				default:
					if ( option is ' ' or '\t' or '\n' or '\v' or '\f' or '\r' ) {
						throw new EnvUsageException(
							$"invalid option -- '{option}'",
							suggestSplitString: true
						);
					}
					throw new EnvUsageException( $"invalid option -- '{option}'" );
			}
		}
		index++;
		return true;
	}

	private static void RestartWithSplitString(
		List<string> work,
		ref int index,
		string value,
		ProcessEnvironment originalEnvironment
	) {
		var remaining = work.Skip( index ).ToArray();
		var split = EnvSplitStringParser.Parse( value, originalEnvironment );
		work.Clear();
		work.AddRange( split );
		work.AddRange( remaining );
		index = 0;
	}

	private static string GetRequiredValue(
		List<string> work,
		ref int index,
		string option,
		string? attached
	) {
		if ( null != attached ) {
			index++;
			return attached;
		}
		if ( index + 1 >= work.Count ) {
			throw new EnvUsageException( $"option '{option}' requires an argument" );
		}
		var value = work[ index + 1 ];
		index += 2;
		return value;
	}

	private static void RequireNoAttachedValue( string option, string? attached ) {
		if ( null != attached ) {
			throw new EnvUsageException( $"option '{option}' doesn't allow an argument" );
		}
	}

	private static void ApplySignalDisposition(
		ProcessLaunchSignalPolicy policy,
		string? value,
		ProcessSignalLaunchDisposition disposition,
		IProcessSignalProvider signalProvider
	) {
		foreach ( var signal in ParseSignals( value, signalProvider ) ) {
			policy.SetDisposition( signal, disposition, null == value );
		}
	}

	private static void ApplySignalMask(
		ProcessLaunchSignalPolicy policy,
		string? value,
		bool blocked,
		IProcessSignalProvider signalProvider
	) {
		foreach ( var signal in ParseSignals( value, signalProvider ) ) {
			policy.SetBlocked( signal, blocked );
		}
	}

	private static IEnumerable<ProcessSignal> ParseSignals(
		string? value,
		IProcessSignalProvider signalProvider
	) {
		if ( null == value ) {
			var allSignals = signalProvider.ListSignals().Where( static signal => 0 < signal.Number ).ToList();
			if ( OperatingSystem.IsLinux() ) {
				for ( var number = 34; number <= 64; number++ ) {
					if ( allSignals.Any( signal => signal.Number == number ) ) continue;
					var translated = signalProvider.TranslateSignal( number );
					if ( translated.Succeeded && null != translated.Value ) allSignals.Add( translated.Value );
				}
			}
			return allSignals;
		}
		if ( 0 == value.Length ) {
			return [];
		}
		var signals = new List<ProcessSignal>();
		foreach ( var token in value.Split( ',', StringSplitOptions.RemoveEmptyEntries ) ) {
			var parsed = signalProvider.ParseSignal( token );
			if ( !parsed.Succeeded || null == parsed.Value || 0 >= parsed.Value.Number ) {
				throw new EnvUsageException( $"'{token}': invalid signal" );
			}
			signals.Add( parsed.Value );
		}
		return signals;
	}
}

/// <summary>
/// Reports a command-line syntax error for GNU <c>env</c>.
/// </summary>
public sealed class EnvUsageException : Exception {
	/// <summary>Gets whether the GNU shebang split-string guidance should accompany this error.</summary>
	public bool SuggestSplitString { get; }

	/// <summary>Initializes a usage exception.</summary>
	public EnvUsageException(
		string message,
		bool suggestSplitString = false
	) : base( message ) {
		this.SuggestSplitString = suggestSplitString;
	}
}
