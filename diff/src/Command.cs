// Original behavior/reference: GNU diffutils diff 3.12
// Ported to .NET by Timothy J. Bruce <uniblab@hotmail.com>

namespace Icod.DiffUtils.Diff;

using System.Security;
using Icod.CoreUtils.Shared.CommandLine;
using Icod.CoreUtils.Shared.Diagnostics;
using Icod.CoreUtils.Shared.IO;
using Icod.CoreUtils.Shared.RegularExpressions;
using Icod.DiffUtils.Shared;
using Icod.DiffUtils.Shared.Lines;

/// <summary>Implements the line-oriented GNU <c>diff</c> command.</summary>
public static class Command {
	private const string VersionText = "diff (Icod.DiffUtils) 1.0";

	/// <summary>Runs the command synchronously using supplied text streams.</summary>
	public static int Run(
		IReadOnlyList<string>? arguments,
		TextReader? stdin = null,
		TextWriter? stdout = null,
		TextWriter? stderr = null
	) => RunAsync( arguments, stdin, stdout, stderr ).GetAwaiter().GetResult();

	/// <summary>Runs the command asynchronously using supplied streams.</summary>
	public static async Task<int> RunAsync(
		IReadOnlyList<string>? arguments,
		TextReader? stdin = null,
		TextWriter? stdout = null,
		TextWriter? stderr = null,
		CancellationToken cancellationToken = default,
		Stream? stdinStream = null
	) {
		stdin ??= TextReader.Null;
		stdout ??= TextWriter.Null;
		stderr ??= TextWriter.Null;
		TextReaderStream? adapter = null;
		if ( null == stdinStream ) {
			adapter = new TextReaderStream( stdin, leaveOpen: true );
			stdinStream = adapter;
		}
		try {
			return await RunAsync(
				arguments,
				new CommandContext(
					"diff",
					stdin,
					stdout,
					stderr,
					stdinStream,
					cancellationToken: cancellationToken
				)
			).ConfigureAwait( false );
		} finally {
			adapter?.Dispose();
		}
	}

	/// <summary>Runs the command within an existing command context.</summary>
	public static async Task<int> RunAsync(
		IReadOnlyList<string>? arguments,
		CommandContext context
	) {
		ArgumentNullException.ThrowIfNull( context );
		try {
			var parsed = CreateParser().Parse( arguments );
			if ( !parsed.IsSuccess ) {
				foreach ( var error in parsed.Errors ) {
					await context.StandardError.WriteLineAsync(
						OptionDiagnosticFormatter.Format( context.ProgramName, error ).AsMemory(),
						context.CancellationToken
					).ConfigureAwait( false );
				}
				await WriteTryHelpAsync( context ).ConfigureAwait( false );
				return (int)ComparisonStatus.Trouble;
			}
			if ( parsed.HasOption( "help" ) ) {
				await WriteHelpAsync( context.StandardOutput, context.CancellationToken ).ConfigureAwait( false );
				return (int)ComparisonStatus.Equal;
			}
			if ( parsed.HasOption( "version" ) ) {
				await context.StandardOutput.WriteLineAsync( VersionText.AsMemory(), context.CancellationToken ).ConfigureAwait( false );
				return (int)ComparisonStatus.Equal;
			}
			if ( parsed.HasOption( "paginate" ) ) {
				throw new DiffUsageException( "--paginate is not supported; run diff output through pr explicitly" );
			}

			var options = await CreateOptionsAsync( parsed, context.CancellationToken ).ConfigureAwait( false );
			var pairs = CreateComparisonPairs( parsed, options );
			var coordinator = new DiffCoordinator( options, context );
			var status = ComparisonStatus.Equal;
			foreach ( var pair in pairs ) {
				status = Combine( status, await coordinator.CompareAsync( pair.OldPath, pair.NewPath ).ConfigureAwait( false ) );
			}
			return (int)status;
		} catch ( DiffUsageException exception ) {
			await context.Diagnostics.ErrorAsync( exception.Message, CancellationToken.None ).ConfigureAwait( false );
			await WriteTryHelpAsync( context ).ConfigureAwait( false );
			return (int)ComparisonStatus.Trouble;
		} catch ( OperationCanceledException ) when ( context.CancellationToken.IsCancellationRequested ) {
			return CommandExitCodes.Canceled;
		} catch ( Exception exception ) when ( IsOperationalException( exception ) ) {
			await context.Diagnostics.ErrorAsync( exception.Message, CancellationToken.None ).ConfigureAwait( false );
			return (int)ComparisonStatus.Trouble;
		}
	}

