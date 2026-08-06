namespace Icod.CoreUtils.Shared.Terminal;

using System.Security;

/// <summary>
/// Identifies one of the process standard streams for terminal presentation
/// decisions.
/// </summary>
public enum TerminalStreamKind {
	/// <summary>The process standard-input stream.</summary>
	StandardInput,

	/// <summary>The process standard-output stream.</summary>
	StandardOutput,

	/// <summary>The process standard-error stream.</summary>
	StandardError
}

/// <summary>
/// Describes the result of determining whether a standard stream is attached
/// to a terminal.
/// </summary>
public enum TerminalProbeStatus {
	/// <summary>The stream is attached to a terminal.</summary>
	Terminal,

	/// <summary>The stream is redirected and is not attached to a terminal.</summary>
	Redirected,

	/// <summary>The host could not expose terminal information.</summary>
	Unavailable,

	/// <summary>The host terminal query failed in a controlled manner.</summary>
	Failed
}

/// <summary>
/// Identifies the source used for one resolved terminal dimension.
/// </summary>
public enum TerminalDimensionSource {
	/// <summary>The value came from the process environment.</summary>
	Environment,

	/// <summary>The value came from the attached terminal.</summary>
	Terminal,

	/// <summary>The value came from the configured deterministic fallback.</summary>
	Fallback
}

/// <summary>
/// Represents a positive terminal width and height.
/// </summary>
public readonly record struct TerminalDimensions {
	/// <summary>
	/// Initializes terminal dimensions.
	/// </summary>
	/// <param name="width">The positive terminal width in columns.</param>
	/// <param name="height">The positive terminal height in rows.</param>
	/// <exception cref="ArgumentOutOfRangeException">
	/// <paramref name="width"/> or <paramref name="height"/> is not positive.
	/// </exception>
	public TerminalDimensions(
		int width,
		int height
	) {
		if ( 0 >= width ) {
			throw new ArgumentOutOfRangeException(
				nameof( width ),
				"Terminal width must be positive."
			);
		}
		if ( 0 >= height ) {
			throw new ArgumentOutOfRangeException(
				nameof( height ),
				"Terminal height must be positive."
			);
		}
		this.Width = width;
		this.Height = height;
	}

	/// <summary>Gets the terminal width in columns.</summary>
	public int Width {
		get;
	}

	/// <summary>Gets the terminal height in rows.</summary>
	public int Height {
		get;
	}
}

/// <summary>
/// Describes the raw host observation for one standard stream.
/// </summary>
public sealed class TerminalDeviceObservation {
	private TerminalDeviceObservation(
		TerminalProbeStatus status,
		TerminalDimensions? dimensions,
		string? message
	) {
		this.Status = status;
		this.Dimensions = dimensions;
		this.Message = message;
	}

	/// <summary>Gets the terminal-probe status.</summary>
	public TerminalProbeStatus Status {
		get;
	}

	/// <summary>
	/// Gets the dimensions reported by the terminal, or <see langword="null"/>
	/// when no dimensions were available.
	/// </summary>
	public TerminalDimensions? Dimensions {
		get;
	}

	/// <summary>Gets the controlled probe explanation, when present.</summary>
	public string? Message {
		get;
	}

	/// <summary>Gets whether the stream was observed as a terminal.</summary>
	public bool IsTerminal {
		get {
			return TerminalProbeStatus.Terminal == this.Status;
		}
	}

	/// <summary>Creates an attached-terminal observation.</summary>
	/// <param name="dimensions">Optional dimensions reported by the terminal.</param>
	/// <param name="message">An optional explanation when dimensions were unavailable.</param>
	/// <returns>An attached-terminal observation.</returns>
	public static TerminalDeviceObservation Attached(
		TerminalDimensions? dimensions = null,
		string? message = null
	) {
		return new TerminalDeviceObservation(
			TerminalProbeStatus.Terminal,
			dimensions,
			message
		);
	}

	/// <summary>Creates a redirected-stream observation.</summary>
	/// <returns>A redirected-stream observation.</returns>
	public static TerminalDeviceObservation Redirected() {
		return new TerminalDeviceObservation(
			TerminalProbeStatus.Redirected,
			null,
			null
		);
	}

	/// <summary>Creates an unavailable-terminal observation.</summary>
	/// <param name="message">The controlled explanation.</param>
	/// <returns>An unavailable-terminal observation.</returns>
	public static TerminalDeviceObservation Unavailable(
		string? message
	) {
		return new TerminalDeviceObservation(
			TerminalProbeStatus.Unavailable,
			null,
			NormalizeMessage(
				message,
				"Terminal information is unavailable."
			)
		);
	}

