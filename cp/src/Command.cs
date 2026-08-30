// Original behavior/reference: GNU coreutils
// Ported to .NET by Timothy J. Bruce <uniblab@hotmail.com>

using Icod.CommandFramework.Diagnostics;
using Icod.CoreUtils.Shared.FileSystem.CopyMove;
using Icod.CoreUtils.Shared.FileSystem.Traversal;
using Icod.CommandFramework.FileSystem.RecursiveMutation;
using Icod.CommandFramework.FileSystem.TransactionalReplacement;
using Icod.CommandFramework.FileSystem.Traversal;

namespace Icod.CoreUtils.Cp;

/// <summary>Implements GNU-compatible asynchronous file and directory copying.</summary>
public static class Command {
	private const string Version = "cp (Icod.CoreUtils) 1.0";

	/// <summary>Runs <c>cp</c> synchronously.</summary>
	/// <param name="args">The command-line arguments.</param>
	/// <param name="stdin">Optional standard input.</param>
	/// <param name="stdout">Optional standard output.</param>
	/// <param name="stderr">Optional standard error.</param>
	/// <returns>The process exit status.</returns>
	public static int Run( string[] args, TextReader? stdin = null, TextWriter? stdout = null, TextWriter? stderr = null ) =>
		RunAsync( args, stdin, stdout, stderr ).AsTask().GetAwaiter().GetResult();

	/// <summary>Runs <c>cp</c> asynchronously.</summary>
	/// <param name="args">The command-line arguments.</param>
	/// <param name="stdin">Optional standard input.</param>
	/// <param name="stdout">Optional standard output.</param>
	/// <param name="stderr">Optional standard error.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>The process exit status.</returns>
	public static ValueTask<int> RunAsync(
		string[] args,
		TextReader? stdin = null,
		TextWriter? stdout = null,
		TextWriter? stderr = null,
		CancellationToken cancellationToken = default
	) {
		ArgumentNullException.ThrowIfNull( args );
		return RunAsync(
			args,
			new CommandContext(
				"cp",
				stdin ?? Console.In,
				stdout ?? Console.Out,
				stderr ?? Console.Error,
				cancellationToken: cancellationToken
			)
		);
	}

	/// <summary>Runs <c>cp</c> through a shared command context.</summary>
	/// <param name="args">The command-line arguments.</param>
	/// <param name="context">The standard streams, program identity, and cancellation context.</param>
	/// <returns>The process exit status.</returns>
	public static async ValueTask<int> RunAsync( string[] args, CommandContext context ) {
		ArgumentNullException.ThrowIfNull( args );
		ArgumentNullException.ThrowIfNull( context );
		var stdin = context.StandardInput;
		var stdout = context.StandardOutput;
		var stderr = context.StandardError;
		var cancellationToken = context.CancellationToken;
		ParsedOptions parsed;
		try {
			parsed = Parse( args );
		} catch ( ArgumentException exception ) {
			await stderr.WriteLineAsync( string.Concat( "cp: ", exception.Message ) ).ConfigureAwait( false );
			return CommandExitCodes.UsageError;
		}
		if ( parsed.Help ) {
			await stdout.WriteAsync( HelpText ).ConfigureAwait( false );
			return CommandExitCodes.Success;
		}
		if ( parsed.Version ) {
			await stdout.WriteLineAsync( Version ).ConfigureAwait( false );
			return CommandExitCodes.Success;
		}
		if ( parsed.Sources.Count == 0 || string.IsNullOrEmpty( parsed.Destination ) ) {
			await stderr.WriteLineAsync( "cp: missing file operand" ).ConfigureAwait( false );
			return CommandExitCodes.UsageError;
		}

		var options = new CopyMoveOptions {
			Operation = CopyMoveOperationKind.Copy,
			Recursive = parsed.Recursive,
			DestinationMode = parsed.DestinationMode,
			SymbolicLinkMode = parsed.SymbolicLinkMode,
			FileSystemBoundaryMode = parsed.OneFileSystem
				? FileSystemBoundaryMode.StayOnRootFileSystem
				: FileSystemBoundaryMode.CrossFileSystems,
			MetadataFields = parsed.MetadataFields,
			RequiredMetadataFields = parsed.RequiredMetadataFields,
			SparseFilePolicy = parsed.SparsePolicy,
			ReflinkPolicy = parsed.ReflinkPolicy,
			OverwriteMode = parsed.OverwriteMode,
			BackupMode = parsed.BackupMode,
			BackupSuffix = parsed.BackupSuffix,
			PreserveHardLinks = parsed.PreserveHardLinks,
			CopyAsHardLink = parsed.CopyAsHardLink,
			CopyAsSymbolicLink = parsed.CopyAsSymbolicLink,
			RemoveDestination = parsed.RemoveDestination,
			Verbose = parsed.Verbose,
			Prompt = async ( source, destination, token ) => {
				token.ThrowIfCancellationRequested();
				await stderr.WriteAsync( string.Concat( "cp: overwrite '", destination, "'? " ) ).ConfigureAwait( false );
				var response = await stdin.ReadLineAsync( token ).ConfigureAwait( false );
				return response?.StartsWith( "y", StringComparison.OrdinalIgnoreCase ) == true;
			}
		};
		try {
			var expansion = await PathnameOperandExpander.ExpandAsync(
				parsed.Sources,
				cancellationToken: cancellationToken
			).ConfigureAwait( false );
			var result = await new CopyMoveEngine().ExecuteAsync(
				expansion.Operands,
				parsed.Destination,
				options,
				cancellationToken
			).ConfigureAwait( false );
			foreach ( var item in result.Items ) {
				if ( item.Outcome == CopyMoveItemOutcome.Failed ) {
					await stderr.WriteLineAsync( string.Concat( "cp: cannot copy '", item.SourcePath, "' to '", item.DestinationPath, "': ", item.Message ) ).ConfigureAwait( false );
				} else if ( parsed.Verbose && item.Outcome == CopyMoveItemOutcome.Completed ) {
					await stdout.WriteLineAsync( string.Concat( "'", item.SourcePath, "' -> '", item.DestinationPath, "'" ) ).ConfigureAwait( false );
				}
			}
			return result.Succeeded ? CommandExitCodes.Success : CommandExitCodes.Failure;
		} catch ( OperationCanceledException ) when ( cancellationToken.IsCancellationRequested ) {
			return CommandExitCodes.Canceled;
		} catch ( Exception exception ) when ( exception is ArgumentException or IOException or UnauthorizedAccessException or NotSupportedException ) {
			await stderr.WriteLineAsync( string.Concat( "cp: ", exception.Message ) ).ConfigureAwait( false );
			return CommandExitCodes.Failure;
		}
	}

