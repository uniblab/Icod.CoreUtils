namespace Icod.CoreUtils.UName;

using Icod.CoreUtils.Shared.CommandLine;
using Icod.CoreUtils.Shared.Diagnostics;
using Icod.CoreUtils.Shared.Platform;

/// <summary>Implements the <c>uname</c> command.</summary>
public static class Command {
	private const string ProgramName = "uname";
	private const string Version = "uname (Icod.CoreUtils) 1.0";

	/// <summary>Runs the command synchronously.</summary>
	public static int Run( string[] args, TextReader? stdin = null, TextWriter? stdout = null, TextWriter? stderr = null ) =>
		RunAsync( args, stdin, stdout, stderr ).GetAwaiter().GetResult();

	/// <summary>Runs the command asynchronously.</summary>
	public static Task<int> RunAsync(
		string[] args,
		TextReader? stdin = null,
		TextWriter? stdout = null,
		TextWriter? stderr = null,
		CancellationToken cancellationToken = default
	) => RunAsync(
		args ?? Array.Empty<string>(),
		new CommandContext(
			ProgramName,
			stdin ?? Console.In,
			stdout ?? Console.Out,
			stderr ?? Console.Error,
			cancellationToken: cancellationToken
		)
	);

	/// <summary>Runs the command asynchronously with an injected context and provider.</summary>
	public static async Task<int> RunAsync(
		string[] args,
		CommandContext context,
		ISystemInformationProvider? provider = null
	) {
		ArgumentNullException.ThrowIfNull( context );
		provider ??= SystemInformationProvider.Instance;
		var parser = CreateParser(
			new OptionDefinition( "all", 'a', new[] { "all" } ),
			new OptionDefinition( "kernel-name", 's', new[] { "kernel-name" } ),
			new OptionDefinition( "nodename", 'n', new[] { "nodename" } ),
			new OptionDefinition( "kernel-release", 'r', new[] { "kernel-release" } ),
			new OptionDefinition( "kernel-version", 'v', new[] { "kernel-version" } ),
			new OptionDefinition( "machine", 'm', new[] { "machine" } ),
			new OptionDefinition( "processor", 'p', new[] { "processor" } ),
			new OptionDefinition( "hardware-platform", 'i', new[] { "hardware-platform" } ),
			new OptionDefinition( "operating-system", 'o', new[] { "operating-system" } ),
			new OptionDefinition( "help", null, new[] { "help" } ),
			new OptionDefinition( "version", null, new[] { "version" } )
		);
		try {
			var result = parser.Parse( args );
			if ( await WriteParseErrorsAsync( result, context ).ConfigureAwait( false ) )
				return CommandExitCodes.Failure;
			if ( result.HasOption( "help" ) ) {
				await WriteHelpAsync( context ).ConfigureAwait( false );
				return CommandExitCodes.Success;
			}
			if ( result.HasOption( "version" ) ) {
				await context.StandardOutput.WriteLineAsync( Version.AsMemory(), context.CancellationToken ).ConfigureAwait( false );
				return CommandExitCodes.Success;
			}
			if ( 0 < result.Operands.Count ) {
				await context.Diagnostics.ErrorAsync( $"extra operand '{result.Operands[ 0 ]}'", context.CancellationToken ).ConfigureAwait( false );
				return CommandExitCodes.Failure;
			}

			var information = await provider.GetAsync( context.CancellationToken ).ConfigureAwait( false );
			var all = result.HasOption( "all" );
			var noSelection = !all && !result.Options.Any( option => IsInformationOption( option.Definition.Key ) );
			var fields = new List<string>();
			if ( all || noSelection || result.HasOption( "kernel-name" ) )
				fields.Add( information.KernelName );
			if ( all || result.HasOption( "nodename" ) )
				fields.Add( information.NodeName );
			if ( all || result.HasOption( "kernel-release" ) )
				fields.Add( information.KernelRelease );
			if ( all || result.HasOption( "kernel-version" ) )
				fields.Add( information.KernelVersion );
			if ( all || result.HasOption( "machine" ) )
				fields.Add( information.Machine );
			if ( ( !all && result.HasOption( "processor" ) ) || ( all && !IsUnknown( information.Processor ) ) )
				fields.Add( information.Processor );
			if ( ( !all && result.HasOption( "hardware-platform" ) ) || ( all && !IsUnknown( information.HardwarePlatform ) ) )
				fields.Add( information.HardwarePlatform );
			if ( all || result.HasOption( "operating-system" ) )
				fields.Add( information.OperatingSystem );
			await context.StandardOutput.WriteLineAsync( string.Join( ' ', fields ).AsMemory(), context.CancellationToken ).ConfigureAwait( false );
			return CommandExitCodes.Success;
		} catch ( OperationCanceledException ) {
			return CommandExitCodes.Canceled;
		} catch ( Exception ex ) when ( ex is IOException or UnauthorizedAccessException ) {
			await context.Diagnostics.ErrorAsync( ex.Message, context.CancellationToken ).ConfigureAwait( false );
			return CommandExitCodes.Failure;
		}
	}

	private static bool IsInformationOption( string key ) => key is
		"kernel-name" or "nodename" or "kernel-release" or "kernel-version" or
		"machine" or "processor" or "hardware-platform" or "operating-system";
	private static bool IsUnknown( string value ) => string.Equals( value, "unknown", StringComparison.OrdinalIgnoreCase );

	private static async Task WriteHelpAsync( CommandContext context ) {
		const string text = """
Usage: uname [OPTION]...
Print certain system information.  With no OPTION, same as -s.

  -a, --all                print all information, omitting -p and -i if unknown
  -s, --kernel-name        print the kernel name
  -n, --nodename           print the network node hostname
  -r, --kernel-release     print the kernel release
  -v, --kernel-version     print the kernel version
  -m, --machine            print the machine hardware name
  -p, --processor          print the processor type (non-portable)
  -i, --hardware-platform  print the hardware platform (non-portable)
  -o, --operating-system   print the operating system
      --help               display this help and exit
      --version            output version information and exit
""";
		await context.StandardOutput.WriteAsync(
			text.ReplaceLineEndings( Environment.NewLine ).AsMemory(),
			context.CancellationToken
		).ConfigureAwait( false );
	}

	private static OptionParser CreateParser( params OptionDefinition[] options ) => new(
		options,
		new OptionParserSettings { AllowLongOptionAbbreviations = true, Ordering = OptionOrdering.Permute }
	);

	private static async Task<bool> WriteParseErrorsAsync( OptionParseResult result, CommandContext context ) {
		if ( result.IsSuccess )
			return false;
		foreach ( var error in result.Errors ) {
			await context.StandardError.WriteLineAsync(
				OptionDiagnosticFormatter.Format( context.ProgramName, error ).AsMemory(),
				context.CancellationToken
			).ConfigureAwait( false );
		}
		return true;
	}
}
