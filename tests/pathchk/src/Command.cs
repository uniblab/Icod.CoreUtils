namespace Icod.CoreUtils.PathChk;
using System.Text;
using Icod.CoreUtils.Shared.CommandLine;
using Icod.CoreUtils.Shared.Diagnostics;

public static class Command {
	private const string PROGRAM = "pathchk";
	private const string VERSION = "pathchk (Icod.CoreUtils) 1.0";

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
		var parser = new OptionParser(new[] {
			new OptionDefinition( "posix", 'p', new[] { "posix" } ),
			new OptionDefinition( "leading", 'P', new[] { "leading-hyphen" } ),
			new OptionDefinition( "portability", longNames: new[] { "portability" } ),
			new OptionDefinition( "help", longNames: new[] { "help" } ),
			new OptionDefinition( "version", longNames: new[] { "version" } )
		}, new OptionParserSettings { AllowLongOptionAbbreviations = true, Ordering = OptionOrdering.RequireOrder });
		try {
			var result = parser.Parse( args );
			if ( await WriteParseErrorsAsync( result, context ).ConfigureAwait( false ) ) return 1;
			if ( result.HasOption( "help" ) ) { await WriteHelpAsync( context ).ConfigureAwait( false ); return 0; }
			if ( result.HasOption( "version" ) ) { await context.StandardOutput.WriteLineAsync( VERSION.AsMemory(), context.CancellationToken ).ConfigureAwait( false ); return 0; }
			if ( result.Operands.Count == 0 ) { await context.Diagnostics.ErrorAsync( "missing operand", context.CancellationToken ).ConfigureAwait( false ); return 1; }
			var portable = result.HasOption( "posix" ) || result.HasOption( "portability" );
			var leading = result.HasOption( "leading" ) || result.HasOption( "portability" );
			var failed = false;
			foreach ( var path in result.Operands ) failed |= !await ValidateAsync( path, portable, leading, context ).ConfigureAwait( false );
			return failed ? 1 : 0;
		} catch ( OperationCanceledException ) { return CommandExitCodes.Canceled; }
	}
	private static async Task<bool> ValidateAsync( string path, bool portable, bool leading, CommandContext context ) {
		context.CancellationToken.ThrowIfCancellationRequested();
		var valid = true;
		async Task Bad( string text ) { valid = false; await context.Diagnostics.ErrorAsync( text, context.CancellationToken ).ConfigureAwait( false ); }
		if ( path.Length == 0 ) await Bad( "empty file name" ).ConfigureAwait( false );
		var components = path.Split( '/', StringSplitOptions.RemoveEmptyEntries );
		if ( leading ) foreach ( var component in components ) if ( component.Length > 0 && component[0] == '-' ) await Bad( $"leading '-' in a component of file name '{path}'" ).ConfigureAwait( false );
		if ( portable ) {
			if ( Encoding.UTF8.GetByteCount( path ) > 255 ) await Bad( $"limit 255 exceeded by length {Encoding.UTF8.GetByteCount(path)} of file name '{path}'" ).ConfigureAwait( false );
			foreach ( var component in components ) if ( Encoding.UTF8.GetByteCount( component ) > 14 ) await Bad( $"limit 14 exceeded by length {Encoding.UTF8.GetByteCount(component)} of file name component '{component}'" ).ConfigureAwait( false );
			foreach ( var ch in path ) if ( !IsPortable( ch ) ) { await Bad( $"nonportable character '{ch}' in file name '{path}'" ).ConfigureAwait( false ); break; }
		} else {
			var pathLimit = OperatingSystem.IsWindows() ? 32767 : 4095;
			if ( Encoding.UTF8.GetByteCount( path ) > pathLimit ) await Bad( $"file name '{path}' is too long" ).ConfigureAwait( false );
			foreach ( var component in components ) if ( Encoding.UTF8.GetByteCount( component ) > 255 ) await Bad( $"file name component '{component}' is too long" ).ConfigureAwait( false );
			if ( path.IndexOf( '\0' ) >= 0 ) await Bad( "file name contains a null character" ).ConfigureAwait( false );
			try { _ = Path.GetFullPath( path.Length == 0 ? "." : path ); } catch ( Exception ex ) when ( ex is ArgumentException or NotSupportedException or PathTooLongException ) { await Bad( ex.Message ).ConfigureAwait( false ); }
		}
		return valid;
	}
	private static bool IsPortable( char ch ) => ch == '/' || ch == '.' || ch == '_' || ch == '-' || ch is >= '0' and <= '9' || ch is >= 'A' and <= 'Z' || ch is >= 'a' and <= 'z';
	private static Task WriteHelpAsync( CommandContext c ) => c.StandardOutput.WriteAsync("Usage: pathchk [OPTION]... NAME...\nDiagnose invalid or nonportable file names.\n\n  -p                 check for most POSIX systems\n  -P                 check for empty names and leading '-' components\n      --portability  check both -p and -P\n      --help         display this help and exit\n      --version      output version information and exit\n".AsMemory(), c.CancellationToken);

	private static OptionParser CreateParser( params OptionDefinition[] options ) => new(
		options,
		new OptionParserSettings {
			AllowLongOptionAbbreviations = true,
			Ordering = OptionOrdering.Permute
		}
	);
	private static async Task<bool> WriteParseErrorsAsync( OptionParseResult result, CommandContext context ) {
		if ( result.IsSuccess ) return false;
		foreach ( var error in result.Errors ) {
			await context.StandardError.WriteLineAsync(
				OptionDiagnosticFormatter.Format( context.ProgramName, error ).AsMemory(),
				context.CancellationToken
			).ConfigureAwait( false );
		}
		return true;
	}

}
