// Original behavior/reference: GNU Coreutils 9.11 ln.c
// Ported to .NET by Timothy J. Bruce <uniblab@hotmail.com>

namespace Icod.CoreUtils.Ln;

using Icod.CoreUtils.Shared.CommandLine;
using Icod.CoreUtils.Shared.Diagnostics;
using Icod.CoreUtils.Shared.FileSystem.Mutation;
using Icod.CoreUtils.Shared.FileSystem.Traversal;

/// <summary>Implements GNU <c>ln</c> over the shared single-path mutation provider.</summary>
public static class Command {
	private sealed record Options(
		bool Symbolic,
		bool Force,
		bool Interactive,
		bool Verbose,
		bool Relative,
		bool NoDereference,
		bool Logical,
		bool Physical,
		bool Backup,
		string BackupControl,
		string Suffix,
		string? TargetDirectory,
		bool NoTargetDirectory
	);

	/// <summary>Runs <c>ln</c> synchronously against optional caller-owned streams.</summary>
	public static int Run( string[] args, TextReader? stdin = null, TextWriter? stdout = null, TextWriter? stderr = null ) {
		var context = new CommandContext( "ln", stdin ?? Console.In, stdout ?? Console.Out, stderr ?? Console.Error );
		return RunAsync( args, context ).AsTask().GetAwaiter().GetResult();
	}

	/// <summary>Runs <c>ln</c> asynchronously with the system mutation provider.</summary>
	public static ValueTask<int> RunAsync( string[] args, CommandContext? context = null ) {
		return RunAsync( args, context ?? CommandContext.CreateConsole( "ln" ), SystemFileSystemMutationProvider.Instance );
	}

	/// <summary>Runs <c>ln</c> asynchronously with an injected mutation provider.</summary>
	public static async ValueTask<int> RunAsync( string[] args, CommandContext context, IFileSystemMutationProvider mutationProvider ) {
		ArgumentNullException.ThrowIfNull( context );
		ArgumentNullException.ThrowIfNull( mutationProvider );
		args ??= Array.Empty<string>();
		try {
			var parsing = Parse( args );
			if ( parsing.Help ) { await WriteUsageAsync( context.StandardOutput, context.CancellationToken ).ConfigureAwait( false ); return 0; }
			if ( parsing.Version ) { await context.StandardOutput.WriteLineAsync( "ln (Icod.CoreUtils) 0.1" ).ConfigureAwait( false ); return 0; }
			if ( parsing.Error is not null ) {
				await context.StandardError.WriteLineAsync( string.Concat( "ln: ", parsing.Error ) ).ConfigureAwait( false );
				await context.StandardError.WriteLineAsync( "Try 'ln --help' for more information." ).ConfigureAwait( false );
				return 1;
			}
			var operands = parsing.Operands;
			if ( operands.Count == 0 ) return await OperandFailureAsync( context, "missing file operand" ).ConfigureAwait( false );
			if ( parsing.Options.TargetDirectory is null && operands.Count == 1 ) return await OperandFailureAsync( context, string.Concat( "missing destination file operand after ", Quote( operands[ 0 ] ) ) ).ConfigureAwait( false );

			var targetDirectory = parsing.Options.TargetDirectory;
			IReadOnlyList<string> sources;
			string destination;
			if ( targetDirectory is not null ) {
				sources = operands;
				destination = targetDirectory;
			} else {
				sources = operands.Take( operands.Count - 1 ).ToArray();
				destination = operands[ ^1 ];
			}
			var destinationIsDirectory = !parsing.Options.NoTargetDirectory && IsDirectoryTarget( destination, parsing.Options.NoDereference );
			if ( sources.Count > 1 && !destinationIsDirectory ) {
				await context.StandardError.WriteLineAsync( string.Concat( "ln: target ", Quote( destination ), ": Not a directory" ) ).ConfigureAwait( false );
				return 1;
			}
			var status = 0;
			foreach ( var source in sources ) {
				var linkPath = destinationIsDirectory ? Path.Combine( destination, Basename( source ) ) : destination;
				if ( !await CreateOneAsync( source, linkPath, parsing.Options, context, mutationProvider ).ConfigureAwait( false ) ) status = 1;
			}
			return status;
		} catch ( OperationCanceledException ) when ( context.CancellationToken.IsCancellationRequested ) { return CommandExitCodes.Canceled; }
	}

