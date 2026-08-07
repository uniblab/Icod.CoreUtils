namespace Icod.CoreUtils.Shared.Tests.Terminal;

using Icod.CoreUtils.Shared.Terminal;

using Xunit;

/// <summary>
/// Verifies that the native provider converts runner and redirection state into
/// controlled observations without mutating the active terminal.
/// </summary>
public sealed class SystemTerminalControlProviderTests {
	/// <summary>
	/// Verifies that standard-input attachment inspection always returns one of
	/// the public controlled states on a supported or fallback runner.
	/// </summary>
	[Fact]
	public void StandardInputObservationIsControlled() {
		var result = SystemTerminalControlProvider.Instance.Observe(
			TerminalEndpoint.StandardInput
		);
		Assert.Contains(
			result.Status,
			new[] {
				TerminalControlStatus.Available,
				TerminalControlStatus.Unavailable,
				TerminalControlStatus.Unsupported,
				TerminalControlStatus.Failed
			}
		);
		if ( result.IsAvailable && result.GetRequiredValue().IsTerminal ) {
			Assert.True(
				result.GetRequiredValue().Capabilities.HasFlag(
					TerminalControlCapabilities.Attachment
				)
			);
		}
	}

	/// <summary>
	/// Verifies that a regular file is observed as a nonterminal on Linux,
	/// macOS, and Windows.
	/// </summary>
	[Fact]
	public void RegularFileIsNotATerminal() {
		var path = System.IO.Path.GetTempFileName();
		try {
			var result = SystemTerminalControlProvider.Instance.Observe(
				TerminalEndpoint.ForPath( path )
			);
			if ( OperatingSystem.IsLinux()
				|| OperatingSystem.IsMacOS()
				|| OperatingSystem.IsWindows() ) {
				Assert.True( result.IsAvailable );
				Assert.False( result.GetRequiredValue().IsTerminal );
			} else {
				Assert.Equal( TerminalControlStatus.Unsupported, result.Status );
			}
		} finally {
			File.Delete( path );
		}
	}

	/// <summary>
	/// Verifies that mode retrieval for the active standard input is controlled
	/// and that available snapshots identify the current native model.
	/// </summary>
	[Fact]
	public void StandardInputModeRetrievalIsControlled() {
		var result = SystemTerminalControlProvider.Instance.GetMode(
			TerminalEndpoint.StandardInput
		);
		Assert.Contains(
			result.Status,
			new[] {
				TerminalControlStatus.Available,
				TerminalControlStatus.Unavailable,
				TerminalControlStatus.Unsupported,
				TerminalControlStatus.Failed
			}
		);
		if ( result.IsAvailable ) {
			var mode = result.GetRequiredValue();
			if ( OperatingSystem.IsWindows() ) {
				Assert.Equal( TerminalPlatformKind.WindowsConsole, mode.Platform );
				Assert.Empty( mode.ControlCharacters );
				Assert.Null( mode.InputSpeed );
				Assert.Null( mode.OutputSpeed );
			} else {
				Assert.Equal( TerminalPlatformKind.PosixTermios, mode.Platform );
				Assert.NotEmpty( mode.ControlCharacters );
				Assert.NotNull( mode.InputSpeed );
				Assert.NotNull( mode.OutputSpeed );
			}
		}
	}
}
