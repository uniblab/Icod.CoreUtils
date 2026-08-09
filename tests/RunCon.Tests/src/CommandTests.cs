namespace Icod.CoreUtils.RunCon.Tests;

using System.Collections.Generic;
using System.IO;
using Icod.CoreUtils.Shared.Platform;
using Xunit;

public sealed class CommandTests {
	[Fact]
	public void NoOperandsPrintsCurrentContext() {
		var output = new StringWriter();
		var platform = new FakeSelinuxPlatform { CurrentContext = "staff_u:staff_r:staff_t:s0" };
		var status = Command.Run( System.Array.Empty<string>(), stdout: output, platform: platform );
		Assert.Equal( 0, status );
		Assert.Contains( platform.CurrentContext, output.ToString() );
	}

	[Fact]
	public void HelpDoesNotRequireSelinux() {
		var output = new StringWriter();
		var status = Command.Run( new[] { "--help" }, stdout: output, platform: new FakeSelinuxPlatform { IsSupported = false } );
		Assert.Equal( 0, status );
		Assert.Contains( "Usage: runcon", output.ToString() );
	}


	[Fact]
	public void OptionsWithoutCommandStillPrintCurrentContextLikeGnuRuncon() {
		var output = new StringWriter();
		var platform = new FakeSelinuxPlatform { CurrentContext = "staff_u:staff_r:staff_t:s0" };
		var status = Command.Run( new[] { "--type=ignored_t" }, stdout: output, platform: platform );
		Assert.Equal( 0, status );
		Assert.Contains( platform.CurrentContext, output.ToString() );
		Assert.Null( platform.Command );
	}

	[Fact]
	public void InvalidOptionBeforeHelpIsStillAnError() {
		var output = new StringWriter();
		var status = Command.Run( new[] { "--invalid", "--help" }, stdout: output, platform: new FakeSelinuxPlatform { IsSupported = false } );
		Assert.Equal( 125, status );
		Assert.DoesNotContain( "Usage: runcon", output.ToString() );
	}

	[Fact]
	public void CompleteContextExecutesLiteralArgumentVector() {
		var platform = new FakeSelinuxPlatform();
		var status = Command.Run( new[] { "user_u:role_r:type_t:s0", "printf", "%s", "; rm -rf /" }, platform: platform );
		Assert.Equal( 0, status );
		Assert.Equal( "user_u:role_r:type_t:s0", platform.ExecutionContext );
		Assert.Equal( new[] { "printf", "%s", "; rm -rf /" }, platform.Command! );
		Assert.True( platform.SearchPath );
	}

	[Fact]
	public void ComponentsModifyCurrentContext() {
		var platform = new FakeSelinuxPlatform { CurrentContext = "user_u:role_r:old_t:s0" };
		var status = Command.Run( new[] { "--type=new_t", "--range=s0:c1", "program" }, platform: platform );
		Assert.Equal( 0, status );
		Assert.Equal( "user_u:role_r:new_t:s0:c1", platform.ExecutionContext );
	}

	[Fact]
	public void ComputeUsesExecutableContextBeforeComponentOverrides() {
		var platform = new FakeSelinuxPlatform { CurrentContext = "u:r:old_t:s0", ComputedContext = "u:r:computed_t:s0" };
		platform.FileContexts["/bin/program"] = "system_u:object_r:bin_t:s0";
		var status = Command.Run( new[] { "--compute", "--role=new_r", "/bin/program", "arg" }, platform: platform );
		Assert.Equal( 0, status );
		Assert.Equal( "u:new_r:computed_t:s0", platform.ExecutionContext );
		Assert.Equal( "u:r:old_t:s0", platform.ComputeSource );
		Assert.Equal( "system_u:object_r:bin_t:s0", platform.ComputeTarget );
		Assert.False( platform.SearchPath );
	}


	[Fact]
	public void CommandHelpArgumentIsNotConsumedAsRunconHelp() {
		var output = new StringWriter();
		var platform = new FakeSelinuxPlatform();
		var status = Command.Run( new[] { "u:r:t:s0", "program", "--help" }, stdout: output, platform: platform );
		Assert.Equal( 0, status );
		Assert.DoesNotContain( "Usage: runcon", output.ToString() );
		Assert.Equal( new[] { "program", "--help" }, platform.Command! );
	}