	private static async ValueTask<bool> CreateOneAsync( string source, string linkPath, Options options, CommandContext context, IFileSystemMutationProvider mutationProvider ) {
		if ( DestinationExists( linkPath ) ) {
			if ( options.Interactive ) {
				await context.StandardError.WriteAsync( string.Concat( "ln: replace ", Quote( linkPath ), "? " ) ).ConfigureAwait( false );
				var answer = await context.StandardInput.ReadLineAsync().ConfigureAwait( false );
				if ( string.IsNullOrEmpty( answer ) || char.ToLowerInvariant( answer[ 0 ] ) != 'y' ) return true;
			} else if ( !options.Force && !options.Backup ) {
				await FailureAsync( context, linkPath, "File exists" ).ConfigureAwait( false );
				return false;
			}
			if ( options.Backup ) {
				var backup = ChooseBackupName( linkPath, options.BackupControl, options.Suffix );
				try { MovePhysical( linkPath, backup ); }
				catch ( Exception ex ) when ( ex is IOException or UnauthorizedAccessException ) { await FailureAsync( context, linkPath, ex.Message ).ConfigureAwait( false ); return false; }
			} else {
				var removal = await mutationProvider.RemoveFileAsync( linkPath, cancellationToken: context.CancellationToken ).ConfigureAwait( false );
				if ( !removal.Succeeded ) { await FailureAsync( context, linkPath, removal.Message ?? "cannot remove destination" ).ConfigureAwait( false ); return false; }
			}
		}

		FileSystemMutationResult result;
		if ( options.Symbolic ) {
			var targetText = options.Relative ? MakeRelativeTarget( source, linkPath ) : source;
			result = await mutationProvider.CreateSymbolicLinkAsync(
				linkPath,
				targetText,
				TargetIsDirectory( source ),
				FileSystemMutationPrecondition.DestinationMustNotExist(),
				context.CancellationToken
			).ConfigureAwait( false );
		} else {
			var dereference = options.Physical ? PathDereferenceMode.NoFollow : PathDereferenceMode.FollowEligiblePathIndirection;
			result = await mutationProvider.CreateHardLinkAsync(
				linkPath,
				source,
				dereference,
				FileSystemMutationPrecondition.DestinationMustNotExist(),
				cancellationToken: context.CancellationToken
			).ConfigureAwait( false );
		}
		if ( !result.Succeeded ) { await FailureAsync( context, linkPath, Describe( result ) ).ConfigureAwait( false ); return false; }
		if ( options.Verbose ) await context.StandardOutput.WriteLineAsync( string.Concat( Quote( linkPath ), " -> ", Quote( options.Symbolic && options.Relative ? MakeRelativeTarget( source, linkPath ) : source ) ) ).ConfigureAwait( false );
		return true;
	}

	private static (Options Options, List<string> Operands, string? Error, bool Help, bool Version) Parse( string[] args ) {
		var symbolic = false; var force = false; var interactive = false; var verbose = false; var relative = false;
		var noDereference = false; var logical = true; var physical = false; var backup = false; var control = "existing"; var suffix = "~";
		string? targetDirectory = null; var noTargetDirectory = false; var operands = new List<string>(); var optionsDone = false;
		for ( var i = 0; i < args.Length; i++ ) {
			var arg = args[ i ];
			if (
				optionsDone
				|| ( arg == "-" )
				|| !arg.StartsWith( "-", StringComparison.Ordinal )
			) {
				operands.Add( arg );
				continue;
			}
			if ( arg == "--" ) { optionsDone = true; continue; }
			if ( arg == "--help" ) return ( default!, operands, null, true, false );
			if ( arg == "--version" ) return ( default!, operands, null, false, true );
			if ( arg is "-s" or "--symbolic" ) symbolic = true;
			else if ( arg is "-f" or "--force" ) { force = true; interactive = false; }
			else if ( arg is "-i" or "--interactive" ) { interactive = true; force = false; }
			else if ( arg is "-v" or "--verbose" ) verbose = true;
			else if ( arg is "-r" or "--relative" ) relative = true;
			else if ( arg is "-n" or "--no-dereference" ) noDereference = true;
			else if ( arg is "-L" or "--logical" ) { logical = true; physical = false; }
			else if ( arg is "-P" or "--physical" ) { physical = true; logical = false; }
			else if ( arg is "-b" or "--backup" ) backup = true;
			else if ( arg.StartsWith( "--backup=", StringComparison.Ordinal ) ) { backup = true; control = arg[ 9.. ]; }
			else if ( arg is "-T" or "--no-target-directory" ) noTargetDirectory = true;
			else if ( arg is "-t" or "--target-directory" ) { if ( ++i >= args.Length ) return ( default!, operands, "option requires an argument -- 't'", false, false ); targetDirectory = args[ i ]; }
			else if ( arg.StartsWith( "--target-directory=", StringComparison.Ordinal ) ) targetDirectory = arg[ 19.. ];
			else if ( arg is "-S" or "--suffix" ) { if ( ++i >= args.Length ) return ( default!, operands, "option requires an argument -- 'S'", false, false ); suffix = args[ i ]; }
			else if ( arg.StartsWith( "--suffix=", StringComparison.Ordinal ) ) suffix = arg[ 9.. ];
			else return ( default!, operands, string.Concat( "unrecognized option ", Quote( arg ) ), false, false );
		}
		if ( relative && !symbolic ) return ( default!, operands, "--relative can only be used with --symbolic", false, false );
		if ( targetDirectory is not null && noTargetDirectory ) return ( default!, operands, "cannot combine --target-directory and --no-target-directory", false, false );
		return ( new Options( symbolic, force, interactive, verbose, relative, noDereference, logical, physical, backup, control, suffix, targetDirectory, noTargetDirectory ), operands, null, false, false );
	}

