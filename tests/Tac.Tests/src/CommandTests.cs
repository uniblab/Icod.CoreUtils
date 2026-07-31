namespace Icod.CoreUtils.Tac.Tests;

using System.Text;
using Icod.CoreUtils.Shared.Diagnostics;
using TacCommand = Icod.CoreUtils.Tac.Command;

/// <summary>Exercises GNU-compatible tac command behavior.</summary>
public sealed class CommandTests {
	/// <summary>Verifies default newline records are reversed without normalizing bytes.</summary>
	[Fact]
	public async Task ReversesNewlineRecords() {
		var result = await RunAsync( Array.Empty<string>(), new MemoryStream( "a\nb\nc"u8.ToArray(), writable: false ) );

		Assert.Equal( CommandExitCodes.Success, result.Status );
		Assert.Equal( "cb\na\n"u8.ToArray(), result.Output );
	}

	/// <summary>Verifies each file is reversed independently and operands retain command-line order.</summary>
	[Fact]
	public async Task ReversesMultipleFilesIndependently() {
		using var directory = new TemporaryDirectory();
		var first = directory.File( "first" );
		var second = directory.File( "second" );
		await File.WriteAllBytesAsync( first, "a\nb\n"u8.ToArray() );
		await File.WriteAllBytesAsync( second, "c\nd\n"u8.ToArray() );

		var result = await RunAsync( new[] { first, second } );

		Assert.Equal( CommandExitCodes.Success, result.Status );
		Assert.Equal( "b\na\nd\nc\n"u8.ToArray(), result.Output );
	}

	/// <summary>Verifies nonseekable standard input is securely spooled.</summary>
	[Fact]
	public async Task SpoolsNonseekableStandardInput() {
		await using var input = new NonSeekableReadStream( "one\ntwo\n"u8.ToArray() );

		var result = await RunAsync( Array.Empty<string>(), input );

		Assert.Equal( CommandExitCodes.Success, result.Status );
		Assert.Equal( "two\none\n"u8.ToArray(), result.Output );
	}

	/// <summary>Verifies multi-byte literal separators are retained after their records.</summary>
	[Fact]
	public async Task UsesLiteralSeparator() {
		var result = await RunAsync(
			new[] { "-s", "XX" },
			new MemoryStream( "aXXbXX"u8.ToArray(), writable: false )
		);

		Assert.Equal( CommandExitCodes.Success, result.Status );
		Assert.Equal( "bXXaXX"u8.ToArray(), result.Output );
	}

	/// <summary>Verifies before mode attaches separators to the following records.</summary>
	[Fact]
	public async Task AttachesSeparatorBeforeRecords() {
		var result = await RunAsync(
			new[] { "--before" },
			new MemoryStream( "a\nb\n"u8.ToArray(), writable: false )
		);

		Assert.Equal( CommandExitCodes.Success, result.Status );
		Assert.Equal( new byte[] { (byte)'\n', (byte)'\n', (byte)'b', (byte)'a' }, result.Output );
	}

	/// <summary>Verifies an empty separator argument selects NUL records.</summary>
	[Fact]
	public async Task UsesNulForEmptySeparator() {
		var input = new byte[] { (byte)'a', 0, (byte)'b', 0, (byte)'c' };

		var result = await RunAsync( new[] { "--separator=" }, new MemoryStream( input, writable: false ) );

		Assert.Equal( CommandExitCodes.Success, result.Status );
		Assert.Equal( new byte[] { (byte)'c', (byte)'b', 0, (byte)'a', 0 }, result.Output );
	}

	/// <summary>Verifies regular-expression separators use the shared GNU matcher.</summary>
	[Fact]
	public async Task UsesRegularExpressionSeparator() {
		var result = await RunAsync(
			new[] { "-r", "-s", "X[0-9]" },
			new MemoryStream( "aX1bX2c"u8.ToArray(), writable: false )
		);

		Assert.Equal( CommandExitCodes.Success, result.Status );
		Assert.Equal( "cbX2aX1"u8.ToArray(), result.Output );
	}

