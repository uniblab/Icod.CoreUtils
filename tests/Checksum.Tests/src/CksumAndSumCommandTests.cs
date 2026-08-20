namespace Icod.CoreUtils.Checksum.Tests;

using System.Text;
using CksumCommandWrapper = Icod.CoreUtils.Cksum.Command;
using SumCommandWrapper = Icod.CoreUtils.Sum.Command;
using Xunit;

public sealed class CksumAndSumCommandTests {

	[Fact]
	public async Task CksumDefaultMatchesPosixVector() {
		var result = await CommandTestHelper.RunAsync(
			CksumCommandWrapper.RunAsync,
			Array.Empty<string>(),
			Encoding.ASCII.GetBytes(
				"abc"
			)
		);
		Assert.Equal(
			0,
			result.ExitCode
		);
		Assert.Equal(
			"1219131554 3\n",
			CommandTestHelper.DecodeOutput(
				result
			)
		);
	}

	[Theory]
	[InlineData( "crc32b", "CRC32B (-) = 352441c2\n" )]
	[InlineData( "md5", "MD5 (-) = 900150983cd24fb0d6963f7d28e17f72\n" )]
	[InlineData( "sha1", "SHA1 (-) = a9993e364706816aba3e25717850c26c9cd0d89d\n" )]
	[InlineData( "sm3", "SM3 (-) = 66c7f0f462eeedd9d1f2d46bdc10e4e24167c4875cf2f7a2297da02b8f4ba8e0\n" )]
	public async Task CksumSelectsNamedDigestAlgorithms(
		string algorithm,
		string expected
	) {
		var result = await CommandTestHelper.RunAsync(
			CksumCommandWrapper.RunAsync,
			new string[] {
				"-a",
				algorithm
			},
			Encoding.ASCII.GetBytes(
				"abc"
			)
		);
		Assert.Equal(
			0,
			result.ExitCode
		);
		Assert.Equal(
			expected,
			CommandTestHelper.DecodeOutput(
				result
			)
		);
	}

	[Fact]
	public async Task CksumSupportsSha2Sha3AndBlake2Lengths() {
		var sha2 = await CommandTestHelper.RunAsync(
			CksumCommandWrapper.RunAsync,
			new string[] {
				"-a",
				"sha2",
				"-l",
				"224"
			},
			Encoding.ASCII.GetBytes(
				"abc"
			)
		);
		var sha3 = await CommandTestHelper.RunAsync(
			CksumCommandWrapper.RunAsync,
			new string[] {
				"-a",
				"sha3",
				"-l",
				"256"
			},
			Encoding.ASCII.GetBytes(
				"abc"
			)
		);
		var blake = await CommandTestHelper.RunAsync(
			CksumCommandWrapper.RunAsync,
			new string[] {
				"-a",
				"blake2b",
				"-l",
				"256"
			},
			Encoding.ASCII.GetBytes(
				"abc"
			)
		);

		Assert.Equal(
			"SHA224 (-) = 23097d223405d8228642a477bda255b32aadbce4bda0b3f7e36c9da7\n",
			CommandTestHelper.DecodeOutput(
				sha2
			)
		);
		Assert.Equal(
			"SHA3-256 (-) = 3a985da74fe225b2045c172d6bd390bd855f086e3e9d525b46bfe24511431532\n",
			CommandTestHelper.DecodeOutput(
				sha3
			)
		);
		Assert.Equal(
			"BLAKE2b (-) = bddd813c634239723171ef3fee98579b94964e3bb1cb3e427262c8c068d52319\n",
			CommandTestHelper.DecodeOutput(
				blake
			)
		);
	}

	[Fact]
	public async Task CksumSupportsBase64UntaggedRawAndZeroOutput() {
		var base64 = await CommandTestHelper.RunAsync(
			CksumCommandWrapper.RunAsync,
			new string[] {
				"-a",
				"md5",
				"--base64"
			},
			Encoding.ASCII.GetBytes(
				"abc"
			)
		);
		var untagged = await CommandTestHelper.RunAsync(
			CksumCommandWrapper.RunAsync,
			new string[] {
				"-a",
				"md5",
				"--untagged"
			},
			Encoding.ASCII.GetBytes(
				"abc"
			)
		);
		var raw = await CommandTestHelper.RunAsync(
			CksumCommandWrapper.RunAsync,
			new string[] {
				"-a",
				"md5",
				"--raw"
			},
			Encoding.ASCII.GetBytes(
				"abc"
			)
		);
		var zero = await CommandTestHelper.RunAsync(
			CksumCommandWrapper.RunAsync,
			new string[] {
				"-a",
				"md5",
				"-z"
			},
			Encoding.ASCII.GetBytes(
				"abc"
			)
		);

		Assert.Equal(
			"MD5 (-) = kAFQmDzST7DWlj99KOF/cg==\n",
			CommandTestHelper.DecodeOutput(
				base64
			)
		);
		Assert.Equal(
			"900150983cd24fb0d6963f7d28e17f72  -\n",
			CommandTestHelper.DecodeOutput(
				untagged
			)
		);
		Assert.Equal(
			Convert.FromHexString(
				"900150983cd24fb0d6963f7d28e17f72"
			),
			raw.OutputBytes
		);
		Assert.Equal(
			0,
			zero.OutputBytes[ ^1 ]
		);
	}

