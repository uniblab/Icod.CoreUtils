namespace Icod.CoreUtils.Shared.Processes;

using System.Text;

/// <summary>
/// Identifies how a canceled or timed-out process execution handles a live child.
/// </summary>
public enum ProcessCancellationPolicy {
	/// <summary>Terminate the child and, where supported, its descendants.</summary>
	KillProcessTree,
	/// <summary>Terminate only the immediate child.</summary>
	KillProcess,
	/// <summary>Detach redirected streams and leave the child running.</summary>
	LeaveRunning
}

/// <summary>
/// Configures asynchronous child-process execution.
/// </summary>
public sealed class ProcessRunOptions {
	private ProcessCancellationPolicy _cancellationPolicy = ProcessCancellationPolicy.KillProcessTree;

	/// <summary>Gets the exact child-process arguments.</summary>
	public IList<string> Arguments {
		get;
	} = new List<string>();

	/// <summary>Gets or sets whether standard error is captured in the result.</summary>
	public bool CaptureStandardError {
		get;
		set;
	}

	/// <summary>Gets or sets whether standard output is captured in the result.</summary>
	public bool CaptureStandardOutput {
		get;
		set;
	}

	/// <summary>Gets or sets how a live child is handled after cancellation or timeout.</summary>
	public ProcessCancellationPolicy CancellationPolicy {
		get => this._cancellationPolicy;
		set {
			if ( !Enum.IsDefined(
				typeof( ProcessCancellationPolicy ),
				value
			) ) {
				throw new ArgumentOutOfRangeException(
					nameof( value )
				);
			}
			this._cancellationPolicy = value;
		}
	}

	/// <summary>Gets or sets whether the inherited environment is cleared.</summary>
	public bool ClearEnvironment {
		get;
		set;
	}

	/// <summary>
	/// Gets or sets an exact environment snapshot. When set, it takes precedence over inherited-environment selection.
	/// </summary>
	public ProcessEnvironment? Environment {
		get;
		set;
	}

	/// <summary>Gets environment changes. A null value removes a variable.</summary>
	public IDictionary<string, string?> EnvironmentVariables {
		get;
	} = new Dictionary<string, string?>(
		ProcessEnvironmentBuilder.VariableNameComparer
	);

	/// <summary>Gets the executable file name.</summary>
	public string FileName {
		get;
	}

	/// <summary>
	/// Gets or sets whether cancellation terminates the process tree.
	/// </summary>
	/// <remarks>
	/// This compatibility property maps to <see cref="CancellationPolicy"/>. Setting false selects immediate-child termination.
	/// </remarks>
	public bool KillEntireProcessTreeOnCancellation {
		get => ProcessCancellationPolicy.KillProcessTree == this.CancellationPolicy;
		set => this.CancellationPolicy = value
			? ProcessCancellationPolicy.KillProcessTree
			: ProcessCancellationPolicy.KillProcess
		;
	}

	/// <summary>Gets or sets the encoding used to decode captured output.</summary>
	public Encoding OutputEncoding {
		get;
		set;
	} = Encoding.UTF8;

	/// <summary>
	/// Gets or sets a callback invoked after the child identity is observed and before waiting begins.
	/// </summary>
	public Action<ProcessIdentity>? ProcessStarted {
		get;
		set;
	}

	/// <summary>Gets or sets whether the executable is resolved before launch.</summary>
	public bool ResolveExecutable {
		get;
		set;
	}

	/// <summary>Gets or sets whether launch failures are returned instead of thrown.</summary>
	public bool ReturnLaunchFailureResult {
		get;
		set;
	}

	/// <summary>Gets or sets a destination for standard error.</summary>
	public Stream? StandardError {
		get;
		set;
	}

	/// <summary>Gets or sets a source for standard input.</summary>
	public Stream? StandardInput {
		get;
		set;
	}

	/// <summary>Gets or sets a destination for standard output.</summary>
	public Stream? StandardOutput {
		get;
		set;
	}

	/// <summary>Gets or sets an optional monotonic execution timeout.</summary>
	public TimeSpan? Timeout {
		get;
		set;
	}

	/// <summary>Gets or sets the working directory.</summary>
	public string? WorkingDirectory {
		get;
		set;
	}

	/// <summary>Initializes process options.</summary>
	public ProcessRunOptions(
		string fileName
	) {
		ArgumentException.ThrowIfNullOrWhiteSpace(
			fileName
		);
		this.FileName = fileName;
	}
}
