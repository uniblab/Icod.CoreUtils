namespace Icod.CoreUtils.RealPath.Tests;

using Icod.CoreUtils.PathCommandTests;
using Icod.CoreUtils.Shared.Diagnostics;
using Icod.Path;
using Xunit;

/// <summary>Exercises GNU-compatible <c>realpath</c> command policy over deterministic paths.</summary>
public sealed class CommandTests {
	/// <summary>Verifies physical mode resolves a link before a following parent component.</summary>
	[Fact]
	public async Task PhysicalModeResolvesLinkBeforeParent() {
		var provider = CreateProvider()
			.AddDirectory( "/actual" )
			.AddDirectory( "/actual/dir" )
			.AddFile( "/actual/file" )
			.AddLink( "/work/link", "/actual/dir" );
		var result = await RunAsync( new[] { "-P", "link/../file" }, provider );

		Assert.Equal( 0, result.Status );
		Assert.Equal( "/actual/file" + Environment.NewLine, result.Output );
	}

	/// <summary>Verifies logical mode removes parent components before resolving links.</summary>
	[Fact]
	public async Task LogicalModeNormalizesParentBeforeLink() {
		var provider = CreateProvider()
			.AddFile( "/work/file" )
			.AddDirectory( "/actual" )
			.AddDirectory( "/actual/dir" )
			.AddLink( "/work/link", "/actual/dir" );
		var result = await RunAsync( new[] { "-L", "link/../file" }, provider );

		Assert.Equal( 0, result.Status );
		Assert.Equal( "/work/file" + Environment.NewLine, result.Output );
	}

	/// <summary>Verifies strict logical mode validates components before removing a parent reference.</summary>
	[Fact]
	public async Task LogicalExistingModeRejectsMissingComponentBeforeParent() {
		var provider = CreateProvider().AddFile( "/work/file" );
		var result = await RunAsync( new[] { "-Le", "missing/../file" }, provider );

		Assert.Equal( 1, result.Status );
		Assert.Empty( result.Output );
	}

	/// <summary>Verifies default logical mode validates the no-link pass before removing a parent reference.</summary>
	[Fact]
	public async Task LogicalDefaultModeRejectsMissingComponentBeforeParent() {
		var provider = CreateProvider().AddFile( "/work/file" );
		var result = await RunAsync( new[] { "-L", "missing/../file" }, provider );

		Assert.Equal( 1, result.Status );
		Assert.Empty( result.Output );
	}

	/// <summary>Verifies no-link mode preserves a symbolic-link component.</summary>
	[Fact]
	public async Task StripModePreservesLinkSpelling() {
		var provider = CreateProvider()
			.AddDirectory( "/actual" )
			.AddDirectory( "/actual/dir" )
			.AddFile( "/actual/dir/child" )
			.AddLink( "/work/link", "/actual/dir" )
			.AddFile( "/work/link/child" );
		var result = await RunAsync( new[] { "-s", "link/child" }, provider );

		Assert.Equal( 0, result.Status );
		Assert.Equal( "/work/link/child" + Environment.NewLine, result.Output );
	}

	/// <summary>Verifies default no-link mode still requires every nonfinal component.</summary>
	[Fact]
	public async Task StripModeRejectsMissingIntermediateComponentsByDefault() {
		var result = await RunAsync( new[] { "-s", "missing/child" }, CreateProvider() );

		Assert.Equal( 1, result.Status );
		Assert.Empty( result.Output );
	}

	/// <summary>Verifies no-link missing mode operates lexically without filesystem observations.</summary>
	[Fact]
	public async Task StripMissingModeAllowsMissingIntermediateComponents() {
		var result = await RunAsync( new[] { "-sm", "missing/child" }, CreateProvider() );

		Assert.Equal( 0, result.Status );
		Assert.Equal( "/work/missing/child" + Environment.NewLine, result.Output );
	}

	/// <summary>Verifies strict no-link mode still requires every component to exist.</summary>
	[Fact]
	public async Task StripExistingModeRejectsMissingComponents() {
		var result = await RunAsync( new[] { "-se", "missing/child" }, CreateProvider() );

		Assert.Equal( 1, result.Status );
		Assert.Empty( result.Output );
	}

	/// <summary>Verifies the default, strict, and missing-suffix policies.</summary>
	[Fact]
	public async Task CanonicalModesApplyMissingPolicies() {
		var provider = CreateProvider();
		var defaultResult = await RunAsync( new[] { "new" }, provider );
		var strictResult = await RunAsync( new[] { "-e", "new" }, provider );
		var missingResult = await RunAsync( new[] { "-m", "new/child" }, provider );

		Assert.Equal( 0, defaultResult.Status );
		Assert.Equal( "/work/new" + Environment.NewLine, defaultResult.Output );
		Assert.Equal( 1, strictResult.Status );
		Assert.Empty( strictResult.Output );
		Assert.Equal( 0, missingResult.Status );
		Assert.Equal( "/work/new/child" + Environment.NewLine, missingResult.Output );
	}

