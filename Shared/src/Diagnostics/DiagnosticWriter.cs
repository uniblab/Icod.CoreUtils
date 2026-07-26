namespace Icod.CoreUtils.Shared.Diagnostics;

/// <summary>
/// Writes program-name-prefixed diagnostics.
/// </summary>
public sealed class DiagnosticWriter {

	private readonly TextWriter myError;

	/// <summary>Gets the program-name prefix.</summary>
	public string ProgramName {
		get;
	}

	/// <summary>
	/// Initializes a diagnostic writer.
	/// </summary>
	public DiagnosticWriter(
		string programName,
		TextWriter error
	) {
		ArgumentException.ThrowIfNullOrWhiteSpace(
			programName
		);
		this.ProgramName = programName;
		this.myError = error ?? throw new ArgumentNullException(
			nameof( error )
		);
	}

	/// <summary>Writes an error message.</summary>
	public void Error(
		string message
	) {
		this.myError.WriteLine(
			this.Format(
				message
			)
		);
	}

	/// <summary>Writes an error message asynchronously.</summary>
	public ValueTask ErrorAsync(
		string message,
		CancellationToken cancellationToken = default
	) {
		return new ValueTask(
			this.myError.WriteLineAsync(
				this.Format( message ).AsMemory(),
				cancellationToken
			)
		);
	}

	/// <summary>Writes a warning message.</summary>
	public void Warning(
		string message
	) {
		this.myError.WriteLine(
			this.Format(
				string.Concat(
					"warning: ",
					message
				)
			)
		);
	}

	/// <summary>Writes a warning message asynchronously.</summary>
	public ValueTask WarningAsync(
		string message,
		CancellationToken cancellationToken = default
	) {
		return new ValueTask(
			this.myError.WriteLineAsync(
				this.Format(
					string.Concat(
						"warning: ",
						message
					)
				).AsMemory(),
				cancellationToken
			)
		);
	}

	private string Format(
		string message
	) {
		return string.Concat(
			this.ProgramName,
			": ",
			message ?? string.Empty
		);
	}

}
