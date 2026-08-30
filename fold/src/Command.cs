namespace Icod.CoreUtils.Fold;

using Icod.CommandFramework.CommandLine;
using Icod.CommandFramework.Diagnostics;
using Icod.CommandFramework.IO;
using Icod.CommandFramework.Text;
using Icod.CoreUtils.Shared.FileSystem.Traversal;

/// <summary>Implements GNU <c>fold</c> for .NET.</summary>
/// <remarks>
/// <para>Usage:</para>
/// <code>fold [OPTION]... [FILE]...</code>
/// <para>Input is folded by display columns by default, without normalizing untouched bytes.</para>
/// </remarks>
public static class Command {
	private const string BytesKey = "bytes";
	private const string CharactersKey = "characters";
	private const string SpacesKey = "spaces";
	private const string WidthKey = "width";
	private const string HelpKey = "help";
	private const string VersionKey = "version";

	/// <summary>Runs <c>fold</c> synchronously with optional injected text streams.</summary>
	/// <param name="args">The command-line arguments.</param>
	/// <param name="standardInput">The standard-input reader.</param>
	/// <param name="standardOutput">The standard-output writer.</param>
	/// <param name="standardError">The standard-error writer.</param>
	/// <returns>The command exit status.</returns>
	public static int Run( string[] args, TextReader? standardInput = null, TextWriter? standardOutput = null, TextWriter? standardError = null ) {
		return RunAsync( args, standardInput, standardOutput, standardError ).GetAwaiter().GetResult();
	}

	/// <summary>Runs <c>fold</c> asynchronously with optional injected text streams.</summary>
	/// <param name="args">The command-line arguments.</param>
	/// <param name="standardInput">The standard-input reader.</param>
	/// <param name="standardOutput">The standard-output writer.</param>
	/// <param name="standardError">The standard-error writer.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>A task whose result is the command exit status.</returns>
	public static async Task<int> RunAsync(
		string[] args,
		TextReader? standardInput = null,
		TextWriter? standardOutput = null,
		TextWriter? standardError = null,
		CancellationToken cancellationToken = default
	) {
		standardInput ??= Console.In;
		standardOutput ??= Console.Out;
		standardError ??= Console.Error;
		using var inputAdapter = new TextReaderStream( standardInput, leaveOpen: true );
		return await RunAsync(
			args,
			new CommandContext( "fold", standardInput, standardOutput, standardError, inputAdapter, null, null, cancellationToken )
		).ConfigureAwait( false );
	}

	/// <summary>Runs <c>fold</c> asynchronously against a command context.</summary>
	/// <param name="args">The command-line arguments.</param>
	/// <param name="context">The command context.</param>
	/// <returns>A task whose result is the command exit status.</returns>
	public static async Task<int> RunAsync( string[] args, CommandContext context ) {
		ArgumentNullException.ThrowIfNull( args );
		ArgumentNullException.ThrowIfNull( context );
		try {
			var parsed = CreateParser().Parse( args );
			if ( !parsed.IsSuccess ) {
				await WriteOptionErrorAsync( parsed.Errors[0], context ).ConfigureAwait( false );
				return CommandExitCodes.Failure;
			}
			var terminal = parsed.Options.FirstOrDefault( option => option.Definition.Key is HelpKey or VersionKey );
			if ( terminal?.Definition.Key == HelpKey ) {
				await WriteHelpAsync( context.StandardOutput, context.CancellationToken ).ConfigureAwait( false );
				return CommandExitCodes.Success;
			}
			if ( terminal?.Definition.Key == VersionKey ) {
				await WriteVersionAsync( context.StandardOutput, context.CancellationToken ).ConfigureAwait( false );
				return CommandExitCodes.Success;
			}

			var mode = FoldCountingMode.DisplayColumns;
			var width = 80UL;
			foreach ( var option in parsed.Options ) {
				switch ( option.Definition.Key ) {
					case BytesKey:
						mode = FoldCountingMode.Bytes;
						break;
					case CharactersKey:
						mode = FoldCountingMode.Characters;
						break;
					case WidthKey:
						if ( !TryParseWidth( option.Value, out width ) ) {
							await context.Diagnostics.ErrorAsync( string.Concat( "invalid number of columns: '", option.Value, "'" ), context.CancellationToken ).ConfigureAwait( false );
							return CommandExitCodes.Failure;
						}
						break;
				}
			}
			var expansion = await PathnameOperandExpander.ExpandAsync(
				parsed.Operands,
				cancellationToken: context.CancellationToken
			).ConfigureAwait( false );
			if ( RequiresStandardInput( expansion.Operands ) && context.StandardInputStream is null ) {
				await context.Diagnostics.ErrorAsync( "a binary standard-input stream was not supplied", context.CancellationToken ).ConfigureAwait( false );
				return CommandExitCodes.Failure;
			}
			var options = new FoldOptions( mode, parsed.HasOption( SpacesKey ), width, expansion.Operands );
			await using var output = new ByteOutputStream( context.StandardOutput, context.StandardOutputStream );
			var processor = new FoldProcessor(
				options,
				TextLocaleEnvironment.Resolve(),
				UnicodeDisplayWidthProvider.Instance
			);
			var success = await processor.ProcessAsync( context, output ).ConfigureAwait( false );
			await output.CompleteAsync( context.CancellationToken ).ConfigureAwait( false );
			return success ? CommandExitCodes.Success : CommandExitCodes.Failure;
		} catch ( OperationCanceledException ) {
			return CommandExitCodes.Canceled;
		} catch ( OverflowException ) {
			try {
				await context.Diagnostics.ErrorAsync( "input line is too long", CancellationToken.None ).ConfigureAwait( false );
			} catch { }
			return CommandExitCodes.Failure;
		} catch ( IOException exception ) {
			try {
				await context.Diagnostics.ErrorAsync( string.Concat( "write failed: ", exception.Message ), CancellationToken.None ).ConfigureAwait( false );
			} catch { }
			return CommandExitCodes.Failure;
		}
	}

