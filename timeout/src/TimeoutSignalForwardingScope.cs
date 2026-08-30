namespace Icod.CoreUtils.Timeout;

using System.Runtime.InteropServices;
using Icod.Processes;

/// <summary>Owns POSIX signal registrations used to forward terminal and termination signals to a supervised job.</summary>
internal sealed class TimeoutSignalForwardingScope : IDisposable {
	private readonly List<PosixSignalRegistration> _registrations = new();

	/// <summary>Creates forwarding registrations on POSIX hosts and returns null on Windows.</summary>
	internal static TimeoutSignalForwardingScope? Create(
		ProcessSignal timeoutSignal,
		Action<string> forward
	) {
		ArgumentNullException.ThrowIfNull( timeoutSignal );
		ArgumentNullException.ThrowIfNull( forward );
		if ( OperatingSystem.IsWindows() ) return null;
		var scope = new TimeoutSignalForwardingScope();
		try {
			scope.Add( PosixSignal.SIGHUP, "HUP", forward );
			scope.Add( PosixSignal.SIGINT, "INT", forward );
			scope.Add( PosixSignal.SIGQUIT, "QUIT", forward );
			scope.Add( PosixSignal.SIGTERM, "TERM", forward );
			if ( 0 < timeoutSignal.Number
				&& timeoutSignal.Name is not "HUP" and not "INT" and not "QUIT" and not "TERM"
				&& timeoutSignal.Name is not "KILL" and not "STOP"
			) {
				scope.Add( (PosixSignal)timeoutSignal.Number, timeoutSignal.Name, forward );
			}
			return scope;
		} catch ( Exception exception ) when ( exception is IOException or PlatformNotSupportedException or NotSupportedException ) {
			scope.Dispose();
			return null;
		} catch {
			scope.Dispose();
			throw;
		}
	}

	private void Add(
		PosixSignal signal,
		string name,
		Action<string> forward
	) => this._registrations.Add(
		PosixSignalRegistration.Create(
			signal,
			context => {
				context.Cancel = true;
				forward( name );
			}
		)
	);

	/// <inheritdoc />
	public void Dispose() {
		foreach ( var registration in this._registrations ) registration.Dispose();
		this._registrations.Clear();
	}
}