	/// <summary>Creates the option parser used by <c>diff</c>.</summary>
	public static OptionParser CreateParser() {
		return new OptionParser(
			new OptionDefinition[] {
				new( "normal", longNames: new[] { "normal" } ),
				new( "brief", 'q', new[] { "brief" } ),
				new( "report-identical-files", 's', new[] { "report-identical-files" } ),
				new( "context", 'c', new[] { "context" }, OptionValueArity.Optional ),
				new( "context-lines", 'C', valueArity: OptionValueArity.Required ),
				new( "unified", 'u', new[] { "unified" }, OptionValueArity.Optional ),
				new( "unified-lines", 'U', valueArity: OptionValueArity.Required ),
				new( "ed", 'e', new[] { "ed" } ),
				new( "forward-ed", 'f', new[] { "forward-ed" } ),
				new( "rcs", 'n', new[] { "rcs" } ),
				new( "side-by-side", 'y', new[] { "side-by-side" } ),
				new( "width", 'W', new[] { "width" }, OptionValueArity.Required ),
				new( "left-column", longNames: new[] { "left-column" } ),
				new( "suppress-common-lines", longNames: new[] { "suppress-common-lines" } ),
				new( "show-c-function", 'p', new[] { "show-c-function" } ),
				new( "show-function-line", 'F', new[] { "show-function-line" }, OptionValueArity.Required ),
				new( "label", longNames: new[] { "label" }, valueArity: OptionValueArity.Required ),
				new( "expand-tabs", 't', new[] { "expand-tabs" } ),
				new( "initial-tab", 'T', new[] { "initial-tab" } ),
				new( "tabsize", longNames: new[] { "tabsize" }, valueArity: OptionValueArity.Required ),
				new( "suppress-blank-empty", longNames: new[] { "suppress-blank-empty" } ),
				new( "paginate", 'l', new[] { "paginate" } ),
				new( "recursive", 'r', new[] { "recursive" } ),
				new( "no-dereference", longNames: new[] { "no-dereference" } ),
				new( "new-file", 'N', new[] { "new-file" } ),
				new( "unidirectional-new-file", longNames: new[] { "unidirectional-new-file" } ),
				new( "ignore-file-name-case", longNames: new[] { "ignore-file-name-case" } ),
				new( "no-ignore-file-name-case", longNames: new[] { "no-ignore-file-name-case" } ),
				new( "exclude", 'x', new[] { "exclude" }, OptionValueArity.Required ),
				new( "exclude-from", 'X', new[] { "exclude-from" }, OptionValueArity.Required ),
				new( "starting-file", 'S', new[] { "starting-file" }, OptionValueArity.Required ),
				new( "from-file", longNames: new[] { "from-file" }, valueArity: OptionValueArity.Required ),
				new( "to-file", longNames: new[] { "to-file" }, valueArity: OptionValueArity.Required ),
				new( "ignore-case", 'i', new[] { "ignore-case" } ),
				new( "ignore-tab-expansion", 'E', new[] { "ignore-tab-expansion" } ),
				new( "ignore-trailing-space", 'Z', new[] { "ignore-trailing-space" } ),
				new( "ignore-space-change", 'b', new[] { "ignore-space-change" } ),
				new( "ignore-all-space", 'w', new[] { "ignore-all-space" } ),
				new( "ignore-blank-lines", 'B', new[] { "ignore-blank-lines" } ),
				new( "ignore-matching-lines", 'I', new[] { "ignore-matching-lines" }, OptionValueArity.Required ),
				new( "text", 'a', new[] { "text" } ),
				new( "strip-trailing-cr", longNames: new[] { "strip-trailing-cr" } ),
				new( "ifdef", 'D', new[] { "ifdef" }, OptionValueArity.Required ),
				new( "minimal", 'd', new[] { "minimal" } ),
				new( "horizon-lines", longNames: new[] { "horizon-lines" }, valueArity: OptionValueArity.Required ),
				new( "speed-large-files", 'H', new[] { "speed-large-files" } ),
				new( "help", longNames: new[] { "help" }, allowMultiple: false ),
				new( "version", 'v', new[] { "version" }, allowMultiple: false )
			},
			new OptionParserSettings {
				AllowLongOptionAbbreviations = true,
				Ordering = OptionOrdering.Permute
			}
		);
	}