	/// <summary>Writes command usage.</summary>
	public static async ValueTask WriteUsageAsync( TextWriter output, CancellationToken cancellationToken = default ) {
		foreach ( var line in new[] { "Usage: ln [OPTION]... [-T] TARGET LINK_NAME", "  or:  ln [OPTION]... TARGET", "  or:  ln [OPTION]... TARGET... DIRECTORY", "  or:  ln [OPTION]... -t DIRECTORY TARGET...", "Create links between files.", string.Empty, "  -b, --backup[=CONTROL]      make a backup of each existing destination file", "  -f, --force                 remove existing destination files", "  -i, --interactive           prompt whether to remove destinations", "  -L, --logical               dereference TARGETs that are symbolic links", "  -n, --no-dereference        treat a destination symlink to a directory as a file", "  -P, --physical              make hard links directly to symbolic links", "  -r, --relative              create symbolic links relative to link location", "  -s, --symbolic              make symbolic links instead of hard links", "  -S, --suffix=SUFFIX         override the usual backup suffix", "  -t, --target-directory=DIR  specify the DIRECTORY in which to create links", "  -T, --no-target-directory   treat LINK_NAME as a normal file", "  -v, --verbose               print name of each linked file", "      --help                  display this help and exit", "      --version               output version information and exit" } ) { cancellationToken.ThrowIfCancellationRequested(); await output.WriteLineAsync( line ).ConfigureAwait( false ); }
	}

	private static bool DestinationExists( string path ) { try { _ = File.GetAttributes( path ); return true; } catch ( FileNotFoundException ) { return false; } catch ( DirectoryNotFoundException ) { return false; } }
	private static bool IsDirectoryTarget( string path, bool noDereference ) { try { var info = new DirectoryInfo( path ); return info.Exists && !( noDereference && info.LinkTarget is not null ); } catch { return false; } }
	private static bool TargetIsDirectory( string path ) { try { return File.GetAttributes( path ).HasFlag( FileAttributes.Directory ); } catch { return false; } }
	private static string Basename( string path ) => Path.GetFileName( path.TrimEnd( Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar ) );
	private static string MakeRelativeTarget( string source, string linkPath ) { var sourceFull = Path.GetFullPath( source ); var parent = Path.GetDirectoryName( Path.GetFullPath( linkPath ) ) ?? Directory.GetCurrentDirectory(); return Path.GetRelativePath( parent, sourceFull ); }
	private static string ChooseBackupName( string path, string control, string suffix ) { if ( control is "numbered" or "t" || control is "existing" or "nil" && File.Exists( string.Concat( path, ".~1~" ) ) ) { for ( var n = 1; ; n++ ) { var candidate = string.Concat( path, ".~", n.ToString( System.Globalization.CultureInfo.InvariantCulture ), "~" ); if ( !DestinationExists( candidate ) ) return candidate; } } return string.Concat( path, suffix ); }
	private static void MovePhysical( string source, string destination ) { if ( DestinationExists( destination ) ) throw new IOException( "backup file already exists" ); var attributes = File.GetAttributes( source ); if ( attributes.HasFlag( FileAttributes.Directory ) ) Directory.Move( source, destination ); else File.Move( source, destination ); }
	private static string Describe( FileSystemMutationResult result ) => result.ErrorCode switch { FileSystemMutationErrorCode.AlreadyExists => "File exists", FileSystemMutationErrorCode.NotFound or FileSystemMutationErrorCode.ParentNotFound => "No such file or directory", FileSystemMutationErrorCode.CrossDevice => "Invalid cross-device link", FileSystemMutationErrorCode.WrongObjectKind => "hard link not allowed for directory", FileSystemMutationErrorCode.AccessDenied => "Permission denied", FileSystemMutationErrorCode.PrivilegeRequired => "Operation not permitted", _ => result.Message ?? "Input/output error" };
	private static async ValueTask FailureAsync( CommandContext context, string path, string reason ) => await context.StandardError.WriteLineAsync( string.Concat( "ln: failed to create link ", Quote( path ), ": ", reason ) ).ConfigureAwait( false );
	private static async ValueTask<int> OperandFailureAsync( CommandContext context, string message ) { await context.StandardError.WriteLineAsync( string.Concat( "ln: ", message ) ).ConfigureAwait( false ); await context.StandardError.WriteLineAsync( "Try 'ln --help' for more information." ).ConfigureAwait( false ); return 1; }
	private static string Quote( string value ) => string.Concat( "'", value.Replace( "'", "'\\''", StringComparison.Ordinal ), "'" );
}