	private static ParsedOptions Parse( string[] args ) {
		var parsed = new ParsedOptions();
		var operands = new List<string>();
		var optionsEnded = false;
		for ( var index = 0; index < args.Length; index++ ) {
			var argument = args[index];
			if ( optionsEnded || argument == "-" || !argument.StartsWith( '-' ) ) {
				operands.Add( argument );
				continue;
			}
			if ( argument == "--" ) {
				optionsEnded = true;
				continue;
			}
			if ( argument.StartsWith( "--", StringComparison.Ordinal ) ) {
				ParseLongOption( argument, args, ref index, parsed );
				continue;
			}
			ParseShortOptions( argument, args, ref index, parsed );
		}
		if ( parsed.TargetDirectory is not null && parsed.DestinationMode == CopyMoveDestinationMode.NoTargetDirectory ) {
			throw new ArgumentException( "--target-directory and --no-target-directory are mutually exclusive" );
		}
		if ( parsed.TargetDirectory is not null ) {
			parsed.Destination = parsed.TargetDirectory;
			parsed.DestinationMode = CopyMoveDestinationMode.TargetDirectory;
			parsed.Sources.AddRange( operands );
		} else if ( operands.Count >= 2 ) {
			parsed.Destination = operands[^1];
			parsed.Sources.AddRange( operands.Take( operands.Count - 1 ) );
		} else {
			parsed.Sources.AddRange( operands );
		}
		return parsed;
	}

