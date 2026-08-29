namespace Icod.CoreUtils.Checksum.Tests;

using Icod.CoreUtils.Shared.FileSystem.Traversal;
using Xunit;

public sealed class CanonicalPathnameExpansionTests {

	[Fact]
	public async Task ExpandsAsteriskAndQuestionMarkWithinOneSegment() {
		using var directory = new TemporaryDirectory();
		var alpha = System.IO.Path.Combine(
			directory.Path,
			"alpha.txt"
		);
		var beta = System.IO.Path.Combine(
			directory.Path,
			"beta.txt"
		);
		var binary = System.IO.Path.Combine(
			directory.Path,
			"beta.bin"
		);
		File.WriteAllText( alpha, string.Empty );
		File.WriteAllText( beta, string.Empty );
		File.WriteAllText( binary, string.Empty );

		var star = await PathnameOperandExpander.ExpandAsync(
			new string[] {
				System.IO.Path.Combine(
					directory.Path,
					"*.txt"
				)
			}
		);
		var question = await PathnameOperandExpander.ExpandAsync(
			new string[] {
				System.IO.Path.Combine(
					directory.Path,
					"????.txt"
				)
			}
		);

		Assert.Equal(
			new string[] {
				alpha,
				beta
			},
			star.Paths
		);
		Assert.Equal(
			new string[] {
				beta
			},
			question.Paths
		);
	}

	[Fact]
	public async Task DoubleAsteriskMatchesZeroAndMultipleDirectories() {
		using var directory = new TemporaryDirectory();
		var root = System.IO.Path.Combine(
			directory.Path,
			"root.txt"
		);
		var firstDirectory = Directory.CreateDirectory(
			System.IO.Path.Combine(
				directory.Path,
				"one"
			)
		);
		var secondDirectory = Directory.CreateDirectory(
			System.IO.Path.Combine(
				firstDirectory.FullName,
				"two"
			)
		);
		var first = System.IO.Path.Combine(
			firstDirectory.FullName,
			"first.txt"
		);
		var deep = System.IO.Path.Combine(
			secondDirectory.FullName,
			"deep.txt"
		);
		File.WriteAllText( root, string.Empty );
		File.WriteAllText( first, string.Empty );
		File.WriteAllText( deep, string.Empty );

		var expansion = await PathnameOperandExpander.ExpandAsync(
			new string[] {
				System.IO.Path.Combine(
					directory.Path,
					"**",
					"*.txt"
				)
			}
		);

		Assert.Equal(
			3,
			expansion.Paths.Count
		);
		Assert.Contains(
			root,
			expansion.Paths
		);
		Assert.Contains(
			first,
			expansion.Paths
		);
		Assert.Contains(
			deep,
			expansion.Paths
		);
	}

	[Fact]
	public async Task WildcardsDoNotImplicitlyMatchLeadingPeriods() {
		using var directory = new TemporaryDirectory();
		var visible = System.IO.Path.Combine(
			directory.Path,
			"visible.txt"
		);
		var hidden = System.IO.Path.Combine(
			directory.Path,
			".hidden.txt"
		);
		File.WriteAllText( visible, string.Empty );
		File.WriteAllText( hidden, string.Empty );

		var expansion = await PathnameOperandExpander.ExpandAsync(
			new string[] {
				System.IO.Path.Combine(
					directory.Path,
					"*.txt"
				)
			}
		);

		Assert.Contains(
			visible,
			expansion.Paths
		);
		Assert.DoesNotContain(
			hidden,
			expansion.Paths
		);
	}

	[Fact]
	public async Task PreservesUnmatchedPatternsByDefault() {
		using var directory = new TemporaryDirectory();
		var pattern = System.IO.Path.Combine(
			directory.Path,
			"missing",
			"**",
			"*.txt"
		);

		var expansion = await PathnameOperandExpander.ExpandAsync(
			new string[] {
				pattern
			}
		);

		Assert.Equal(
			new string[] {
				pattern
			},
			expansion.Paths
		);
	}

	private sealed class TemporaryDirectory : IDisposable {

		public string Path {
			get;
		}

		public TemporaryDirectory() {
			this.Path = System.IO.Path.Combine(
				System.IO.Path.GetTempPath(),
				$"icod-canonical-glob-{Guid.NewGuid():N}"
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
