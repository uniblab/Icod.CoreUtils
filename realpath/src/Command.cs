namespace Icod.CoreUtils.RealPath;

using Icod.CoreUtils.Shared.CommandLine;
using Icod.CoreUtils.Shared.Diagnostics;
using Icod.Path;

/// <summary>Implements GNU-compatible <c>realpath</c> canonical pathname output.</summary>
public static class Command {
	private const string PROGRAM = "realpath";
	private const string VERSION = "realpath (Icod.CoreUtils) 1.0";

	private enum ExistenceMode {
		AllowFinal,
		RequireExisting,
		AllowMissing,
	}

	private enum ResolutionMode {
		Physical,
		Logical,
		NoLinks,
	}

	/// <summary>Executes <c>realpath</c> synchronously with optional standard-stream substitution.</summary>
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

	/// <summary>Executes <c>realpath</c> asynchronously with optional standard-stream substitution.</summary>
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

	/// <summary>Executes <c>realpath</c> using a complete shared command context.</summary>
	/// <param name="args">The command-line arguments, excluding the executable name.</param>
	/// <param name="context">The command context.</param>
	/// <returns>Zero when every operand succeeds; otherwise one.</returns>
	public static Task<int> RunAsync( string[] args, CommandContext context ) =>
		RunAsync( args, context, new CanonicalPathResolver() )
	;

	/// <summary>Executes <c>realpath</c> using an injected canonical-path resolver.</summary>
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

			var existence = ExistenceMode.AllowFinal;
			var resolution = ResolutionMode.Physical;
			foreach ( var option in result.Options ) {
				switch ( option.Definition.Key ) {
					case "canonicalize": existence = ExistenceMode.AllowFinal; break;
					case "canonicalize-existing": existence = ExistenceMode.RequireExisting; break;
					case "canonicalize-missing": existence = ExistenceMode.AllowMissing; break;
					case "logical": resolution = ResolutionMode.Logical; break;
					case "physical": resolution = ResolutionMode.Physical; break;
					case "strip": resolution = ResolutionMode.NoLinks; break;
				}
			}
			var quiet = result.HasOption( "quiet" );
			var delimiter = result.HasOption( "zero" ) ? "\0" : Environment.NewLine;
			var relativeToText = result.GetLastValue( "relative-to" );
			var relativeBaseText = result.GetLastValue( "relative-base" );
			if ( null == relativeToText && null != relativeBaseText ) {
				relativeToText = relativeBaseText;
			}

			string? relativeTo = null;
			string? relativeBase = null;
			if ( null != relativeToText ) {
				var relativeToResult = await ResolveAsync(
					relativeToText,
					existence,
					resolution,
					resolver,
					context.CancellationToken,
					requireDirectory: ExistenceMode.RequireExisting == existence
				).ConfigureAwait( false );
				if ( !relativeToResult.Succeeded ) {
					await WriteFailureAsync(
						relativeToText,
						relativeToResult.Failure,
						context
					).ConfigureAwait( false );
					return 1;
				}
				relativeTo = relativeToResult.Path;
			}
			if ( null != relativeBaseText ) {
				var relativeBaseResult = await ResolveAsync(
					relativeBaseText,
					existence,
					resolution,
					resolver,
					context.CancellationToken,
					requireDirectory: ExistenceMode.RequireExisting == existence
				).ConfigureAwait( false );
				if ( !relativeBaseResult.Succeeded ) {
					await WriteFailureAsync(
						relativeBaseText,
						relativeBaseResult.Failure,
						context
					).ConfigureAwait( false );
					return 1;
				}
				relativeBase = relativeBaseResult.Path;
			}
			if ( null != relativeTo && null != relativeBase ) {
				var relativeToWithinBase = resolver.EvaluateContainment(
					relativeBase,
					relativeTo
				);
				if ( !relativeToWithinBase.Succeeded ) {
					await WriteFailureAsync(
						relativeToText!,
						relativeToWithinBase.Failure,
						context
					).ConfigureAwait( false );
					return 1;
				}
				if ( !relativeToWithinBase.IsContained ) {
					relativeTo = null;
					relativeBase = null;
				}
			}

