namespace Icod.CoreUtils.Install;

using Icod.CoreUtils.Shared.FileSystem.TransactionalReplacement;

/// <summary>Parses GNU-compatible <c>install</c> command-line options.</summary>
internal static class InstallArgumentParser {
	/// <summary>Parses an argument vector.</summary>
	/// <param name="args">The argument vector.</param>
	/// <param name="options">The parsed options when successful.</param>
	/// <param name="error">A controlled usage diagnostic when parsing fails.</param>
	/// <returns><see langword="true"/> when parsing succeeds.</returns>
	public static bool TryParse( string[] args, out InstallOptions options, out string? error ) {
		ArgumentNullException.ThrowIfNull( args );
		options = new InstallOptions();
		error = null;
		var endOfOptions = false;
		for ( var index = 0; index < args.Length; index++ ) {
			var argument = args[index];
			if ( endOfOptions || argument.Length < 2 || argument[0] != '-' || argument == "-" ) {
				options.Operands.Add( argument );
				continue;
			}
			if ( argument == "--" ) {
				endOfOptions = true;
				continue;
			}
			if ( argument.StartsWith( "--", StringComparison.Ordinal ) ) {
				if ( !ParseLong( args, ref index, argument[2..], options, out error ) ) return false;
				continue;
			}
			if ( !ParseShort( args, ref index, argument[1..], options, out error ) ) return false;
		}
		if ( options.TargetDirectory is not null && options.TreatDestinationAsFile ) {
			error = "options --target-directory and --no-target-directory are mutually exclusive";
			return false;
		}
		if ( options.DirectoryMode && options.TargetDirectory is not null ) {
			error = "options --directory and --target-directory are mutually exclusive";
			return false;
		}
		if ( options.DirectoryMode && options.TreatDestinationAsFile ) {
			error = "options --directory and --no-target-directory are mutually exclusive";
			return false;
		}
		if ( options.DirectoryMode && options.Strip ) {
			error = "the strip option may not be used when installing a directory";
			return false;
		}
		if ( options.Compare && options.PreserveTimestamps ) {
			error = "options --compare and --preserve-timestamps are mutually exclusive";
			return false;
		}
		if ( options.Compare && options.Strip ) {
			error = "options --compare and --strip are mutually exclusive";
			return false;
		}
		if ( options.PreserveContext && options.ContextRequested ) {
			error = "options --preserve-context and --context are mutually exclusive";
			return false;
		}
		if ( string.IsNullOrWhiteSpace( options.StripProgram ) ) {
			error = "option --strip-program requires a nonempty program name";
			return false;
		}
		if ( options.BackupSuffix.Length == 0 || options.BackupSuffix.IndexOfAny( new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar } ) >= 0 ) {
			error = "backup suffix must be one nonempty filename suffix";
			return false;
		}
		return true;
	}

	private static bool ParseLong(
		string[] args,
		ref int index,
		string text,
		InstallOptions options,
		out string? error
	) {
		error = null;
		var equals = text.IndexOf( '=' );
		var name = equals < 0 ? text : text[..equals];
		var attached = equals < 0 ? null : text[(equals + 1)..];
		switch ( name ) {
			case "backup": {
				var control = attached ?? Environment.GetEnvironmentVariable( "VERSION_CONTROL" ) ?? "existing";
				if ( !TryParseBackupMode( control, out var backupMode ) ) {
					error = attached is null
						? string.Concat( "invalid argument '", control, "' for $VERSION_CONTROL" )
						: string.Concat( "invalid backup type '", control, "'" );
					return false;
				}
				options.BackupMode = backupMode;
				options.Backup = backupMode != TransactionalReplacementBackupMode.None;
				return true;
			}
			case "compare": options.Compare = true; return RequireNoAttached( name, attached, out error );
			case "directory": options.DirectoryMode = true; return RequireNoAttached( name, attached, out error );
			case "create-leading-dirs": options.CreateLeadingDirectories = true; return RequireNoAttached( name, attached, out error );
			case "group": return RequireValue( args, ref index, name, attached, value => options.Group = value, out error );
			case "mode":
				return RequireValue( args, ref index, name, attached, value => { options.ModeText = value; options.ModeWasExplicit = true; }, out error );
			case "owner": return RequireValue( args, ref index, name, attached, value => options.Owner = value, out error );
			case "preserve-timestamps": options.PreserveTimestamps = true; return RequireNoAttached( name, attached, out error );
			case "strip": options.Strip = true; return RequireNoAttached( name, attached, out error );
			case "strip-program": return RequireValue( args, ref index, name, attached, value => { options.StripProgram = value; options.StripProgramWasExplicit = true; }, out error );
			case "suffix": return RequireValue( args, ref index, name, attached, value => options.BackupSuffix = value, out error );
			case "target-directory": return RequireValue( args, ref index, name, attached, value => options.TargetDirectory = value, out error );
			case "no-target-directory": options.TreatDestinationAsFile = true; return RequireNoAttached( name, attached, out error );
			case "verbose": options.Verbose = true; return RequireNoAttached( name, attached, out error );
			case "preserve-context": options.PreserveContext = true; return RequireNoAttached( name, attached, out error );
			case "context":
				if ( attached is not null && attached.Length == 0 ) {
					error = "option '--context' requires a nonempty context when '=' is used";
					return false;
				}
				options.ContextRequested = true;
				options.ExplicitContext = attached;
				return true;
			case "debug": options.Debug = true; options.Verbose = true; return RequireNoAttached( name, attached, out error );
			case "help": options.ShowHelp = true; return RequireNoAttached( name, attached, out error );
			case "version": options.ShowVersion = true; return RequireNoAttached( name, attached, out error );
			default:
				error = string.Concat( "unrecognized option '--", name, "'" );
				return false;
		}
	}

	private static bool ParseShort(
		string[] args,
		ref int index,
		string text,
		InstallOptions options,
		out string? error
	) {
		error = null;
		for ( var offset = 0; offset < text.Length; offset++ ) {
			var option = text[offset];
			switch ( option ) {
				case 'b':
					options.Backup = true;
					options.BackupMode = TransactionalReplacementBackupMode.Existing;
					break;
				case 'C': options.Compare = true; break;
				case 'c': break; // Historical compatibility option; intentionally ignored.
				case 'd': options.DirectoryMode = true; break;
				case 'D': options.CreateLeadingDirectories = true; break;
				case 'p': options.PreserveTimestamps = true; break;
				case 's': options.Strip = true; break;
				case 'T': options.TreatDestinationAsFile = true; break;
				case 'v': options.Verbose = true; break;
				case 'Z': options.ContextRequested = true; break;
				case '?': options.ShowHelp = true; break;
				case 'g':
				case 'm':
				case 'o':
				case 'S':
				case 't': {
					var value = offset + 1 < text.Length ? text[(offset + 1)..] : null;
					if ( value is null ) {
						if ( ++index >= args.Length ) {
							error = string.Concat( "option requires an argument -- '", option, "'" );
							return false;
						}
						value = args[index];
					}
					switch ( option ) {
						case 'g': options.Group = value; break;
						case 'm': options.ModeText = value; options.ModeWasExplicit = true; break;
						case 'o': options.Owner = value; break;
						case 'S': options.BackupSuffix = value; break;
						case 't': options.TargetDirectory = value; break;
					}
					return true;
				}
				default:
					error = string.Concat( "invalid option -- '", option, "'" );
					return false;
			}
		}
		return true;
	}

	private static bool RequireNoAttached( string name, string? attached, out string? error ) {
		if ( attached is null ) { error = null; return true; }
		error = string.Concat( "option '--", name, "' doesn't allow an argument" );
		return false;
	}

	private static bool RequireValue(
		string[] args,
		ref int index,
		string name,
		string? attached,
		Action<string> assign,
		out string? error
	) {
		var value = attached;
		if ( value is null ) {
			if ( ++index >= args.Length ) {
				error = string.Concat( "option '--", name, "' requires an argument" );
				return false;
			}
			value = args[index];
		}
		assign( value );
		error = null;
		return true;
	}

	private static bool TryParseBackupMode( string text, out TransactionalReplacementBackupMode mode ) {
		mode = (TransactionalReplacementBackupMode)(-1);
		if ( text.Length == 0 ) return false;
		var candidates = new[] {
			( Name: "none", Mode: TransactionalReplacementBackupMode.None ),
			( Name: "off", Mode: TransactionalReplacementBackupMode.None ),
			( Name: "simple", Mode: TransactionalReplacementBackupMode.Simple ),
			( Name: "never", Mode: TransactionalReplacementBackupMode.Simple ),
			( Name: "numbered", Mode: TransactionalReplacementBackupMode.Numbered ),
			( Name: "t", Mode: TransactionalReplacementBackupMode.Numbered ),
			( Name: "existing", Mode: TransactionalReplacementBackupMode.Existing ),
			( Name: "nil", Mode: TransactionalReplacementBackupMode.Existing )
		};
		var matches = candidates
			.Where( candidate => candidate.Name.StartsWith( text, StringComparison.Ordinal ) )
			.Select( candidate => candidate.Mode )
			.Distinct()
			.ToArray();
		if ( matches.Length != 1 ) return false;
		mode = matches[0];
		return true;
	}
}
