namespace Icod.CoreUtils.Shred;

/// <summary>Provides the command boundary for GNU-compatible secure overwrite operations.</summary>
public static class Command {
	private const string VersionText = "shred (Icod CoreUtils) 0.1.0";

	/// <summary>Runs <c>shred</c> synchronously.</summary>
	/// <param name="args">The command-line arguments.</param>
	/// <param name="stdin">The standard-input reader. Binary standard input is not used by this command.</param>
	/// <param name="stdout">The standard-output writer.</param>
	/// <param name="stderr">The standard-error writer.</param>
	/// <returns>The process exit code.</returns>
	public static int Run(
		string[] args,
		TextReader? stdin = null,
		TextWriter? stdout = null,
		TextWriter? stderr = null
	) => RunAsync( args, stdin, stdout, stderr ).GetAwaiter().GetResult();

	/// <summary>Runs <c>shred</c> asynchronously.</summary>
	/// <param name="args">The command-line arguments.</param>
	/// <param name="stdin">The standard-input reader. Binary standard input is not used by this command.</param>
	/// <param name="stdout">The standard-output writer.</param>
	/// <param name="stderr">The standard-error writer.</param>
	/// <param name="cancellationToken">A cancellation token.</param>
	/// <returns>The process exit code.</returns>
	public static async Task<int> RunAsync(
		string[] args,
		TextReader? stdin = null,
		TextWriter? stdout = null,
		TextWriter? stderr = null,
		CancellationToken cancellationToken = default
	) {
		ArgumentNullException.ThrowIfNull( args );
		_ = stdin;
		stderr ??= Console.Error;

		ShredOptions options;
		try {
			options = ShredOptions.Parse( args );
		} catch ( ShredUsageException exception ) {
			await stderr.WriteLineAsync( string.Concat( "shred: ", exception.Message ) ).ConfigureAwait( false );
			await stderr.WriteLineAsync( "Try 'shred --help' for more information." ).ConfigureAwait( false );
			return 1;
		}

		var textOutput = stdout ?? Console.Out;
		if ( options.Help ) {
			await textOutput.WriteAsync( HelpText ).ConfigureAwait( false );
			return 0;
		}
		if ( options.Version ) {
			await textOutput.WriteLineAsync( VersionText ).ConfigureAwait( false );
			return 0;
		}

		Stream? binaryOutput = null;
		if ( options.Targets.Contains( "-", StringComparer.Ordinal ) ) {
			try {
				binaryOutput = ResolveBinaryOutput( stdout );
			} catch ( Exception exception ) when ( exception is ShredUsageException
				or IOException
				or UnauthorizedAccessException
				or NotSupportedException ) {
				await stderr.WriteLineAsync( string.Concat( "shred: ", exception.Message ) ).ConfigureAwait( false );
				return 1;
			}
		}

		try {
			var engine = new ShredEngine( stderr );
			return await engine.ExecuteAsync( options, binaryOutput, cancellationToken ).ConfigureAwait( false );
		} catch ( OperationCanceledException ) when ( cancellationToken.IsCancellationRequested ) {
			await stderr.WriteLineAsync( "shred: operation canceled" ).ConfigureAwait( false );
			return 1;
		} catch ( Exception exception ) when ( exception is IOException
			or UnauthorizedAccessException
			or NotSupportedException
			or InvalidOperationException
			or ArgumentException ) {
			await stderr.WriteLineAsync( string.Concat( "shred: ", exception.Message ) ).ConfigureAwait( false );
			return 1;
		}
	}

	private static Stream ResolveBinaryOutput( TextWriter? output ) {
		if ( output is null ) {
			return Console.OpenStandardOutput();
		}
		output.Flush();
		if ( output is StreamWriter streamWriter ) {
			return streamWriter.BaseStream;
		}
		throw new ShredUsageException( "standard output is not backed by a binary stream" );
	}

	private static readonly string HelpText = """
Usage: shred [OPTION]... FILE...
Overwrite the specified FILE(s) repeatedly, in order to make recovery harder.

  -f, --force                 change permissions to allow writing if necessary
  -n, --iterations=N          overwrite N times instead of the default (3)
      --random-source=FILE    get random bytes from FILE
  -s, --size=N                shred this many bytes
  -u, --remove[=HOW]          truncate and remove after overwriting
  -v, --verbose               show progress
  -x, --exact                 do not round file sizes up to a full block
  -z, --zero                  add a final overwrite with zeros
      --help                  display this help and exit
      --version               output version information and exit

HOW may be 'unlink', 'wipe', or 'wipesync' (the default for -u).
FILE '-' writes to standard output; non-seekable output requires --size.

CAUTION: shred cannot guarantee erasure on copy-on-write or journaled storage,
SSDs with remapping or wear leveling, snapshots, backups, RAID caches, or remote
storage.  It overwrites only the blocks exposed through the selected file path.
""" + Environment.NewLine;
}
