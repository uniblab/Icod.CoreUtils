namespace Icod.CoreUtils.Shared.Terminal;

/// <summary>
/// Configures deterministic terminal-dimension fallbacks.
/// </summary>
public sealed class TerminalPresentationOptions {
	/// <summary>Gets or initializes the fallback width in columns.</summary>
	public int FallbackWidth {
		get;
		init;
	} = 80;

	/// <summary>Gets or initializes the fallback height in rows.</summary>
	public int FallbackHeight {
		get;
		init;
	} = 24;

	/// <summary>
	/// Validates that both configured fallback dimensions are positive.
	/// </summary>
	/// <exception cref="ArgumentOutOfRangeException">
	/// A configured fallback dimension is not positive.
	/// </exception>
	internal void Validate() {
		if ( 0 >= this.FallbackWidth ) {
			throw new ArgumentOutOfRangeException(
				nameof( this.FallbackWidth ),
				"Fallback width must be positive."
			);
		}
		if ( 0 >= this.FallbackHeight ) {
			throw new ArgumentOutOfRangeException(
				nameof( this.FallbackHeight ),
				"Fallback height must be positive."
			);
		}
	}
}

/// <summary>
/// Represents the resolved presentation capabilities for one standard stream.
/// </summary>
public sealed class TerminalPresentationSnapshot {
	/// <summary>
	/// Initializes a resolved terminal-presentation snapshot.
	/// </summary>
	/// <param name="stream">The observed standard stream.</param>
	/// <param name="device">The raw terminal-device observation.</param>
	/// <param name="environment">The captured environment inputs.</param>
	/// <param name="width">The resolved positive width.</param>
	/// <param name="height">The resolved positive height.</param>
	/// <param name="widthSource">The width provenance.</param>
	/// <param name="heightSource">The height provenance.</param>
	internal TerminalPresentationSnapshot(
		TerminalStreamKind stream,
		TerminalDeviceObservation device,
		TerminalEnvironmentSnapshot environment,
		int width,
		int height,
		TerminalDimensionSource widthSource,
		TerminalDimensionSource heightSource
	) {
		this.Stream = stream;
		this.Device = device;
		this.Environment = environment;
		this.Width = width;
		this.Height = height;
		this.WidthSource = widthSource;
		this.HeightSource = heightSource;
	}

	/// <summary>Gets the observed standard stream.</summary>
	public TerminalStreamKind Stream {
		get;
	}

	/// <summary>Gets the raw terminal-device observation.</summary>
	public TerminalDeviceObservation Device {
		get;
	}

	/// <summary>Gets the captured environment inputs.</summary>
	public TerminalEnvironmentSnapshot Environment {
		get;
	}

	/// <summary>Gets the resolved width in columns.</summary>
	public int Width {
		get;
	}

	/// <summary>Gets the resolved height in rows.</summary>
	public int Height {
		get;
	}

	/// <summary>Gets the source of the resolved width.</summary>
	public TerminalDimensionSource WidthSource {
		get;
	}

	/// <summary>Gets the source of the resolved height.</summary>
	public TerminalDimensionSource HeightSource {
		get;
	}

	/// <summary>Gets whether the stream is attached to a terminal.</summary>
	public bool IsTerminal {
		get {
			return this.Device.IsTerminal;
		}
	}
}

/// <summary>
/// Resolves terminal attachment, dimensions, and environment inputs through
/// injectable providers and deterministic fallback policy.
/// </summary>
public sealed class TerminalPresentationProvider {
	private readonly ITerminalDeviceProvider deviceProvider;
	private readonly IEnvironmentVariableProvider environmentProvider;
	private readonly TerminalPresentationOptions options;

	/// <summary>
	/// Initializes a terminal-presentation provider.
	/// </summary>
	/// <param name="deviceProvider">The terminal-device provider.</param>
	/// <param name="environmentProvider">The environment provider.</param>
	/// <param name="options">Optional deterministic fallback settings.</param>
	/// <exception cref="ArgumentNullException">
	/// A required provider is <see langword="null"/>.
	/// </exception>
	/// <exception cref="ArgumentOutOfRangeException">
	/// A configured fallback dimension is not positive.
	/// </exception>
	public TerminalPresentationProvider(
		ITerminalDeviceProvider deviceProvider,
		IEnvironmentVariableProvider environmentProvider,
		TerminalPresentationOptions? options = null
	) {
		ArgumentNullException.ThrowIfNull( deviceProvider );
		ArgumentNullException.ThrowIfNull( environmentProvider );
		this.deviceProvider = deviceProvider;
		this.environmentProvider = environmentProvider;
		this.options = options ?? new TerminalPresentationOptions();
		this.options.Validate();
	}

	/// <summary>
	/// Creates a provider backed by the current process console and environment.
	/// </summary>
	/// <param name="options">Optional deterministic fallback settings.</param>
	/// <returns>A system-backed terminal-presentation provider.</returns>
	public static TerminalPresentationProvider CreateSystem(
		TerminalPresentationOptions? options = null
	) {
		return new TerminalPresentationProvider(
			SystemTerminalDeviceProvider.Instance,
			SystemEnvironmentVariableProvider.Instance,
			options
		);
	}

	/// <summary>
	/// Resolves presentation capabilities for one standard stream.
	/// </summary>
	/// <param name="stream">The standard stream to observe.</param>
	/// <returns>An immutable presentation snapshot.</returns>
	/// <exception cref="ArgumentOutOfRangeException">
	/// <paramref name="stream"/> is not defined.
	/// </exception>
	public TerminalPresentationSnapshot Observe(
		TerminalStreamKind stream
	) {
		if ( !Enum.IsDefined( stream ) ) {
			throw new ArgumentOutOfRangeException(
				nameof( stream ),
				stream,
				"Unknown standard stream."
			);
		}
		var device = this.deviceProvider.Observe( stream );
		var environment = TerminalEnvironmentSnapshot.Capture(
			this.environmentProvider
		);

		var width = ResolveDimension(
			environment.Columns,
			device.Dimensions?.Width,
			this.options.FallbackWidth
		);
		var height = ResolveDimension(
			environment.Lines,
			device.Dimensions?.Height,
			this.options.FallbackHeight
		);

		return new TerminalPresentationSnapshot(
			stream,
			device,
			environment,
			width.Value,
			height.Value,
			width.Source,
			height.Source
		);
	}

	private static ResolvedDimension ResolveDimension(
		string? environmentValue,
		int? terminalValue,
		int fallbackValue
	) {
		if ( TerminalEnvironmentSnapshot.TryParsePositiveDimension(
			environmentValue,
			out var parsed
		) ) {
			return new ResolvedDimension(
				parsed,
				TerminalDimensionSource.Environment
			);
		}
		if ( terminalValue is > 0 ) {
			return new ResolvedDimension(
				terminalValue.Value,
				TerminalDimensionSource.Terminal
			);
		}
		return new ResolvedDimension(
			fallbackValue,
			TerminalDimensionSource.Fallback
		);
	}

	private readonly record struct ResolvedDimension(
		int Value,
		TerminalDimensionSource Source
	);
}
