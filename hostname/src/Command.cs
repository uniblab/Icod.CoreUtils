namespace Icod.CoreUtils.HostName;
using System.Net;
using System.Net.Sockets;
using Icod.CommandFramework.CommandLine;
using Icod.CommandFramework.Diagnostics;

/// <summary>
/// Implements GNU-compatible <c>hostname</c> and prints the current host name.
/// </summary>
/// <remarks>
/// The implementation reports the host name through the BCL and uses controlled diagnostics on failure.
/// </remarks>
public static class Command {
	private const string PROGRAM = "hostname";
	private const string VERSION = "hostname (Icod.CoreUtils) 1.0";
	private enum DisplayKind { Basic, Alias, Domain, Fqdn, Ip, Short, Nis }

	/// <summary>
	/// Executes <c>hostname</c> synchronously with optional standard-stream substitution.
	/// </summary>
	/// <remarks>
	/// This compatibility entry point blocks on the TAP implementation. A <see langword="null"/> text stream selects the corresponding <see cref="Console"/> stream; caller-supplied streams remain caller-owned.
	/// </remarks>
	/// <param name="args">The command-line arguments, excluding the executable name.</param>
	/// <param name="stdin">The text reader to use as standard input, or <see langword="null"/> to use <see cref="Console.In"/>.</param>
	/// <param name="stdout">The text writer to use as standard output, or <see langword="null"/> to use <see cref="Console.Out"/>.</param>
	/// <param name="stderr">The text writer to use as standard error, or <see langword="null"/> to use <see cref="Console.Error"/>.</param>
	/// <returns>The GNU-compatible process exit status: zero for successful command execution and nonzero for a usage or operational failure.</returns>
	public static int Run( string[] args, TextReader? stdin = null, TextWriter? stdout = null, TextWriter? stderr = null ) =>
		RunAsync( args, stdin, stdout, stderr ).GetAwaiter().GetResult();
	/// <summary>
	/// Executes <c>hostname</c> asynchronously with optional injected standard streams.
	/// </summary>
	/// <remarks>
	/// A <see langword="null"/> text stream selects the corresponding <see cref="Console"/> stream. Caller-supplied streams remain caller-owned.
	/// </remarks>
	/// <param name="args">The command-line arguments, excluding the executable name.</param>
	/// <param name="stdin">The text reader to use as standard input, or <see langword="null"/> to use <see cref="Console.In"/>.</param>
	/// <param name="stdout">The text writer to use as standard output, or <see langword="null"/> to use <see cref="Console.Out"/>.</param>
	/// <param name="stderr">The text writer to use as standard error, or <see langword="null"/> to use <see cref="Console.Error"/>.</param>
	/// <param name="cancellationToken">The token used to cancel parsing, platform queries, and asynchronous I/O.</param>
	/// <returns>The GNU-compatible process exit status: zero for successful command execution and nonzero for a usage or operational failure.</returns>
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

	/// <summary>
	/// Executes <c>hostname</c> asynchronously using a complete shared command context.
	/// </summary>
	/// <remarks>
	/// The context carries text and optional binary standard streams, centralized diagnostics, and cancellation. The command does not dispose caller-owned standard streams.
	/// </remarks>
	/// <param name="args">The command-line arguments, excluding the executable name.</param>
	/// <param name="context">The command context that supplies standard streams, diagnostics, and cancellation.</param>
	/// <returns>The GNU-compatible process exit status: zero for successful command execution and nonzero for a usage or operational failure.</returns>
	/// <exception cref="ArgumentNullException"><paramref name="context"/> is <see langword="null"/>.</exception>
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
	private static async Task WriteHelpAsync( CommandContext context ) {
		const string text = """
Usage: hostname [OPTION]... [NAME]
Show or set the system's host name. Setting is not supported by this implementation.

  -a, --alias          display aliases
  -d, --domain         display DNS domain
  -f, --fqdn, --long   display fully qualified name
  -i, --ip-address     display resolved addresses
  -s, --short          display short host name
  -F, --file=FILE      read a name to set from FILE
  -y, --yp, --nis      display NIS domain (unsupported)
  -h, --help           display this help and exit
  -V, --version        output version information and exit
""";
		await context.StandardOutput.WriteAsync(
			text.ReplaceLineEndings( Environment.NewLine ).AsMemory(),
			context.CancellationToken
		).ConfigureAwait( false );
	}
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
