namespace Icod.CoreUtils.Shred;

using System.Security.Cryptography;

/// <summary>Executes overwrite passes and optional name-removal policy for <c>shred</c>.</summary>
internal sealed class ShredEngine {
	private const int BufferSize = 128 * 1024;
	private const ulong RegularFileBlockSize = 4096;
	private static readonly char[] WipeNameAlphabet = "0123456789abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ_".ToCharArray();

	private readonly TextWriter error;

	/// <summary>Initializes an engine that reports diagnostics to the supplied writer.</summary>
	/// <param name="error">The diagnostic and progress writer.</param>
	public ShredEngine( TextWriter error ) {
		this.error = error ?? throw new ArgumentNullException( nameof( error ) );
	}

	/// <summary>Processes all targets, continuing after target-local failures.</summary>
	/// <param name="options">The parsed command options.</param>
	/// <param name="standardOutput">The binary standard-output stream, when <c>-</c> is an operand.</param>
	/// <param name="cancellationToken">A cancellation token.</param>
	/// <returns>Zero when every target succeeds; otherwise one.</returns>
	public async Task<int> ExecuteAsync(
		ShredOptions options,
		Stream? standardOutput,
		CancellationToken cancellationToken
	) {
		ArgumentNullException.ThrowIfNull( options );

		IShredRandomSource randomSource;
		try {
			randomSource = OpenRandomSource( options.RandomSourcePath );
		} catch ( Exception exception ) when ( exception is IOException or UnauthorizedAccessException or NotSupportedException or ArgumentException ) {
			await error.WriteLineAsync( string.Concat(
				"shred: ", Quote( options.RandomSourcePath ?? string.Empty ), ": ", exception.Message
			) ).ConfigureAwait( false );
			return 1;
		}

		await using var ownedRandomSource = randomSource;
		var exitCode = 0;
		foreach ( var target in options.Targets ) {
			cancellationToken.ThrowIfCancellationRequested();
			try {
				if ( target == "-" ) {
					if ( standardOutput is null ) {
						throw new InvalidOperationException( "standard output is not available as a binary stream" );
					}
					await ProcessStreamAsync(
						target,
						standardOutput,
						isRegularFile: false,
						requiresExplicitSize: false,
						options: options,
						randomSource: ownedRandomSource,
						cancellationToken: cancellationToken
					).ConfigureAwait( false );
				} else {
					await ProcessNamedTargetAsync( target, options, ownedRandomSource, cancellationToken ).ConfigureAwait( false );
				}
			} catch ( OperationCanceledException ) {
				throw;
			} catch ( Exception exception ) when ( exception is IOException
				or UnauthorizedAccessException
				or NotSupportedException
				or InvalidOperationException
				or ArgumentException
				or OverflowException ) {
				await error.WriteLineAsync( string.Concat(
					"shred: ", Quote( target ), ": ", exception.Message
				) ).ConfigureAwait( false );
				exitCode = 1;
			}
		}
		return exitCode;
	}

	private static IShredRandomSource OpenRandomSource( string? path ) {
		if ( path is null ) {
			return new CryptoShredRandomSource();
		}

		var stream = new FileStream( path, new FileStreamOptions {
			Mode = FileMode.Open,
			Access = FileAccess.Read,
			Share = FileShare.Read,
			Options = FileOptions.Asynchronous | FileOptions.SequentialScan,
			BufferSize = BufferSize
		} );
		return new StreamShredRandomSource( stream );
	}

