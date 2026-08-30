namespace Icod.CoreUtils.Shared.Tests.FileSystem;

using Icod.CoreUtils.Shared.FileSystem.Traversal;
using Xunit;

/// <summary>Verifies CoreUtils pathname-operand compatibility behavior.</summary>
public sealed class PathnameOperandExpanderTests {

	/// <summary>Verifies non-host literal path spelling is preserved exactly.</summary>
	[Fact]
	public async Task PreservesNonHostLiteralSpelling() {
		var literal = OperatingSystem.IsWindows()
			? "/work/new/child"
			: @"C:\work\new\child"
		;

		var operands = await PathnameOperandExpander.ExpandPatternsPreservingLiteralsAsync(
			new[] { literal }
		);

		Assert.Equal(
			literal,
			Assert.Single( operands )
		);
	}

	/// <summary>Verifies actual wildcard operands still use canonical pathname expansion.</summary>
	[Fact]
	public async Task ExpandsWildcardOperands() {
		var temporary = System.IO.Path.Combine(
			System.IO.Path.GetTempPath(),
			String.Concat(
				"Icod.CoreUtils.PathnameOperandExpanderTests-",
				Guid.NewGuid().ToString( "N" )
			)
		);
		Directory.CreateDirectory(
			temporary
		);
		try {
			var first = System.IO.Path.Combine( temporary, "a.txt" );
			var second = System.IO.Path.Combine( temporary, "b.txt" );
			var ignored = System.IO.Path.Combine( temporary, "ignored.bin" );
			await File.WriteAllTextAsync( first, "a" );
			await File.WriteAllTextAsync( second, "b" );
			await File.WriteAllTextAsync( ignored, "x" );

			var operands = await PathnameOperandExpander.ExpandPatternsPreservingLiteralsAsync(
				new[] { System.IO.Path.Combine( temporary, "*.txt" ) }
			);

			Assert.Equal(
				new[] { first, second },
				operands
			);
		} finally {
			try {
				Directory.Delete(
					temporary,
					recursive: true
				);
			} catch ( IOException ) {
			} catch ( UnauthorizedAccessException ) {
			}
		}
	}

	/// <summary>Verifies a singular slot accepts exactly one wildcard match.</summary>
	[Fact]
	public async Task ExpandsSingularWildcardWithOneMatch() {
		var temporary = System.IO.Path.Combine(
			System.IO.Path.GetTempPath(),
			String.Concat(
				"Icod.CoreUtils.PathnameOperandExpanderTests-",
				Guid.NewGuid().ToString( "N" )
			)
		);
		Directory.CreateDirectory( temporary );
		try {
			var expected = System.IO.Path.Combine( temporary, "only.txt" );
			await File.WriteAllTextAsync( expected, "x" );

			var operand = await PathnameOperandExpander.ExpandSingularAsync(
				System.IO.Path.Combine( temporary, "*.txt" )
			);

			Assert.Equal( expected, operand );
		} finally {
			try {
				Directory.Delete( temporary, recursive: true );
			} catch ( IOException ) {
			} catch ( UnauthorizedAccessException ) {
			}
		}
	}

	/// <summary>Verifies an unmatched singular wildcard remains literal.</summary>
	[Fact]
	public async Task PreservesUnmatchedSingularWildcard() {
		var pattern = System.IO.Path.Combine(
			System.IO.Path.GetTempPath(),
			String.Concat(
				"Icod.CoreUtils.PathnameOperandExpanderTests-",
				Guid.NewGuid().ToString( "N" ),
				"-*.missing"
			)
		);

		var operand = await PathnameOperandExpander.ExpandSingularAsync( pattern );

		Assert.Equal( pattern, operand );
	}

	/// <summary>Verifies a singular slot rejects a wildcard with multiple matches.</summary>
	[Fact]
	public async Task RejectsSingularWildcardWithMultipleMatches() {
		var temporary = System.IO.Path.Combine(
			System.IO.Path.GetTempPath(),
			String.Concat(
				"Icod.CoreUtils.PathnameOperandExpanderTests-",
				Guid.NewGuid().ToString( "N" )
			)
		);
		Directory.CreateDirectory( temporary );
		try {
			await File.WriteAllTextAsync(
				System.IO.Path.Combine( temporary, "a.txt" ),
				"a"
			);
			await File.WriteAllTextAsync(
				System.IO.Path.Combine( temporary, "b.txt" ),
				"b"
			);

			var exception = await Assert.ThrowsAsync<IOException>(
				() => PathnameOperandExpander.ExpandSingularAsync(
					System.IO.Path.Combine( temporary, "*.txt" )
				)
			);

			Assert.Contains(
				"matched 2 pathnames",
				exception.Message,
				StringComparison.Ordinal
			);
		} finally {
			try {
				Directory.Delete( temporary, recursive: true );
			} catch ( IOException ) {
			} catch ( UnauthorizedAccessException ) {
			}
		}
	}

}
