/*
	Icod.CoreUtils.Shared
	Shared support library for the Icod.CoreUtils command suite.
	Copyright (C) 2026  Timothy J. Bruce <uniblab@hotmail.com>
*/

/*
	This program is free software: you can redistribute it and/or modify
	it under the terms of the GNU Lesser General Public License as published by
	the Free Software Foundation, either version 3 of the License, or
	(at your option) any later version.

	This program is distributed in the hope that it will be useful,
	but WITHOUT ANY WARRANTY; without even the implied warranty of
	MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
	GNU Lesser General Public License for more details.

	You should have received a copy of the GNU Lesser General Public License
	along with this program.  If not, see <https://www.gnu.org/licenses/>.
*/

namespace Icod.CoreUtils.Shared.Presentation;

using Icod.Terminal;

/// <summary>
/// Identifies one process standard stream for output-presentation decisions.
/// </summary>
public enum StandardStreamKind {
	/// <summary>The process standard-input stream.</summary>
	StandardInput,
	/// <summary>The process standard-output stream.</summary>
	StandardOutput,
	/// <summary>The process standard-error stream.</summary>
	StandardError
}

/// <summary>
/// Identifies the source used for one resolved presentation dimension.
/// </summary>
public enum PresentationDimensionSource {
	/// <summary>The value came from the process environment.</summary>
	Environment,
	/// <summary>The value came from the attached terminal.</summary>
	Terminal,
	/// <summary>The value came from the configured deterministic fallback.</summary>
	Fallback
}

/// <summary>
/// Configures deterministic output-presentation dimension fallbacks.
/// </summary>
public sealed class OutputPresentationOptions {

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
/// Represents resolved presentation capabilities for one standard stream.
/// </summary>
public sealed class OutputPresentationSnapshot {

	internal OutputPresentationSnapshot(
		StandardStreamKind stream,
		TerminalEndpointObservation? observation,
		OutputEnvironmentSnapshot environment,
		int width,
		int height,
		PresentationDimensionSource widthSource,
		PresentationDimensionSource heightSource
	) {
		ArgumentNullException.ThrowIfNull(
			environment
		);
		if ( 0 >= width ) {
			throw new ArgumentOutOfRangeException(
				nameof( width )
			);
		}
		if ( 0 >= height ) {
			throw new ArgumentOutOfRangeException(
				nameof( height )
			);
		}
		this.Stream = stream;
		this.Observation = observation;
		this.Environment = environment;
		this.Width = width;
		this.Height = height;
		this.WidthSource = widthSource;
		this.HeightSource = heightSource;
	}

	/// <summary>Gets the observed standard stream.</summary>
	public StandardStreamKind Stream {
		get;
	}

	/// <summary>Gets the low-level terminal observation, when available.</summary>
	public TerminalEndpointObservation? Observation {
		get;
	}

	/// <summary>Gets the captured process-environment inputs.</summary>
	public OutputEnvironmentSnapshot Environment {
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
	public PresentationDimensionSource WidthSource {
		get;
	}

	/// <summary>Gets the source of the resolved height.</summary>
	public PresentationDimensionSource HeightSource {
		get;
	}

	/// <summary>Gets whether the stream is attached to a terminal.</summary>
	public bool IsTerminal {
		get {
			return true == this.Observation?.IsTerminal;
		}
	}

}

/// <summary>
/// Resolves terminal attachment, dimensions, and process-environment inputs for
/// command presentation without taking ownership of the terminal.
/// </summary>
public sealed class OutputPresentationProvider {
	private readonly ITerminalControlProvider controlProvider;
	private readonly IEnvironmentVariableProvider environmentProvider;
	private readonly OutputPresentationOptions options;

	/// <summary>
	/// Initializes an output-presentation provider.
	/// </summary>
	public OutputPresentationProvider(
		ITerminalControlProvider controlProvider,
		IEnvironmentVariableProvider environmentProvider,
		OutputPresentationOptions? options = null
	) {
		ArgumentNullException.ThrowIfNull(
			controlProvider
		);
		ArgumentNullException.ThrowIfNull(
			environmentProvider
		);
		this.controlProvider = controlProvider;
		this.environmentProvider = environmentProvider;
		this.options = options ?? new OutputPresentationOptions();
		this.options.Validate();
	}

	/// <summary>
	/// Creates a provider backed by the current process terminal and environment.
	/// </summary>
	public static OutputPresentationProvider CreateSystem(
		OutputPresentationOptions? options = null
	) {
		return new OutputPresentationProvider(
			SystemTerminalControlProvider.Instance,
			SystemEnvironmentVariableProvider.Instance,
			options
		);
	}

	/// <summary>
	/// Resolves presentation capabilities for one standard stream.
	/// </summary>
	public OutputPresentationSnapshot Observe(
		StandardStreamKind stream
	) {
		if ( !Enum.IsDefined(
			stream
		) ) {
			throw new ArgumentOutOfRangeException(
				nameof( stream ),
				stream,
				"Unknown standard stream."
			);
		}

		var endpoint = stream switch {
			StandardStreamKind.StandardInput => TerminalEndpoint.StandardInput,
			StandardStreamKind.StandardOutput => TerminalEndpoint.StandardOutput,
			StandardStreamKind.StandardError => TerminalEndpoint.StandardError,
			_ => throw new ArgumentOutOfRangeException(
				nameof( stream ),
				stream,
				"Unknown standard stream."
			)
		};
		var observationResult = this.controlProvider.Observe(
			endpoint
		);
		var observation = observationResult.IsAvailable
			? observationResult.GetRequiredValue()
			: null
		;
		var environment = OutputEnvironmentSnapshot.Capture(
			this.environmentProvider
		);

		int? terminalWidth = null;
		int? terminalHeight = null;
		if ( true == observation?.IsTerminal ) {
			var sizeResult = this.controlProvider.GetSize(
				endpoint
			);
			if ( sizeResult.IsAvailable ) {
				var size = sizeResult.GetRequiredValue();
				if ( 0 < size.Columns ) {
					terminalWidth = size.Columns;
				}
				if ( 0 < size.Rows ) {
					terminalHeight = size.Rows;
				}
			}
		}

		var width = ResolveDimension(
			environment.Columns,
			terminalWidth,
			this.options.FallbackWidth
		);
		var height = ResolveDimension(
			environment.Lines,
			terminalHeight,
			this.options.FallbackHeight
		);

		return new OutputPresentationSnapshot(
			stream,
			observation,
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
		if ( OutputEnvironmentSnapshot.TryParsePositiveDimension(
			environmentValue,
			out var parsed
		) ) {
			return new ResolvedDimension(
				parsed,
				PresentationDimensionSource.Environment
			);
		}
		if ( terminalValue is > 0 ) {
			return new ResolvedDimension(
				terminalValue.Value,
				PresentationDimensionSource.Terminal
			);
		}
		return new ResolvedDimension(
			fallbackValue,
			PresentationDimensionSource.Fallback
		);
	}

	private readonly record struct ResolvedDimension(
		int Value,
		PresentationDimensionSource Source
	);

}
