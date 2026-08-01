// Original behavior/reference: GNU diffutils sdiff 3.12
// Ported to .NET by Timothy J. Bruce <uniblab@hotmail.com>

namespace Icod.DiffUtils.SDiff;

using System.Security;
using Icod.CoreUtils.Shared.CommandLine;
using Icod.CoreUtils.Shared.Diagnostics;
using Icod.CoreUtils.Shared.IO;
using Icod.CoreUtils.Shared.RegularExpressions;
using Icod.DiffUtils.Shared;
using Icod.DiffUtils.Shared.Edits;
using Icod.DiffUtils.Shared.Lines;

/// <summary>Implements side-by-side comparison and interactive merging.</summary>
public static class Command {
	private const string VersionText = "sdiff (Icod.DiffUtils) 1.0";

	/// <summary>Runs the command synchronously using supplied text streams.</summary>
	/// <param name="arguments">The command-line arguments.</param>
	/// <param name="stdin">The standard-input reader.</param>
	/// <param name="stdout">The standard-output writer.</param>
	/// <param name="stderr">The standard-error writer.</param>
	/// <returns>The command exit status.</returns>
	public static int Run(
		IReadOnlyList<string>? arguments,
		TextReader? stdin = null,
		TextWriter? stdout = null,
		TextWriter? stderr = null
	) => RunAsync( arguments, stdin, stdout, stderr ).GetAwaiter().GetResult();

