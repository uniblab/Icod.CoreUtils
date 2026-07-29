namespace Icod.CoreUtils.Shared.Processes;

/// <summary>
/// Contains the result of asynchronous process execution.
/// </summary>
public sealed class ProcessResult {

	/// <summary>Gets the process exit code, when available.</summary>
	public int? ExitCode {
		get;
	}

	/// <summary>Gets captured standard error.</summary>
	public string? StandardError {
		get;
	}

	/// <summary>Gets captured standard output.</summary>
	public string? StandardOutput {
		get;
	}

	/// <summary>Gets whether execution was canceled.</summary>
	public bool WasCanceled {
		get;
	}

	/// <summary>
	/// Initializes a new instance of the ProcessResult class.
	/// </summary>
	internal ProcessResult(
		int? exitCode,
		bool wasCanceled,
		string? standardOutput,
		string? standardError
	) {
		this.ExitCode = exitCode;
		this.WasCanceled = wasCanceled;
		this.StandardOutput = standardOutput;
		this.StandardError = standardError;
	}

}
