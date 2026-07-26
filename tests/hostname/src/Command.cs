namespace Icod.CoreUtils.HostName;
using System.Net;
using System.Net.Sockets;
using Icod.CoreUtils.Shared.CommandLine;
using Icod.CoreUtils.Shared.Diagnostics;

public static class Command {
	private const string PROGRAM = "hostname";
	private const string VERSION = "hostname (Icod.CoreUtils) 1.0";
	private enum DisplayKind { Basic, Alias, Domain, Fqdn, Ip, Short, Nis }

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
		var parser = CreateParser(
			new OptionDefinition( "alias", 'a', new[] { "alias" } ),
			new OptionDefinition( "domain", 'd', new[] { "domain" } ),
			new OptionDefinition( "file", 'F', new[] { "file" }, OptionValueArity.Required ),
			new OptionDefinition( "fqdn", 'f', new[] { "fqdn", "long" } ),
			new OptionDefinition( "help", 'h', new[] { "help" } ),
			new OptionDefinition( "ip", 'i', new[] { "ip-address" } ),
			new OptionDefinition( "node", 'n', new[] { "node" } ),
			new OptionDefinition( "short", 's', new[] { "short" } ),
			new OptionDefinition( "version", 'V', new[] { "version" } ),
			new OptionDefinition( "verbose", 'v', new[] { "verbose" } ),
			new OptionDefinition( "nis", 'y', new[] { "yp", "nis" } )
		);
		try {
			var result = parser.Parse( args );
			if ( await WriteParseErrorsAsync( result, context ).ConfigureAwait( false ) ) return 1;
			if ( result.HasOption( "help" ) ) { await WriteHelpAsync( context ).ConfigureAwait( false ); return 0; }
			if ( result.HasOption( "version" ) ) { await context.StandardOutput.WriteLineAsync( VERSION.AsMemory(), context.CancellationToken ).ConfigureAwait( false ); return 0; }
			if ( result.Operands.Count > 1 ) { await context.Diagnostics.ErrorAsync( $"extra operand '{result.Operands[1]}'", context.CancellationToken ).ConfigureAwait( false ); return 1; }
			var file = result.GetLastValue( "file" );
			if ( file is not null || result.Operands.Count == 1 ) {
				if ( file is not null ) _ = await ReadHostNameAsync( file, context.CancellationToken ).ConfigureAwait( false );
				await context.Diagnostics.ErrorAsync( "setting the host name is not supported by this implementation", context.CancellationToken ).ConfigureAwait( false );
				return 1;
			}
			var kind = DisplayKind.Basic;
			foreach ( var option in result.Options ) kind = option.Definition.Key switch { "alias" => DisplayKind.Alias, "domain" => DisplayKind.Domain, "fqdn" => DisplayKind.Fqdn, "ip" => DisplayKind.Ip, "short" => DisplayKind.Short, "node" or "nis" => DisplayKind.Nis, _ => kind };
			if ( kind == DisplayKind.Nis ) { await context.Diagnostics.ErrorAsync( "NIS/YP domain lookup is not supported on this platform", context.CancellationToken ).ConfigureAwait( false ); return 1; }
			var host = Dns.GetHostName();
			var output = await ResolveAsync( host, kind, context.CancellationToken ).ConfigureAwait( false );
			await context.StandardOutput.WriteLineAsync( output.AsMemory(), context.CancellationToken ).ConfigureAwait( false );
			return 0;
		} catch ( OperationCanceledException ) { return CommandExitCodes.Canceled; }
		catch ( Exception ex ) when ( ex is IOException or UnauthorizedAccessException or SocketException ) { await context.Diagnostics.ErrorAsync( ex.Message, context.CancellationToken ).ConfigureAwait( false ); return 1; }
	}
	private static async Task<string> ResolveAsync( string host, DisplayKind kind, CancellationToken token ) {
		if ( kind == DisplayKind.Basic ) return host;
		if ( kind == DisplayKind.Short ) return host.Split( '.', 2 )[0];
		try {
			if ( kind == DisplayKind.Ip ) { var addresses = await Dns.GetHostAddressesAsync( host ).WaitAsync( token ).ConfigureAwait( false ); return string.Join( " ", addresses.Where( x => !IPAddress.IsLoopback( x ) ).Select( x => x.ToString() ) ); }
			var entry = await Dns.GetHostEntryAsync( host ).WaitAsync( token ).ConfigureAwait( false );
			if ( kind == DisplayKind.Alias ) return string.Join( " ", entry.Aliases );
			if ( kind == DisplayKind.Domain ) { var dot = entry.HostName.IndexOf( '.' ); return dot < 0 ? string.Empty : entry.HostName[(dot + 1)..]; }
			return entry.HostName;
		} catch ( SocketException ) {
			if ( kind == DisplayKind.Domain || kind == DisplayKind.Alias || kind == DisplayKind.Ip ) return string.Empty;
			return host;
		}
	}
	private static async Task<string> ReadHostNameAsync( string file, CancellationToken token ) {
		var lines = await File.ReadAllLinesAsync( file, token ).ConfigureAwait( false );
		foreach ( var raw in lines ) { var line = raw.Split( '#', 2 )[0].Trim(); if ( line.Length > 0 ) return line.Split( (char[]?)null, StringSplitOptions.RemoveEmptyEntries )[0]; }
		throw new InvalidDataException( "host name file contains no host name" );
	}
	private static Task WriteHelpAsync( CommandContext c ) => c.StandardOutput.WriteAsync("Usage: hostname [OPTION]... [NAME]\nShow or set the system's host name. Setting is not supported by this implementation.\n\n  -a, --alias          display aliases\n  -d, --domain         display DNS domain\n  -f, --fqdn, --long   display fully qualified name\n  -i, --ip-address     display resolved addresses\n  -s, --short          display short host name\n  -F, --file=FILE      read a name to set from FILE\n  -y, --yp, --nis      display NIS domain (unsupported)\n  -h, --help           display this help and exit\n  -V, --version        output version information and exit\n".AsMemory(), c.CancellationToken);

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