	/// <summary>Creates a failed-terminal observation.</summary>
	/// <param name="message">The controlled failure explanation.</param>
	/// <returns>A failed-terminal observation.</returns>
	public static TerminalDeviceObservation Failed(
		string? message
	) {
		return new TerminalDeviceObservation(
			TerminalProbeStatus.Failed,
			null,
			NormalizeMessage(
				message,
				"The terminal query failed."
			)
		);
	}

	private static string NormalizeMessage(
		string? message,
		string fallback
	) {
		return string.IsNullOrWhiteSpace( message )
			? fallback
			: message.Trim();
	}
}

/// <summary>
/// Supplies terminal attachment and geometry observations for standard
/// streams.
/// </summary>
public interface ITerminalDeviceProvider {
	/// <summary>
	/// Observes one process standard stream.
	/// </summary>
	/// <param name="stream">The standard stream to observe.</param>
	/// <returns>A controlled terminal-device observation.</returns>
	TerminalDeviceObservation Observe(
		TerminalStreamKind stream
	);
}

/// <summary>
/// Supplies environment-variable values to presentation policy.
/// </summary>
public interface IEnvironmentVariableProvider {
	/// <summary>
	/// Gets one environment-variable value.
	/// </summary>
	/// <param name="name">The variable name.</param>
	/// <returns>The value, or <see langword="null"/> when the variable is absent.</returns>
	string? GetValue(
		string name
	);
}

/// <summary>
/// Reads environment variables from the current process.
/// </summary>
public sealed class SystemEnvironmentVariableProvider : IEnvironmentVariableProvider {
	/// <summary>Gets the reusable system environment provider.</summary>
	public static SystemEnvironmentVariableProvider Instance {
		get;
	} = new();

	private SystemEnvironmentVariableProvider() {
	}

	/// <inheritdoc/>
	public string? GetValue(
		string name
	) {
		ArgumentException.ThrowIfNullOrWhiteSpace( name );
		return Environment.GetEnvironmentVariable( name );
	}
}

/// <summary>
/// Observes terminal attachment and dimensions through the managed console
/// APIs while converting unsupported and failed probes into controlled results.
/// </summary>
public sealed class SystemTerminalDeviceProvider : ITerminalDeviceProvider {
	/// <summary>Gets the reusable system terminal provider.</summary>
	public static SystemTerminalDeviceProvider Instance {
		get;
	} = new();

	private SystemTerminalDeviceProvider() {
	}

	/// <inheritdoc/>
	public TerminalDeviceObservation Observe(
		TerminalStreamKind stream
	) {
		bool redirected;
		try {
			redirected = stream switch {
				TerminalStreamKind.StandardInput => Console.IsInputRedirected,
				TerminalStreamKind.StandardOutput => Console.IsOutputRedirected,
				TerminalStreamKind.StandardError => Console.IsErrorRedirected,
				_ => throw new ArgumentOutOfRangeException(
					nameof( stream ),
					stream,
					"Unknown standard stream."
				)
			};
		} catch ( PlatformNotSupportedException exception ) {
			return TerminalDeviceObservation.Unavailable( exception.Message );
		} catch ( IOException exception ) {
			return TerminalDeviceObservation.Failed( exception.Message );
		} catch ( InvalidOperationException exception ) {
			return TerminalDeviceObservation.Failed( exception.Message );
		} catch ( SecurityException exception ) {
			return TerminalDeviceObservation.Failed( exception.Message );
		}

		if ( redirected ) {
			return TerminalDeviceObservation.Redirected();
		}

		try {
			var width = Console.WindowWidth;
			var height = Console.WindowHeight;
			if ( ( 0 < width ) && ( 0 < height ) ) {
				return TerminalDeviceObservation.Attached(
					new TerminalDimensions( width, height )
				);
			}
			return TerminalDeviceObservation.Attached(
				null,
				"The terminal reported nonpositive dimensions."
			);
		} catch ( PlatformNotSupportedException exception ) {
			return TerminalDeviceObservation.Attached( null, exception.Message );
		} catch ( IOException exception ) {
			return TerminalDeviceObservation.Attached( null, exception.Message );
		} catch ( InvalidOperationException exception ) {
			return TerminalDeviceObservation.Attached( null, exception.Message );
		} catch ( SecurityException exception ) {
			return TerminalDeviceObservation.Attached( null, exception.Message );
		}
	}
}