	/// <summary>Determines whether an exception represents an expected input or filesystem failure.</summary>
	internal static bool IsOperationalException( Exception exception ) {
		return exception is IOException
			or UnauthorizedAccessException
			or SecurityException
			or NotSupportedException;
	}

	private static async Task<DiffOptions> CreateOptionsAsync( OptionParseResult parsed, CancellationToken cancellationToken ) {
		var options = new DiffOptions {
			ReportIdenticalFiles = parsed.HasOption( "report-identical-files" ),
			Recursive = parsed.HasOption( "recursive" ),
			NoDereference = parsed.HasOption( "no-dereference" ),
			NewFile = parsed.HasOption( "new-file" ),
			UnidirectionalNewFile = parsed.HasOption( "unidirectional-new-file" ),
			TreatAsText = parsed.HasOption( "text" ),
			IgnoreBlankLines = parsed.HasOption( "ignore-blank-lines" ),
			ExpandTabs = parsed.HasOption( "expand-tabs" ),
			InitialTab = parsed.HasOption( "initial-tab" ),
			SuppressBlankEmpty = parsed.HasOption( "suppress-blank-empty" ),
			LeftColumn = parsed.HasOption( "left-column" ),
			SuppressCommonLines = parsed.HasOption( "suppress-common-lines" ),
			FromFile = parsed.GetLastValue( "from-file" ),
			ToFile = parsed.GetLastValue( "to-file" ),
			StartingFile = parsed.GetLastValue( "starting-file" ),
			ShowCFunction = parsed.HasOption( "show-c-function" )
		};
		if ( null != options.FromFile && null != options.ToFile ) {
			throw new DiffUsageException( "--from-file and --to-file are mutually exclusive" );
		}
		foreach ( var occurrence in parsed.GetOccurrences( "label" ) ) {
			options.Labels.Add( occurrence.Value! );
		}
		if ( 2 < options.Labels.Count ) {
			throw new DiffUsageException( "too many file label options" );
		}
		foreach ( var occurrence in parsed.GetOccurrences( "exclude" ) ) {
			options.ExcludePatterns.Add( occurrence.Value! );
		}
		foreach ( var occurrence in parsed.GetOccurrences( "exclude-from" ) ) {
			var lines = await File.ReadAllLinesAsync( occurrence.Value!, cancellationToken ).ConfigureAwait( false );
			options.ExcludePatterns.AddRange( lines.Where( line => 0 < line.Length ) );
		}

		options.IgnoreFileNameCase = ResolveBooleanOption(
			parsed,
			"ignore-file-name-case",
			"no-ignore-file-name-case"
		);
		options.TabSize = ParsePositiveInt( parsed.GetLastValue( "tabsize" ) ?? "8", "tab size" );
		options.Width = ParsePositiveInt( parsed.GetLastValue( "width" ) ?? "130", "width" );
		_ = ParseNonNegativeInt( parsed.GetLastValue( "horizon-lines" ) ?? "0", "horizon lines" );
		options.ComparisonPolicy = new LineComparisonPolicy {
			IgnoreCase = parsed.HasOption( "ignore-case" ),
			IgnoreTabExpansion = parsed.HasOption( "ignore-tab-expansion" ),
			IgnoreTrailingSpace = parsed.HasOption( "ignore-trailing-space" ),
			IgnoreSpaceChange = parsed.HasOption( "ignore-space-change" ),
			IgnoreAllSpace = parsed.HasOption( "ignore-all-space" ),
			StripTrailingCarriageReturn = parsed.HasOption( "strip-trailing-cr" ),
			TabSize = options.TabSize
		};

		var explicitOutputStyle = false;
		foreach ( var occurrence in parsed.Options ) {
			switch ( occurrence.Definition.Key ) {
				case "normal":
					options.OutputStyle = DiffOutputStyle.Normal;
					explicitOutputStyle = true;
					break;
				case "brief":
					options.OutputStyle = DiffOutputStyle.Brief;
					explicitOutputStyle = true;
					break;
				case "context":
					options.OutputStyle = DiffOutputStyle.Context;
					options.ContextLines = ParseNonNegativeInt( occurrence.Value ?? "3", "context length" );
					explicitOutputStyle = true;
					break;
				case "context-lines":
					options.OutputStyle = DiffOutputStyle.Context;
					options.ContextLines = ParseNonNegativeInt( occurrence.Value!, "context length" );
					explicitOutputStyle = true;
					break;
				case "unified":
					options.OutputStyle = DiffOutputStyle.Unified;
					options.ContextLines = ParseNonNegativeInt( occurrence.Value ?? "3", "context length" );
					explicitOutputStyle = true;
					break;
				case "unified-lines":
					options.OutputStyle = DiffOutputStyle.Unified;
					options.ContextLines = ParseNonNegativeInt( occurrence.Value!, "context length" );
					explicitOutputStyle = true;
					break;
				case "ed":
					options.OutputStyle = DiffOutputStyle.Ed;
					explicitOutputStyle = true;
					break;
				case "forward-ed":
					options.OutputStyle = DiffOutputStyle.ForwardEd;
					explicitOutputStyle = true;
					break;
				case "rcs":
					options.OutputStyle = DiffOutputStyle.Rcs;
					explicitOutputStyle = true;
					break;
				case "side-by-side":
					options.OutputStyle = DiffOutputStyle.SideBySide;
					explicitOutputStyle = true;
					break;
				case "ifdef":
					options.OutputStyle = DiffOutputStyle.IfDef;
					options.IfDefName = occurrence.Value;
					explicitOutputStyle = true;
					break;
			}
		}
		if ( !explicitOutputStyle && ( options.ShowCFunction || parsed.HasOption( "show-function-line" ) ) ) {
			options.OutputStyle = DiffOutputStyle.Context;
		}

		var provider = GnuBasicRegularExpressionProvider.Default;
		var regexOptions = new RegularExpressionOptions {
			IgnoreCase = options.ComparisonPolicy.IgnoreCase,
			NewLineSensitive = true
		};
		foreach ( var occurrence in parsed.GetOccurrences( "ignore-matching-lines" ) ) {
			options.IgnoredLinePatterns.Add( CompileExpression( provider, occurrence.Value!, regexOptions, cancellationToken ) );
		}
		var functionPattern = parsed.GetLastValue( "show-function-line" );
		if ( null != functionPattern ) {
			options.FunctionExpression = CompileExpression( provider, functionPattern, regexOptions, cancellationToken );
		}
		return options;
	}