	[Fact]
	public void LeadingHelpAfterComponentOptionDoesNotRequireSelinux() {
		var output = new StringWriter();
		var platform = new FakeSelinuxPlatform { IsSupported = false };
		var status = Command.Run( new[] { "--user", "staff_u", "--help" }, stdout: output, platform: platform );
		Assert.Equal( 0, status );
		Assert.Contains( "Usage: runcon", output.ToString() );
	}

	[Fact]
	public void DuplicateComponentIsRejected() {
		var status = Command.Run( new[] { "-u", "one", "--user=two", "program" }, platform: new FakeSelinuxPlatform() );
		Assert.Equal( 125, status );
	}

	[Fact]
	public void CommandLookupFailureStatusIsPreserved() {
		var platform = new FakeSelinuxPlatform { ExecutionResult = new SelinuxExecutionResult( 127, 2, "not found" ) };
		var status = Command.Run( new[] { "u:r:t:s0", "missing-command" }, platform: platform );
		Assert.Equal( 127, status );
	}

	[Fact]
	public void MissingCommandIsDiagnosedBeforeSelinuxAvailability() {
		var error = new StringWriter();
		var platform = new FakeSelinuxPlatform { IsSupported = false, UnsupportedReason = "SELinux unavailable" };
		var status = Command.Run( new[] { "u:r:t:s0" }, stderr: error, platform: platform );
		Assert.Equal( 125, status );
		Assert.Contains( "missing command operand after context", error.ToString() );
		Assert.DoesNotContain( "SELinux unavailable", error.ToString() );
	}

	[Fact]
	public void UnsupportedHostReturnsControlledInternalFailure() {
		var error = new StringWriter();
		var platform = new FakeSelinuxPlatform { IsSupported = false, UnsupportedReason = "SELinux unavailable" };
		var status = Command.Run( System.Array.Empty<string>(), stderr: error, platform: platform );
		Assert.Equal( 125, status );
		Assert.Contains( "SELinux unavailable", error.ToString() );
	}

	public sealed class FakeSelinuxPlatform : ISelinuxPlatform {
		public bool IsSupported { get; set; } = true;
		public string UnsupportedReason { get; set; } = "unsupported";
		public bool Enabled { get; set; } = true;
		public string CurrentContext { get; set; } = "user_u:role_r:type_t:s0";
		public string ComputedContext { get; set; } = "user_u:role_r:computed_t:s0";
		public Dictionary<string, string> FileContexts { get; } = new();
		public string? ExecutionContext { get; set; }
		public IReadOnlyList<string>? Command { get; set; }
		public string? ComputeSource { get; set; }
		public string? ComputeTarget { get; set; }
		public bool SearchPath { get; set; }
		public SelinuxExecutionResult ExecutionResult { get; set; } = new( 0, 0, null );
		public bool IsEnabled( out int errorNumber ) { errorNumber = Enabled ? 0 : 1; return Enabled; }
		public bool TryGetCurrentContext( out string context, out int errorNumber ) { context = CurrentContext; errorNumber = 0; return true; }
		public bool TryGetFileContext( string path, bool dereference, out string context, out int errorNumber ) { errorNumber = 0; return FileContexts.TryGetValue( path, out context! ); }
		public bool TrySetFileContext( string path, string context, bool dereference, out int errorNumber ) { errorNumber = 0; return true; }
		public bool TryValidateContext( string context, out int errorNumber ) { errorNumber = 0; return true; }
		public bool TryComputeProcessContext( string sourceContext, string executableContext, out string context, out int errorNumber ) {
			ComputeSource = sourceContext; ComputeTarget = executableContext; context = ComputedContext; errorNumber = 0; return true;
		}
		public SelinuxExecutionResult ExecuteWithContext( string context, IReadOnlyList<string> command, bool searchPath ) { ExecutionContext = context; Command = command; SearchPath = searchPath; return ExecutionResult; }
		public string DescribeError( int errorNumber ) { return $"error {errorNumber}"; }
	}
}