	/// <summary>Verifies output relative to a canonical base directory.</summary>
	[Fact]
	public async Task RelativeToWritesRelativePath() {
		var provider = CreateProvider()
			.AddDirectory( "/work/base" )
			.AddDirectory( "/work/base/dir" )
			.AddFile( "/work/base/dir/file" );
		var result = await RunAsync(
			new[] { "--relative-to=/work/base", "/work/base/dir/file" },
			provider
		);

		Assert.Equal( 0, result.Status );
		Assert.Equal( "dir/file" + Environment.NewLine, result.Output );
	}

	/// <summary>Verifies relative-base alone uses the base as the relative-to directory.</summary>
	[Fact]
	public async Task RelativeBaseWritesDescendantsRelatively() {
		var provider = CreateProvider()
			.AddDirectory( "/work/base" )
			.AddDirectory( "/work/base/dir" )
			.AddFile( "/work/base/dir/file" );
		var result = await RunAsync(
			new[] { "--relative-base=/work/base", "/work/base/dir/file" },
			provider
		);

		Assert.Equal( 0, result.Status );
		Assert.Equal( "dir/file" + Environment.NewLine, result.Output );
	}

	/// <summary>Verifies relative-base keeps targets outside the base absolute.</summary>
	[Fact]
	public async Task RelativeBaseKeepsOutsideTargetAbsolute() {
		var provider = CreateProvider()
			.AddDirectory( "/work/base" )
			.AddDirectory( "/other" )
			.AddFile( "/other/file" );
		var result = await RunAsync(
			new[] {
				"--relative-to=/work/base",
				"--relative-base=/work/base",
				"/other/file"
			},
			provider
		);

		Assert.Equal( 0, result.Status );
		Assert.Equal( "/other/file" + Environment.NewLine, result.Output );
	}

	/// <summary>Verifies relative output is disabled when the selected base excludes the relative-to directory.</summary>
	[Fact]
	public async Task RelativeBaseOutsideRelativeToDisablesRelativeOutput() {
		var provider = CreateProvider()
			.AddDirectory( "/work/base" )
			.AddDirectory( "/work/other" )
			.AddFile( "/work/base/file" );
		var result = await RunAsync(
			new[] {
				"--relative-to=/work/other",
				"--relative-base=/work/base",
				"/work/base/file"
			},
			provider
		);

		Assert.Equal( 0, result.Status );
		Assert.Equal( "/work/base/file" + Environment.NewLine, result.Output );
	}