	private static void ParseLongOption( string argument, string[] args, ref int index, ParsedOptions parsed ) {
		var separator = argument.IndexOf( '=' );
		var name = separator < 0 ? argument : argument[..separator];
		var value = separator < 0 ? null : argument[(separator + 1)..];
		switch ( name ) {
			case "--archive":
				parsed.Recursive = true;
				parsed.SymbolicLinkMode = SymbolicLinkTraversalMode.Never;
				parsed.MetadataFields = RecursiveMetadataFields.All;
				parsed.PreserveHardLinks = true;
				break;
			case "--recursive": parsed.Recursive = true; break;
			case "--dereference": parsed.SymbolicLinkMode = SymbolicLinkTraversalMode.Always; break;
			case "--no-dereference": parsed.SymbolicLinkMode = SymbolicLinkTraversalMode.Never; break;
			case "--preserve": ApplyPreserve( value ?? "mode,ownership,timestamps", parsed, true ); break;
			case "--no-preserve": ApplyPreserve( RequireValue( name, value, args, ref index ), parsed, false ); break;
			case "--sparse": parsed.SparsePolicy = ParseSparse( RequireValue( name, value, args, ref index ) ); break;
			case "--reflink": parsed.ReflinkPolicy = ParseReflink( value ?? "always" ); break;
			case "--backup": parsed.BackupMode = ParseBackup( value ?? "existing" ); break;
			case "--suffix": parsed.BackupSuffix = RequireValue( name, value, args, ref index ); break;
			case "--force": parsed.OverwriteMode = CopyMoveOverwriteMode.Replace; break;
			case "--interactive": parsed.OverwriteMode = CopyMoveOverwriteMode.Interactive; break;
			case "--no-clobber": parsed.OverwriteMode = CopyMoveOverwriteMode.NoClobber; break;
			case "--update": parsed.OverwriteMode = CopyMoveOverwriteMode.Update; break;
			case "--remove-destination": parsed.RemoveDestination = true; break;
			case "--link": parsed.CopyAsHardLink = true; break;
			case "--symbolic-link": parsed.CopyAsSymbolicLink = true; break;
			case "--target-directory": parsed.TargetDirectory = RequireValue( name, value, args, ref index ); break;
			case "--no-target-directory": parsed.DestinationMode = CopyMoveDestinationMode.NoTargetDirectory; break;
			case "--verbose": parsed.Verbose = true; break;
			case "--one-file-system": parsed.OneFileSystem = true; break;
			case "--help": parsed.Help = true; break;
			case "--version": parsed.Version = true; break;
			default: throw new ArgumentException( string.Concat( "unrecognized option '", argument, "'" ) );
		}
	}

	private static void ParseShortOptions( string argument, string[] args, ref int index, ParsedOptions parsed ) {
		for ( var offset = 1; offset < argument.Length; offset++ ) {
			var option = argument[offset];
			switch ( option ) {
				case 'a':
					parsed.Recursive = true;
					parsed.SymbolicLinkMode = SymbolicLinkTraversalMode.Never;
					parsed.MetadataFields = RecursiveMetadataFields.All;
					parsed.PreserveHardLinks = true;
					break;
				case 'R': case 'r': parsed.Recursive = true; break;
				case 'H': parsed.SymbolicLinkMode = SymbolicLinkTraversalMode.RootsOnly; break;
				case 'L': parsed.SymbolicLinkMode = SymbolicLinkTraversalMode.Always; break;
				case 'P': parsed.SymbolicLinkMode = SymbolicLinkTraversalMode.Never; break;
				case 'd': parsed.SymbolicLinkMode = SymbolicLinkTraversalMode.Never; parsed.PreserveHardLinks = true; break;
				case 'p': parsed.MetadataFields |= RecursiveMetadataFields.Mode | RecursiveMetadataFields.Ownership | RecursiveMetadataFields.Timestamps; break;
				case 'b': parsed.BackupMode = TransactionalReplacementBackupMode.Existing; break;
				case 'f': parsed.OverwriteMode = CopyMoveOverwriteMode.Replace; break;
				case 'i': parsed.OverwriteMode = CopyMoveOverwriteMode.Interactive; break;
				case 'n': parsed.OverwriteMode = CopyMoveOverwriteMode.NoClobber; break;
				case 'u': parsed.OverwriteMode = CopyMoveOverwriteMode.Update; break;
				case 'l': parsed.CopyAsHardLink = true; break;
				case 's': parsed.CopyAsSymbolicLink = true; break;
				case 'v': parsed.Verbose = true; break;
				case 'x': parsed.OneFileSystem = true; break;
				case 'S': parsed.BackupSuffix = TakeShortValue( argument, ref offset, args, ref index, option ); break;
				case 't': parsed.TargetDirectory = TakeShortValue( argument, ref offset, args, ref index, option ); break;
				case 'T': parsed.DestinationMode = CopyMoveDestinationMode.NoTargetDirectory; break;
				default: throw new ArgumentException( string.Concat( "invalid option -- '", option, "'" ) );
			}
		}
	}