	private async Task ProcessNamedTargetAsync(
		string path,
		ShredOptions options,
		IShredRandomSource randomSource,
		CancellationToken cancellationToken
	) {
		var isDevice = IsDevicePath( path );
		if ( Directory.Exists( path ) ) {
			throw new IOException( "is a directory" );
		}
		if ( !isDevice && !File.Exists( path ) ) {
			throw new FileNotFoundException( "no such file or directory", path );
		}

		if ( options.Force && !isDevice ) {
			MakeWritable( path );
		}

		await using ( var stream = OpenTarget( path ) ) {
			await ProcessStreamAsync(
				path,
				stream,
				isRegularFile: !isDevice && stream.CanSeek,
				requiresExplicitSize: isDevice,
				options: options,
				randomSource: randomSource,
				cancellationToken: cancellationToken
			).ConfigureAwait( false );
			if ( options.RemovalMode != ShredRemovalMode.None && !isDevice && stream.CanSeek ) {
				stream.SetLength( 0 );
				await FlushPassAsync( stream, cancellationToken ).ConfigureAwait( false );
			}
		}

		if ( options.RemovalMode != ShredRemovalMode.None ) {
			await RemoveAsync( path, options.RemovalMode, options.Verbose, cancellationToken ).ConfigureAwait( false );
		}
	}

	private static FileStream OpenTarget( string path ) => new( path, new FileStreamOptions {
		Mode = FileMode.Open,
		Access = FileAccess.Write,
		Share = FileShare.ReadWrite | FileShare.Delete,
		Options = FileOptions.Asynchronous | FileOptions.SequentialScan,
		BufferSize = BufferSize
	} );

	private async Task ProcessStreamAsync(
		string displayName,
		Stream stream,
		bool isRegularFile,
		bool requiresExplicitSize,
		ShredOptions options,
		IShredRandomSource randomSource,
		CancellationToken cancellationToken
	) {
		var requestedSize = ResolveSize( displayName, stream, options.Size, requiresExplicitSize );
		var overwriteSize = isRegularFile && !options.Exact
			? RoundUp( requestedSize, RegularFileBlockSize )
			: requestedSize;
		if ( stream.CanSeek && overwriteSize > long.MaxValue ) {
			throw new IOException( "requested overwrite size exceeds the stream addressing limit" );
		}

		var passes = await ShredPassPlanner.CreateAsync(
			options.Iterations,
			options.Zero,
			randomSource,
			cancellationToken
		).ConfigureAwait( false );

		for ( var passIndex = 0; passIndex < passes.Count; passIndex++ ) {
			var pass = passes[ passIndex ];
			if ( options.Verbose ) {
				await error.WriteLineAsync( string.Concat(
					"shred: ", Quote( displayName ), ": pass ",
					( passIndex + 1 ).ToString( System.Globalization.CultureInfo.InvariantCulture ), "/",
					passes.Count.ToString( System.Globalization.CultureInfo.InvariantCulture ),
					" (", pass.Description, ")...0%"
				) ).ConfigureAwait( false );
			}

			if ( stream.CanSeek ) {
				stream.Seek( 0, SeekOrigin.Begin );
			}
			await WritePassAsync(
				displayName,
				stream,
				overwriteSize,
				pass,
				randomSource,
				options.Verbose,
				passIndex,
				passes.Count,
				cancellationToken
			).ConfigureAwait( false );
			await FlushPassAsync( stream, cancellationToken ).ConfigureAwait( false );
		}
	}

	private async Task WritePassAsync(
		string displayName,
		Stream stream,
		ulong length,
		ShredPass pass,
		IShredRandomSource randomSource,
		bool verbose,
		int passIndex,
		int passCount,
		CancellationToken cancellationToken
	) {
		var buffer = new byte[ BufferSize ];
		ulong written = 0;
		var reportedPercent = 0;
		while ( written < length ) {
			cancellationToken.ThrowIfCancellationRequested();
			var count = (int)Math.Min( (ulong)buffer.Length, length - written );
			var destination = buffer.AsMemory( 0, count );
			if ( pass.IsRandom ) {
				await randomSource.FillAsync( destination, cancellationToken ).ConfigureAwait( false );
			} else {
				FillPattern( destination.Span, pass.Pattern!, written );
			}
			await stream.WriteAsync( destination, cancellationToken ).ConfigureAwait( false );
			written += (uint)count;

			if ( verbose && length > 0 ) {
				var percent = (int)Math.Min( 100D, Math.Floor( (double)written / length * 100D ) );
				var milestone = percent / 10 * 10;
				if ( milestone > reportedPercent ) {
					reportedPercent = milestone;
					await ReportProgressAsync( displayName, pass, passIndex, passCount, milestone ).ConfigureAwait( false );
				}
			}
		}

		if ( verbose && reportedPercent < 100 ) {
			await ReportProgressAsync( displayName, pass, passIndex, passCount, 100 ).ConfigureAwait( false );
		}
	}

