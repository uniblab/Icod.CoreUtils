// Original behavior/reference: GNU coreutils
// Ported to .NET by Timothy J. Bruce <uniblab@hotmail.com>

namespace Icod.CoreUtils.Shuf;

using System.Globalization;
using System.Numerics;
using Icod.CommandFramework.CommandLine;
using Icod.CommandFramework.Diagnostics;
using Icod.CommandFramework.IO;

/// <summary>Implements GNU <c>shuf</c> for .NET.</summary>
/// <remarks>
/// <para>Usage:</para>
/// <code>shuf [OPTION]... [FILE]
/// shuf -e [OPTION]... [ARG]...
/// shuf -i LO-HI [OPTION]...</code>
/// <para>Input records are preserved byte-for-byte and randomized with bounded memory.</para>
/// </remarks>
public static class Command {
	private const string EchoKey = "echo";
	private const string HeadCountKey = "head-count";
	private const string HelpKey = "help";
	private const string InputRangeKey = "input-range";
	private const string OutputKey = "output";
	private const string RandomSourceKey = "random-source";
	private const string RepeatKey = "repeat";
	private const string VersionKey = "version";
	private const string ZeroKey = "zero-terminated";

	/// <summary>Runs <c>shuf</c> synchronously with optional injected text streams.</summary>
	/// <param name="args">The command-line arguments.</param>
	/// <param name="standardInput">The standard-input reader.</param>
	/// <param name="standardOutput">The standard-output writer.</param>
	/// <param name="standardError">The standard-error writer.</param>
	/// <returns>The command exit status.</returns>
	public static int Run(
		string[] args,
		TextReader? standardInput = null,
		TextWriter? standardOutput = null,
		TextWriter? standardError = null
	) => RunAsync( args, standardInput, standardOutput, standardError ).GetAwaiter().GetResult();

