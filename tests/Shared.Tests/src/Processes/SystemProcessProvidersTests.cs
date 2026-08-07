namespace Icod.CoreUtils.Shared.Tests.Processes;

using Xunit;
using Icod.CoreUtils.Shared.Processes;

/// <summary>
/// Exercises system identity, liveness, signal, and priority providers without mutating unrelated processes.
/// </summary>
public sealed class SystemProcessProvidersTests {
	/// <summary>Verifies current-process identity and PID-reuse protection.</summary>
	[Fact]
	public void ObservesIdentityLivenessAndReuseMismatch() {
		var inspector = SystemProcessInspector.Instance;
		var identity = inspector.ObserveIdentity(
			Environment.ProcessId
		);

		Assert.True(
			identity.Succeeded,
			identity.Message
		);
		var liveness = inspector.ObserveLiveness(
			ProcessTarget.ForProcess(
				identity.Value!
			)
		);
		Assert.True(
			liveness.Succeeded,
			liveness.Message
		);
		Assert.True(
			liveness.Value
		);

		var mismatch = inspector.ObserveLiveness(
			ProcessTarget.ForProcess(
				new ProcessIdentity(
					Environment.ProcessId,
					new ProcessReuseToken(
						"test",
						"not-the-current-process"
					)
				)
			)
		);
		Assert.Equal(
			ProcessOperationStatus.Reused,
			mismatch.Status
		);
	}

	/// <summary>Verifies safe signal-zero delivery to the current process.</summary>
	[Fact]
	public async Task DeliversSignalZeroAsLivenessProbe() {
		var identity = SystemProcessInspector.Instance.ObserveIdentity(
			Environment.ProcessId
		);
		Assert.True(
			identity.Succeeded,
			identity.Message
		);
		var result = await SystemProcessSignalProvider.Instance.DeliverAsync(
			ProcessTarget.ForProcess(
				identity.Value!
			),
			new ProcessSignal(
				0,
				"0"
			)
		);

		Assert.True(
			result.Succeeded,
			result.Message
		);
	}

	/// <summary>Verifies that the current process priority can be observed or fails in a controlled manner.</summary>
	[Fact]
	public void ObservesCurrentProcessPriority() {
		var result = SystemProcessPriorityProvider.Instance.GetPriority(
			ProcessTarget.ForProcess(
				Environment.ProcessId
			)
		);

		Assert.NotEqual(
			ProcessOperationStatus.Unsupported,
			result.Status
		);
		if ( result.Succeeded ) {
			Assert.InRange(
				result.Value!.NiceValue,
				-20,
				19
			);
		}
	}

	/// <summary>Verifies Linux signal disposition inspection for the current process.</summary>
	[Fact]
	public void ObservesLinuxSignalDispositionWhenAvailable() {
		if ( !OperatingSystem.IsLinux() ) {
			return;
		}
		var identity = SystemProcessInspector.Instance.ObserveIdentity(
			Environment.ProcessId
		);
		Assert.True(
			identity.Succeeded,
			identity.Message
		);
		var result = SystemProcessSignalProvider.Instance.ObserveDisposition(
			identity.Value!,
			ProcessSignalCatalog.Parse( "TERM" ).Value!
		);

		Assert.True(
			result.Succeeded,
			result.Message
		);
	}
	/// <summary>Verifies arbitrary-process waits reject a mismatched PID-reuse token before waiting.</summary>
	[Fact]
	public async Task ArbitraryWaitRejectsReusedIdentity() {
		var result = await SystemProcessInspector.Instance.WaitAsync(
			new ProcessIdentity(
				Environment.ProcessId,
				new ProcessReuseToken(
					"test",
					"not-the-current-process"
				)
			)
		);

		Assert.Equal(
			ProcessOperationStatus.Reused,
			result.Status
		);
	}

}
