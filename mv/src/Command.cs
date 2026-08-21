// Original behavior/reference: GNU coreutils
// Ported to .NET by Timothy J. Bruce <uniblab@hotmail.com>

using Icod.CoreUtils.Shared.Diagnostics;
using Icod.CoreUtils.Shared.FileSystem.CopyMove;
using Icod.CommandFramework.FileSystem.RecursiveMutation;
using Icod.CoreUtils.Shared.FileSystem.TransactionalReplacement;
using Icod.CommandFramework.FileSystem.Traversal;

namespace Icod.CoreUtils.Mv;

/// <summary>Implements GNU-compatible asynchronous file and directory moves.</summary>
public static class Command {
	private const string Version = "mv (Icod.CoreUtils) 1.0";

	/// <summary>Runs <c>mv</c> synchronously.</summary>
	/// <param name="args">The command-line arguments.</param>
	/// <param name="stdin">Optional standard input.</param>
	/// <param name="stdout">Optional standard output.</param>
	/// <param name="stderr">Optional standard error.</param>
	/// <returns>The process exit status.</returns>
	public static int Run( string[] args, TextReader? stdin = null, TextWriter? stdout = null, TextWriter? stderr = null ) =>
		RunAsync( args, stdin, stdout, stderr ).AsTask().GetAwaiter().GetResult();