	private Task ReportProgressAsync(
		string displayName,
		ShredPass pass,
		int passIndex,
		int passCount,
		int percent
	) => error.WriteLineAsync( string.Concat(
		"shred: ", Quote( displayName ), ": pass ",
		( passIndex + 1 ).ToString( System.Globalization.CultureInfo.InvariantCulture ), "/",
		passCount.ToString( System.Globalization.CultureInfo.InvariantCulture ),
		" (", pass.Description, ")...",
		percent.ToString( System.Globalization.CultureInfo.InvariantCulture ), "%"
	) );

	private static void FillPattern( Span<byte> destination, byte[] pattern, ulong offset ) {
		for ( var index = 0; index < destination.Length; index++ ) {
			destination[ index ] = pattern[ (int)( ( offset + (uint)index ) % (uint)pattern.Length ) ];
		}
	}

	private static async Task FlushPassAsync( Stream stream, CancellationToken cancellationToken ) {
		await stream.FlushAsync( cancellationToken ).ConfigureAwait( false );
		if ( stream is FileStream fileStream ) {
			fileStream.Flush( flushToDisk: true );
		}
	}

	private static ulong ResolveSize(
		string displayName,
		Stream stream,
		ulong? explicitSize,
		bool requiresExplicitSize
	) {
		if ( explicitSize.HasValue ) {
			return explicitSize.Value;
		}
		if ( requiresExplicitSize ) {
			throw new InvalidOperationException( displayName == "-"
				? "standard output requires an explicit --size"
				: "device target requires an explicit --size" );
		}
		if ( !stream.CanSeek ) {
			throw new InvalidOperationException( "cannot determine target size; specify --size" );
		}
		try {
			return checked( (ulong)stream.Length );
		} catch ( Exception exception ) when ( exception is NotSupportedException or IOException ) {
			throw new InvalidOperationException( "cannot determine target size; specify --size", exception );
		}
	}

	private static ulong RoundUp( ulong value, ulong blockSize ) {
		if ( value == 0 ) {
			return 0;
		}
		try {
			return checked( ( value + blockSize - 1 ) / blockSize * blockSize );
		} catch ( OverflowException ) {
			throw new IOException( "target size is too large to round to a complete block" );
		}
	}