	private static ICompiledRegularExpression CompileExpression(
		GnuBasicRegularExpressionProvider provider,
		string pattern,
		RegularExpressionOptions options,
		CancellationToken cancellationToken
	) {
		var result = provider.Compile( pattern, options, cancellationToken );
		if ( !result.IsSuccess ) {
			throw new DiffUsageException( result.Diagnostic?.Message ?? "invalid regular expression" );
		}
		return result.Expression!;
	}

	private static IReadOnlyList<(string OldPath, string NewPath)> CreateComparisonPairs(
		OptionParseResult parsed,
		DiffOptions options
	) {
		if ( null == options.FromFile && null == options.ToFile ) {
			if ( parsed.Operands.Count < 2 ) {
				throw new DiffUsageException( "missing operand after 'diff'" );
			}
			if ( 2 < parsed.Operands.Count ) {
				throw new DiffUsageException( $"extra operand '{parsed.Operands[2]}'" );
			}
			return new[] { (parsed.Operands[0], parsed.Operands[1]) };
		}
		if ( 0 == parsed.Operands.Count ) {
			throw new DiffUsageException( "missing file operand" );
		}
		return null != options.FromFile
			? parsed.Operands.Select( operand => (options.FromFile!, operand) ).ToArray()
			: parsed.Operands.Select( operand => (operand, options.ToFile!) ).ToArray();
	}

