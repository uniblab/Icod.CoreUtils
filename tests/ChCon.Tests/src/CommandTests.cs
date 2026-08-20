namespace Icod.CoreUtils.ChCon.Tests;

using System.Collections.Generic;
using System.IO;
using Icod.CoreUtils.Shared.Platform;
using Xunit;

public sealed class CommandTests {
	[Fact]
	public void HelpDoesNotRequireSelinux() {
		var output = new StringWriter();
		var status = Command.Run( new[] { "--help" }, stdout: output, platform: new FakeSelinuxPlatform { IsSupported = false } );
		Assert.Equal( 0, status );
		Assert.Contains( "Usage: chcon", output.ToString() );
	}


	[Fact]
	public void InvalidOptionBeforeHelpIsStillAnError() {
		var output = new StringWriter();
		var status = Command.Run( new[] { "--invalid", "--help" }, stdout: output, platform: new FakeSelinuxPlatform { IsSupported = false } );
		Assert.Equal( 1, status );
		Assert.DoesNotContain( "Usage: chcon", output.ToString() );
	}

	[Fact]
	public void AppliesCompleteContextToEveryOperand() {
		var platform = new FakeSelinuxPlatform();
		var status = Command.Run( new[] { "system_u:object_r:tmp_t:s0", "one", "two" }, platform: platform );
		Assert.Equal( 0, status );
		Assert.Equal( 2, platform.Sets.Count );
		Assert.All( platform.Sets, item => Assert.Equal( "system_u:object_r:tmp_t:s0", item.Context ) );
	}

	[Fact]
	public void AppliesPartialContextAgainstEachExistingContext() {
		var platform = new FakeSelinuxPlatform();
		platform.FileContexts["one"] = "user_u:object_r:old_t:s0:c1";
		var status = Command.Run( new[] { "--type=new_t", "--range=s0:c2", "one" }, platform: platform );
		Assert.Equal( 0, status );
		Assert.Equal( "user_u:object_r:new_t:s0:c2", platform.Sets[0].Context );
	}

	[Fact]
	public void ReferenceContextIsReadWithoutShellingOut() {
		var platform = new FakeSelinuxPlatform();
		platform.FileContexts["reference"] = "staff_u:object_r:etc_t:s0";
		var status = Command.Run( new[] { "--reference=reference", "target" }, platform: platform );
		Assert.Equal( 0, status );
		Assert.Equal( "staff_u:object_r:etc_t:s0", platform.Sets[0].Context );
	}

	[Fact]
	public void NoDereferenceTargetsLinkObject() {
		var platform = new FakeSelinuxPlatform();
		var status = Command.Run( new[] { "-h", "system_u:object_r:tmp_t:s0", "link" }, platform: platform );
		Assert.Equal( 0, status );
		Assert.False( platform.Sets[0].Dereference );
	}

	[Fact]
	public void RecursiveDereferenceRequiresHOrL() {
		var error = new StringWriter();
		var status = Command.Run( new[] { "-R", "--dereference", "system_u:object_r:tmp_t:s0", "tree" }, stderr: error, platform: new FakeSelinuxPlatform() );
		Assert.Equal( 1, status );
		Assert.Contains( "requires either -H or -L", error.ToString() );
	}


	[Fact]
	public void RecursivePhysicalTraversalUsesNoDereferenceAndPostOrder() {
		var root = System.IO.Path.Combine( System.IO.Path.GetTempPath(), $"icod-chcon-{System.Guid.NewGuid():N}" );
		Directory.CreateDirectory( root );
		var child = System.IO.Path.Combine( root, "child.txt" );
		File.WriteAllText( child, "content" );
		try {
			var platform = new FakeSelinuxPlatform();
			var status = Command.Run( new[] { "-R", "system_u:object_r:tmp_t:s0", root }, platform: platform );
			Assert.Equal( 0, status );
			Assert.Equal( 2, platform.Sets.Count );
			Assert.All( platform.Sets, item => Assert.False( item.Dereference ) );
			Assert.Equal( child, platform.Sets[0].Path );
			Assert.Equal( root, platform.Sets[1].Path );
		} finally {
			Directory.Delete( root, true );
		}
	}

