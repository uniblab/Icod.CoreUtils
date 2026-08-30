namespace Icod.CoreUtils.Stty.Tests;

using Icod.Terminal;
using Xunit;

/// <summary>Tests pure <c>stty</c> mode editing policy.</summary>
public sealed class ModeEditorTests {
	/// <summary>Verifies that <c>speed</c> reports without mutating state.</summary>
	[Fact]
	public void SpeedIsReportingOnly() {
		var baseline = FakeTerminalProvider.CreateLinuxMode();
		var result = SttyModeEditor.Apply( baseline, new[] { "speed" } );
		Assert.False( result.Changed );
		Assert.Equal( "speed 9600 baud", Assert.Single( result.OutputLines ) );
	}

	/// <summary>Verifies a bare numeric operand changes both speeds.</summary>
	[Fact]
	public void BareNumberChangesBothSpeeds() {
		var result = SttyModeEditor.Apply( FakeTerminalProvider.CreateLinuxMode(), new[] { "115200" } );
		Assert.True( result.Changed );
		Assert.Equal( 115200UL, result.Mode.InputSpeed!.Value.BaudRate );
		Assert.Equal( 115200UL, result.Mode.OutputSpeed!.Value.BaudRate );
	}

	/// <summary>Verifies raw mode clears canonical and echo processing.</summary>
	[Fact]
	public void RawClearsCanonicalAndEcho() {
		var result = SttyModeEditor.Apply( FakeTerminalProvider.CreateLinuxMode(), new[] { "raw" } );
		Assert.Equal( 0UL, result.Mode.LocalFlags & ( 0x2UL | 0x8UL ) );
	}

	/// <summary>Verifies Windows refuses the POSIX speed report operand.</summary>
	[Fact]
	public void WindowsRejectsSpeedReport() {
		var mode = TerminalModeSnapshot.CreateWindowsConsole( TerminalConsoleDirection.Input, 0x7 );
		Assert.Throws<SttyUsageException>( () => SttyModeEditor.Apply( mode, new[] { "speed" } ) );
	}

	/// <summary>Verifies Windows refuses POSIX speed mutation.</summary>
	[Fact]
	public void WindowsRejectsSpeedMutation() {
		var mode = TerminalModeSnapshot.CreateWindowsConsole( TerminalConsoleDirection.Input, 0x7 );
		Assert.Throws<SttyUsageException>( () => SttyModeEditor.Apply( mode, new[] { "9600" } ) );
	}

	/// <summary>Verifies a saved POSIX state round-trips through the command editor.</summary>
	[Fact]
	public void SavedStateRestores() {
		var baseline = FakeTerminalProvider.CreateLinuxMode();
		var saved = TerminalModeCodec.Serialize( baseline );
		var result = SttyModeEditor.Apply( baseline, new[] { saved } );
		Assert.True( result.Changed );
		Assert.Equal( saved, TerminalModeCodec.Serialize( result.Mode ) );
	}
}
