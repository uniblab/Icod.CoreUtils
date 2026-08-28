namespace Icod.CoreUtils.Shred.Tests;

using System.Text;
using Icod.CoreUtils.Shred;
using Xunit;

/// <summary>Verifies overwrite, reporting, recovery, and removal behavior through the public command boundary.</summary>
public sealed class CommandTests {
	/// <summary>Verifies exact overwriting from an external finite random source.</summary>
	[Fact]
	public async Task OverwritesExactSizeFromRandomSource() {
		using var temporary = new TemporaryDirectory();
		var target = temporary.PathFor( "target.bin" );
		var source = temporary.PathFor( "random.bin" );
		await File.WriteAllBytesAsync( target, new byte[ 16 ] );
		await File.WriteAllBytesAsync( source, Enumerable.Repeat( (byte)0xA5, 16 ).ToArray() );
		var error = new StringWriter();

		var exitCode = await Command.RunAsync( [ "-x", "-n1", "--random-source", source, target ], stderr: error );

		Assert.Equal( 0, exitCode );
		Assert.Equal( Enumerable.Repeat( (byte)0xA5, 16 ), await File.ReadAllBytesAsync( target ) );
		Assert.Equal( string.Empty, error.ToString() );
	}

	/// <summary>Verifies that a short random source fails rather than silently falling back.</summary>
	[Fact]
	public async Task ReportsExhaustedRandomSource() {
		using var temporary = new TemporaryDirectory();
		var target = temporary.PathFor( "target.bin" );
		var source = temporary.PathFor( "random.bin" );
		await File.WriteAllBytesAsync( target, new byte[ 8 ] );
		await File.WriteAllBytesAsync( source, [ 1, 2, 3, 4 ] );
		var error = new StringWriter();

		var exitCode = await Command.RunAsync( [ "-x", "-n1", "--remove=unlink", "--random-source", source, target ], stderr: error );

		Assert.Equal( 1, exitCode );
		Assert.Contains( "random source exhausted", error.ToString(), StringComparison.OrdinalIgnoreCase );
		Assert.True( File.Exists( target ) );
	}

	/// <summary>Verifies the requested final zero pass.</summary>
	[Fact]
	public async Task WritesFinalZeroPass() {
		using var temporary = new TemporaryDirectory();
		var target = temporary.PathFor( "target.bin" );
		await File.WriteAllBytesAsync( target, Enumerable.Repeat( (byte)0x7F, 32 ).ToArray() );

		var exitCode = await Command.RunAsync( [ "-x", "-n0", "-z", target ], stderr: new StringWriter() );

		Assert.Equal( 0, exitCode );
		Assert.Equal( new byte[ 32 ], await File.ReadAllBytesAsync( target ) );
	}

	/// <summary>Verifies explicit-size extension while preserving exact byte count.</summary>
	[Fact]
	public async Task ExtendsFileToExplicitExactSize() {
		using var temporary = new TemporaryDirectory();
		var target = temporary.PathFor( "target.bin" );
		await File.WriteAllBytesAsync( target, new byte[ 3 ] );

		var exitCode = await Command.RunAsync( [ "-x", "-n0", "-z", "-s17", target ], stderr: new StringWriter() );

		Assert.Equal( 0, exitCode );
		Assert.Equal( 17, new FileInfo( target ).Length );
		Assert.Equal( new byte[ 17 ], await File.ReadAllBytesAsync( target ) );
	}

	/// <summary>Verifies regular-file block rounding when exact mode is not selected.</summary>
	[Fact]
	public async Task RoundsRegularFilesToCompleteBlock() {
		using var temporary = new TemporaryDirectory();
		var target = temporary.PathFor( "target.bin" );
		await File.WriteAllBytesAsync( target, new byte[ 7 ] );

		var exitCode = await Command.RunAsync( [ "-n0", "-z", target ], stderr: new StringWriter() );

		Assert.Equal( 0, exitCode );
		Assert.Equal( 4096L, new FileInfo( target ).Length );
	}

