namespace Icod.CoreUtils.Checksum.Tests;

using System.Text;
using B2Command = Icod.CoreUtils.B2Sum.Command;
using Md5Command = Icod.CoreUtils.MD5Sum.Command;
using Sha1Command = Icod.CoreUtils.Sha1Sum.Command;
using Sha224Command = Icod.CoreUtils.Sha224Sum.Command;
using Sha256Command = Icod.CoreUtils.Sha256Sum.Command;
using Sha384Command = Icod.CoreUtils.Sha384Sum.Command;
using Sha512Command = Icod.CoreUtils.Sha512Sum.Command;
using Xunit;

public sealed class DigestCommandTests {

	public static IEnumerable<object[]> DigestCommands {
		get {
			yield return new object[] {
				"b2sum",
				"ba80a53f981c4d0d6a2797b69f12f6e94c212f14685ac4b74b12bb6fdbffa2d17d87c5392aab792dc252d5de4533cc9518d38aa8dbf1925ab92386edd4009923"
			};
			yield return new object[] {
				"md5sum",
				"900150983cd24fb0d6963f7d28e17f72"
			};
			yield return new object[] {
				"sha1sum",
				"a9993e364706816aba3e25717850c26c9cd0d89d"
			};
			yield return new object[] {
				"sha224sum",
				"23097d223405d8228642a477bda255b32aadbce4bda0b3f7e36c9da7"
			};
			yield return new object[] {
				"sha256sum",
				"ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad"
			};
			yield return new object[] {
				"sha384sum",
				"cb00753f45a35e8bb5a03d699ac65007272c32ab0eded1631a8b605a43ff5bed8086072ba1e7cc2358baeca134c825a7"
			};
			yield return new object[] {
				"sha512sum",
				"ddaf35a193617abacc417349ae20413112e6fa4e89a97ea20a9eeee64b55d39a2192992a274fc1a836ba3c23a3feebbd454d4423643ce80e2a9ac94fa54ca49f"
			};
		}
	}

	[Theory]
	[MemberData( nameof( DigestCommands ) )]
	public async Task StandaloneCommandsMatchAbcVectors(
		string program,
		string expected
	) {
		var result = await RunDigestAsync(
			program,
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
			$"{expected}  -\n",
			CommandTestHelper.DecodeOutput(
				result
			)
		);
	}

	[Fact]
	public async Task B2sumLengthControlsDigestSizeAndVerification() {
		var computed = await CommandTestHelper.RunAsync(
			B2Command.RunAsync,
			new string[] {
				"-l",
				"256"
			},
			Encoding.ASCII.GetBytes(
				"abc"
			)
		);
		Assert.StartsWith(
			"bddd813c634239723171ef3fee98579b94964e3bb1cb3e427262c8c068d52319",
			CommandTestHelper.DecodeOutput(
				computed
			)
		);

		var file = CreatePath(
			"data.bin"
		);
		var manifest = CreatePath(
			"manifest.txt"
		);
		await File.WriteAllTextAsync(
			file,
			"abc",
			new UTF8Encoding(
				encoderShouldEmitUTF8Identifier: false
			)
		);
		await File.WriteAllTextAsync(
			manifest,
			$"bddd813c634239723171ef3fee98579b94964e3bb1cb3e427262c8c068d52319  {file}\n"
		);
		try {
			var verified = await CommandTestHelper.RunAsync(
				B2Command.RunAsync,
				new string[] {
					"-c",
					manifest
				},
				Array.Empty<byte>()
			);
			Assert.Equal(
				0,
				verified.ExitCode
			);
			Assert.Contains(
				$"{file}: OK",
				CommandTestHelper.DecodeOutput(
					verified
				)
			);
		} finally {
			File.Delete( file );
			File.Delete( manifest );
		}
	}

	[Fact]
	public async Task ComputesAllFilesMatchedByRecursiveGlob() {
		var directory = CreateDirectory();
		var nested = Directory.CreateDirectory(
			System.IO.Path.Combine(
				directory,
				"nested"
			)
		);
		var rootFile = System.IO.Path.Combine(
			directory,
			"root.txt"
		);
		var deepFile = System.IO.Path.Combine(
			nested.FullName,
			"deep.txt"
		);
		await File.WriteAllTextAsync( rootFile, "root" );
		await File.WriteAllTextAsync( deepFile, "deep" );
		try {
			var pattern = System.IO.Path.Combine(
				directory,
				"**",
				"*.txt"
			);
			var result = await CommandTestHelper.RunAsync(
				Sha256Command.RunAsync,
				new string[] {
					pattern
				},
				Array.Empty<byte>()
			);
			var text = CommandTestHelper.DecodeOutput(
				result
			);
			Assert.Equal( 0, result.ExitCode );
			Assert.Contains(
				EscapeFileNameForDigestOutput(
					rootFile
				),
				text
			);
			Assert.Contains(
				EscapeFileNameForDigestOutput(
					deepFile
				),
				text
			);
		} finally {
			Directory.Delete(
				directory,
				recursive: true
			);
		}
	}

