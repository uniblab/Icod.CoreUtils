namespace Icod.CoreUtils.Stty;

using Icod.CoreUtils.Shared.Terminal;

/// <summary>Implements GNU-compatible terminal-mode reporting and mutation.</summary>
public static class Command {
	private const string VersionText = "stty (Icod CoreUtils) 0.1.0";

	/// <summary>Runs <c>stty</c> synchronously.</summary>
	/// <param name="args">The command-line arguments.</param>
	/// <param name="stdout">The standard-output writer.</param>
	/// <param name="stderr">The standard-error writer.</param>
	/// <param name="provider">The terminal-control provider.</param>
	/// <returns>The process exit code.</returns>
	public static int Run(
		string[] args,
		TextWriter? stdout = null,
		TextWriter? stderr = null,
		ITerminalControlProvider? provider = null
	) => RunAsync( args, stdout, stderr, provider ).GetAwaiter().GetResult();

	/// <summary>Runs <c>stty</c> asynchronously.</summary>
	/// <param name="args">The command-line arguments.</param>
	/// <param name="stdout">The standard-output writer.</param>
	/// <param name="stderr">The standard-error writer.</param>
	/// <param name="provider">The terminal-control provider.</param>
	/// <param name="cancellationToken">A cancellation token.</param>
	/// <returns>The process exit code.</returns>
	public static async Task<int> RunAsync(
		string[] args,
		TextWriter? stdout = null,
		TextWriter? stderr = null,
		ITerminalControlProvider? provider = null,
		CancellationToken cancellationToken = default
	) {
		ArgumentNullException.ThrowIfNull( args );
		stdout ??= Console.Out;
		stderr ??= Console.Error;

		SttyOptions options;
		try {
			options = SttyOptions.Parse( args );
		} catch ( SttyUsageException exception ) {
			await WriteUsageErrorAsync( stderr, exception.Message ).ConfigureAwait( false );
			return 1;
		}
		if ( options.Help ) {
			await stdout.WriteAsync( HelpText ).ConfigureAwait( false );
			return 0;
		}
		if ( options.Version ) {
			await stdout.WriteLineAsync( VersionText ).ConfigureAwait( false );
			return 0;
		}

		provider ??= SystemTerminalControlProvider.Instance;
		var endpoint = ( options.File is null )
			? TerminalEndpoint.StandardInput
			: TerminalEndpoint.ForPath( options.File! )
		;
		try {
			cancellationToken.ThrowIfCancellationRequested();
			var result = provider.GetMode( endpoint );
			if ( !result.IsAvailable ) {
				await WriteProviderErrorAsync( stderr, endpoint, result.Message ).ConfigureAwait( false );
				return 1;
			}
			var mode = result.GetRequiredValue();
			if ( options.Save ) {
				await stdout.WriteLineAsync( TerminalModeCodec.Serialize( mode ) ).ConfigureAwait( false );
				return 0;
			}
			if ( options.All ) {
				await stdout.WriteAsync( SttyFormatter.FormatAll( mode ) ).ConfigureAwait( false );
				return 0;
			}
			if ( 0 == options.Settings.Count ) {
				await stdout.WriteAsync( SttyFormatter.FormatDefault( mode ) ).ConfigureAwait( false );
				return 0;
			}

			SttyEditResult edit;
			try {
				edit = SttyModeEditor.Apply( mode, options.Settings );
			} catch ( SttyUsageException exception ) {
				await stderr.WriteLineAsync( string.Concat( "stty: ", exception.Message ) ).ConfigureAwait( false );
				return 1;
			}
			foreach ( var line in edit.OutputLines ) {
				await stdout.WriteLineAsync( line ).ConfigureAwait( false );
			}
			if ( edit.Changed ) {
				var mutation = provider.SetMode( endpoint, edit.Mode, edit.Timing );
				if ( !mutation.Succeeded ) {
					await WriteProviderErrorAsync( stderr, endpoint, mutation.Message ).ConfigureAwait( false );
					return 1;
				}
			}
			return 0;
		} catch ( OperationCanceledException ) when ( cancellationToken.IsCancellationRequested ) {
			await stderr.WriteLineAsync( "stty: operation canceled" ).ConfigureAwait( false );
			return 1;
		} catch ( Exception exception ) {
			await stderr.WriteLineAsync( string.Concat( "stty: ", exception.Message ) ).ConfigureAwait( false );
			return 1;
		}
	}

	private static async Task WriteUsageErrorAsync( TextWriter stderr, string message ) {
		await stderr.WriteLineAsync( string.Concat( "stty: ", message ) ).ConfigureAwait( false );
		await stderr.WriteLineAsync( "Try 'stty --help' for more information." ).ConfigureAwait( false );
	}

	private static async Task WriteProviderErrorAsync(
		TextWriter stderr,
		TerminalEndpoint endpoint,
		string? message
	) {
		await stderr.WriteLineAsync(
			string.Concat(
				"stty: ",
				endpoint.DisplayName,
				": ",
				message ?? "terminal operation is unavailable"
			)
		).ConfigureAwait( false );
	}

	private static readonly string HelpText = """
Usage: stty [-F DEVICE | --file=DEVICE] [SETTING]...
  or:  stty [-F DEVICE | --file=DEVICE] [-a|--all]
  or:  stty [-F DEVICE | --file=DEVICE] [-g|--save]
Print or change terminal characteristics.

  -a, --all          print all current settings in human-readable form
  -g, --save         print all current settings in machine-readable form
  -F, --file=DEVICE  open and use the specified DEVICE instead of standard input
      --help         display this help and exit
      --version      output version information and exit

A bare numeric SETTING changes both input and output speed.  The operand
'speed' reports the current speed without changing it.  Common settings include
sane, raw, cooked, echo, icanon, isig, opost, control-character names, ispeed,
ospeed, line, drain, and -drain.  Prefix a boolean setting with '-' to disable it.

Windows console support preserves the complete native console mode and supports
sane/raw and processed, line-input, echo, and output-processing toggles.  POSIX
speeds, parity, line discipline, control characters, and drain timing are
reported as unsupported rather than emulated.
""" + Environment.NewLine;
}