	[Fact]
	public async Task CksumVerifiesTraditionalCrcRecords() {
		var data = CreatePath(
			"crc-data.txt"
		);
		var manifest = CreatePath(
			"crc-manifest.txt"
		);
		await File.WriteAllTextAsync(
			data,
			"abc"
		);
		await File.WriteAllTextAsync(
			manifest,
			$"1219131554 3 {data}\n"
		);
		try {
			var result = await CommandTestHelper.RunAsync(
				CksumCommandWrapper.RunAsync,
				new string[] {
					"-c",
					manifest
				},
				Array.Empty<byte>()
			);
			Assert.Equal(
				0,
				result.ExitCode
			);
			Assert.Contains(
				$"{data}: OK",
				CommandTestHelper.DecodeOutput(
					result
				)
			);
		} finally {
			File.Delete( data );
			File.Delete( manifest );
		}
	}

	[Fact]
	public async Task CksumVerifiesTaggedRecords() {
		var data = CreatePath(
			"data.txt"
		);
		var manifest = CreatePath(
			"manifest.txt"
		);
		await File.WriteAllTextAsync(
			data,
			"abc"
		);
		await File.WriteAllTextAsync(
			manifest,
			$"MD5 ({data}) = 900150983cd24fb0d6963f7d28e17f72\n"
		);
		try {
			var result = await CommandTestHelper.RunAsync(
				CksumCommandWrapper.RunAsync,
				new string[] {
					"-c",
					manifest
				},
				Array.Empty<byte>()
			);
			Assert.Equal(
				0,
				result.ExitCode
			);
			Assert.Contains(
				$"{data}: OK",
				CommandTestHelper.DecodeOutput(
					result
				)
			);
		} finally {
			File.Delete( data );
			File.Delete( manifest );
		}
	}

	[Fact]
	public async Task CksumDebugAndInvalidCombinationsAreDiagnosed() {
		var debug = await CommandTestHelper.RunAsync(
			CksumCommandWrapper.RunAsync,
			new string[] {
				"--debug",
				"-a",
				"sha3"
			},
			Encoding.ASCII.GetBytes(
				"abc"
			)
		);
		var incompatible = await CommandTestHelper.RunAsync(
			CksumCommandWrapper.RunAsync,
			new string[] {
				"--raw",
				"--base64"
			},
			Array.Empty<byte>()
		);
		Assert.Equal( 0, debug.ExitCode );
		Assert.Contains(
			"managed streaming implementation",
			debug.Error
		);
		Assert.Equal( 1, incompatible.ExitCode );
		Assert.Contains(
			"mutually exclusive",
			incompatible.Error
		);
	}

	[Fact]
	public async Task SumMatchesBsdAndSystemVNativeVectors() {
		var bsd = await CommandTestHelper.RunAsync(
			SumCommandWrapper.RunAsync,
			Array.Empty<string>(),
			Encoding.ASCII.GetBytes(
				"abc"
			)
		);
		var sysv = await CommandTestHelper.RunAsync(
			SumCommandWrapper.RunAsync,
			new string[] {
				"-s"
			},
			Encoding.ASCII.GetBytes(
				"abc"
			)
		);
		Assert.Equal(
			System.String.Concat(
				"16556     1",
				Environment.NewLine
			),
			CommandTestHelper.DecodeOutput(
				bsd
			)
		);
		Assert.Equal(
			System.String.Concat(
				"294 1",
				Environment.NewLine
			),
			CommandTestHelper.DecodeOutput(
				sysv
			)
		);
	}

	[Fact]
	public async Task SumExpandsWildcardsAndReportsFileNames() {
		var directory = CreateDirectory();
		var first = System.IO.Path.Combine(
			directory,
			"a.bin"
		);
		var second = System.IO.Path.Combine(
			directory,
			"b.bin"
		);
		await File.WriteAllTextAsync( first, "a" );
		await File.WriteAllTextAsync( second, "b" );
		try {
			var result = await CommandTestHelper.RunAsync(
				SumCommandWrapper.RunAsync,
				new string[] {
					System.IO.Path.Combine(
						directory,
						"?.bin"
					)
				},
				Array.Empty<byte>()
			);
			var text = CommandTestHelper.DecodeOutput(
				result
			);
			Assert.Equal( 0, result.ExitCode );
			Assert.Contains( first, text );
			Assert.Contains( second, text );
		} finally {
			Directory.Delete(
				directory,
				recursive: true
			);
		}
	}

	private static string CreateDirectory() {
		var path = System.IO.Path.Combine(
			System.IO.Path.GetTempPath(),
			$"icod-cksum-{Guid.NewGuid():N}"
		);
		Directory.CreateDirectory(
			path
		);
		return path;
	}

	private static string CreatePath(
		string suffix
	) {
		return System.IO.Path.Combine(
			System.IO.Path.GetTempPath(),
			$"icod-cksum-{Guid.NewGuid():N}-{suffix}"
		);
	}

}
