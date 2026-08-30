namespace Icod.CoreUtils.ReadLink;

using Icod.CommandFramework.CommandLine;
using Icod.CommandFramework.Diagnostics;
using Icod.CoreUtils.Shared.FileSystem.Traversal;
using Icod.Path;

/// <summary>Implements GNU-compatible <c>readlink</c> link inspection and canonicalization.</summary>
public static class Command {
	private const string PROGRAM = "readlink";
	private const string VERSION = "readlink (Icod.CoreUtils) 1.0";

	private enum CanonicalizationMode {
		None,
		AllowFinal,
		RequireExisting,
		AllowMissing,
	}

	/// <summary>Executes <c>readlink</c> synchronously with optional standard-stream substitution.</summary>
	/// <param name="args">The command-line arguments, excluding the executable name.</param>
	/// <param name="stdin">The standard input reader, or <see langword="null"/> for <see cref="Console.In"/>.</param>
	/// <param name="stdout">The standard output writer, or <see langword="null"/> for <see cref="Console.Out"/>.</param>
	/// <param name="stderr">The standard error writer, or <see langword="null"/> for <see cref="Console.Error"/>.</param>
	/// <returns>Zero when every operand succeeds; otherwise one.</returns>
	public static int Run(
		string[] args,
		TextReader? stdin = null,
		TextWriter? stdout = null,
		TextWriter? stderr = null
	) => RunAsync( args, stdin, stdout, stderr ).GetAwaiter().GetResult();

