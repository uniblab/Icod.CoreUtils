namespace Icod.CoreUtils.ChRoot.Tests;

using Xunit;

public sealed class CommandTests {
	[Fact]
	public async Task HelpAndVersionDoNotRequireNativeSupport() {
		var platform = new FakeChrootPlatform { IsSupported = false };
		var helpOut = new StringWriter();
		var helpErr = new StringWriter();
		var helpStatus = await Command.RunAsync( [ "--help" ], stdout: helpOut, stderr: helpErr, platform: platform );
		Assert.Equal( 0, helpStatus );
		Assert.Contains( "Usage: chroot", helpOut.ToString() );
		Assert.Equal( string.Empty, helpErr.ToString() );
		var versionOut = new StringWriter();
		var versionStatus = await Command.RunAsync( [ "--version" ], stdout: versionOut, stderr: new StringWriter(), platform: platform );
		Assert.Equal( 0, versionStatus );
		Assert.Contains( "9.11", versionOut.ToString() );
		Assert.Equal( 0, platform.ExecuteCount );
	}

	[Fact]
	public async Task MissingOperandAndUnknownOptionReturnInternalFailure() {
		var platform = new FakeChrootPlatform();
		var missingError = new StringWriter();
		var missing = await Command.RunAsync( [], stdout: new StringWriter(), stderr: missingError, platform: platform );
		Assert.Equal( 125, missing );
		Assert.Contains( "missing operand", missingError.ToString() );
		var optionError = new StringWriter();
		var unknown = await Command.RunAsync( [ "--bogus" ], stdout: new StringWriter(), stderr: optionError, platform: platform );
		Assert.Equal( 125, unknown );
		Assert.Contains( "unrecognized option", optionError.ToString() );
		Assert.Equal( 0, platform.ExecuteCount );
	}

	[Fact]
	public async Task CommandArgumentsArePreservedWithoutShellInterpolation() {
		var platform = new FakeChrootPlatform { ExitCode = 42 };
		var status = await Command.RunAsync(
			[ "/sandbox", "/bin/printf", "%s", "hello world", "; rm -rf /" ],
			stdout: new StringWriter(),
			stderr: new StringWriter(),
			platform: platform
		);
		Assert.Equal( 42, status );
		Assert.NotNull( platform.Request );
		Assert.Equal( "/sandbox", platform.Request!.RootDirectory );
		Assert.Equal( new[] { "/bin/printf", "%s", "hello world", "; rm -rf /" }, platform.Request.Command.ToArray() );
	}

	[Fact]
	public async Task OptionsStopAtNewRoot() {
		var platform = new FakeChrootPlatform();
		var status = await Command.RunAsync(
			[ "/sandbox", "--userspec=root:root", "literal" ],
			stdout: new StringWriter(),
			stderr: new StringWriter(),
			platform: platform
		);
		Assert.Equal( 0, status );
		Assert.NotNull( platform.Request );
		Assert.Null( platform.Request!.UserSpec );
		Assert.Equal( new[] { "--userspec=root:root", "literal" }, platform.Request.Command.ToArray() );
	}

	[Fact]
	public async Task DoubleDashEndsOptionParsingBeforeRoot() {
		var platform = new FakeChrootPlatform();
		var status = await Command.RunAsync(
			[ "--", "--root-looking-name", "/bin/true" ],
			stdout: new StringWriter(),
			stderr: new StringWriter(),
			platform: platform
		);
		Assert.Equal( 0, status );
		Assert.Equal( "--root-looking-name", platform.Request!.RootDirectory );
		Assert.Equal( new[] { "/bin/true" }, platform.Request.Command.ToArray() );
	}

	[Fact]
	public async Task AcceptsUnambiguousLongOptionAbbreviations() {
		var platform = new FakeChrootPlatform { CurrentRoot = true };
		var status = await Command.RunAsync(
			[ "--gr=wheel,audio", "--user=alice:staff", "--skip", "/", "/bin/id" ],
			stdout: new StringWriter(),
			stderr: new StringWriter(),
			platform: platform
		);
		Assert.Equal( 0, status );
		Assert.NotNull( platform.Request );
		Assert.Equal( "wheel,audio", platform.Request!.GroupsSpec );
		Assert.Equal( "alice:staff", platform.Request.UserSpec );
		Assert.True( platform.Request.SkipChdir );
	}

	[Fact]
	public async Task UserGroupsAndSkipChdirArePassedToPlatform() {
		var platform = new FakeChrootPlatform { CurrentRoot = true };
		var status = await Command.RunAsync(
			[ "--userspec", "alice:staff", "--groups=wheel,audio", "--skip-chdir", "/", "/bin/id" ],
			stdout: new StringWriter(),
			stderr: new StringWriter(),
			platform: platform
		);
		Assert.Equal( 0, status );
		Assert.NotNull( platform.Request );
		Assert.Equal( "alice:staff", platform.Request!.UserSpec );
		Assert.Equal( "wheel,audio", platform.Request.GroupsSpec );
		Assert.True( platform.Request.SkipChdir );
		Assert.Equal( new[] { "/bin/id" }, platform.Request.Command.ToArray() );
	}

	[Fact]
	public async Task EmptyGroupsSpecificationIsPreservedForClearing() {
		var platform = new FakeChrootPlatform();
		var status = await Command.RunAsync(
			[ "--groups=", "/sandbox", "/bin/id" ],
			stdout: new StringWriter(),
			stderr: new StringWriter(),
			platform: platform
		);
		Assert.Equal( 0, status );
		Assert.Equal( string.Empty, platform.Request!.GroupsSpec );
	}