	/// <summary>Runs <c>shuf</c> asynchronously with optional injected text streams.</summary>
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
			new CommandContext(
				"shuf",
				standardInput,
				standardOutput,
				standardError,
				inputAdapter,
				null,
				null,
				cancellationToken
			)
		).ConfigureAwait( false );
	}

	/// <summary>Runs <c>shuf</c> asynchronously against a command context.</summary>
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
			var terminal = parsed.Options.FirstOrDefault(
				option => option.Definition.Key is HelpKey or VersionKey
			);
			if ( terminal?.Definition.Key == HelpKey ) {
				await WriteHelpAsync( context.StandardOutput, context.CancellationToken ).ConfigureAwait( false );
				return CommandExitCodes.Success;
			}
			if ( terminal?.Definition.Key == VersionKey ) {
				await WriteVersionAsync( context.StandardOutput, context.CancellationToken ).ConfigureAwait( false );
				return CommandExitCodes.Success;
			}
			var options = await CreateOptionsAsync( parsed, context ).ConfigureAwait( false );
			if ( null == options ) {
				return CommandExitCodes.Failure;
			}
			await ShufEngine.ExecuteAsync( options, context ).ConfigureAwait( false );
			return CommandExitCodes.Success;
		} catch ( OperationCanceledException ) {
			return CommandExitCodes.Canceled;
		} catch ( Exception exception ) when (
			exception is IOException
			or UnauthorizedAccessException
			or InvalidOperationException
			or OverflowException
			or ArgumentException
			or NotSupportedException
		) {
			try {
				await context.Diagnostics.ErrorAsync(
					exception.Message,
					CancellationToken.None
				).ConfigureAwait( false );
			} catch {
				// Diagnostics must not replace the command's conventional failure status.
			}
			return CommandExitCodes.Failure;
		}
	}

	private static OptionParser CreateParser() => new(
		new[] {
			new OptionDefinition( EchoKey, 'e', new[] { "echo" } ),
			new OptionDefinition( HeadCountKey, 'n', new[] { "head-count" }, OptionValueArity.Required ),
			new OptionDefinition( InputRangeKey, 'i', new[] { "input-range" }, OptionValueArity.Required ),
			new OptionDefinition( OutputKey, 'o', new[] { "output" }, OptionValueArity.Required ),
			new OptionDefinition( RandomSourceKey, null, new[] { "random-source" }, OptionValueArity.Required ),
			new OptionDefinition( RepeatKey, 'r', new[] { "repeat" } ),
			new OptionDefinition( ZeroKey, 'z', new[] { "zero-terminated" } ),
			new OptionDefinition( HelpKey, null, new[] { "help" } ),
			new OptionDefinition( VersionKey, null, new[] { "version" } )
		},
		new OptionParserSettings {
			AllowLongOptionAbbreviations = true,
			Ordering = OptionOrdering.Permute
		}
	);

	private static async Task<ShufOptions?> CreateOptionsAsync(
		OptionParseResult parsed,
		CommandContext context
	) {
		var echo = parsed.HasOption( EchoKey );
		var rangeValues = GetValues( parsed, InputRangeKey );
		if ( 1 < rangeValues.Count ) {
			await WriteSemanticErrorAsync(
				context,
				"multiple -i options specified"
			).ConfigureAwait( false );
			return null;
		}
		var rangeText = 0 == rangeValues.Count ? null : rangeValues[0];
		if ( echo && null != rangeText ) {
			await WriteSemanticErrorAsync(
				context,
				"cannot combine --echo with --input-range"
			).ConfigureAwait( false );
			return null;
		}
		var inputMode = echo
			? ShufInputMode.Echo
			: null != rangeText
				? ShufInputMode.Range
				: ShufInputMode.Standard;
		if ( ShufInputMode.Range == inputMode && 0 != parsed.Operands.Count ) {
			await WriteSemanticErrorAsync(
				context,
				"extra operand is not permitted with --input-range"
			).ConfigureAwait( false );
			return null;
		}
		if ( ShufInputMode.Standard == inputMode && 1 < parsed.Operands.Count ) {
			await WriteSemanticErrorAsync(
				context,
				string.Concat( "extra operand '", parsed.Operands[1], "'" )
			).ConfigureAwait( false );
			return null;
		}
		BigInteger? headCount = null;
		foreach ( var headText in GetValues( parsed, HeadCountKey ) ) {
			if ( !TryParseNonnegativeInteger( headText, out var parsedHeadCount ) ) {
				await WriteSemanticErrorAsync(
					context,
					string.Concat( "invalid line count: '", headText, "'" )
				).ConfigureAwait( false );
				return null;
			}
			headCount = !headCount.HasValue || parsedHeadCount < headCount.Value
				? parsedHeadCount
				: headCount.Value;
		}
		var outputValues = GetValues( parsed, OutputKey );
		if ( !AllValuesAgree( outputValues ) ) {
			await WriteSemanticErrorAsync(
				context,
				"multiple output files specified"
			).ConfigureAwait( false );
			return null;
		}
		var randomSourceValues = GetValues( parsed, RandomSourceKey );
		if ( !AllValuesAgree( randomSourceValues ) ) {
			await WriteSemanticErrorAsync(
				context,
				"multiple random sources specified"
			).ConfigureAwait( false );
			return null;
		}
		ulong rangeLow = 0;
		ulong rangeHigh = 0;
		if (
			ShufInputMode.Range == inputMode
			&& !TryParseRange( rangeText!, out rangeLow, out rangeHigh )
		) {
			await WriteSemanticErrorAsync(
				context,
				string.Concat( "invalid input range: '", rangeText, "'" )
			).ConfigureAwait( false );
			return null;
		}
		return new ShufOptions(
			inputMode,
			parsed.Operands,
			rangeLow,
			rangeHigh,
			headCount,
			0 == outputValues.Count ? null : outputValues[^1],
			0 == randomSourceValues.Count ? null : randomSourceValues[^1],
			parsed.HasOption( RepeatKey ),
			parsed.HasOption( ZeroKey ) ? (byte)0 : (byte)'\n'
		);
	}

	private static IReadOnlyList<string> GetValues( OptionParseResult parsed, string key ) {
		return parsed.GetOccurrences( key )
			.Select( option => option.Value ?? string.Empty )
			.ToArray();
	}

	private static bool AllValuesAgree( IReadOnlyList<string> values ) {
		return 2 > values.Count || values.Skip( 1 ).All( value => value == values[0] );
	}

	private static bool TryParseNonnegativeInteger( string value, out BigInteger result ) {
		result = BigInteger.Zero;
		if ( string.IsNullOrEmpty( value ) ) {
			return false;
		}
		var digits = value.AsSpan().TrimStart();
		if ( !digits.IsEmpty && '+' == digits[0] ) {
			digits = digits[1..];
		}
		if ( digits.IsEmpty ) {
			return false;
		}
		foreach ( var character in digits ) {
			if ( character is < '0' or > '9' ) {
				return false;
			}
		}
		return BigInteger.TryParse(
			digits,
			NumberStyles.None,
			CultureInfo.InvariantCulture,
			out result
		);
	}

	private static bool TryParseRange( string value, out ulong low, out ulong high ) {
		low = 0;
		high = 0;
		if ( string.IsNullOrEmpty( value ) ) {
			return false;
		}
		var range = value.AsSpan().TrimStart();
		var searchStart = !range.IsEmpty && '+' == range[0] ? 1 : 0;
		var relativeSeparatorIndex = range[searchStart..].IndexOf( '-' );
		var separatorIndex = 0 > relativeSeparatorIndex
			? -1
			: searchStart + relativeSeparatorIndex;
		if ( 0 >= separatorIndex || separatorIndex == range.Length - 1 ) {
			return false;
		}
		var lowText = range[..separatorIndex];
		var highText = range[( separatorIndex + 1 )..];
		if ( !TryParseUnsignedEndpoint( lowText, out low ) || !TryParseUnsignedEndpoint( highText, out high ) ) {
			return false;
		}
		if ( high < low ) {
			return false;
		}
		return !( 0UL == low && ulong.MaxValue == high );
	}

	private static bool TryParseUnsignedEndpoint( ReadOnlySpan<char> value, out ulong result ) {
		value = value.TrimStart();
		if ( !value.IsEmpty && '+' == value[0] ) {
			value = value[1..];
		}
		if ( value.IsEmpty ) {
			result = 0;
			return false;
		}
		foreach ( var character in value ) {
			if ( character is < '0' or > '9' ) {
				result = 0;
				return false;
			}
		}
		return ulong.TryParse(
			value,
			NumberStyles.None,
			CultureInfo.InvariantCulture,
			out result
		);
	}

	private static async Task WriteOptionErrorAsync( OptionParseError error, CommandContext context ) {
		await context.StandardError.WriteLineAsync(
			OptionDiagnosticFormatter.Format( context.ProgramName, error ).AsMemory(),
			context.CancellationToken
		).ConfigureAwait( false );
		await context.StandardError.WriteLineAsync(
			"Try 'shuf --help' for more information.".AsMemory(),
			context.CancellationToken
		).ConfigureAwait( false );
	}

	private static async Task WriteSemanticErrorAsync( CommandContext context, string message ) {
		await context.Diagnostics.ErrorAsync( message, context.CancellationToken ).ConfigureAwait( false );
		await context.StandardError.WriteLineAsync(
			"Try 'shuf --help' for more information.".AsMemory(),
			context.CancellationToken
		).ConfigureAwait( false );
	}

	private static async Task WriteHelpAsync( TextWriter writer, CancellationToken cancellationToken ) {
		await writer.WriteAsync(
			string.Concat(
				"Usage: shuf [OPTION]... [FILE]", Environment.NewLine,
				"  or:  shuf -e [OPTION]... [ARG]...", Environment.NewLine,
				"  or:  shuf -i LO-HI [OPTION]...", Environment.NewLine,
				"Write a random permutation of the input records to standard output.", Environment.NewLine,
				Environment.NewLine,
				"  -e, --echo                treat each ARG as an input record", Environment.NewLine,
				"  -i, --input-range=LO-HI   treat each number LO through HI as an input record", Environment.NewLine,
				"  -n, --head-count=COUNT    output at most COUNT records", Environment.NewLine,
				"  -o, --output=FILE         write result to FILE instead of standard output", Environment.NewLine,
				"      --random-source=FILE  get random bytes from FILE", Environment.NewLine,
				"  -r, --repeat              output records can be repeated", Environment.NewLine,
				"  -z, --zero-terminated     records end with NUL, not newline", Environment.NewLine,
				"      --help                display this help and exit", Environment.NewLine,
				"      --version             output version information and exit", Environment.NewLine,
				Environment.NewLine,
				"With no FILE, or when FILE is -, read standard input.", Environment.NewLine
			).AsMemory(),
			cancellationToken
		).ConfigureAwait( false );
	}

	private static Task WriteVersionAsync( TextWriter writer, CancellationToken cancellationToken ) {
		return writer.WriteLineAsync(
			"shuf (Icod.CoreUtils) 1.0".AsMemory(),
			cancellationToken
		);
	}
}