	/// <summary>Runs the command asynchronously using supplied streams.</summary>
	/// <param name="arguments">The command-line arguments.</param>
	/// <param name="stdin">The standard-input reader.</param>
	/// <param name="stdout">The standard-output writer.</param>
	/// <param name="stderr">The standard-error writer.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <param name="stdinStream">The optional binary standard-input stream.</param>
	/// <param name="editor">The optional interactive editor implementation.</param>
	/// <returns>A task whose result is the command exit status.</returns>
	public static async Task<int> RunAsync(
		IReadOnlyList<string>? arguments,
		TextReader? stdin = null,
		TextWriter? stdout = null,
		TextWriter? stderr = null,
		CancellationToken cancellationToken = default,
		Stream? stdinStream = null,
		ISDiffEditor? editor = null
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
					"sdiff",
					stdin,
					stdout,
					stderr,
					stdinStream,
					cancellationToken: cancellationToken
				),
				editor
			).ConfigureAwait( false );
		} finally {
			adapter?.Dispose();
		}
	}

	/// <summary>Runs the command within an existing command context.</summary>
	/// <param name="arguments">The command-line arguments.</param>
	/// <param name="context">The injected command context.</param>
	/// <param name="editor">The optional interactive editor implementation.</param>
	/// <returns>A task whose result is the command exit status.</returns>
	public static async Task<int> RunAsync(
		IReadOnlyList<string>? arguments,
		CommandContext context,
		ISDiffEditor? editor = null
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
				await context.StandardOutput.WriteLineAsync(
					VersionText.AsMemory(),
					context.CancellationToken
				).ConfigureAwait( false );
				return (int)ComparisonStatus.Equal;
			}

			var options = CreateOptions( parsed, context.CancellationToken );
			var operands = ResolveDirectoryOperands( options.Operands );
			var inputs = await ReadInputsAsync( operands, context ).ConfigureAwait( false );
			var bytesEqual = inputs[0].Document.Bytes.Span.SequenceEqual( inputs[1].Document.Bytes.Span );
			if ( !options.TreatAsText && ( inputs[0].Document.ContainsNullByte || inputs[1].Document.ContainsNullByte ) ) {
				if ( !bytesEqual ) {
					await context.StandardOutput.WriteLineAsync(
						$"Binary files {inputs[0].DisplayName} and {inputs[1].DisplayName} differ".AsMemory(),
						context.CancellationToken
					).ConfigureAwait( false );
				}
				if ( null != options.OutputPath ) {
					await WriteTransactionalBytesAsync(
						options.OutputPath,
						ReadOnlyMemory<byte>.Empty,
						context.CancellationToken
					).ConfigureAwait( false );
				}
				return bytesEqual ? (int)ComparisonStatus.Equal : (int)ComparisonStatus.Different;
			}

			var script = LineDiffEngine.Compare(
				inputs[0].Document.Lines,
				inputs[1].Document.Lines,
				options.ComparisonPolicy,
				context.CancellationToken
			);
			var comparison = SDiffComparisonBuilder.Build(
				inputs[0],
				inputs[1],
				script,
				options,
				context.CancellationToken
			);
			if ( null == options.OutputPath ) {
				var writer = new SDiffOutputWriter( context.StandardOutput, options, context.CancellationToken );
				await writer.WriteComparisonAsync( comparison ).ConfigureAwait( false );
			} else {
				await SDiffMergeEngine.MergeAsync(
					comparison,
					inputs[0],
					inputs[1],
					options,
					context,
					editor ?? new ProcessSDiffEditor()
				).ConfigureAwait( false );
			}
			return comparison.HasDifferences
				? (int)ComparisonStatus.Different
				: (int)ComparisonStatus.Equal;
		} catch ( SDiffQuitException ) {
			return (int)ComparisonStatus.Trouble;
		} catch ( SDiffUsageException exception ) {
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

	/// <summary>Creates the option parser used by <c>sdiff</c>.</summary>
	/// <returns>A parser configured for the supported GNU <c>sdiff</c> options.</returns>
	public static OptionParser CreateParser() {
		return new OptionParser(
			new OptionDefinition[] {
				new( "output", 'o', new[] { "output" }, OptionValueArity.Required ),
				new( "ignore-case", 'i', new[] { "ignore-case" } ),
				new( "ignore-tab-expansion", 'E', new[] { "ignore-tab-expansion" } ),
				new( "ignore-trailing-space", 'Z', new[] { "ignore-trailing-space" } ),
				new( "ignore-space-change", 'b', new[] { "ignore-space-change" } ),
				new( "ignore-all-space", 'W', new[] { "ignore-all-space" } ),
				new( "ignore-blank-lines", 'B', new[] { "ignore-blank-lines" } ),
				new( "ignore-matching-lines", 'I', new[] { "ignore-matching-lines" }, OptionValueArity.Required ),
				new( "strip-trailing-cr", longNames: new[] { "strip-trailing-cr" } ),
				new( "text", 'a', new[] { "text" } ),
				new( "width", 'w', new[] { "width" }, OptionValueArity.Required ),
				new( "left-column", 'l', new[] { "left-column" } ),
				new( "suppress-common-lines", 's', new[] { "suppress-common-lines" } ),
				new( "expand-tabs", 't', new[] { "expand-tabs" } ),
				new( "tabsize", longNames: new[] { "tabsize" }, valueArity: OptionValueArity.Required ),
				new( "minimal", 'd', new[] { "minimal" } ),
				new( "speed-large-files", 'H', new[] { "speed-large-files" } ),
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

	/// <summary>Determines whether an exception is an expected operational failure.</summary>
	/// <param name="exception">The exception to classify.</param>
	/// <returns><see langword="true"/> for a controlled operational failure.</returns>
	internal static bool IsOperationalException( Exception exception ) {
		return exception is IOException
			or UnauthorizedAccessException
			or SecurityException
			or NotSupportedException;
	}

	private static SDiffOptions CreateOptions( OptionParseResult parsed, CancellationToken cancellationToken ) {
		if ( 2 != parsed.Operands.Count ) {
			throw new SDiffUsageException( parsed.Operands.Count < 2 ? "missing operand" : $"extra operand '{parsed.Operands[2]}'" );
		}
		if ( 1 < parsed.Operands.Count( operand => "-" == operand ) ) {
			throw new SDiffUsageException( "standard input may be specified only once" );
		}
		if ( null != parsed.GetLastValue( "output" ) && parsed.Operands.Any( operand => "-" == operand ) ) {
			throw new SDiffUsageException( "cannot interactively merge standard input" );
		}
		if ( null != parsed.GetLastValue( "diff-program" ) ) {
			throw new SDiffUsageException(
				"--diff-program is not supported; sdiff uses the in-process Icod.DiffUtils.Shared engine"
			);
		}
		var width = ParsePositiveInt( parsed.GetLastValue( "width" ) ?? "130", "width" );
		var tabSize = ParsePositiveInt( parsed.GetLastValue( "tabsize" ) ?? "8", "tab size" );
		var options = new SDiffOptions {
			OutputPath = parsed.GetLastValue( "output" ),
			TreatAsText = parsed.HasOption( "text" ),
			IgnoreBlankLines = parsed.HasOption( "ignore-blank-lines" ),
			Width = width,
			LeftColumn = parsed.HasOption( "left-column" ),
			SuppressCommonLines = parsed.HasOption( "suppress-common-lines" ),
			ExpandTabs = parsed.HasOption( "expand-tabs" ),
			TabSize = tabSize,
			Operands = parsed.Operands.ToArray(),
			ComparisonPolicy = new LineComparisonPolicy {
				IgnoreCase = parsed.HasOption( "ignore-case" ),
				IgnoreTabExpansion = parsed.HasOption( "ignore-tab-expansion" ),
				IgnoreTrailingSpace = parsed.HasOption( "ignore-trailing-space" ),
				IgnoreSpaceChange = parsed.HasOption( "ignore-space-change" ),
				IgnoreAllSpace = parsed.HasOption( "ignore-all-space" ),
				StripTrailingCarriageReturn = parsed.HasOption( "strip-trailing-cr" ),
				TabSize = tabSize
			}
		};
		var provider = GnuBasicRegularExpressionProvider.Default;
		var regexOptions = new RegularExpressionOptions {
			IgnoreCase = options.ComparisonPolicy.IgnoreCase,
			NewLineSensitive = true
		};
		foreach ( var occurrence in parsed.GetOccurrences( "ignore-matching-lines" ) ) {
			var result = provider.Compile( occurrence.Value!, regexOptions, cancellationToken );
			if ( !result.IsSuccess ) {
				throw new SDiffUsageException( result.Diagnostic?.Message ?? "invalid regular expression" );
			}
			options.IgnoredLinePatterns.Add( result.Expression! );
		}
		return options;
	}

	private static string[] ResolveDirectoryOperands( IReadOnlyList<string> operands ) {
		var left = operands[0];
		var right = operands[1];
		var leftDirectory = "-" != left && Directory.Exists( left );
		var rightDirectory = "-" != right && Directory.Exists( right );
		if ( leftDirectory && rightDirectory ) {
			throw new SDiffUsageException( "both files to be compared are directories" );
		}
		if ( leftDirectory ) {
			left = Path.Combine( left, Path.GetFileName( right ) );
		} else if ( rightDirectory ) {
			right = Path.Combine( right, Path.GetFileName( left ) );
		}
		return new[] { left, right };
	}

	private static async Task<IReadOnlyList<SDiffInput>> ReadInputsAsync(
		IReadOnlyList<string> operands,
		CommandContext context
	) {
		var inputs = new List<SDiffInput>( 2 );
		foreach ( var operand in operands ) {
			context.CancellationToken.ThrowIfCancellationRequested();
			var comparisonInput = ComparisonInput.Create( operand );
			await using var source = comparisonInput.OpenBinary( context );
			var document = await ComparisonDocumentReader.ReadAsync(
				source.BinaryStream!,
				context.CancellationToken
			).ConfigureAwait( false );
			inputs.Add( new SDiffInput( operand, comparisonInput.DisplayName, document ) );
		}
		return inputs.AsReadOnly();
	}

	private static async Task WriteTransactionalBytesAsync(
		string outputPath,
		ReadOnlyMemory<byte> bytes,
		CancellationToken cancellationToken
	) {
		var fullPath = Path.GetFullPath( outputPath );
		var directory = Path.GetDirectoryName( fullPath ) ?? Directory.GetCurrentDirectory();
		var temporaryPath = Path.Combine(
			directory,
			$".{Path.GetFileName( fullPath )}.sdiff-{Guid.NewGuid():N}.tmp"
		);
		var committed = false;
		try {
			await using ( var stream = new FileStream(
				temporaryPath,
				FileMode.CreateNew,
				FileAccess.Write,
				FileShare.None,
				65536,
				FileOptions.Asynchronous | FileOptions.SequentialScan
			) ) {
				await stream.WriteAsync( bytes, cancellationToken ).ConfigureAwait( false );
				await stream.FlushAsync( cancellationToken ).ConfigureAwait( false );
			}
			File.Move( temporaryPath, fullPath, overwrite: true );
			committed = true;
		} finally {
			if ( !committed ) {
				try {
					File.Delete( temporaryPath );
				} catch ( IOException ) {
					// Cleanup must not mask the original failure.
				} catch ( UnauthorizedAccessException ) {
					// Cleanup must not mask the original failure.
				}
			}
		}
	}

	private static int ParsePositiveInt( string value, string description ) {
		if ( !int.TryParse(
			value,
			System.Globalization.NumberStyles.None,
			System.Globalization.CultureInfo.InvariantCulture,
			out var result
		) || result <= 0 ) {
			throw new SDiffUsageException( $"invalid {description} '{value}'" );
		}
		return result;
	}

	private static Task WriteTryHelpAsync( CommandContext context ) {
		return context.StandardError.WriteLineAsync(
			$"Try '{context.ProgramName} --help' for more information.".AsMemory(),
			CancellationToken.None
		);
	}

	private static async Task WriteHelpAsync( TextWriter output, CancellationToken cancellationToken ) {
		var lines = new[] {
			"Usage: sdiff [OPTION]... FILE1 FILE2",
			"Side-by-side merge of differences between FILE1 and FILE2.",
			string.Empty,
			"  -o, --output=FILE            operate interactively, sending output to FILE",
			"  -i, --ignore-case            consider upper- and lower-case to be the same",
			"  -E, --ignore-tab-expansion   ignore changes due to tab expansion",
			"  -Z, --ignore-trailing-space  ignore white space at line end",
			"  -b, --ignore-space-change    ignore changes in the amount of white space",
			"  -W, --ignore-all-space       ignore all white space",
			"  -B, --ignore-blank-lines     ignore changes whose lines are all blank",
			"  -I, --ignore-matching-lines=RE  ignore changes all whose lines match RE",
			"      --strip-trailing-cr      strip trailing carriage return on input",
			"  -a, --text                   treat all files as text",
			"  -w, --width=NUM              output at most NUM (default 130) print columns",
			"  -l, --left-column            output only the left column of common lines",
			"  -s, --suppress-common-lines  do not output common lines",
			"  -t, --expand-tabs            expand tabs to spaces in output",
			"      --tabsize=NUM            tab stops at every NUM (default 8) print columns",
			"  -d, --minimal                try hard to find a smaller set of changes",
			"  -H, --speed-large-files      assume large files, many scattered small changes",
			"      --help                   display this help and exit",
			"  -v, --version                output version information and exit",
			string.Empty,
			"If a FILE is '-', read standard input.",
			"Exit status is 0 if inputs are the same, 1 if different, 2 if trouble."
		};
		foreach ( var line in lines ) {
			await output.WriteLineAsync( line.AsMemory(), cancellationToken ).ConfigureAwait( false );
		}
	}
}
