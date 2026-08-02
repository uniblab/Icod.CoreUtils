namespace Icod.CoreUtils.ReadLink.Tests;

using Icod.CoreUtils.PathCommandTests;
using Icod.CoreUtils.Shared.Diagnostics;
using Icod.Path;
using Xunit;

/// <summary>Exercises GNU-compatible <c>readlink</c> command policy over deterministic paths.</summary>
public sealed class CommandTests {
	/// <summary>Verifies direct link inspection returns the stored target text.</summary>
	[Fact]
	public async Task DirectModePrintsRawLinkTarget() {
		var provider = CreateProvider()
			.AddFile( "/work/target" )
			.AddLink( "/work/link", "target" );
		var result = await RunAsync( new[] { "-n", "link" }, provider );

		Assert.Equal( 0, result.Status );
		Assert.Equal( "target", result.Output );
		Assert.Empty( result.Error );
	}

	/// <summary>Verifies a Windows link-like reparse point exposes its raw target.</summary>
	[Fact]
	public async Task DirectModeReadsSupportedWindowsReparseLink() {
		var provider = new SyntheticCanonicalPathFileSystemProvider(
			PathPlatformSemantics.Windows,
			@"C:\work"
		)
			.AddDirectory( @"C:\work" )
			.AddLink( @"C:\work\link", @"..\target", isReparsePoint: true );
		var result = await RunAsync( new[] { "-n", @"C:\work\link" }, provider );

		Assert.Equal( 0, result.Status );
		Assert.Equal( @"..\target", result.Output );
	}

	/// <summary>Verifies unsupported reparse points fail with a controlled diagnostic.</summary>
	[Fact]
	public async Task DirectModeRejectsUnsupportedReparsePoint() {
		var provider = CreateProvider().AddUnsupportedReparsePoint( "/work/object" );
		var result = await RunAsync( new[] { "-v", "object" }, provider );

		Assert.Equal( 1, result.Status );
		Assert.Empty( result.Output );
		Assert.Equal(
			"readlink: 'object': the reparse point does not expose supported symbolic-link semantics"
				+ Environment.NewLine,
			result.Error
		);
	}

	/// <summary>Verifies canonical modes implement the three missing-component policies.</summary>
	[Fact]
	public async Task CanonicalModesApplyMissingPolicies() {
		var provider = CreateProvider();
		var allowFinal = await RunAsync( new[] { "-f", "new" }, provider );
		var requireExisting = await RunAsync( new[] { "-e", "new" }, provider );
		var allowMissing = await RunAsync( new[] { "-m", "new/child" }, provider );

		Assert.Equal( "/work/new" + Environment.NewLine, allowFinal.Output );
		Assert.Equal( 0, allowFinal.Status );
		Assert.Equal( 1, requireExisting.Status );
		Assert.Empty( requireExisting.Output );
		Assert.Equal( "/work/new/child" + Environment.NewLine, allowMissing.Output );
		Assert.Equal( 0, allowMissing.Status );
	}

	/// <summary>Verifies verbose mode diagnoses a non-link operand.</summary>
	[Fact]
	public async Task VerboseModeDiagnosesNonLink() {
		var provider = CreateProvider().AddFile( "/work/file" );
		var result = await RunAsync( new[] { "-v", "file" }, provider );

		Assert.Equal( 1, result.Status );
		Assert.Empty( result.Output );
		Assert.Equal(
			"readlink: 'file': the pathname is not a symbolic link" + Environment.NewLine,
			result.Error
		);
	}

	/// <summary>Verifies multiple operands ignore no-newline and retain record boundaries.</summary>
	[Fact]
	public async Task MultipleOperandsIgnoreNoNewlineWithWarning() {
		var provider = CreateProvider()
			.AddLink( "/work/one", "first" )
			.AddLink( "/work/two", "second" );
		var result = await RunAsync( new[] { "-n", "one", "two" }, provider );

		Assert.Equal( 0, result.Status );
		Assert.Equal(
			"first" + Environment.NewLine + "second" + Environment.NewLine,
			result.Output
		);
		Assert.Equal(
			"readlink: ignoring --no-newline with multiple arguments"
				+ Environment.NewLine,
			result.Error
		);
	}

