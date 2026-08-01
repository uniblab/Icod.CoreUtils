// Original behavior/reference: GNU diffutils diff3 3.12
// Ported to .NET by Timothy J. Bruce <uniblab@hotmail.com>

namespace Icod.DiffUtils.Diff3;

using System.Security;
using Icod.CoreUtils.Shared.CommandLine;
using Icod.CoreUtils.Shared.Diagnostics;
using Icod.CoreUtils.Shared.IO;
using Icod.DiffUtils.Shared;
using Icod.DiffUtils.Shared.Lines;
using Icod.DiffUtils.Shared.Merge;

/// <summary>Implements <c>diff3 [OPTION]... MYFILE OLDFILE YOURFILE</c>.</summary>
public static class Command {
	private const string VersionText = "diff3 (Icod.DiffUtils) 1.0";

	/// <summary>Runs the command synchronously using supplied text streams.</summary>
	public static int Run(
		IReadOnlyList<string>? arguments,
		TextReader? stdin = null,
		TextWriter? stdout = null,
		TextWriter? stderr = null
	) {
		return RunAsync( arguments, stdin, stdout, stderr ).GetAwaiter().GetResult();
	}

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
					"diff3",
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
	public static async Task<int> RunAsync( IReadOnlyList<string>? arguments, CommandContext context ) {
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
				await context.StandardOutput.WriteLineAsync(
					VersionText.AsMemory(),
					context.CancellationToken
				).ConfigureAwait( false );
				return (int)ComparisonStatus.Equal;
			}

			var options = CreateOptions( parsed );
			var inputs = await ReadInputsAsync( options, context ).ConfigureAwait( false );
			if ( !options.TreatAsText && inputs.Any( input => input.Document.ContainsNullByte ) ) {
				throw new Diff3UsageException( "binary input differs; use --text to compare it line by line" );
			}
			var policy = new LineComparisonPolicy {
				StripTrailingCarriageReturn = options.StripTrailingCarriageReturn
			};
			var commonInput = GetCommonInput( options );
			var comparison = ThreeWayMergeEngine.Compare(
				inputs[0].Document.Lines,
				inputs[1].Document.Lines,
				inputs[2].Document.Lines,
				commonInput,
				policy,
				context.CancellationToken
			);
			if ( Diff3OutputMode.Normal == options.Mode && !options.Merge ) {
				await Diff3OutputWriter.WriteNormalAsync(
					comparison,
					options,
					context.StandardOutput,
					context.CancellationToken
				).ConfigureAwait( false );
				return (int)ComparisonStatus.Equal;
			}