	[Fact]
	public void RecursiveCommandLineTraversalDefaultsToDereference() {
		var root = System.IO.Path.Combine( System.IO.Path.GetTempPath(), $"icod-chcon-{System.Guid.NewGuid():N}" );
		Directory.CreateDirectory( root );
		try {
			var platform = new FakeSelinuxPlatform();
			var status = Command.Run( new[] { "-R", "-H", "system_u:object_r:tmp_t:s0", root }, platform: platform );
			Assert.Equal( 0, status );
			Assert.Single( platform.Sets );
			Assert.True( platform.Sets[0].Dereference );
		} finally {
			Directory.Delete( root, true );
		}
	}

	[Fact]
	public void PreserveRootRejectsRecursiveRootOperand() {
		var root = System.IO.Path.GetPathRoot( System.IO.Path.GetFullPath( "." ) )!;
		var error = new StringWriter();
		var platform = new FakeSelinuxPlatform();
		var status = Command.Run( new[] { "-R", "--preserve-root", "system_u:object_r:tmp_t:s0", root }, stderr: error, platform: platform );
		Assert.Equal( 1, status );
		Assert.Empty( platform.Sets );
		Assert.Contains( "dangerous to operate recursively", error.ToString() );
	}

	[Fact]
	public void HelpTokenAfterDoubleDashRemainsAnOperand() {
		var output = new StringWriter();
		var platform = new FakeSelinuxPlatform();
		var status = Command.Run( new[] { "system_u:object_r:tmp_t:s0", "--", "--help" }, stdout: output, platform: platform );
		Assert.Equal( 0, status );
		Assert.DoesNotContain( "Usage: chcon", output.ToString() );
		Assert.Equal( "--help", platform.Sets[0].Path );
	}

	[Fact]
	public void ReferenceAndComponentsConflict() {
		var status = Command.Run( new[] { "--reference=ref", "--type=x_t", "target" }, platform: new FakeSelinuxPlatform() );
		Assert.Equal( 1, status );
	}

	[Fact]
	public void UnsupportedHostReturnsControlledFailure() {
		var error = new StringWriter();
		var platform = new FakeSelinuxPlatform { IsSupported = false, UnsupportedReason = "not available here" };
		var status = Command.Run( new[] { "u:r:t:s0", "target" }, stderr: error, platform: platform );
		Assert.Equal( 1, status );
		Assert.Contains( "not available here", error.ToString() );
	}

	public sealed class FakeSelinuxPlatform : ISelinuxPlatform {
		public bool IsSupported { get; set; } = true;
		public string UnsupportedReason { get; set; } = "unsupported";
		public bool Enabled { get; set; } = true;
		public Dictionary<string, string> FileContexts { get; } = new();
		public List<(string Path, string Context, bool Dereference)> Sets { get; } = new();
		public bool IsEnabled( out int errorNumber ) { errorNumber = Enabled ? 0 : 1; return Enabled; }
		public bool TryGetCurrentContext( out string context, out int errorNumber ) { context = "user_u:role_r:type_t:s0"; errorNumber = 0; return true; }
		public bool TryGetFileContext( string path, bool dereference, out string context, out int errorNumber ) {
			errorNumber = 0;
			return FileContexts.TryGetValue( path, out context! );
		}
		public bool TrySetFileContext( string path, string context, bool dereference, out int errorNumber ) {
			Sets.Add( ( path, context, dereference ) ); errorNumber = 0; return true;
		}
		public bool TryValidateContext( string context, out int errorNumber ) { errorNumber = 0; return true; }
		public bool TryComputeProcessContext( string sourceContext, string executableContext, out string context, out int errorNumber ) { context = sourceContext; errorNumber = 0; return true; }
		public SelinuxExecutionResult ExecuteWithContext( string context, IReadOnlyList<string> command, bool searchPath ) { return new SelinuxExecutionResult( 0, 0, null ); }
		public string DescribeError( int errorNumber ) { return $"error {errorNumber}"; }
	}
}
