namespace Icod.CoreUtils.Arch;
using System.Runtime.InteropServices;
using Icod.CoreUtils.Shared.CommandLine;
using Icod.CoreUtils.Shared.Diagnostics;

public static class Command {
	private const string PROGRAM = "arch";
	private const string VERSION = "arch (Icod.CoreUtils) 1.0";

	public static int Run( string[] args, TextReader? stdin = null, TextWriter? stdout = null, TextWriter? stderr = null ) =>
		RunAsync( args, stdin, stdout, stderr ).GetAwaiter().GetResult();
	public static Task<int> RunAsync(
		string[] args,
		TextReader? stdin = null,
		TextWriter? stdout = null,
		TextWriter? stderr = null,
		CancellationToken cancellationToken = default
	) => RunAsync(
		args ?? Array.Empty<string>(),
		new CommandContext(
			PROGRAM,
			stdin ?? Console.In,
			stdout ?? Console.Out,
			stderr ?? Console.Error,
			cancellationToken: cancellationToken
		)
	);

	public static async Task<int> RunAsync( string[] args, CommandContext context ) {
		ArgumentNullException.ThrowIfNull( context );
		var parser = CreateParser( new OptionDefinition( "help", longNames: new[] { "help" } ), new OptionDefinition( "version", longNames: new[] { "version" } ) );
		try {
			var result = parser.Parse( args );
			if ( await WriteParseErrorsAsync( result, context ).ConfigureAwait( false ) )
				return 1;
			if ( result.HasOption( "help" ) ) {
				await context.StandardOutput.WriteAsync( "Usage: arch [OPTION]...\nPrint machine architecture.\n\n      --help     display this help and exit\n      --version  output version information and exit\n".AsMemory(), context.CancellationToken ).ConfigureAwait( false );
				return 0;
			}
			if ( result.HasOption( "version" ) ) {
				await context.StandardOutput.WriteLineAsync( VERSION.AsMemory(), context.CancellationToken ).ConfigureAwait( false );
				return 0;
			}
			if ( result.Operands.Count > 0 ) {
				await context.Diagnostics.ErrorAsync( $"extra operand '{result.Operands[ 0 ]}'", context.CancellationToken ).ConfigureAwait( false );
				return 1;
			}
			context.CancellationToken.ThrowIfCancellationRequested();
			await context.StandardOutput.WriteLineAsync( GetArchitecture().AsMemory(), context.CancellationToken ).ConfigureAwait( false );
			return 0;
		} catch ( OperationCanceledException ) { return CommandExitCodes.Canceled; }
	}
	internal static string GetArchitecture() => RuntimeInformation.OSArchitecture.ToString() switch {
		"X64" => "x86_64",
		"X86" => "i686",
		"Arm64" => "aarch64",
		"Arm" => "armv7l",
		"Armv6" => "armv6l",
		"Ppc64le" => "ppc64le",
		"S390x" => "s390x",
		"LoongArch64" => "loongarch64",
		"RiscV64" => "riscv64",
		"Wasm" => "wasm",
		var value => value.ToLowerInvariant()
	};

	private static OptionParser CreateParser( params OptionDefinition[] options ) => new(
		options,
		new OptionParserSettings {
			AllowLongOptionAbbreviations = true,
			Ordering = OptionOrdering.Permute
		}
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