	/// <summary>Verifies that force mode clears a read-only marker before opening the target.</summary>
	[Fact]
	public async Task ForceMakesReadOnlyTargetWritable() {
		using var temporary = new TemporaryDirectory();
		var target = temporary.PathFor( "target.bin" );
		await File.WriteAllBytesAsync( target, Enumerable.Repeat( (byte)0x7F, 8 ).ToArray() );
		File.SetAttributes( target, File.GetAttributes( target ) | FileAttributes.ReadOnly );
		try {
			var exitCode = await Command.RunAsync( [ "-f", "-x", "-n0", "-z", target ], stderr: new StringWriter() );

			Assert.Equal( 0, exitCode );
			Assert.Equal( new byte[ 8 ], await File.ReadAllBytesAsync( target ) );
		} finally {
			if ( File.Exists( target ) ) {
				File.SetAttributes( target, File.GetAttributes( target ) & ~FileAttributes.ReadOnly );
			}
		}
	}

	/// <summary>Verifies direct unlink removal after successful overwriting.</summary>
	[Fact]
	public async Task RemovesWithUnlinkPolicy() {
		using var temporary = new TemporaryDirectory();
		var target = temporary.PathFor( "target.bin" );
		await File.WriteAllBytesAsync( target, new byte[ 4 ] );

		var exitCode = await Command.RunAsync( [ "-x", "-n0", "--remove=unlink", target ], stderr: new StringWriter() );

		Assert.Equal( 0, exitCode );
		Assert.False( File.Exists( target ) );
	}

	/// <summary>Verifies default wipe-and-synchronize removal.</summary>
	[Fact]
	public async Task RemovesWithWipeSyncPolicy() {
		using var temporary = new TemporaryDirectory();
		var target = temporary.PathFor( "recognizable-name.bin" );
		await File.WriteAllBytesAsync( target, new byte[ 4 ] );

		var exitCode = await Command.RunAsync( [ "-x", "-n0", "-u", target ], stderr: new StringWriter() );

		Assert.Equal( 0, exitCode );
		Assert.Empty( Directory.EnumerateFileSystemEntries( temporary.Path ) );
	}

	/// <summary>Verifies that a target-local failure does not prevent later operands from being processed.</summary>
	[Fact]
	public async Task ContinuesAfterMissingOperand() {
		using var temporary = new TemporaryDirectory();
		var missing = temporary.PathFor( "missing.bin" );
		var target = temporary.PathFor( "target.bin" );
		await File.WriteAllBytesAsync( target, Enumerable.Repeat( (byte)0xA5, 8 ).ToArray() );
		var error = new StringWriter();

		var exitCode = await Command.RunAsync( [ "-x", "-n0", "-z", missing, target ], stderr: error );

		Assert.Equal( 1, exitCode );
		Assert.Contains( "missing.bin", error.ToString(), StringComparison.Ordinal );
		Assert.Equal( new byte[ 8 ], await File.ReadAllBytesAsync( target ) );
	}

	/// <summary>Verifies that seekable standard output uses its existing length.</summary>
	[Fact]
	public async Task WritesUnsizedSeekableStandardOutput() {
		using var output = new MemoryStream( Enumerable.Repeat( (byte)0x7F, 5 ).ToArray(), writable: true );
		await using var writer = new StreamWriter( output, new UTF8Encoding( false ), 1024, leaveOpen: true );

		var exitCode = await Command.RunAsync( [ "-x", "-n0", "-z", "-" ], stdout: writer, stderr: new StringWriter() );

		Assert.Equal( 0, exitCode );
		Assert.Equal( new byte[ 5 ], output.ToArray() );
	}

	/// <summary>Verifies that non-seekable standard output requires an explicit finite size.</summary>
	[Fact]
	public async Task RejectsUnsizedNonSeekableStandardOutput() {
		using var storage = new MemoryStream();
		await using var output = new NonSeekableWriteStream( storage );
		await using var writer = new StreamWriter( output, new UTF8Encoding( false ), 1024, leaveOpen: true );
		var error = new StringWriter();

		var exitCode = await Command.RunAsync( [ "-x", "-n0", "-z", "-" ], stdout: writer, stderr: error );

		Assert.Equal( 1, exitCode );
		Assert.Contains( "cannot determine target size", error.ToString(), StringComparison.Ordinal );
		Assert.Empty( storage.ToArray() );
	}

	/// <summary>Verifies finite binary output for the dash operand.</summary>
	[Fact]
	public async Task WritesSizedStandardOutput() {
		using var storage = new MemoryStream();
		await using var output = new NonSeekableWriteStream( storage );
		await using var writer = new StreamWriter( output, new UTF8Encoding( false ), 1024, leaveOpen: true );

		var exitCode = await Command.RunAsync( [ "-x", "-n0", "-z", "-s5", "-" ], stdout: writer, stderr: new StringWriter() );

		Assert.Equal( 0, exitCode );
		Assert.Equal( new byte[ 5 ], storage.ToArray() );
	}

