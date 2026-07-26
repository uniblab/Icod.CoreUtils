namespace Icod.CoreUtils.Shared.Diagnostics;

/// <summary>
/// Provides conventional process exit codes used by command-line tools.
/// </summary>
public static class CommandExitCodes {
	/// <summary>Successful completion.</summary>
	public const int Success = 0;
	/// <summary>General operational failure.</summary>
	public const int Failure = 1;
	/// <summary>Invalid command usage.</summary>
	public const int UsageError = 2;
	/// <summary>Termination caused by an interrupt or cancellation request.</summary>
	public const int Canceled = 130;
}