	[Fact]
	public async Task TrailingUserspecColonUsesPrimaryGroupLookup() {
		var platform = new FakeChrootPlatform();
		var status = await Command.RunAsync(
			[ "--userspec=1000:", "/sandbox", "/bin/id" ],
			stdout: new StringWriter(),
			stderr: new StringWriter(),
			platform: platform
		);
		Assert.Equal( 0, status );
		Assert.Equal( "1000", platform.Request!.UserSpec );
	}

	[Fact]
	public async Task SkipChdirIsRejectedForAChangedRoot() {
		var platform = new FakeChrootPlatform { CurrentRoot = false };
		var error = new StringWriter();
		var status = await Command.RunAsync(
			[ "--skip-chdir", "/sandbox", "/bin/true" ],
			stdout: new StringWriter(),
			stderr: error,
			platform: platform
		);
		Assert.Equal( 125, status );
		Assert.Contains( "only permitted", error.ToString() );
		Assert.Equal( 0, platform.ExecuteCount );
	}

	[Fact]
	public async Task DefaultShellUsesEnvironmentThenPortableFallback() {
		var platform = new FakeChrootPlatform();
		var environment = new Dictionary<string, string>( StringComparer.Ordinal ) { [ "SHELL" ] = "/bin/zsh" };
		string? EnvironmentProvider( string name ) {
			ArgumentNullException.ThrowIfNull( name );
			if ( environment.TryGetValue( name, out var value ) ) {
				return value;
			}
			return null;
		}
		var status = await Command.RunAsync(
			[ "/sandbox" ],
			stdout: new StringWriter(),
			stderr: new StringWriter(),
			platform: platform,
			environmentVariableProvider: EnvironmentProvider
		);
		Assert.Equal( 0, status );
		Assert.Equal( new[] { "/bin/zsh", "-i" }, platform.Request!.Command.ToArray() );
		platform.Request = null;
		status = await Command.RunAsync(
			[ "/sandbox" ],
			stdout: new StringWriter(),
			stderr: new StringWriter(),
			platform: platform,
			environmentVariableProvider: EmptyEnvironment
		);
		Assert.Equal( 0, status );
		Assert.Equal( new[] { "/bin/sh", "-i" }, platform.Request!.Command.ToArray() );
		platform.Request = null;
		status = await Command.RunAsync(
			[ "/sandbox" ],
			stdout: new StringWriter(),
			stderr: new StringWriter(),
			platform: platform,
			environmentVariableProvider: static _ => string.Empty
		);
		Assert.Equal( 0, status );
		Assert.Equal( new[] { string.Empty, "-i" }, platform.Request!.Command.ToArray() );
	}

	[Fact]
	public async Task UnsupportedPlatformIsControlled() {
		var platform = new FakeChrootPlatform { IsSupported = false, UnsupportedReason = "not available here" };
		var error = new StringWriter();
		var status = await Command.RunAsync( [ "/sandbox", "/bin/true" ], stdout: new StringWriter(), stderr: error, platform: platform );
		Assert.Equal( 125, status );
		Assert.Contains( "not available here", error.ToString() );
		Assert.Equal( 0, platform.ExecuteCount );
	}

	[Theory]
	[InlineData( 126 )]
	[InlineData( 127 )]
	public async Task PlatformExecutionStatusAndDiagnosticArePropagated( int exitCode ) {
		var platform = new FakeChrootPlatform { ExitCode = exitCode, Diagnostic = "launch failed" };
		var error = new StringWriter();
		var status = await Command.RunAsync( [ "/sandbox", "/missing" ], stdout: new StringWriter(), stderr: error, platform: platform );
		Assert.Equal( exitCode, status );
		Assert.Contains( "launch failed", error.ToString() );
	}

	[Fact]
	public async Task CancellationBeforeExecutionReturnsInternalFailure() {
		var platform = new FakeChrootPlatform();
		using var cancellation = new CancellationTokenSource();
		cancellation.Cancel();
		var status = await Command.RunAsync(
			[ "/sandbox", "/bin/true" ],
			stdout: new StringWriter(),
			stderr: new StringWriter(),
			platform: platform,
			cancellationToken: cancellation.Token
		);
		Assert.Equal( 125, status );
		Assert.Equal( 0, platform.ExecuteCount );
	}

	private static string? EmptyEnvironment( string name ) {
		ArgumentNullException.ThrowIfNull( name );
		return null;
	}

	private sealed class FakeChrootPlatform : IChrootPlatform {
		public bool IsSupported { get; set; } = true;
		public string UnsupportedReason { get; set; } = "unsupported";
		public bool CurrentRoot { get; set; }
		public int ExitCode { get; set; }
		public string? Diagnostic { get; set; }
		public int ExecuteCount { get; private set; }
		public ChrootExecutionRequest? Request { get; set; }
		public bool IsCurrentRoot( string path ) {
			ArgumentException.ThrowIfNullOrEmpty( path );
			return CurrentRoot;
		}
		public ValueTask<ChrootExecutionResult> ExecuteAsync( ChrootExecutionRequest request, CancellationToken cancellationToken = default ) {
			ArgumentNullException.ThrowIfNull( request );
			cancellationToken.ThrowIfCancellationRequested();
			ExecuteCount++;
			Request = request;
			return ValueTask.FromResult( new ChrootExecutionResult( ExitCode, Diagnostic ) );
		}
	}
}
