namespace Icod.CoreUtils.Shared.Tests;

using System.Text;
using Icod.CoreUtils.Shared.IO;
using Xunit;

public sealed class TextReaderStreamTests {

	[Fact]
	public async Task EncodesTextIncrementallyWithoutAddingABom() {
		using var reader = new StringReader(
			"alpha é 界"
		);
		await using var stream = new TextReaderStream(
			reader,
			characterBufferSize: 2
		);
		await using var output = new MemoryStream();

		var buffer = new byte[ 3 ];
		while ( true ) {
			var count = await stream.ReadAsync(
				buffer.AsMemory()
			);
			if ( 0 == count ) {
				break;
			}
			await output.WriteAsync(
				buffer.AsMemory(
					0,
					count
				)
			);
		}

		Assert.Equal(
			Encoding.UTF8.GetBytes(
				"alpha é 界"
			),
			output.ToArray()
		);
	}

	[Fact]
	public async Task SupportsDestinationBuffersSmallerThanEncodedCharacters() {
		using var reader = new StringReader(
			"界"
		);
		await using var stream = new TextReaderStream(
			reader,
			characterBufferSize: 1
		);
		var output = new List<byte>();
		var buffer = new byte[ 1 ];

		while ( true ) {
			var count = await stream.ReadAsync(
				buffer.AsMemory()
			);
			if ( 0 == count ) {
				break;
			}
			output.Add(
				buffer[ 0 ]
			);
		}

		Assert.Equal(
			Encoding.UTF8.GetBytes(
				"界"
			),
			output.ToArray()
		);
	}

	[Fact]
	public async Task HonorsCancellation() {
		using var cancellation = new CancellationTokenSource();
		cancellation.Cancel();
		using var reader = new StringReader(
			"alpha"
		);
		await using var stream = new TextReaderStream(
			reader
		);

		await Assert.ThrowsAnyAsync<OperationCanceledException>(
			async () => {
				_ = await stream.ReadAsync(
					new byte[ 8 ].AsMemory(),
					cancellation.Token
				);
			}
		);
	}

}
