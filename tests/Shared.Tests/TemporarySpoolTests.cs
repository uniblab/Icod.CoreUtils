namespace Icod.CoreUtils.Shared.Tests;

using System.Text;
using Icod.CoreUtils.Shared.IO;
using Xunit;

public sealed class TemporarySpoolTests {

	[Fact]
	public async Task SpoolRewindsAndDeletesBackingFile() {
		string path;
		await using (
			var spool = TemporarySpool.Create()
		) {
			path = spool.Path;
			var bytes = Encoding.UTF8.GetBytes(
				"payload"
			);
			await spool.Stream.WriteAsync(
				bytes
			);
			await spool.RewindAsync();
			var output = new byte[ bytes.Length ];
			var read = await spool.Stream.ReadAsync(
				output
			);

			Assert.Equal( bytes.Length, read );
			Assert.Equal( bytes, output );
		}

		Assert.False( File.Exists( path ) );
	}

}