			var wroteConflict = options.Merge
				? await Diff3OutputWriter.WriteMergedAsync(
					comparison,
					inputs[0],
					options,
					context.StandardOutput,
					context.CancellationToken
				).ConfigureAwait( false )
				: await Diff3OutputWriter.WriteEdScriptAsync(
					comparison,
					inputs[0],
					options,
					context.StandardOutput,
					context.StandardError,
					context.CancellationToken
				).ConfigureAwait( false );
			return wroteConflict ? (int)ComparisonStatus.Different : (int)ComparisonStatus.Equal;
		} catch ( Diff3UsageException exception ) {
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

	/// <summary>Creates the option parser used by <c>diff3</c>.</summary>
	public static OptionParser CreateParser() {
		return new OptionParser(
			new OptionDefinition[] {
				new( "text", 'a', new[] { "text" } ),
				new( "show-all", 'A', new[] { "show-all" } ),
				new( "ed", 'e', new[] { "ed" } ),
				new( "show-overlap", 'E', new[] { "show-overlap" } ),
				new( "append-write-quit", 'i' ),
				new( "label", 'L', new[] { "label" }, OptionValueArity.Required ),
				new( "merge", 'm', new[] { "merge" } ),
				new( "strip-trailing-cr", longNames: new[] { "strip-trailing-cr" } ),
				new( "initial-tab", 'T', new[] { "initial-tab" } ),
				new( "overlap-only", 'x', new[] { "overlap-only" } ),
				new( "marked-overlap-only", 'X' ),
				new( "easy-only", '3', new[] { "easy-only" } ),
				new( "diff-program", longNames: new[] { "diff-program" }, valueArity: OptionValueArity.Required ),
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

	private static Diff3Options CreateOptions( OptionParseResult parsed ) {
		if ( null != parsed.GetLastValue( "diff-program" ) ) {
			throw new Diff3UsageException(
				"--diff-program is not supported; diff3 uses the in-process Icod.DiffUtils.Shared engine"
			);
		}
		if ( 3 != parsed.Operands.Count ) {
			throw new Diff3UsageException(
				parsed.Operands.Count < 3 ? "missing operand" : "extra operand"
			);
		}
		if ( 1 < parsed.Operands.Count( operand => "-" == operand ) ) {
			throw new Diff3UsageException( "standard input may be specified only once" );
		}

		var selectedModes = new List<Diff3OutputMode>();
		AddMode( parsed, "show-all", Diff3OutputMode.ShowAll, selectedModes );
		AddMode( parsed, "ed", Diff3OutputMode.Ed, selectedModes );
		AddMode( parsed, "show-overlap", Diff3OutputMode.ShowOverlap, selectedModes );
		AddMode( parsed, "overlap-only", Diff3OutputMode.OverlapOnly, selectedModes );
		AddMode( parsed, "marked-overlap-only", Diff3OutputMode.MarkedOverlapOnly, selectedModes );
		AddMode( parsed, "easy-only", Diff3OutputMode.EasyOnly, selectedModes );
		if ( 1 < selectedModes.Count ) {
			throw new Diff3UsageException( "incompatible options" );
		}

		var merge = parsed.HasOption( "merge" );
		if ( merge && parsed.HasOption( "append-write-quit" ) ) {
			throw new Diff3UsageException( "options -i and -m are incompatible" );
		}
		var mode = 0 < selectedModes.Count
			? selectedModes[0]
			: merge ? Diff3OutputMode.ShowAll : Diff3OutputMode.Normal;
		var labels = parsed.GetOccurrences( "label" ).Select( occurrence => occurrence.Value! ).ToList();
		if ( 3 < labels.Count ) {
			throw new Diff3UsageException( "too many file label options" );
		}
		if ( 0 < labels.Count && mode is not Diff3OutputMode.ShowAll
			and not Diff3OutputMode.ShowOverlap
			and not Diff3OutputMode.MarkedOverlapOnly ) {
			throw new Diff3UsageException( "file labels require a conflict-marking mode" );
		}
		while ( labels.Count < 3 ) {
			labels.Add( parsed.Operands[labels.Count] );
		}
		return new Diff3Options {
			Mode = mode,
			Merge = merge,
			TreatAsText = parsed.HasOption( "text" ),
			StripTrailingCarriageReturn = parsed.HasOption( "strip-trailing-cr" ),
			InitialTab = parsed.HasOption( "initial-tab" ),
			AppendWriteAndQuit = parsed.HasOption( "append-write-quit" ),
			Operands = parsed.Operands.ToArray(),
			Labels = labels.AsReadOnly()
		};
	}

	private static ThreeWayCommonInput GetCommonInput( Diff3Options options ) {
		var normalReport = Diff3OutputMode.Normal == options.Mode && !options.Merge;
		if ( normalReport ) {
			return "-" == options.Operands[2]
				? ThreeWayCommonInput.Second
				: ThreeWayCommonInput.Third;
		}
		return "-" == options.Operands[1]
			? ThreeWayCommonInput.Third
			: ThreeWayCommonInput.Second;
	}

	private static void AddMode(
		OptionParseResult parsed,
		string optionName,
		Diff3OutputMode mode,
		ICollection<Diff3OutputMode> modes
	) {
		if ( parsed.HasOption( optionName ) ) {
			modes.Add( mode );
		}
	}

	private static async Task<IReadOnlyList<Diff3Input>> ReadInputsAsync(
		Diff3Options options,
		CommandContext context
	) {
		var inputs = new List<Diff3Input>( 3 );
		for ( var index = 0; index < 3; index++ ) {
			context.CancellationToken.ThrowIfCancellationRequested();
			var comparisonInput = ComparisonInput.Create( options.Operands[index] );
			await using var source = comparisonInput.OpenBinary( context );
			var document = await ComparisonDocumentReader.ReadAsync(
				source.BinaryStream!,
				context.CancellationToken
			).ConfigureAwait( false );
			inputs.Add( new Diff3Input(
				comparisonInput.Value,
				comparisonInput.DisplayName,
				options.Labels[index],
				document
			) );
		}
		return inputs.AsReadOnly();
	}

	private static async Task WriteHelpAsync( TextWriter output, CancellationToken cancellationToken ) {
		foreach ( var line in new[] {
			"Usage: diff3 [OPTION]... MYFILE OLDFILE YOURFILE",
			"Compare three files line by line.",
			string.Empty,
			"  -A, --show-all              output all changes, bracketing conflicts",
			"  -e, --ed                    output an ed script incorporating changes",
			"  -E, --show-overlap          like -e, but bracket overlapping changes",
			"  -3, --easy-only             incorporate only nonoverlapping changes",
			"  -x, --overlap-only          incorporate only overlapping changes",
			"  -X                          like -x, but bracket overlapping changes",
			"  -i                          append w and q commands to ed scripts",
			"  -m, --merge                 output the merged file directly",
			"  -a, --text                  treat all files as text",
			"      --strip-trailing-cr     strip trailing carriage returns on changed input",
			"  -T, --initial-tab           prepend a tab to normal-format content",
			"  -L, --label=LABEL           use LABEL in conflict markers (up to three)",
			"      --help                  display this help and exit",
			"  -v, --version               output version information and exit"
		} ) {
			await output.WriteLineAsync( line.AsMemory(), cancellationToken ).ConfigureAwait( false );
		}
	}

	private static Task WriteTryHelpAsync( CommandContext context ) {
		return context.StandardError.WriteLineAsync(
			"Try 'diff3 --help' for more information.".AsMemory(),
			CancellationToken.None
		);
	}
}