	/// <summary>Runs <c>mv</c> asynchronously.</summary>
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
				"mv",
				stdin ?? Console.In,
				stdout ?? Console.Out,
				stderr ?? Console.Error,
				cancellationToken: cancellationToken
			)
		);
	}

	/// <summary>Runs <c>mv</c> through a shared command context.</summary>
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
			await stderr.WriteLineAsync( string.Concat( "mv: ", exception.Message ) ).ConfigureAwait( false );
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
			await stderr.WriteLineAsync( "mv: missing file operand" ).ConfigureAwait( false );
			return CommandExitCodes.UsageError;
		}

		var options = new CopyMoveOptions {
			Operation = CopyMoveOperationKind.Move,
			Recursive = true,
			DestinationMode = parsed.DestinationMode,
			SymbolicLinkMode = SymbolicLinkTraversalMode.Never,
			MetadataFields = RecursiveMetadataFields.All,
			SparseFilePolicy = RecursiveSparseFilePolicy.WhenSupported,
			ReflinkPolicy = CopyMoveReflinkPolicy.Auto,
			OverwriteMode = parsed.OverwriteMode,
			BackupMode = parsed.BackupMode,
			BackupSuffix = parsed.BackupSuffix,
			PreserveHardLinks = true,
			NoCopyFallback = parsed.NoCopy,
			Verbose = parsed.Verbose,
			Prompt = async ( source, destination, token ) => {
				token.ThrowIfCancellationRequested();
				await stderr.WriteAsync( string.Concat( "mv: overwrite '", destination, "'? " ) ).ConfigureAwait( false );
				var response = await stdin.ReadLineAsync( token ).ConfigureAwait( false );
				return response?.StartsWith( "y", StringComparison.OrdinalIgnoreCase ) == true;
			}
		};
		try {
			var result = await new CopyMoveEngine().ExecuteAsync(
				parsed.Sources,
				parsed.Destination,
				options,
				cancellationToken
			).ConfigureAwait( false );
			foreach ( var item in result.Items ) {
				if ( item.Outcome == CopyMoveItemOutcome.Failed ) {
					await stderr.WriteLineAsync( string.Concat( "mv: cannot move '", item.SourcePath, "' to '", item.DestinationPath, "': ", item.Message ) ).ConfigureAwait( false );
				} else if ( parsed.Verbose && item.Outcome == CopyMoveItemOutcome.Completed ) {
					await stdout.WriteLineAsync( string.Concat( "renamed '", item.SourcePath, "' -> '", item.DestinationPath, "'" ) ).ConfigureAwait( false );
				}
			}
			return result.Succeeded ? CommandExitCodes.Success : CommandExitCodes.Failure;
		} catch ( OperationCanceledException ) when ( cancellationToken.IsCancellationRequested ) {
			return CommandExitCodes.Canceled;
		} catch ( Exception exception ) when ( exception is ArgumentException or IOException or UnauthorizedAccessException or NotSupportedException ) {
			await stderr.WriteLineAsync( string.Concat( "mv: ", exception.Message ) ).ConfigureAwait( false );
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
			if ( argument.StartsWith( "--", StringComparison.Ordinal ) ) ParseLongOption( argument, args, ref index, parsed );
			else ParseShortOptions( argument, args, ref index, parsed );
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
			case "--backup": parsed.BackupMode = ParseBackup( value ?? "existing" ); break;
			case "--suffix": parsed.BackupSuffix = RequireValue( name, value, args, ref index ); break;
			case "--force": parsed.OverwriteMode = CopyMoveOverwriteMode.Replace; break;
			case "--interactive": parsed.OverwriteMode = CopyMoveOverwriteMode.Interactive; break;
			case "--no-clobber": parsed.OverwriteMode = CopyMoveOverwriteMode.NoClobber; break;
			case "--update": parsed.OverwriteMode = CopyMoveOverwriteMode.Update; break;
			case "--no-copy": parsed.NoCopy = true; break;
			case "--target-directory": parsed.TargetDirectory = RequireValue( name, value, args, ref index ); break;
			case "--no-target-directory": parsed.DestinationMode = CopyMoveDestinationMode.NoTargetDirectory; break;
			case "--verbose": parsed.Verbose = true; break;
			case "--help": parsed.Help = true; break;
			case "--version": parsed.Version = true; break;
			default: throw new ArgumentException( string.Concat( "unrecognized option '", argument, "'" ) );
		}
	}

	private static void ParseShortOptions( string argument, string[] args, ref int index, ParsedOptions parsed ) {
		for ( var offset = 1; offset < argument.Length; offset++ ) {
			var option = argument[offset];
			switch ( option ) {
				case 'b': parsed.BackupMode = TransactionalReplacementBackupMode.Existing; break;
				case 'f': parsed.OverwriteMode = CopyMoveOverwriteMode.Replace; break;
				case 'i': parsed.OverwriteMode = CopyMoveOverwriteMode.Interactive; break;
				case 'n': parsed.OverwriteMode = CopyMoveOverwriteMode.NoClobber; break;
				case 'u': parsed.OverwriteMode = CopyMoveOverwriteMode.Update; break;
				case 'v': parsed.Verbose = true; break;
				case 'S': parsed.BackupSuffix = TakeShortValue( argument, ref offset, args, ref index, option ); break;
				case 't': parsed.TargetDirectory = TakeShortValue( argument, ref offset, args, ref index, option ); break;
				case 'T': parsed.DestinationMode = CopyMoveDestinationMode.NoTargetDirectory; break;
				default: throw new ArgumentException( string.Concat( "invalid option -- '", option, "'" ) );
			}
		}
	}

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
Usage: mv [OPTION]... SOURCE... DEST
  or:  mv [OPTION]... SOURCE... DIRECTORY
  or:  mv [OPTION]... -t DIRECTORY SOURCE...
Rename SOURCE to DEST, or move SOURCE(s) to DIRECTORY.

  -b, --backup[=CONTROL]       make a backup of each existing destination
  -f, --force                  do not prompt before overwriting
  -i, --interactive            prompt before overwrite
  -n, --no-clobber             do not overwrite an existing file
  -u, --update                 move only when SOURCE is newer
      --no-copy                do not copy when rename crosses filesystems
  -t, --target-directory=DIR   move all SOURCE arguments into DIR
  -T, --no-target-directory    treat DEST as a normal file
  -v, --verbose                explain what is being done
      --help                   display this help and exit
      --version                output version information and exit
""";

	private sealed class ParsedOptions {
		public List<string> Sources { get; } = new();
		public string? Destination { get; set; }
		public string? TargetDirectory { get; set; }
		public CopyMoveDestinationMode DestinationMode { get; set; } = CopyMoveDestinationMode.Auto;
		public CopyMoveOverwriteMode OverwriteMode { get; set; } = CopyMoveOverwriteMode.Replace;
		public TransactionalReplacementBackupMode BackupMode { get; set; }
		public string BackupSuffix { get; set; } = "~";
		public bool NoCopy { get; set; }
		public bool Verbose { get; set; }
		public bool Help { get; set; }
		public bool Version { get; set; }
	}
}