	/// <summary>Verifies GNU Emacs alternation and newline bracket behavior.</summary>
	[Fact]
	public async Task UsesGnuEmacsRegularExpressionSyntax() {
		var result = await RunAsync(
			new[] { "-r", "-s", "x\\|[^x]" },
			new MemoryStream( "a\nb"u8.ToArray(), writable: false )
		);

		Assert.Equal( CommandExitCodes.Success, result.Status );
		Assert.Equal( "b\na"u8.ToArray(), result.Output );
	}

	/// <summary>Verifies fixed separators can span the internal read-buffer boundary.</summary>
	[Fact]
	public async Task FindsSeparatorAcrossReadBufferBoundary() {
		var prefix = Enumerable.Repeat( (byte)'a', (64 * 1024) - 1 ).ToArray();
		var input = prefix.Concat( "XYZb"u8.ToArray() ).ToArray();

		var result = await RunAsync(
			new[] { "-s", "XYZ" },
			new MemoryStream( input, writable: false )
		);

		Assert.Equal( CommandExitCodes.Success, result.Status );
		Assert.Equal( new byte[] { (byte)'b' }.Concat( prefix ).Concat( "XYZ"u8.ToArray() ).ToArray(), result.Output );
	}

	/// <summary>Verifies regex searches expand backward when one record exceeds the initial window.</summary>
	[Fact]
	public async Task ExpandsRegularExpressionSearchWindow() {
		var suffix = Enumerable.Repeat( (byte)'q', (64 * 1024) + 17 ).ToArray();
		var input = "aXYZ"u8.ToArray().Concat( suffix ).ToArray();

		var result = await RunAsync(
			new[] { "-r", "-s", "XYZ" },
			new MemoryStream( input, writable: false )
		);

		Assert.Equal( CommandExitCodes.Success, result.Status );
		Assert.Equal( suffix.Concat( "aXYZ"u8.ToArray() ).ToArray(), result.Output );
	}

	/// <summary>Verifies invalid UTF-8 bytes remain authoritative output bytes.</summary>
	[Fact]
	public async Task PreservesArbitraryBytes() {
		var input = new byte[] { 0xFF, (byte)'\n', 0x80, (byte)'\n' };

		var result = await RunAsync( Array.Empty<string>(), new MemoryStream( input, writable: false ) );

		Assert.Equal( CommandExitCodes.Success, result.Status );
		Assert.Equal( new byte[] { 0x80, (byte)'\n', 0xFF, (byte)'\n' }, result.Output );
	}

	/// <summary>Verifies a failed operand does not prevent later operands from being processed.</summary>
	[Fact]
	public async Task ContinuesAfterInputError() {
		using var directory = new TemporaryDirectory();
		var missing = directory.File( "missing" );
		var valid = directory.File( "valid" );
		await File.WriteAllBytesAsync( valid, "a\nb\n"u8.ToArray() );

		var result = await RunAsync( new[] { missing, valid } );

		Assert.Equal( CommandExitCodes.Failure, result.Status );
		Assert.Equal( "b\na\n"u8.ToArray(), result.Output );
		Assert.Contains( "missing", result.Error, StringComparison.Ordinal );
	}

	/// <summary>Verifies zero-length regular-expression matches advance by one input character.</summary>
	[Fact]
	public async Task SupportsEmptyMatchingRegularExpression() {
		var result = await RunAsync(
			new[] { "-r", "-s", "x*" },
			new MemoryStream( "abc"u8.ToArray(), writable: false )
		);

		Assert.Equal( CommandExitCodes.Success, result.Status );
		Assert.Equal( "cba"u8.ToArray(), result.Output );
	}

	/// <summary>Verifies regex separators are selected from the rightmost possible start.</summary>
	[Fact]
	public async Task UsesRightmostRegularExpressionMatches() {
		var result = await RunAsync(
			new[] { "-r", "-s", ".*" },
			new MemoryStream( "abc"u8.ToArray(), writable: false )
		);

		Assert.Equal( CommandExitCodes.Success, result.Status );
		Assert.Equal( "cba"u8.ToArray(), result.Output );
	}