	private static bool IsDevicePath( string path ) {
		if ( OperatingSystem.IsWindows() ) {
			return path.StartsWith( @"\\.\", StringComparison.OrdinalIgnoreCase )
				|| path.StartsWith( @"\\?\GLOBALROOT\Device\", StringComparison.OrdinalIgnoreCase );
		}
		return path.StartsWith( "/dev/", StringComparison.Ordinal );
	}

	private static void MakeWritable( string path ) {
		var attributes = File.GetAttributes( path );
		if ( ( attributes & FileAttributes.ReadOnly ) != 0 ) {
			File.SetAttributes( path, attributes & ~FileAttributes.ReadOnly );
		}

		if ( !OperatingSystem.IsWindows() ) {
			try {
				var mode = File.GetUnixFileMode( path );
				if ( ( mode & UnixFileMode.UserWrite ) == 0 ) {
					File.SetUnixFileMode( path, mode | UnixFileMode.UserWrite );
				}
			} catch ( PlatformNotSupportedException ) {
				// The read-only attribute path above remains the portable fallback.
			}
		}
	}

	private async Task RemoveAsync(
		string originalPath,
		ShredRemovalMode mode,
		bool verbose,
		CancellationToken cancellationToken
	) {
		cancellationToken.ThrowIfCancellationRequested();
		if ( mode == ShredRemovalMode.Unlink ) {
			File.Delete( originalPath );
			if ( verbose ) {
				await error.WriteLineAsync( string.Concat( "shred: ", Quote( originalPath ), ": removed" ) ).ConfigureAwait( false );
			}
			return;
		}

		var currentPath = originalPath;
		var directory = System.IO.Path.GetDirectoryName( System.IO.Path.GetFullPath( originalPath ) ) ?? Directory.GetCurrentDirectory();
		var currentName = System.IO.Path.GetFileName( currentPath );
		try {
			foreach ( var length in GetWipeNameLengths( currentName.Length ) ) {
				cancellationToken.ThrowIfCancellationRequested();
				string nextPath;
				try {
					nextPath = RenameToUnusedWipeName( currentPath, directory, length );
				} catch ( Exception exception ) when ( exception is IOException or UnauthorizedAccessException ) {
					throw new IOException( string.Concat(
						"cannot rename during removal; data remains at ", Quote( currentPath ), ": ", exception.Message
					), exception );
				}

				if ( verbose ) {
					await error.WriteLineAsync( string.Concat(
						"shred: ", Quote( currentPath ), ": renamed to ", Quote( nextPath )
					) ).ConfigureAwait( false );
				}
				currentPath = nextPath;
				if ( mode == ShredRemovalMode.WipeSync ) {
					TrySynchronizeDirectory( directory );
				}
			}
		} catch ( OperationCanceledException exception ) {
			throw new IOException( string.Concat(
				"removal canceled; overwritten data remains at ", Quote( currentPath )
			), exception );
		}

		try {
			File.Delete( currentPath );
		} catch ( Exception exception ) when ( exception is IOException or UnauthorizedAccessException ) {
			throw new IOException( string.Concat(
				"cannot remove; overwritten data remains at ", Quote( currentPath ), ": ", exception.Message
			), exception );
		}
		if ( mode == ShredRemovalMode.WipeSync ) {
			TrySynchronizeDirectory( directory );
		}
		if ( verbose ) {
			await error.WriteLineAsync( string.Concat( "shred: ", Quote( currentPath ), ": removed" ) ).ConfigureAwait( false );
		}
	}

	private static IEnumerable<int> GetWipeNameLengths( int originalLength ) {
		var length = Math.Max( 1, originalLength );
		while ( true ) {
			yield return length;
			if ( length == 1 ) {
				yield break;
			}
			length--;
		}
	}

	private static string RenameToUnusedWipeName( string currentPath, string directory, int length ) {
		var random = new byte[ length ];
		for ( var attempt = 0; attempt < 64; attempt++ ) {
			RandomNumberGenerator.Fill( random );
			var name = new char[ length ];
			for ( var index = 0; index < name.Length; index++ ) {
				name[ index ] = WipeNameAlphabet[ random[ index ] % WipeNameAlphabet.Length ];
			}
			var candidate = System.IO.Path.Combine( directory, new string( name ) );
			if ( File.Exists( candidate ) || Directory.Exists( candidate ) ) {
				continue;
			}
			try {
				File.Move( currentPath, candidate );
				return candidate;
			} catch ( IOException ) when ( File.Exists( candidate ) || Directory.Exists( candidate ) ) {
				// A competing creator won the race; choose another name.
			}
		}
		throw new IOException( "unable to choose an unused replacement name" );
	}

	private static void TrySynchronizeDirectory( string directory ) {
		if ( OperatingSystem.IsWindows() ) {
			return;
		}
		try {
			using var handle = File.OpenHandle(
				directory,
				FileMode.Open,
				FileAccess.Read,
				FileShare.ReadWrite | FileShare.Delete,
				FileOptions.None
			);
			RandomAccess.FlushToDisk( handle );
		} catch ( Exception exception ) when ( exception is IOException
			or UnauthorizedAccessException
			or NotSupportedException ) {
			// Directory synchronization is best effort on hosts that do not expose it through the BCL.
		}
	}

	private static string Quote( string value ) => string.Concat( "'", value.Replace( "'", "'\\''", StringComparison.Ordinal ), "'" );
}
