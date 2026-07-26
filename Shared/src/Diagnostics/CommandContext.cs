namespace Icod.CoreUtils.Shared.Diagnostics;


/// <summary>
/// Carries standard streams, program identity, and cancellation through a command.
/// </summary>
/// <remarks>
/// The context does not own or dispose supplied streams and readers.
/// </remarks>
public sealed class CommandContext {

	/// <summary>Gets the cancellation token for the command.</summary>
	public CancellationToken CancellationToken {
		get;
	}

	/// <summary>Gets the diagnostic writer associated with this context.</summary>
	public DiagnosticWriter Diagnostics {
		get;
	}

	/// <summary>Gets the display name used in diagnostics.</summary>
	public string ProgramName {
		get;
	}

	/// <summary>Gets the binary standard-error stream when available.</summary>
	public Stream? StandardErrorStream {
		get;
	}

	/// <summary>Gets the binary standard-input stream when available.</summary>
	public Stream? StandardInputStream {
		get;
	}

	/// <summary>Gets the binary standard-output stream when available.</summary>
	public Stream? StandardOutputStream {
		get;
	}

	/// <summary>Gets standard error as text.</summary>
	public TextWriter StandardError {
		get;
	}

	/// <summary>Gets standard input as text.</summary>
	public TextReader StandardInput {
		get;
	}

	/// <summary>Gets standard output as text.</summary>
	public TextWriter StandardOutput {
		get;
	}

	/// <summary>
	/// Initializes a command context.
	/// </summary>
	public CommandContext(
		string programName,
		TextReader standardInput,
		TextWriter standardOutput,
		TextWriter standardError,
		Stream? standardInputStream = null,
		Stream? standardOutputStream = null,
		Stream? standardErrorStream = null,
		CancellationToken cancellationToken = default
	) {
		ArgumentException.ThrowIfNullOrWhiteSpace(
			programName
		);
		this.ProgramName = programName;
		this.StandardInput = standardInput ?? throw new ArgumentNullException(
			nameof( standardInput )
		);
		this.StandardOutput = standardOutput ?? throw new ArgumentNullException(
			nameof( standardOutput )
		);
		this.StandardError = standardError ?? throw new ArgumentNullException(
			nameof( standardError )
		);
		this.StandardInputStream = standardInputStream;
		this.StandardOutputStream = standardOutputStream;
		this.StandardErrorStream = standardErrorStream;
		this.CancellationToken = cancellationToken;
		this.Diagnostics = new DiagnosticWriter(
			programName,
			standardError
		);
	}

	/// <summary>
	/// Creates a context backed by the process console streams.
	/// </summary>
	public static CommandContext CreateConsole(
		string programName,
		CancellationToken cancellationToken = default
	) {
		return new CommandContext(
			programName,
			Console.In,
			Console.Out,
			Console.Error,
			Console.OpenStandardInput(),
			Console.OpenStandardOutput(),
			Console.OpenStandardError(),
			cancellationToken
		);
	}

	/// <summary>
	/// Creates a copy using a different cancellation token.
	/// </summary>
	public CommandContext WithCancellation(
		CancellationToken cancellationToken
	) {
		return new CommandContext(
			this.ProgramName,
			this.StandardInput,
			this.StandardOutput,
			this.StandardError,
			this.StandardInputStream,
			this.StandardOutputStream,
			this.StandardErrorStream,
			cancellationToken
		);
	}

}