	/// <summary>Verifies no-newline takes precedence over NUL termination for one operand.</summary>
	[Fact]
	public async Task NoNewlineOverridesZeroForSingleOperand() {
		var provider = CreateProvider().AddLink( "/work/link", "target" );
		var result = await RunAsync( new[] { "-nz", "link" }, provider );

		Assert.Equal( 0, result.Status );
		Assert.Equal( "target", result.Output );
	}

	/// <summary>Verifies one failed operand does not prevent a later link from being printed.</summary>
	[Fact]
	public async Task MultipleOperandsContinueAfterFailure() {
		var provider = CreateProvider().AddLink( "/work/link", "target" );
		var result = await RunAsync( new[] { "-q", "missing", "link" }, provider );

		Assert.Equal( 1, result.Status );
		Assert.Equal( "target" + Environment.NewLine, result.Output );
		Assert.Empty( result.Error );
	}

	/// <summary>Verifies NUL-delimited output.</summary>
	[Fact]
	public async Task ZeroOptionWritesNulDelimiter() {
		var provider = CreateProvider().AddLink( "/work/link", "target" );
		var result = await RunAsync( new[] { "-z", "link" }, provider );

		Assert.Equal( 0, result.Status );
		Assert.Equal( "target\0", result.Output );
	}

	/// <summary>Verifies symbolic-link loops fail without echoing unresolved input.</summary>
	[Fact]
	public async Task CanonicalLoopDoesNotProduceFalseSuccess() {
		var provider = CreateProvider()
			.AddLink( "/work/a", "b" )
			.AddLink( "/work/b", "a" );
		var result = await RunAsync( new[] { "-fv", "a" }, provider );

		Assert.Equal( 1, result.Status );
		Assert.Empty( result.Output );
		Assert.Equal(
			"readlink: 'a': the symbolic-link chain contains a resolution loop"
				+ Environment.NewLine,
			result.Error
		);
	}

	/// <summary>Verifies all-but-last canonicalization ignores a trailing separator.</summary>
	[Fact]
	public async Task CanonicalizeModeIgnoresTrailingSeparator() {
		var provider = CreateProvider().AddFile( "/work/file" );
		var result = await RunAsync( new[] { "-fv", "file/" }, provider );

		Assert.Equal( 0, result.Status );
		Assert.Equal( "/work/file" + Environment.NewLine, result.Output );
		Assert.Empty( result.Error );
	}

	/// <summary>Verifies strict trailing-separator canonicalization requires a directory.</summary>
	[Fact]
	public async Task ExistingModeRequiresDirectoryAfterTrailingSeparator() {
		var provider = CreateProvider().AddFile( "/work/file" );
		var result = await RunAsync( new[] { "-ev", "file/" }, provider );

		Assert.Equal( 1, result.Status );
		Assert.Empty( result.Output );
		Assert.Equal(
			"readlink: 'file/': the final pathname component is not a directory"
				+ Environment.NewLine,
			result.Error
		);
	}

	/// <summary>Verifies the last canonicalization option controls missing-component policy.</summary>
	[Fact]
	public async Task LastCanonicalizationOptionWins() {
		var provider = CreateProvider();
		var missingWins = await RunAsync( new[] { "-e", "-m", "missing/child" }, provider );
		var existingWins = await RunAsync( new[] { "-m", "-e", "missing/child" }, provider );

		Assert.Equal( 0, missingWins.Status );
		Assert.Equal( "/work/missing/child" + Environment.NewLine, missingWins.Output );
		Assert.Equal( 1, existingWins.Status );
		Assert.Empty( existingWins.Output );
	}

	/// <summary>Verifies the standard help and version switches.</summary>
	[Fact]
	public async Task ReportsHelpAndVersion() {
		var help = await RunAsync( new[] { "--help" }, CreateProvider() );
		var version = await RunAsync( new[] { "--version" }, CreateProvider() );

		Assert.Equal( 0, help.Status );
		Assert.StartsWith( "Usage: readlink ", help.Output );
		Assert.Equal( 0, version.Status );
		Assert.Equal( "readlink (Icod.CoreUtils) 1.0" + Environment.NewLine, version.Output );
	}

	/// <summary>Verifies repository-standard cancellation status.</summary>
	[Fact]
	public async Task CancellationReturnsCanceledStatus() {
		using var source = new CancellationTokenSource();
		source.Cancel();
		var result = await RunAsync( new[] { "-f", "file" }, CreateProvider(), source.Token );

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
			"readlink",
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
