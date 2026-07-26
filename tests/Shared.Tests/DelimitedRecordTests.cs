namespace Icod.CoreUtils.Shared.Tests;

using Icod.CoreUtils.Shared.IO;
using Xunit;

public sealed class DelimitedRecordTests {

	[Fact]
	public async Task ReadsLfAndCrLfRecords() {
		var reader = new DelimitedRecordReader(
			new StringReader(
				"alpha\r\nbeta\ngamma"
			)
		);

		Assert.Equal( "alpha", await reader.ReadAsync() );
		Assert.Equal( "beta", await reader.ReadAsync() );
		Assert.Equal( "gamma", await reader.ReadAsync() );
		Assert.Null( await reader.ReadAsync() );
	}

	[Fact]
	public async Task ReadsNulDelimitedEmptyRecords() {
		var reader = new DelimitedRecordReader(
			new StringReader(
				"alpha\0\0beta\0"
			),
			'\0'
		);

		Assert.Equal( "alpha", await reader.ReadAsync() );
		Assert.Equal( string.Empty, await reader.ReadAsync() );
		Assert.Equal( "beta", await reader.ReadAsync() );
		Assert.Null( await reader.ReadAsync() );
	}

	[Fact]
	public async Task ReadsRecordsLargerThanTheBuffer() {
		var value = new string(
			'x',
			10000
		);
		var reader = new DelimitedRecordReader(
			new StringReader(
				string.Concat( value, "\n" )
			),
			bufferSize: 17
		);

		Assert.Equal( value, await reader.ReadAsync() );
		Assert.Null( await reader.ReadAsync() );
	}

	[Fact]
	public async Task EmptyInputHasNoRecords() {
		var reader = new DelimitedRecordReader(
			new StringReader(
				string.Empty
			)
		);

		Assert.Null( await reader.ReadAsync() );
	}

	[Fact]
	public async Task WriterUsesConfiguredSeparator() {
		var output = new StringWriter();
		var writer = new DelimitedRecordWriter(
			output,
			'\0'
		);

		await writer.WriteAsync( "alpha" );
		await writer.WriteAsync( string.Empty );
		await writer.FlushAsync();

		Assert.Equal( "alpha\0\0", output.ToString() );
	}

	[Fact]
	public async Task CancellationIsObserved() {
		using var cancellation = new CancellationTokenSource();
		cancellation.Cancel();
		var reader = new DelimitedRecordReader(
			new StringReader(
				"alpha"
			)
		);

		await Assert.ThrowsAnyAsync<OperationCanceledException>(
			async () => {
				_ = await reader.ReadAsync(
					cancellation.Token
				);
			}
		);
	}

}