	/// <summary>Verifies regex mode rejects an empty separator string.</summary>
	[Fact]
	public async Task RejectsEmptyRegularExpressionSeparator() {
		var result = await RunAsync(
			new[] { "-r", "--separator=" },
			new MemoryStream( "abc"u8.ToArray(), writable: false )
		);

		Assert.NotEqual( CommandExitCodes.Success, result.Status );
		Assert.Contains( "empty", result.Error, StringComparison.OrdinalIgnoreCase );
	}

	/// <summary>Verifies help and version requests succeed.</summary>
	[Theory]
	[InlineData( "--help", "Usage: tac" )]
	[InlineData( "--version", "tac (Icod.CoreUtils)" )]
	public async Task ReportsHelpAndVersion( string option, string expected ) {
		var result = await RunAsync( new[] { option } );

		Assert.Equal( CommandExitCodes.Success, result.Status );
		Assert.Contains( expected, Encoding.UTF8.GetString( result.Output ), StringComparison.Ordinal );
	}

	/// <summary>Verifies cancellation returns the shared canceled status.</summary>
	[Fact]
	public async Task ObservesCancellation() {
		using var cancellation = new CancellationTokenSource();
		cancellation.Cancel();

		var result = await RunAsync(
			Array.Empty<string>(),
			new MemoryStream( "a\n"u8.ToArray(), writable: false ),
			cancellation.Token
		);

		Assert.Equal( CommandExitCodes.Canceled, result.Status );
	}

	private static async Task<CommandResult> RunAsync(
		string[] arguments,
		Stream? input = null,
		CancellationToken cancellationToken = default
	) {
		input ??= new MemoryStream( Array.Empty<byte>(), writable: false );
		await using var output = new MemoryStream();
		await using var error = new MemoryStream();
		using var outputText = new StringWriter();
		using var errorText = new StringWriter();
		var status = await TacCommand.RunAsync(
			arguments,
			new CommandContext(
				"tac",
				TextReader.Null,
				outputText,
				errorText,
				input,
				output,
				error,
				cancellationToken
			)
		);
		return new CommandResult( status, output.ToArray(), errorText.ToString() );
	}

	private sealed record CommandResult( int Status, byte[] Output, string Error );

	private sealed class TemporaryDirectory : IDisposable {
		public string Path { get; }

		public TemporaryDirectory() {
			this.Path = System.IO.Path.Combine(
				System.IO.Path.GetTempPath(),
				$"icod-tac-tests-{Guid.NewGuid():N}"
			);
			Directory.CreateDirectory( this.Path );
		}

		public string File( string name ) => System.IO.Path.Combine( this.Path, name );

		public void Dispose() {
			try {
				Directory.Delete( this.Path, recursive: true );
			} catch {
				// Test cleanup must not hide an assertion failure.
			}
		}
	}

	private sealed class NonSeekableReadStream : Stream {
		private readonly MemoryStream inner;

		public NonSeekableReadStream( byte[] data ) {
			this.inner = new MemoryStream( data, writable: false );
		}

		public override bool CanRead => true;
		public override bool CanSeek => false;
		public override bool CanWrite => false;
		public override long Length => throw new NotSupportedException();
		public override long Position {
			get => throw new NotSupportedException();
			set => throw new NotSupportedException();
		}
		public override void Flush() {
		}
		public override int Read( byte[] buffer, int offset, int count ) => this.inner.Read( buffer, offset, count );
		public override int Read( Span<byte> buffer ) => this.inner.Read( buffer );
		public override ValueTask<int> ReadAsync( Memory<byte> buffer, CancellationToken cancellationToken = default ) => this.inner.ReadAsync( buffer, cancellationToken );
		public override long Seek( long offset, SeekOrigin origin ) => throw new NotSupportedException();
		public override void SetLength( long value ) => throw new NotSupportedException();
		public override void Write( byte[] buffer, int offset, int count ) => throw new NotSupportedException();
		protected override void Dispose( bool disposing ) {
			if ( disposing ) {
				this.inner.Dispose();
			}
			base.Dispose( disposing );
		}
		public override ValueTask DisposeAsync() {
			this.inner.Dispose();
			GC.SuppressFinalize( this );
			return ValueTask.CompletedTask;
		}
	}
}