			var failed = false;
			foreach ( var operand in result.Operands ) {
				context.CancellationToken.ThrowIfCancellationRequested();
				var canonical = await ResolveAsync(
					operand,
					existence,
					resolution,
					resolver,
					context.CancellationToken,
					requireDirectory: ExistenceMode.RequireExisting == existence
						&& EndsWithDirectorySeparator( operand, resolver.Semantics )
				).ConfigureAwait( false );
				if ( !canonical.Succeeded ) {
					failed = true;
					if ( !quiet ) {
						await WriteFailureAsync( operand, canonical.Failure, context ).ConfigureAwait( false );
					}
					continue;
				}
				var output = canonical.Path!;
				if ( null != relativeTo ) {
					var shouldRelativize = true;
					if ( null != relativeBase ) {
						var containment = resolver.EvaluateContainment( relativeBase, output );
						if ( !containment.Succeeded ) {
							failed = true;
							if ( !quiet ) {
								await WriteFailureAsync( operand, containment.Failure, context ).ConfigureAwait( false );
							}
							continue;
						}
						shouldRelativize = containment.IsContained;
					}
					if ( shouldRelativize ) {
						var relative = resolver.GetRelativePath( relativeTo, output );
						if ( relative.Succeeded ) {
							output = relative.Path!;
						}
					}
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

	private static async ValueTask<CanonicalPathResult> ResolveAsync(
		string operand,
		ExistenceMode existence,
		ResolutionMode resolution,
		CanonicalPathResolver resolver,
		CancellationToken cancellationToken,
		bool requireDirectory
	) {
		var missingPolicy = existence switch {
			ExistenceMode.RequireExisting => MissingPathComponentPolicy.RequireExisting,
			ExistenceMode.AllowMissing => MissingPathComponentPolicy.AllowMissingSuffix,
			_ => MissingPathComponentPolicy.AllowFinalComponent,
		};
		if (
			ResolutionMode.NoLinks == resolution
			&& ExistenceMode.AllowMissing == existence
			&& !requireDirectory
		) {
			return resolver.NormalizeLexically( operand );
		}

		var input = operand;
		if ( ResolutionMode.Logical == resolution ) {
			var noLinks = await resolver.ResolvePhysicalAsync(
				operand,
				new CanonicalPathResolutionOptions {
					MissingComponentPolicy = missingPolicy,
					FollowSymbolicLinks = false,
					RequireFinalDirectory = requireDirectory,
					RejectUnsupportedFinalReparsePoint = false,
				},
				cancellationToken
			).ConfigureAwait( false );
			if ( !noLinks.Succeeded ) {
				return noLinks;
			}
			input = noLinks.Path!;
		}

		return await resolver.ResolvePhysicalAsync(
			input,
			new CanonicalPathResolutionOptions {
				MissingComponentPolicy = missingPolicy,
				FollowSymbolicLinks = ResolutionMode.NoLinks != resolution,
				RequireFinalDirectory = requireDirectory,
				RejectUnsupportedFinalReparsePoint = ResolutionMode.NoLinks != resolution,
			},
			cancellationToken
		).ConfigureAwait( false );
	}

	private static bool EndsWithDirectorySeparator(
		string value,
		PathPlatformSemantics semantics
	) => 0 < value.Length && semantics.IsDirectorySeparator( value[^1] );

	private static OptionParser CreateParser() => new(
		new[] {
			new OptionDefinition( "canonicalize", 'E', new[] { "canonicalize" } ),
			new OptionDefinition( "canonicalize-existing", 'e', new[] { "canonicalize-existing" } ),
			new OptionDefinition( "canonicalize-missing", 'm', new[] { "canonicalize-missing" } ),
			new OptionDefinition( "logical", 'L', new[] { "logical" } ),
			new OptionDefinition( "physical", 'P', new[] { "physical" } ),
			new OptionDefinition( "quiet", 'q', new[] { "quiet" } ),
			new OptionDefinition(
				"relative-to",
				longNames: new[] { "relative-to" },
				valueArity: OptionValueArity.Required
			),
			new OptionDefinition(
				"relative-base",
				longNames: new[] { "relative-base" },
				valueArity: OptionValueArity.Required
			),
			new OptionDefinition( "strip", 's', new[] { "strip", "no-symlinks" } ),
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
Usage: realpath [OPTION]... FILE...
Print the resolved absolute file name.
  -e, --canonicalize-existing  all components of the path must exist
  -m, --canonicalize-missing   no path components need exist
  -E, --canonicalize           all but the last component must exist
  -L, --logical                resolve '..' components before symlinks
  -P, --physical               resolve symlinks before '..' components
  -q, --quiet                  suppress most error messages
      --relative-to=DIR        print the resolved path relative to DIR
      --relative-base=DIR      print absolute paths unless below DIR
  -s, --strip, --no-symlinks   do not expand symbolic links
  -z, --zero                   end each output line with NUL, not newline
      --help                   display this help and exit
      --version                output version information and exit
""";
		await context.StandardOutput.WriteAsync(
			text.ReplaceLineEndings( Environment.NewLine ).AsMemory(),
			context.CancellationToken
		).ConfigureAwait( false );
	}
}
