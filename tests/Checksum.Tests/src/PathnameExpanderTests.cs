namespace Icod.CoreUtils.Checksum.Tests;

using Icod.CoreUtils.Shared.IO;
using Xunit;

public sealed class PathnameExpanderTests {

	[Fact]
	public void ExpandsAsteriskAndQuestionMarkWithinOneSegment() {
		using var directory = new TemporaryDirectory();
		File.WriteAllText(
			Path.Combine(
				directory.Path,
				"alpha.txt"
			),
			string.Empty
		);
		File.WriteAllText(
			Path.Combine(
				directory.Path,
				"beta.txt"
			),
			string.Empty
		);
		File.WriteAllText(
			Path.Combine(
				directory.Path,
				"beta.bin"
			),
			string.Empty
		);

		var star = PathnameExpander.Expand(
			new string[] {
				"*.txt"
			},
			new PathnameExpansionOptions {
				BaseDirectory = directory.Path
			}
		);
		var question = PathnameExpander.Expand(
			new string[] {
				"????.txt"
			},
			new PathnameExpansionOptions {
				BaseDirectory = directory.Path
			}
		);

		Assert.Equal(
			new string[] {
				"alpha.txt",
				"beta.txt"
			},
			star
		);
		Assert.Equal(
			new string[] {
				"beta.txt"
			},
			question
		);
	}

	[Fact]
	public void DoubleAsteriskMatchesZeroAndMultipleDirectories() {
		using var directory = new TemporaryDirectory();
		File.WriteAllText(
			Path.Combine(
				directory.Path,
				"root.txt"
			),
			string.Empty
		);
		var first = Directory.CreateDirectory(
			Path.Combine(
				directory.Path,
				"one"
			)
		);
		var second = Directory.CreateDirectory(
			Path.Combine(
				first.FullName,
				"two"
			)
		);
		File.WriteAllText(
			Path.Combine(
				first.FullName,
				"first.txt"
			),
			string.Empty
		);
		File.WriteAllText(
			Path.Combine(
				second.FullName,
				"deep.txt"
			),
			string.Empty
		);

		var matches = PathnameExpander.Expand(
			new string[] {
				Path.Combine(
					"**",
					"*.txt"
				)
			},
			new PathnameExpansionOptions {
				BaseDirectory = directory.Path
			}
		);

		Assert.Equal(
			new string[] {
				Path.Combine( "one", "first.txt" ),
				Path.Combine( "one", "two", "deep.txt" ),
				"root.txt"
			}.OrderBy(
				value => value,
				OperatingSystem.IsWindows()
					? StringComparer.OrdinalIgnoreCase
					: StringComparer.Ordinal
			),
			matches
		);
	}

	[Fact]
	public void TerminalDoubleAsteriskReturnsFilesRecursively() {
		using var directory = new TemporaryDirectory();
		File.WriteAllText(
			Path.Combine(
				directory.Path,
				"root.bin"
			),
			string.Empty
		);
		var nested = Directory.CreateDirectory(
			Path.Combine(
				directory.Path,
				"nested"
			)
		);
		File.WriteAllText(
			Path.Combine(
				nested.FullName,
				"deep.bin"
			),
			string.Empty
		);

		var matches = PathnameExpander.Expand(
			new string[] {
				"**"
			},
			new PathnameExpansionOptions {
				BaseDirectory = directory.Path
			}
		);

		Assert.Equal(
			new string[] {
				Path.Combine( "nested", "deep.bin" ),
				"root.bin"
			},
			matches
		);
	}

	[Fact]
	public void PreservesUnmatchedPatternsByDefault() {
		using var directory = new TemporaryDirectory();
		var pattern = Path.Combine(
			"missing",
			"**",
			"*.txt"
		);
		Assert.Equal(
			new string[] {
				pattern
			},
			PathnameExpander.Expand(
				new string[] {
					pattern
				},
				new PathnameExpansionOptions {
					BaseDirectory = directory.Path
				}
			)
		);
	}

	[Fact]
	public void KeepsOperandOrderAndSortsMatchesPerOperand() {
		using var directory = new TemporaryDirectory();
		File.WriteAllText( Path.Combine( directory.Path, "b.txt" ), string.Empty );
		File.WriteAllText( Path.Combine( directory.Path, "a.txt" ), string.Empty );
		File.WriteAllText( Path.Combine( directory.Path, "z.bin" ), string.Empty );

		var matches = PathnameExpander.Expand(
			new string[] {
				"*.bin",
				"*.txt"
			},
			new PathnameExpansionOptions {
				BaseDirectory = directory.Path
			}
		);

		Assert.Equal(
			new string[] {
				"z.bin",
				"a.txt",
				"b.txt"
			},
			matches
		);
	}

	[Fact]
	public void DoesNotTraverseDirectorySymlinksByDefault() {
		using var directory = new TemporaryDirectory();
		var target = Directory.CreateDirectory(
			Path.Combine(
				directory.Path,
				"target"
			)
		);
		File.WriteAllText(
			Path.Combine(
				target.FullName,
				"inside.txt"
			),
			string.Empty
		);
		var link = Path.Combine(
			directory.Path,
			"link"
		);
		try {
			Directory.CreateSymbolicLink(
				link,
				target.FullName
			);
		} catch (
			Exception ex
		) when (
			ex is IOException
				or UnauthorizedAccessException
				or PlatformNotSupportedException
		) {
			return;
		}

		var matches = PathnameExpander.Expand(
			new string[] {
				Path.Combine(
					"**",
					"*.txt"
				)
			},
			new PathnameExpansionOptions {
				BaseDirectory = directory.Path
			}
		);

		Assert.Contains(
			Path.Combine( "target", "inside.txt" ),
			matches
		);
		Assert.DoesNotContain(
			Path.Combine( "link", "inside.txt" ),
			matches
		);
	}

	private sealed class TemporaryDirectory : IDisposable {

		public string Path {
			get;
		}

		public TemporaryDirectory() {
			this.Path = System.IO.Path.Combine(
				System.IO.Path.GetTempPath(),
				$"icod-glob-{Guid.NewGuid():N}"
			);
			Directory.CreateDirectory(
				this.Path
			);
		}

		public void Dispose() {
			try {
				Directory.Delete(
					this.Path,
					recursive: true
				);
			} catch {
			}
		}

	}

}