	private static void ApplyPreserve( string value, ParsedOptions parsed, bool enable ) {
		foreach ( var token in value.Split( ',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries ) ) {
			var field = token switch {
				"mode" => RecursiveMetadataFields.Mode,
				"ownership" => RecursiveMetadataFields.Ownership,
				"timestamps" => RecursiveMetadataFields.Timestamps,
				"links" => RecursiveMetadataFields.HardLinks,
				"all" => RecursiveMetadataFields.All,
				"context" or "xattr" => throw new ArgumentException( string.Concat( "metadata class '", token, "' is not supported by the E5 contract" ) ),
				_ => throw new ArgumentException( string.Concat( "invalid --preserve attribute '", token, "'" ) )
			};
			parsed.MetadataFields = enable ? parsed.MetadataFields | field : parsed.MetadataFields & ~field;
			if ( field.HasFlag( RecursiveMetadataFields.HardLinks ) ) parsed.PreserveHardLinks = enable;
		}
	}

	private static RecursiveSparseFilePolicy ParseSparse( string value ) => value switch {
		"never" => RecursiveSparseFilePolicy.Never,
		"auto" => RecursiveSparseFilePolicy.WhenSupported,
		"always" => RecursiveSparseFilePolicy.Require,
		_ => throw new ArgumentException( string.Concat( "invalid --sparse argument '", value, "'" ) )
	};

	private static CopyMoveReflinkPolicy ParseReflink( string value ) => value switch {
		"never" => CopyMoveReflinkPolicy.Never,
		"auto" => CopyMoveReflinkPolicy.Auto,
		"always" => CopyMoveReflinkPolicy.Always,
		_ => throw new ArgumentException( string.Concat( "invalid --reflink argument '", value, "'" ) )
	};

	private static TransactionalReplacementBackupMode ParseBackup( string value ) => value switch {
		"none" or "off" => TransactionalReplacementBackupMode.None,
		"simple" or "never" => TransactionalReplacementBackupMode.Simple,
		"numbered" or "t" => TransactionalReplacementBackupMode.Numbered,
		"existing" or "nil" => TransactionalReplacementBackupMode.Existing,
		_ => throw new ArgumentException( string.Concat( "invalid backup type '", value, "'" ) )
	};

	private static string RequireValue( string option, string? inline, string[] args, ref int index ) {
		if ( inline is not null ) return inline;
		if ( ++index >= args.Length ) throw new ArgumentException( string.Concat( "option '", option, "' requires an argument" ) );
		return args[index];
	}

	private static string TakeShortValue( string argument, ref int offset, string[] args, ref int index, char option ) {
		if ( offset + 1 < argument.Length ) {
			var value = argument[(offset + 1)..];
			offset = argument.Length;
			return value;
		}
		if ( ++index >= args.Length ) throw new ArgumentException( string.Concat( "option requires an argument -- '", option, "'" ) );
		return args[index];
	}

	private const string HelpText = """
Usage: cp [OPTION]... SOURCE... DEST
  or:  cp [OPTION]... SOURCE... DIRECTORY
  or:  cp [OPTION]... -t DIRECTORY SOURCE...
Copy SOURCE to DEST, or multiple SOURCE(s) to DIRECTORY.

  -a, --archive                same as -R -d --preserve=all
  -R, -r, --recursive          copy directories recursively
  -H                           follow command-line symbolic links
  -L, --dereference            follow all symbolic links
  -P, --no-dereference         never follow symbolic links
  -p                           preserve mode, ownership, and timestamps
      --preserve[=ATTR_LIST]   preserve selected metadata
      --sparse=WHEN            control creation of sparse files
      --reflink[=WHEN]         control clone/reflink copies
  -b, --backup[=CONTROL]       make a backup of each existing destination
  -f, --force                  replace existing destinations
  -i, --interactive            prompt before overwrite
  -n, --no-clobber             do not overwrite existing files
  -u, --update                 copy only when SOURCE is newer
  -t, --target-directory=DIR   copy all SOURCE arguments into DIR
  -T, --no-target-directory    treat DEST as a normal file
  -v, --verbose                explain what is being done
      --help                   display this help and exit
      --version                output version information and exit
""";

	private sealed class ParsedOptions {
		public List<string> Sources { get; } = new();
		public string? Destination { get; set; }
		public string? TargetDirectory { get; set; }
		public bool Recursive { get; set; }
		public CopyMoveDestinationMode DestinationMode { get; set; } = CopyMoveDestinationMode.Auto;
		public SymbolicLinkTraversalMode SymbolicLinkMode { get; set; } = SymbolicLinkTraversalMode.Never;
		public RecursiveMetadataFields MetadataFields { get; set; }
		public RecursiveMetadataFields RequiredMetadataFields { get; set; }
		public RecursiveSparseFilePolicy SparsePolicy { get; set; } = RecursiveSparseFilePolicy.WhenSupported;
		public CopyMoveReflinkPolicy ReflinkPolicy { get; set; } = CopyMoveReflinkPolicy.Auto;
		public CopyMoveOverwriteMode OverwriteMode { get; set; } = CopyMoveOverwriteMode.Replace;
		public TransactionalReplacementBackupMode BackupMode { get; set; }
		public string BackupSuffix { get; set; } = "~";
		public bool PreserveHardLinks { get; set; }
		public bool CopyAsHardLink { get; set; }
		public bool CopyAsSymbolicLink { get; set; }
		public bool RemoveDestination { get; set; }
		public bool OneFileSystem { get; set; }
		public bool Verbose { get; set; }
		public bool Help { get; set; }
		public bool Version { get; set; }
	}
}
