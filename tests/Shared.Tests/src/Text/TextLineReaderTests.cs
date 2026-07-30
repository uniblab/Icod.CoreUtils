namespace Icod.CoreUtils.Shared.Tests.Text;

using System.Text;
using Icod.CoreUtils.Shared.Text;
using Xunit;

/// <summary>Tests byte-preserving logical-line iteration.</summary>
public sealed class TextLineReaderTests {
	/// <summary>Verifies empty, terminated, and unterminated logical lines.</summary>
	[Fact]
	public void ReadsLogicalLinesWithoutNormalizingTerminators() {
		using var input = new MemoryStream( "a\r\n\nlast"u8.ToArray(), writable: false );
		var reader = new TextLineReader( new TextUnitReader( input ) );
		var first = reader.Read();
		var second = reader.Read();
		var third = reader.Read();
		Assert.NotNull( first );
		Assert.Equal( "a\r\n"u8.ToArray(), first.ToByteArray() );
		Assert.NotNull( second );
		Assert.True( second.IsEmpty );
		Assert.True( second.HasLineFeed );
		Assert.NotNull( third );
		Assert.Equal( "last"u8.ToArray(), third.ToByteArray() );
		Assert.False( third.HasLineFeed );
		Assert.Null( reader.Read() );
	}

	/// <summary>Verifies exact preservation of Unicode and malformed bytes.</summary>
	/// <returns>A task that represents the asynchronous test.</returns>
	[Fact]
	public async Task PreservesUnicodeAndMalformedBytesAsynchronously() {
		var source = Encoding.UTF8.GetBytes( "界" ).Concat( new byte[] { 0xFF, (byte)'\n' } ).ToArray();
		using var input = new MemoryStream( source, writable: false );
		var reader = new TextLineReader(
			new TextUnitReader( input, TextDecodingMode.Utf8, InvalidEncodingPolicy.PreserveBytes, 1 )
		);
		var line = await reader.ReadAsync();
		Assert.NotNull( line );
		Assert.Equal( source, line.ToByteArray() );
		Assert.Contains( "界", line.ToDecodedString() );
	}

	/// <summary>Verifies that the reader leaves its caller-owned stream open.</summary>
	/// <returns>A task that represents the asynchronous test.</returns>
	[Fact]
	public async Task LeavesSourceStreamOpen() {
		using var input = new MemoryStream( "x\n"u8.ToArray() );
		var reader = new TextLineReader( new TextUnitReader( input ) );
		_ = await reader.ReadAsync();
		Assert.True( input.CanRead );
	}

	/// <summary>Verifies cancellation before an asynchronous read.</summary>
	/// <returns>A task that represents the asynchronous test.</returns>
	[Fact]
	public async Task HonorsCancellation() {
		using var input = new MemoryStream( "x"u8.ToArray() );
		var reader = new TextLineReader( new TextUnitReader( input ) );
		using var source = new CancellationTokenSource();
		source.Cancel();
		await Assert.ThrowsAsync<OperationCanceledException>(
			async () => await reader.ReadAsync( source.Token )
		);
	}
}
