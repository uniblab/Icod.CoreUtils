namespace Icod.CoreUtils.Ptx.Tests;

using Icod.CommandFramework.Diagnostics;
using Xunit;

/// <summary>Protects the byte-coordinate semantics used by custom <c>ptx</c> regular expressions.</summary>
public sealed class PreparedByteInputTests {
	/// <summary>Verifies a UTF-8 continuation byte remains independently matchable in byte mode.</summary>
	[Fact]
	public async Task WordPatternMatchesUtf8ContinuationByteIndependently() {
		var result = await RunAsync(
			[ "-T", "-W", @"\xA9" ],
			[ 0xC3, 0xA9, (byte)' ', (byte)'z', (byte)'\n' ]
		);

		Assert.Equal( 0, result.Status );
		Assert.True(
			ContainsSequence(
				result.Output,
				[ (byte)'{', 0xA9, (byte)'}' ]
			)
		);
		Assert.Empty( result.Error );
	}

	/// <summary>Verifies malformed high bytes remain matchable without UTF-8 normalization.</summary>
	[Fact]
	public async Task WordPatternMatchesMalformedHighByteExactly() {
		var result = await RunAsync(
			[ "-T", "-W", @"\xFF" ],
			[ 0xFF, (byte)' ', (byte)'x', (byte)'\n' ]
		);

		Assert.Equal( 0, result.Status );
		Assert.True(
			ContainsSequence(
				result.Output,
				[ (byte)'{', 0xFF, (byte)'}' ]
			)
		);
		Assert.Empty( result.Error );
	}

	private static async Task<RunResult> RunAsync(
		string[] args,
		byte[] input
	) {
		using var standardInput = new StringReader( string.Empty );
		using var standardOutput = new StringWriter();
		using var standardError = new StringWriter();
		using var inputStream = new MemoryStream( input, writable: false );
		using var outputStream = new MemoryStream();
		using var errorStream = new MemoryStream();
		var context = new CommandContext(
			"ptx",
			standardInput,
			standardOutput,
			standardError,
			inputStream,
			outputStream,
			errorStream
		);
		var status = await Command.RunAsync( args, context ).ConfigureAwait( false );
		return new RunResult(
			status,
			outputStream.ToArray(),
			errorStream.ToArray()
		);
	}

	private static bool ContainsSequence(
		ReadOnlySpan<byte> source,
		ReadOnlySpan<byte> value
	) {
		if ( value.IsEmpty ) {
			return true;
		}
		for ( var index = 0; index <= source.Length - value.Length; index++ ) {
			if ( source.Slice( index, value.Length ).SequenceEqual( value ) ) {
				return true;
			}
		}
		return false;
	}

	private sealed record RunResult(
		int Status,
		byte[] Output,
		byte[] Error
	);
}
