namespace Icod.CoreUtils.ID;

using System.Text;
using Icod.CommandFramework.CommandLine;
using Icod.CommandFramework.Diagnostics;
using Icod.CommandFramework.Platform;

/// <summary>Implements the <c>id</c> command.</summary>
public static class Command {
	private const string ProgramName = "id";
	private const string Version = "id (Icod.CoreUtils) 1.0";

	/// <summary>
	/// Executes <c>id</c> synchronously with optional standard-stream substitution.
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
	/// Executes <c>id</c> asynchronously with optional injected standard streams.
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
			ProgramName,
			stdin ?? Console.In,
			stdout ?? Console.Out,
			stderr ?? Console.Error,
			cancellationToken: cancellationToken
		)
	);

	/// <summary>
	/// Executes <c>id</c> asynchronously using a complete shared command context.
	/// </summary>
	/// <remarks>
	/// The context carries text and optional binary standard streams, centralized diagnostics, and cancellation. The command does not dispose caller-owned standard streams.
	/// </remarks>
	/// <param name="args">The command-line arguments, excluding the executable name.</param>
	/// <param name="context">The command context that supplies standard streams, diagnostics, and cancellation.</param>
	/// <param name="provider">The injectable identity provider; <see langword="null"/> selects the system implementation when supported by this overload.</param>
	/// <returns>The GNU-compatible process exit status: zero for successful command execution and nonzero for a usage or operational failure.</returns>
	/// <exception cref="ArgumentNullException"><paramref name="context"/> is <see langword="null"/>.</exception>
	public static async Task<int> RunAsync( string[] args, CommandContext context, IIdentityProvider? provider = null ) {
		ArgumentNullException.ThrowIfNull( context );
		provider ??= SystemIdentityProvider.Instance;
		var parser = CreateParser(
			new OptionDefinition( "ignore", 'a' ),
			new OptionDefinition( "context", 'Z', new[] { "context" } ),
			new OptionDefinition( "group", 'g', new[] { "group" } ),
			new OptionDefinition( "groups", 'G', new[] { "groups" } ),
			new OptionDefinition( "name", 'n', new[] { "name" } ),
			new OptionDefinition( "real", 'r', new[] { "real" } ),
			new OptionDefinition( "user", 'u', new[] { "user" } ),
			new OptionDefinition( "zero", 'z', new[] { "zero" } ),
			new OptionDefinition( "help", null, new[] { "help" } ),
			new OptionDefinition( "version", null, new[] { "version" } )
		);
		try {
			var result = parser.Parse( args );
			if ( await WriteParseErrorsAsync( result, context ).ConfigureAwait( false ) ) {
				return CommandExitCodes.Failure;
			}
			if ( result.HasOption( "help" ) ) {
				await WriteHelpAsync( context ).ConfigureAwait( false );
				return CommandExitCodes.Success;
			}
			if ( result.HasOption( "version" ) ) {
				await context.StandardOutput.WriteLineAsync(
					Version.AsMemory(),
					context.CancellationToken
				).ConfigureAwait( false );
				return CommandExitCodes.Success;
			}

			var choices = new[] { "context", "group", "groups", "user" }.Count( result.HasOption );
			if ( 1 < choices ) {
				await context.Diagnostics.ErrorAsync( "cannot print \"only\" of more than one choice", context.CancellationToken ).ConfigureAwait( false );
				return CommandExitCodes.Failure;
			}
			var selectedIdentity = result.HasOption( "group" ) || result.HasOption( "groups" ) || result.HasOption( "user" );
			if ( ( result.HasOption( "name" ) || result.HasOption( "real" ) ) && !selectedIdentity ) {
				await context.Diagnostics.ErrorAsync( "printing only names or real IDs requires -u, -g, or -G", context.CancellationToken ).ConfigureAwait( false );
				return CommandExitCodes.Failure;
			}
			if ( result.HasOption( "zero" ) && 0 == choices ) {
				await context.Diagnostics.ErrorAsync( "option --zero not permitted in default format", context.CancellationToken ).ConfigureAwait( false );
				return CommandExitCodes.Failure;
			}

			var current = await provider.GetCurrentAsync( context.CancellationToken ).ConfigureAwait( false );
			if ( result.HasOption( "context" ) ) {
				if ( string.IsNullOrEmpty( current.SecurityContext ) ) {
					await context.Diagnostics.ErrorAsync( "--context (-Z) works only on an SELinux-enabled kernel", context.CancellationToken ).ConfigureAwait( false );
					return CommandExitCodes.Failure;
				}
				if ( 0 < result.Operands.Count ) {
					await context.Diagnostics.ErrorAsync( "--context cannot be used with a user operand", context.CancellationToken ).ConfigureAwait( false );
					return CommandExitCodes.Failure;
				}
				await WriteScalarAsync( current.SecurityContext, result.HasOption( "zero" ), context ).ConfigureAwait( false );
				return CommandExitCodes.Success;
			}

			var exitCode = CommandExitCodes.Success;
			if ( 0 == result.Operands.Count ) {
				await WriteIdentityAsync( null, current, result, context, false ).ConfigureAwait( false );
				return CommandExitCodes.Success;
			}

			var multipleUsers = 1 < result.Operands.Count;
			foreach ( var userName in result.Operands ) {
				context.CancellationToken.ThrowIfCancellationRequested();
				var user = await provider.FindUserAsync( userName, context.CancellationToken ).ConfigureAwait( false );
				if ( null == user ) {
					user = await provider.FindUserByIdAsync(
						userName,
						context.CancellationToken
					).ConfigureAwait( false );
				}
				if ( null == user ) {
					await context.Diagnostics.ErrorAsync( $"'{userName}': no such user", context.CancellationToken ).ConfigureAwait( false );
					exitCode = CommandExitCodes.Failure;
					continue;
				}
				await WriteIdentityAsync( user, current, result, context, multipleUsers ).ConfigureAwait( false );
			}
			return exitCode;
		} catch ( OperationCanceledException ) {
			return CommandExitCodes.Canceled;
		} catch ( Exception ex ) when ( ex is IOException or UnauthorizedAccessException ) {
			await context.Diagnostics.ErrorAsync( ex.Message, context.CancellationToken ).ConfigureAwait( false );
			return CommandExitCodes.Failure;
		}
	}

	private static async Task WriteIdentityAsync(
		UserIdentity? namedUser,
		ProcessIdentity current,
		OptionParseResult result,
		CommandContext context,
		bool multipleUsers
	) {
		var useNames = result.HasOption( "name" );
		var useReal = result.HasOption( "real" );
		var zero = result.HasOption( "zero" );
		if ( result.HasOption( "user" ) ) {
			var user = namedUser ?? (
				( useReal )
					? current.RealUser
					: current.EffectiveUser
			);
			await WriteScalarAsync(
				( useNames )
					? user.Name
					: user.Id,
				zero,
				context
			).ConfigureAwait( false );
			return;
		}
		if ( result.HasOption( "group" ) ) {
			var group = ( null != namedUser )
				? namedUser.PrimaryGroup
				: ( useReal )
					? current.RealGroup
					: current.EffectiveGroup
			;
			await WriteScalarAsync(
				( useNames )
					? group.Name
					: group.Id,
				zero,
				context
			).ConfigureAwait( false );
			return;
		}
		if ( result.HasOption( "groups" ) ) {
			var source = ( null != namedUser )
				? namedUser.Groups.Prepend( namedUser.PrimaryGroup )
				: current.Groups.Prepend( current.EffectiveGroup ).Prepend( current.RealGroup )
			;
			var values = source
				.DistinctBy( group => group.Id )
				.Select(
					group => ( useNames )
						? group.Name
						: group.Id
				)
				.ToArray();
			if ( zero ) {
				await context.StandardOutput.WriteAsync(
					string.Join( '\0', values ).AsMemory(),
					context.CancellationToken
				).ConfigureAwait( false );
				await context.StandardOutput.WriteAsync(
					"\0".AsMemory(),
					context.CancellationToken
				).ConfigureAwait( false );
				if ( multipleUsers ) {
					await context.StandardOutput.WriteAsync(
						"\0".AsMemory(),
						context.CancellationToken
					).ConfigureAwait( false );
				}
			} else {
				await context.StandardOutput.WriteLineAsync(
					string.Join( ' ', values ).AsMemory(),
					context.CancellationToken
				).ConfigureAwait( false );
			}
			return;
		}

		var output = FormatDefault( namedUser, current );
		await context.StandardOutput.WriteLineAsync( output.AsMemory(), context.CancellationToken ).ConfigureAwait( false );
	}

	private static string FormatDefault( UserIdentity? namedUser, ProcessIdentity current ) {
		if ( null != namedUser ) {
			var groups = namedUser.Groups
				.Prepend( namedUser.PrimaryGroup )
				.DistinctBy( group => group.Id );
			return $"uid={Format( namedUser.Id, namedUser.Name )} gid={Format( namedUser.PrimaryGroup.Id, namedUser.PrimaryGroup.Name )} groups={string.Join( ',', groups.Select( group => Format( group.Id, group.Name ) ) )}";
		}
		var builder = new StringBuilder();
		builder.Append( "uid=" ).Append( Format( current.RealUser.Id, current.RealUser.Name ) );
		builder.Append( " gid=" ).Append( Format( current.RealGroup.Id, current.RealGroup.Name ) );
		if ( current.RealUser.Id != current.EffectiveUser.Id ) {
			builder.Append( " euid=" ).Append(
				Format( current.EffectiveUser.Id, current.EffectiveUser.Name )
			);
		}
		if ( current.RealGroup.Id != current.EffectiveGroup.Id ) {
			builder.Append( " egid=" ).Append(
				Format( current.EffectiveGroup.Id, current.EffectiveGroup.Name )
			);
		}
		builder.Append( " groups=" ).Append(
			string.Join(
				',',
				current.Groups
					.Prepend( current.EffectiveGroup )
					.DistinctBy( group => group.Id )
					.Select( group => Format( group.Id, group.Name ) )
			)
		);
		if ( !string.IsNullOrEmpty( current.SecurityContext ) ) {
			builder.Append( " context=" ).Append( current.SecurityContext );
		}
		return builder.ToString();
	}

	private static string Format( string id, string name ) => ( string.IsNullOrEmpty( name ) )
		? id
		: $"{id}({name})"
	;

	private static async Task WriteScalarAsync( string value, bool zero, CommandContext context ) {
		if ( zero ) {
			await context.StandardOutput.WriteAsync(
				System.String.Concat( value, '\0' ).AsMemory(),
				context.CancellationToken
			).ConfigureAwait( false );
		} else {
			await context.StandardOutput.WriteLineAsync(
				value.AsMemory(),
				context.CancellationToken
			).ConfigureAwait( false );
		}
	}

	private static async Task WriteHelpAsync( CommandContext context ) {
		const string text = """
Usage: id [OPTION]... [USER]...
Print user and group information for each specified USER, or for the current process.

  -a                       ignored, for compatibility with other versions
  -Z, --context            print only the security context
  -g, --group              print only the effective group ID
  -G, --groups             print all group IDs
  -n, --name               print a name instead of a number, for -ugG
  -r, --real               print the real ID instead of the effective ID, for -ugG
  -u, --user               print only the effective user ID
  -z, --zero               delimit entries with NUL, not whitespace
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
		new OptionParserSettings {
			AllowLongOptionAbbreviations = true,
			Ordering = OptionOrdering.Permute
		}
	);

	private static async Task<bool> WriteParseErrorsAsync( OptionParseResult result, CommandContext context ) {
		if ( result.IsSuccess ) {
			return false;
		}
		foreach ( var error in result.Errors ) {
			await context.StandardError.WriteLineAsync(
				OptionDiagnosticFormatter.Format( context.ProgramName, error ).AsMemory(),
				context.CancellationToken
			).ConfigureAwait( false );
		}
		return true;
	}
}