	/// <summary>Verifies an explicit binary standard-output stream is independent of the text writer.</summary>
	[Fact]
	public async Task WritesToExplicitBinaryStandardOutput() {
		using var output = new MemoryStream();

		var exitCode = await Command.RunAsync(
			[ "-x", "-n0", "-z", "-s5", "-" ],
			stdout: new StringWriter(),
			stderr: new StringWriter(),
			binaryStdout: output
		);

		Assert.Equal( 0, exitCode );
		Assert.Equal( new byte[ 5 ], output.ToArray() );
	}

	/// <summary>Verifies progress and removal reporting.</summary>
	[Fact]
	public async Task ReportsVerboseProgress() {
		using var temporary = new TemporaryDirectory();
		var target = temporary.PathFor( "target.bin" );
		await File.WriteAllBytesAsync( target, new byte[ 8 ] );
		var error = new StringWriter();

		var exitCode = await Command.RunAsync( [ "-v", "-x", "-n0", "-z", "--remove=unlink", target ], stderr: error );

		Assert.Equal( 0, exitCode );
		Assert.Contains( "pass 1/1", error.ToString(), StringComparison.Ordinal );
		Assert.Contains( "100%", error.ToString(), StringComparison.Ordinal );
		Assert.Contains( "removed", error.ToString(), StringComparison.Ordinal );
	}

	private sealed class NonSeekableWriteStream : Stream {
		private readonly Stream inner;

		/// <summary>Initializes a non-seekable wrapper around writable storage.</summary>
		/// <param name="inner">The writable storage stream.</param>
		public NonSeekableWriteStream( Stream inner ) {
			this.inner = inner ?? throw new ArgumentNullException( nameof( inner ) );
		}

		/// <inheritdoc />
		public override bool CanRead => false;
		/// <inheritdoc />
		public override bool CanSeek => false;
		/// <inheritdoc />
		public override bool CanWrite => inner.CanWrite;
		/// <inheritdoc />
		public override long Length => throw new NotSupportedException();
		/// <inheritdoc />
		public override long Position {
			get => throw new NotSupportedException();
			set => throw new NotSupportedException();
		}

		/// <inheritdoc />
		public override void Flush() => inner.Flush();
		/// <inheritdoc />
		public override Task FlushAsync( CancellationToken cancellationToken ) => inner.FlushAsync( cancellationToken );
		/// <inheritdoc />
		public override int Read( byte[] buffer, int offset, int count ) => throw new NotSupportedException();
		/// <inheritdoc />
		public override long Seek( long offset, SeekOrigin origin ) => throw new NotSupportedException();
		/// <inheritdoc />
		public override void SetLength( long value ) => throw new NotSupportedException();
		/// <inheritdoc />
		public override void Write( byte[] buffer, int offset, int count ) => inner.Write( buffer, offset, count );
		/// <inheritdoc />
		public override ValueTask WriteAsync( ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default )
			=> inner.WriteAsync( buffer, cancellationToken );

		/// <inheritdoc />
		protected override void Dispose( bool disposing ) {
			if ( disposing ) {
				inner.Dispose();
			}
			base.Dispose( disposing );
		}

	}

	private sealed class TemporaryDirectory : IDisposable {
		/// <summary>Initializes a unique temporary directory.</summary>
		public TemporaryDirectory() {
			Path = System.IO.Path.Combine( System.IO.Path.GetTempPath(), string.Concat( "icod-shred-", Guid.NewGuid().ToString( "N" ) ) );
			Directory.CreateDirectory( Path );
		}

		/// <summary>Gets the temporary directory path.</summary>
		public string Path { get; }

		/// <summary>Builds a path below the temporary directory.</summary>
		/// <param name="name">The leaf name.</param>
		/// <returns>The full path.</returns>
		public string PathFor( string name ) => System.IO.Path.Combine( Path, name );

		/// <inheritdoc />
		public void Dispose() {
			try {
				Directory.Delete( Path, recursive: true );
			} catch ( IOException ) {
				// A failed assertion should not be hidden by temporary-directory cleanup.
			} catch ( UnauthorizedAccessException ) {
				// A failed assertion should not be hidden by temporary-directory cleanup.
			}
		}
	}
}