	private static bool ResolveBooleanOption( OptionParseResult parsed, string trueKey, string falseKey ) {
		bool value = false;
		foreach ( var occurrence in parsed.Options ) {
			if ( string.Equals( occurrence.Definition.Key, trueKey, StringComparison.Ordinal ) ) {
				value = true;
			} else if ( string.Equals( occurrence.Definition.Key, falseKey, StringComparison.Ordinal ) ) {
				value = false;
			}
		}
		return value;
	}

	private static int ParsePositiveInt( string value, string description ) {
		var result = ParseNonNegativeInt( value, description );
		if ( 0 == result ) {
			throw new DiffUsageException( $"invalid {description} '{value}'" );
		}
		return result;
	}

	private static int ParseNonNegativeInt( string value, string description ) {
		if ( !int.TryParse( value, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var result ) || result < 0 ) {
			throw new DiffUsageException( $"invalid {description} '{value}'" );
		}
		return result;
	}

	private static ComparisonStatus Combine( ComparisonStatus first, ComparisonStatus second ) {
		return (ComparisonStatus)Math.Max( (int)first, (int)second );
	}

	private static Task WriteTryHelpAsync( CommandContext context ) {
		return context.StandardError.WriteLineAsync(
			$"Try '{context.ProgramName} --help' for more information.".AsMemory(),
			CancellationToken.None
		);
	}

	private static async Task WriteHelpAsync( TextWriter output, CancellationToken cancellationToken ) {
		var lines = new[] {
			"Usage: diff [OPTION]... FILES",
			"Compare FILES line by line.",
			string.Empty,
			"      --normal                  output a normal diff (the default)",
			"  -q, --brief                   report only when files differ",
			"  -s, --report-identical-files  report when two files are the same",
			"  -c, -C NUM, --context[=NUM]   output context format",
			"  -u, -U NUM, --unified[=NUM]   output unified context format",
			"  -e, --ed                      output an ed script",
			"  -n, --rcs                     output RCS format",
			"  -y, --side-by-side            output in two columns",
			"  -D, --ifdef=NAME              output a merged file with conditional differences",
			"  -p, --show-c-function         show the C function containing each change",
			"  -F, --show-function-line=RE   show the most recent line matching RE",
			"      --label=LABEL             replace a file name and timestamp in headers",
			"  -r, --recursive               recursively compare subdirectories",
			"  -N, --new-file                treat absent files as empty",
			"  -i, --ignore-case             ignore case differences",
			"  -E, --ignore-tab-expansion    ignore tab-expansion differences",
			"  -Z, --ignore-trailing-space   ignore trailing white space",
			"  -b, --ignore-space-change     ignore changes in white-space amount",
			"  -w, --ignore-all-space        ignore all white space",
			"  -B, --ignore-blank-lines      ignore all-blank change groups",
			"  -I, --ignore-matching-lines=RE ignore groups whose changed lines match RE",
			"  -a, --text                    treat all files as text",
			"      --help                    display this help and exit",
			"  -v, --version                 output version information and exit",
			string.Empty,
			"Exit status is 0 if inputs are the same, 1 if different, 2 if trouble."
		};
		foreach ( var line in lines ) {
			await output.WriteLineAsync( line.AsMemory(), cancellationToken ).ConfigureAwait( false );
		}
	}
}
