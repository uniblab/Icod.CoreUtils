namespace Icod.CoreUtils.Shared.Processes;

using System.Text;

/// <summary>
/// Configures asynchronous child-process execution.
/// </summary>
public sealed class ProcessRunOptions {

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

	/// <summary>Gets or sets whether the inherited environment is cleared.</summary>
	public bool ClearEnvironment {
		get;
		set;
	}

	/// <summary>Gets environment changes. A null value removes a variable.</summary>
	public IDictionary<string, string?> EnvironmentVariables {
		get;
	} = new Dictionary<string, string?>(
		StringComparer.Ordinal
	);

	/// <summary>Gets the executable file name.</summary>
	public string FileName {
		get;
	}

	/// <summary>Gets or sets whether cancellation terminates the process tree.</summary>
	public bool KillEntireProcessTreeOnCancellation {
		get;
		set;
	} = true;

	/// <summary>Gets or sets the encoding used to decode captured output.</summary>
	public Encoding OutputEncoding {
		get;
		set;
	} = Encoding.UTF8;

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

	/// <summary>Gets or sets the working directory.</summary>
	public string? WorkingDirectory {
		get;
		set;
	}

	/// <summary>
	/// Initializes process options.
	/// </summary>
	public ProcessRunOptions(
		string fileName
	) {
		ArgumentException.ThrowIfNullOrWhiteSpace(
			fileName
		);
		this.FileName = fileName;
	}

}