	/// <summary>Verifies relative output falls back to absolute across Windows volumes.</summary>
	[Fact]
	public async Task RelativeToKeepsDifferentWindowsVolumeAbsolute() {
		var provider = new SyntheticCanonicalPathFileSystemProvider(
			PathPlatformSemantics.Windows,
			@"C:\work"
		)
			.AddDirectory( @"C:\work" )
			.AddDirectory( @"D:\" )
			.AddDirectory( @"D:\data" )
			.AddFile( @"D:\data\file" );
		var result = await RunAsync(
			new[] { @"--relative-to=C:\work", @"D:\data\file" },
			provider
		);

		Assert.Equal( 0, result.Status );
		Assert.Equal( @"D:\data\file" + Environment.NewLine, result.Output );
	}

	/// <summary>Verifies NUL-delimited output.</summary>
	[Fact]
	public async Task ZeroOptionWritesNulDelimiter() {
		var provider = CreateProvider().AddFile( "/work/file" );
		var result = await RunAsync( new[] { "-z", "file" }, provider );

		Assert.Equal( 0, result.Status );
		Assert.Equal( "/work/file\0", result.Output );
	}

	/// <summary>Verifies quiet mode suppresses an operand failure while preserving status.</summary>
	[Fact]
	public async Task QuietModeSuppressesOperandDiagnostic() {
		var result = await RunAsync( new[] { "-qe", "missing" }, CreateProvider() );

		Assert.Equal( 1, result.Status );
		Assert.Empty( result.Output );
		Assert.Empty( result.Error );
	}

	/// <summary>Verifies one failed operand does not prevent a later successful operand.</summary>
	[Fact]
	public async Task MultipleOperandsContinueAfterFailure() {
		var provider = CreateProvider().AddFile( "/work/file" );
		var result = await RunAsync( new[] { "-qe", "missing", "file" }, provider );

		Assert.Equal( 1, result.Status );
		Assert.Equal( "/work/file" + Environment.NewLine, result.Output );
	}

	/// <summary>Verifies loops fail without echoing the input as success.</summary>
	[Fact]
	public async Task LinkLoopDoesNotProduceFalseSuccess() {
		var provider = CreateProvider()
			.AddLink( "/work/a", "b" )
			.AddLink( "/work/b", "a" );
		var result = await RunAsync( new[] { "a" }, provider );

		Assert.Equal( 1, result.Status );
		Assert.Empty( result.Output );
		Assert.Equal(
			"realpath: 'a': the symbolic-link chain contains a resolution loop"
				+ Environment.NewLine,
			result.Error
		);
	}

	/// <summary>Verifies all-but-last canonicalization ignores a trailing separator.</summary>
	[Fact]
	public async Task CanonicalizeModeIgnoresTrailingSeparator() {
		var provider = CreateProvider().AddFile( "/work/file" );
		var result = await RunAsync( new[] { "file/" }, provider );

		Assert.Equal( 0, result.Status );
		Assert.Equal( "/work/file" + Environment.NewLine, result.Output );
		Assert.Empty( result.Error );
	}

	/// <summary>Verifies strict trailing-separator canonicalization requires a directory.</summary>
	[Fact]
	public async Task ExistingModeRequiresDirectoryAfterTrailingSeparator() {
		var provider = CreateProvider().AddFile( "/work/file" );
		var result = await RunAsync( new[] { "-e", "file/" }, provider );

		Assert.Equal( 1, result.Status );
		Assert.Empty( result.Output );
		Assert.Equal(
			"realpath: 'file/': the final pathname component is not a directory"
				+ Environment.NewLine,
			result.Error
		);
	}

	/// <summary>Verifies the last existence and resolution options take precedence.</summary>
	[Fact]
	public async Task LastModeOptionsWin() {
		var provider = CreateProvider()
			.AddDirectory( "/actual" )
			.AddDirectory( "/actual/dir" )
			.AddFile( "/work/file" )
			.AddLink( "/work/link", "/actual/dir" );
		var allowMissing = await RunAsync( new[] { "-e", "-m", "missing/child" }, provider );
		var requireExisting = await RunAsync( new[] { "-m", "-e", "missing/child" }, provider );
		var physicalWins = await RunAsync( new[] { "-L", "-P", "link/../file" }, provider );

		Assert.Equal( 0, allowMissing.Status );
		Assert.Equal( "/work/missing/child" + Environment.NewLine, allowMissing.Output );
		Assert.Equal( 1, requireExisting.Status );
		Assert.Equal( 0, physicalWins.Status );
		Assert.Equal( "/actual/file" + Environment.NewLine, physicalWins.Output );
	}

	/// <summary>Verifies the standard help and version switches.</summary>
	[Fact]
	public async Task ReportsHelpAndVersion() {
		var help = await RunAsync( new[] { "--help" }, CreateProvider() );
		var version = await RunAsync( new[] { "--version" }, CreateProvider() );

		Assert.Equal( 0, help.Status );
		Assert.StartsWith( "Usage: realpath ", help.Output );
		Assert.Equal( 0, version.Status );
		Assert.Equal( "realpath (Icod.CoreUtils) 1.0" + Environment.NewLine, version.Output );
	}

	/// <summary>Verifies repository-standard cancellation status.</summary>
	[Fact]
	public async Task CancellationReturnsCanceledStatus() {
		using var source = new CancellationTokenSource();
		source.Cancel();
		var result = await RunAsync( new[] { "file" }, CreateProvider(), source.Token );

		Assert.Equal( CommandExitCodes.Canceled, result.Status );
	}

	private static SyntheticCanonicalPathFileSystemProvider CreateProvider() =>
		new SyntheticCanonicalPathFileSystemProvider( PathPlatformSemantics.Posix, "/work" )
			.AddDirectory( "/work" )
	;

	private static async Task<(int Status, string Output, string Error)> RunAsync(
		string[] arguments,
		SyntheticCanonicalPathFileSystemProvider provider,
		CancellationToken cancellationToken = default
	) {
		using var output = new StringWriter();
		using var error = new StringWriter();
		var context = new CommandContext(
			"realpath",
			TextReader.Null,
			output,
			error,
			cancellationToken: cancellationToken
		);
		var status = await Command.RunAsync(
			arguments,
			context,
			new CanonicalPathResolver( provider )
		);
		return ( status, output.ToString(), error.ToString() );
	}
}
