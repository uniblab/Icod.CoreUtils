namespace Icod.CoreUtils.Shared.Diagnostics;

/// <summary>
/// Represents the result of a command execution.
/// </summary>
/// <param name="ExitCode">The exit code value.</param>
public readonly record struct CommandResult(
	int ExitCode
) {
	/// <summary>Gets whether the command succeeded.</summary>
	public bool IsSuccess {
		get {
			return CommandExitCodes.Success == this.ExitCode;
		}
	}

	/// <summary>Creates a successful result.</summary>
	public static CommandResult Success() {
		return new CommandResult(
			CommandExitCodes.Success
		);
	}

	/// <summary>Creates a general failure result.</summary>
	public static CommandResult Failure() {
		return new CommandResult(
			CommandExitCodes.Failure
		);
	}
}