	/// <summary>Executes <c>readlink</c> asynchronously with optional standard-stream substitution.</summary>
	/// <param name="args">The command-line arguments, excluding the executable name.</param>
	/// <param name="stdin">The standard input reader, or <see langword="null"/> for <see cref="Console.In"/>.</param>
	/// <param name="stdout">The standard output writer, or <see langword="null"/> for <see cref="Console.Out"/>.</param>
	/// <param name="stderr">The standard error writer, or <see langword="null"/> for <see cref="Console.Error"/>.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>Zero when every operand succeeds; otherwise one.</returns>
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
		),
		new CanonicalPathResolver()
	);

	/// <summary>Executes <c>readlink</c> using a complete shared command context.</summary>
	/// <param name="args">The command-line arguments, excluding the executable name.</param>
	/// <param name="context">The command context.</param>
	/// <returns>Zero when every operand succeeds; otherwise one.</returns>
	public static Task<int> RunAsync( string[] args, CommandContext context ) =>
		RunAsync( args, context, new CanonicalPathResolver() )
	;

	/// <summary>Executes <c>readlink</c> using an injected canonical-path resolver.</summary>
	/// <param name="args">The command-line arguments, excluding the executable name.</param>
	/// <param name="context">The command context.</param>
	/// <param name="resolver">The canonical-path resolver.</param>
	/// <returns>Zero when every operand succeeds; otherwise one.</returns>
	public static async Task<int> RunAsync(
		string[] args,
		CommandContext context,
		CanonicalPathResolver resolver
	) {
		ArgumentNullException.ThrowIfNull( context );
		ArgumentNullException.ThrowIfNull( resolver );
		try {
			var result = CreateParser().Parse( args ?? Array.Empty<string>() );
			if ( await WriteParseErrorsAsync( result, context ).ConfigureAwait( false ) ) {
				return 1;
			}
			if ( result.HasOption( "help" ) ) {
				await WriteHelpAsync( context ).ConfigureAwait( false );
				return 0;
			}
			if ( result.HasOption( "version" ) ) {
				await context.StandardOutput.WriteLineAsync(
					VERSION.AsMemory(),
					context.CancellationToken
				).ConfigureAwait( false );
				return 0;
			}
			if ( 0 == result.Operands.Count ) {
				await context.Diagnostics.ErrorAsync(
					"missing operand",
					context.CancellationToken
				).ConfigureAwait( false );
				return 1;
			}
			var expansion = await PathnameOperandExpander.ExpandAsync(
				result.Operands,
				cancellationToken: context.CancellationToken
			).ConfigureAwait( false );
			var operands = expansion.Operands;

			var mode = CanonicalizationMode.None;
			var verbose = null != Environment.GetEnvironmentVariable( "POSIXLY_CORRECT" );
			foreach ( var option in result.Options ) {
				switch ( option.Definition.Key ) {
					case "canonicalize": mode = CanonicalizationMode.AllowFinal; break;
					case "canonicalize-existing": mode = CanonicalizationMode.RequireExisting; break;
					case "canonicalize-missing": mode = CanonicalizationMode.AllowMissing; break;
					case "quiet":
					case "silent-short": verbose = false; break;
					case "verbose": verbose = true; break;
				}
			}
			if ( null != Environment.GetEnvironmentVariable( "POSIXLY_CORRECT" ) ) {
				verbose = true;
			}
			var zero = result.HasOption( "zero" );
			var noNewline = result.HasOption( "no-newline" );
			if ( noNewline && 1 < operands.Count ) {
				await context.Diagnostics.ErrorAsync(
					"ignoring --no-newline with multiple arguments",
					context.CancellationToken
				).ConfigureAwait( false );
				noNewline = false;
			}
			var delimiter = noNewline ? string.Empty : zero ? "\0" : Environment.NewLine;
			var failed = false;
			foreach ( var operand in operands ) {
				context.CancellationToken.ThrowIfCancellationRequested();
				string? output;
				CanonicalPathFailure? failure;
				if (
					CanonicalizationMode.None == mode
					&& EndsWithDirectorySeparator( operand, resolver.Semantics )
				) {
					output = null;
					failure = new CanonicalPathFailure(
						CanonicalPathFailureCode.NotDirectory,
						operand,
						"the pathname does not designate a symbolic-link object"
					);
				} else if ( CanonicalizationMode.None == mode ) {
					var inspection = await resolver.InspectLinkAsync(
						operand,
						cancellationToken: context.CancellationToken
					).ConfigureAwait( false );
					if ( !inspection.Succeeded ) {
						output = null;
						failure = inspection.Failure;
					} else if ( inspection.IsReparsePoint && !inspection.IsSymbolicLink ) {
						output = null;
						failure = new CanonicalPathFailure(
							CanonicalPathFailureCode.UnsupportedReparsePoint,
							operand,
							"the reparse point does not expose supported symbolic-link semantics"
						);
					} else if ( !inspection.IsSymbolicLink ) {
						output = null;
						failure = new CanonicalPathFailure(
							CanonicalPathFailureCode.LinkTargetUnavailable,
							operand,
							"the pathname is not a symbolic link"
						);
					} else if ( null == inspection.Target ) {
						output = null;
						failure = new CanonicalPathFailure(
							CanonicalPathFailureCode.LinkTargetUnavailable,
							operand,
							"the symbolic-link target is unavailable"
						);
					} else {
						output = inspection.Target;
						failure = null;
					}
				} else {
					var resolution = await resolver.ResolvePhysicalAsync(
						operand,
						CreateResolutionOptions( mode, operand, resolver.Semantics ),
						context.CancellationToken
					).ConfigureAwait( false );
					output = resolution.Path;
					failure = resolution.Failure;
				}
				if ( null == output ) {
					failed = true;
					if ( verbose ) {
						await WriteFailureAsync( operand, failure, context ).ConfigureAwait( false );
					}
					continue;
				}
				await context.StandardOutput.WriteAsync(
					string.Concat( output, delimiter ).AsMemory(),
					context.CancellationToken
				).ConfigureAwait( false );
			}
			return failed ? 1 : 0;
		} catch ( OperationCanceledException ) {
			return CommandExitCodes.Canceled;
		} catch ( Exception exception ) when (
			exception is IOException
			or UnauthorizedAccessException
			or ArgumentException
			or NotSupportedException
		) {
			await context.Diagnostics.ErrorAsync(
				exception.Message,
				context.CancellationToken
			).ConfigureAwait( false );
			return 1;
		}
	}

	private static CanonicalPathResolutionOptions CreateResolutionOptions(
		CanonicalizationMode mode,
		string operand,
		PathPlatformSemantics semantics
	) => new() {
		MissingComponentPolicy = mode switch {
			CanonicalizationMode.RequireExisting => MissingPathComponentPolicy.RequireExisting,
			CanonicalizationMode.AllowMissing => MissingPathComponentPolicy.AllowMissingSuffix,
			_ => MissingPathComponentPolicy.AllowFinalComponent,
		},
		RequireFinalDirectory = CanonicalizationMode.RequireExisting == mode
			&& EndsWithDirectorySeparator( operand, semantics )
	};

	private static bool EndsWithDirectorySeparator(
		string value,
		PathPlatformSemantics semantics
	) => 0 < value.Length && semantics.IsDirectorySeparator( value[^1] );

	private static OptionParser CreateParser() => new(
		new[] {
			new OptionDefinition( "canonicalize", 'f', new[] { "canonicalize" } ),
			new OptionDefinition( "canonicalize-existing", 'e', new[] { "canonicalize-existing" } ),
			new OptionDefinition( "canonicalize-missing", 'm', new[] { "canonicalize-missing" } ),
			new OptionDefinition( "no-newline", 'n', new[] { "no-newline" } ),
			new OptionDefinition( "quiet", 'q', new[] { "quiet", "silent" } ),
			new OptionDefinition( "silent-short", 's' ),
			new OptionDefinition( "verbose", 'v', new[] { "verbose" } ),
			new OptionDefinition( "zero", 'z', new[] { "zero" } ),
			new OptionDefinition( "help", longNames: new[] { "help" } ),
			new OptionDefinition( "version", longNames: new[] { "version" } ),
		},
		new OptionParserSettings {
			AllowLongOptionAbbreviations = true,
			Ordering = OptionOrdering.Permute
		}
	);

	private static async Task WriteFailureAsync(
		string operand,
		CanonicalPathFailure? failure,
		CommandContext context
	) {
		var detail = failure?.Message ?? "the pathname could not be resolved";
		await context.Diagnostics.ErrorAsync(
			$"'{operand}': {detail}",
			context.CancellationToken
		).ConfigureAwait( false );
	}

	private static async Task<bool> WriteParseErrorsAsync(
		OptionParseResult result,
		CommandContext context
	) {
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

	private static async Task WriteHelpAsync( CommandContext context ) {
		const string text = """
Usage: readlink [OPTION]... FILE...
Print value of a symbolic link or canonical file name.
  -f, --canonicalize            canonicalize by following every symlink;
                                  all but the last component must exist
  -e, --canonicalize-existing   canonicalize by following every symlink;
                                  every component must exist
  -m, --canonicalize-missing    canonicalize by following every symlink;
                                  no component need exist
  -n, --no-newline              do not output the trailing delimiter
  -q, -s, --quiet, --silent     suppress most error messages
  -v, --verbose                 report error messages
  -z, --zero                    end each output line with NUL, not newline
      --help                    display this help and exit
      --version                 output version information and exit
""";
		await context.StandardOutput.WriteAsync(
			text.ReplaceLineEndings( Environment.NewLine ).AsMemory(),
			context.CancellationToken
		).ConfigureAwait( false );
	}
}