	[Fact]
	public async Task CheckReportsMismatchMissingAndMalformedRecords() {
		var file = CreatePath(
			"file.txt"
		);
		var manifest = CreatePath(
			"manifest.txt"
		);
		await File.WriteAllTextAsync(
			file,
			"abc"
		);
		await File.WriteAllTextAsync(
			manifest,
			string.Concat(
				"00000000000000000000000000000000  ",
				file,
				"\nnot a checksum\n"
			)
		);
		try {
			var result = await CommandTestHelper.RunAsync(
				Md5Command.RunAsync,
				new string[] {
					"-c",
					"--warn",
					"--strict",
					manifest
				},
				Array.Empty<byte>()
			);
			Assert.Equal( 1, result.ExitCode );
			Assert.Contains(
				$"{file}: FAILED",
				CommandTestHelper.DecodeOutput(
					result
				)
			);
			Assert.Contains(
				"improperly formatted",
				result.Error
			);
		} finally {
			File.Delete( file );
			File.Delete( manifest );
		}
	}

	[Fact]
	public async Task TagZeroQuietAndStatusOptionsAreHonored() {
		var tagged = await CommandTestHelper.RunAsync(
			Md5Command.RunAsync,
			new string[] {
				"--tag"
			},
			Encoding.ASCII.GetBytes(
				"abc"
			)
		);
		Assert.Equal(
			"MD5 (-) = 900150983cd24fb0d6963f7d28e17f72\n",
			CommandTestHelper.DecodeOutput(
				tagged
			)
		);

		var zero = await CommandTestHelper.RunAsync(
			Md5Command.RunAsync,
			new string[] {
				"-z"
			},
			Encoding.ASCII.GetBytes(
				"abc"
			)
		);
		Assert.Equal(
			0,
			zero.OutputBytes[ ^1 ]
		);

		var file = CreatePath( "quiet.txt" );
		var manifest = CreatePath( "quiet.manifest" );
		await File.WriteAllTextAsync( file, "abc" );
		await File.WriteAllTextAsync(
			manifest,
			$"900150983cd24fb0d6963f7d28e17f72  {file}\n"
		);
		try {
			var quiet = await CommandTestHelper.RunAsync(
				Md5Command.RunAsync,
				new string[] {
					"-c",
					"--quiet",
					manifest
				},
				Array.Empty<byte>()
			);
			var status = await CommandTestHelper.RunAsync(
				Md5Command.RunAsync,
				new string[] {
					"-c",
					"--status",
					manifest
				},
				Array.Empty<byte>()
			);
			Assert.Equal( 0, quiet.ExitCode );
			Assert.Equal( string.Empty, CommandTestHelper.DecodeOutput( quiet ) );
			Assert.Equal( 0, status.ExitCode );
			Assert.Equal( string.Empty, CommandTestHelper.DecodeOutput( status ) );
			Assert.Equal( string.Empty, status.Error );
		} finally {
			File.Delete( file );
			File.Delete( manifest );
		}
	}

	[Fact]
	public async Task CancellationReturnsConventionalExitCode() {
		using var cancellation = new CancellationTokenSource();
		cancellation.Cancel();
		var result = await CommandTestHelper.RunAsync(
			Sha512Command.RunAsync,
			Array.Empty<string>(),
			Encoding.ASCII.GetBytes(
				"abc"
			),
			cancellation.Token
		);
		Assert.Equal(
			130,
			result.ExitCode
		);
	}

	private static Task<CommandResult> RunDigestAsync(
		string program,
		string[] args,
		byte[] input
	) {
		return program switch {
			"b2sum" => CommandTestHelper.RunAsync( B2Command.RunAsync, args, input ),
			"md5sum" => CommandTestHelper.RunAsync( Md5Command.RunAsync, args, input ),
			"sha1sum" => CommandTestHelper.RunAsync( Sha1Command.RunAsync, args, input ),
			"sha224sum" => CommandTestHelper.RunAsync( Sha224Command.RunAsync, args, input ),
			"sha256sum" => CommandTestHelper.RunAsync( Sha256Command.RunAsync, args, input ),
			"sha384sum" => CommandTestHelper.RunAsync( Sha384Command.RunAsync, args, input ),
			"sha512sum" => CommandTestHelper.RunAsync( Sha512Command.RunAsync, args, input ),
			_ => throw new ArgumentOutOfRangeException(
				nameof( program )
			)
		};
	}

	private static string EscapeFileNameForDigestOutput(
		string fileName
	) {
		return fileName.Replace(
			"\\",
			"\\\\",
			StringComparison.Ordinal
		);
	}

	private static string CreateDirectory() {
		var path = System.IO.Path.Combine(
			System.IO.Path.GetTempPath(),
			$"icod-digest-{Guid.NewGuid():N}"
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
			$"icod-digest-{Guid.NewGuid():N}-{suffix}"
		);
	}

}