	private static OptionParser CreateParser() {
		var settings = new OptionParserSettings { AllowLongOptionAbbreviations = true, Ordering = OptionOrdering.Permute };
		settings.TokenRewriteRules.Add(
			new OptionTokenRewriteRule(
				token => IsLegacyWidthToken( token )
					? new[] { string.Concat( "--width=", token[1..] ) }
					: null
			)
		);
		return new OptionParser(
			new[] {
				new OptionDefinition( BytesKey, 'b', new[] { "bytes" } ),
				new OptionDefinition( CharactersKey, 'c', new[] { "characters" } ),
				new OptionDefinition( SpacesKey, 's', new[] { "spaces" } ),
				new OptionDefinition( WidthKey, 'w', new[] { "width" }, OptionValueArity.Required ),
				new OptionDefinition( HelpKey, null, new[] { "help" } ),
				new OptionDefinition( VersionKey, null, new[] { "version" } )
			},
			settings
		);
	}

	private static bool IsLegacyWidthToken( string token ) {
		if ( 1 >= token.Length || '-' != token[0] ) {
			return false;
		}
		for ( var index = 1; index < token.Length; index++ ) {
			if ( !char.IsAsciiDigit( token[index] ) ) {
				return false;
			}
		}
		return true;
	}

	private static bool RequiresStandardInput( IReadOnlyList<string> operands ) {
		return 0 == operands.Count || operands.Any( value => "-" == value );
	}

	private static bool TryParseWidth( string? value, out ulong width ) {
		width = 0;
		if ( string.IsNullOrEmpty( value ) ) {
			return false;
		}
		foreach ( var character in value ) {
			if ( !char.IsAsciiDigit( character ) ) {
				return false;
			}
			var digit = (ulong)(character - '0');
			if ( width > ((ulong.MaxValue - digit) / 10) ) {
				return false;
			}
			width = (width * 10) + digit;
		}
		return 0 < width && width <= (ulong.MaxValue - 9);
	}

	private static async Task WriteOptionErrorAsync( OptionParseError error, CommandContext context ) {
		await context.StandardError.WriteLineAsync( OptionDiagnosticFormatter.Format( context.ProgramName, error ).AsMemory(), context.CancellationToken ).ConfigureAwait( false );
		await context.StandardError.WriteLineAsync( "Try 'fold --help' for more information.".AsMemory(), context.CancellationToken ).ConfigureAwait( false );
	}

	private static Task WriteUsageAsync( TextWriter writer, CancellationToken cancellationToken ) {
		return writer.WriteLineAsync( "Usage: fold [OPTION]... [FILE]...".AsMemory(), cancellationToken );
	}

	private static async Task WriteHelpAsync( TextWriter writer, CancellationToken cancellationToken ) {
		await WriteUsageAsync( writer, cancellationToken ).ConfigureAwait( false );
		await writer.WriteAsync(
			string.Concat(
				"Wrap input lines in each FILE, writing to standard output.", Environment.NewLine,
				"With no FILE, or when FILE is -, read standard input.", Environment.NewLine,
				Environment.NewLine,
				"  -b, --bytes         count bytes rather than columns", Environment.NewLine,
				"  -c, --characters    count characters rather than columns", Environment.NewLine,
				"  -s, --spaces        break after blanks, or in words greater than WIDTH", Environment.NewLine,
				"  -w, --width=WIDTH   use WIDTH columns instead of 80", Environment.NewLine,
				"      --help          display this help and exit", Environment.NewLine,
				"      --version       output version information and exit", Environment.NewLine
			).AsMemory(),
			cancellationToken
		).ConfigureAwait( false );
	}

	private static Task WriteVersionAsync( TextWriter writer, CancellationToken cancellationToken ) {
		return writer.WriteLineAsync( "fold (Icod.CoreUtils) GNU Coreutils 9.11 compatibility profile".AsMemory(), cancellationToken );
	}
}
