// Original behavior/reference: GNU coreutils
// Ported to .NET by Timothy J. Bruce <uniblab@hotmail.com>

namespace Icod.CoreUtils.Sort;

using System.Globalization;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using Icod.CoreUtils.Shared.CommandLine;
using Icod.CoreUtils.Shared.Diagnostics;
using Icod.CoreUtils.Shared.IO;
using Icod.CoreUtils.Shared.Ordering;
using Icod.CoreUtils.Shared.Records;
using Icod.CoreUtils.Shared.Temporary;

/// <summary>Implements GNU-compatible external sorting, checking, and merging.</summary>
public static class Command {
	private const long DefaultMemoryLimit = 32L * 1024L * 1024L;
	private const int SortFailure = 2;
	private const string VersionText = "sort (Icod.CoreUtils) 1.0";
	private static readonly UTF8Encoding Utf8 = new( false, false );

	/// <summary>Runs <c>sort</c> with text-stream compatibility injection.</summary>
	/// <param name="args">The command-line arguments.</param>
	/// <param name="stdin">The optional standard-input reader.</param>
	/// <param name="stdout">The optional standard-output writer.</param>
	/// <param name="stderr">The optional standard-error writer.</param>
	/// <returns>The GNU-compatible process status.</returns>
	/// <remarks>The asynchronous <see cref="RunAsync(string[], CommandContext)"/> overload is the byte-preserving production entry point.</remarks>
	public static int Run(
		string[] args,
		TextReader? stdin = null,
		TextWriter? stdout = null,
		TextWriter? stderr = null
	) {
		stdin ??= Console.In;
		stdout ??= Console.Out;
		stderr ??= Console.Error;
		using var input = new MemoryStream( Utf8.GetBytes( stdin.ReadToEnd() ), writable: false );
		var context = new CommandContext(
			"sort",
			stdin,
			stdout,
			stderr,
			input
		);
		return RunAsync( args, context ).GetAwaiter().GetResult();
	}

	/// <summary>Runs <c>sort</c> asynchronously with injected byte and text streams.</summary>
	/// <param name="args">The command-line arguments.</param>
	/// <param name="context">The command execution context.</param>
	/// <returns>A task whose result is the GNU-compatible process status.</returns>
	public static async Task<int> RunAsync(
		string[] args,
		CommandContext context
	) {
		ArgumentNullException.ThrowIfNull( args );
		ArgumentNullException.ThrowIfNull( context );
		try {
			var parser = CreateParser();
			var parsed = parser.Parse( args );
			if ( !parsed.IsSuccess ) {
				foreach ( var error in parsed.Errors ) {
					await context.StandardError.WriteLineAsync(
						OptionDiagnosticFormatter.Format( context.ProgramName, error ).AsMemory(),
						context.CancellationToken
					).ConfigureAwait( false );
				}
				return CommandExitCodes.UsageError;
			}
			if ( parsed.Options.Any( option => "help" == option.Definition.Key ) ) {
				await WriteHelpAsync( context ).ConfigureAwait( false );
				return CommandExitCodes.Success;
			}
			if ( parsed.Options.Any( option => "version" == option.Definition.Key ) ) {
				await context.StandardOutput.WriteLineAsync(
					VersionText.AsMemory(),
					context.CancellationToken
				).ConfigureAwait( false );
				return CommandExitCodes.Success;
			}
			if ( !TryCreateSettings( parsed, out var settings, out var settingsError ) ) {
				await context.Diagnostics.ErrorAsync(
					settingsError!,
					context.CancellationToken
				).ConfigureAwait( false );
				return CommandExitCodes.UsageError;
			}
			var effectiveSettings = settings!;
			await ExpandFileListAsync( effectiveSettings, context ).ConfigureAwait( false );
			if ( effectiveSettings.CheckMode ) {
				return await CheckAsync( effectiveSettings, context ).ConfigureAwait( false );
			}
			return await SortAsync( effectiveSettings, context ).ConfigureAwait( false );
		} catch ( OperationCanceledException ) when ( context.CancellationToken.IsCancellationRequested ) {
			return CommandExitCodes.Canceled;
		} catch ( Exception exception ) when (
			exception is IOException
			or UnauthorizedAccessException
			or InvalidDataException
			or InvalidOperationException
			or NotSupportedException
			or ArgumentException
			or CryptographicException
			or OverflowException
			or AggregateException
		) {
			await context.Diagnostics.ErrorAsync(
				exception.Message,
				CancellationToken.None
			).ConfigureAwait( false );
			return SortFailure;
		}
	}

	private static OptionParser CreateParser() {
		return new OptionParser(
			new[] {
				new OptionDefinition( "ignore-leading-blanks", 'b', new[] { "ignore-leading-blanks" } ),
				new OptionDefinition( "check-short", 'c' ),
				new OptionDefinition( "check", longNames: new[] { "check" }, valueArity: OptionValueArity.Optional ),
				new OptionDefinition( "check-quiet", 'C' ),
				new OptionDefinition( "dictionary-order", 'd', new[] { "dictionary-order" } ),
				new OptionDefinition( "ignore-case", 'f', new[] { "ignore-case" } ),
				new OptionDefinition( "general-numeric-sort", 'g', new[] { "general-numeric-sort" } ),
				new OptionDefinition( "human-numeric-sort", 'h', new[] { "human-numeric-sort" } ),
				new OptionDefinition( "ignore-nonprinting", 'i', new[] { "ignore-nonprinting" } ),
				new OptionDefinition( "key", 'k', new[] { "key" }, OptionValueArity.Required ),
				new OptionDefinition( "merge", 'm', new[] { "merge" } ),
				new OptionDefinition( "month-sort", 'M', new[] { "month-sort" } ),
				new OptionDefinition( "numeric-sort", 'n', new[] { "numeric-sort" } ),
				new OptionDefinition( "output", 'o', new[] { "output" }, OptionValueArity.Required ),
				new OptionDefinition( "random-sort", 'R', new[] { "random-sort" } ),
				new OptionDefinition( "reverse", 'r', new[] { "reverse" } ),
				new OptionDefinition( "stable", 's', new[] { "stable" } ),
				new OptionDefinition( "buffer-size", 'S', new[] { "buffer-size" }, OptionValueArity.Required ),
				new OptionDefinition( "field-separator", 't', new[] { "field-separator" }, OptionValueArity.Required ),
				new OptionDefinition( "temporary-directory", 'T', new[] { "temporary-directory" }, OptionValueArity.Required ),
				new OptionDefinition( "unique", 'u', new[] { "unique" } ),
				new OptionDefinition( "version-sort", 'V', new[] { "version-sort" } ),
				new OptionDefinition( "zero-terminated", 'z', new[] { "zero-terminated" } ),
				new OptionDefinition( "sort", longNames: new[] { "sort" }, valueArity: OptionValueArity.Required ),
				new OptionDefinition( "batch-size", longNames: new[] { "batch-size" }, valueArity: OptionValueArity.Required ),
				new OptionDefinition( "files0-from", longNames: new[] { "files0-from" }, valueArity: OptionValueArity.Required ),
				new OptionDefinition( "random-source", longNames: new[] { "random-source" }, valueArity: OptionValueArity.Required ),
				new OptionDefinition( "help", longNames: new[] { "help" }, allowMultiple: false ),
				new OptionDefinition( "version", longNames: new[] { "version" }, allowMultiple: false )
			}
		);
	}

	private static bool TryCreateSettings(
		OptionParseResult parsed,
		out SortSettings? settings,
		out string? error
	) {
		settings = new SortSettings {
			InputFiles = parsed.Operands.Count == 0
				? new List<string> { "-" }
				: parsed.Operands.ToList()
		};
		error = null;
		ComparisonMode? selectedGlobalMode = null;
		bool? selectedCheckQuiet = null;
		string? selectedFieldSeparator = null;
		foreach ( var option in parsed.Options ) {
			switch ( option.Definition.Key ) {
				case "ignore-leading-blanks":
					settings.GlobalModifiers.IgnoreLeadingBlanks = true;
					break;
				case "check-short":
					if ( !TrySelectCheckMode( true, ref selectedCheckQuiet, out error ) ) {
						return false;
					}
					settings.CheckMode = true;
					settings.CheckQuiet = false;
					break;
				case "check": {
					var quiet = option.Value is "quiet" or "silent";
					if ( null != option.Value && !quiet && !string.Equals( option.Value, "diagnose-first", StringComparison.Ordinal ) ) {
						error = string.Concat( "invalid argument '", option.Value, "' for '--check'" );
						return false;
					}
					if ( !TrySelectCheckMode( !quiet, ref selectedCheckQuiet, out error ) ) {
						return false;
					}
					settings.CheckMode = true;
					settings.CheckQuiet = quiet;
					break;
				}
				case "check-quiet":
					if ( !TrySelectCheckMode( false, ref selectedCheckQuiet, out error ) ) {
						return false;
					}
					settings.CheckMode = true;
					settings.CheckQuiet = true;
					break;
				case "dictionary-order":
					settings.GlobalModifiers.DictionaryOrder = true;
					break;
				case "ignore-case":
					settings.GlobalModifiers.IgnoreCase = true;
					break;
				case "general-numeric-sort":
					if ( !TrySelectComparisonMode( ComparisonMode.GeneralNumeric, "-g", ref selectedGlobalMode, out error ) ) {
						return false;
					}
					settings.GlobalModifiers.Mode = ComparisonMode.GeneralNumeric;
					break;
				case "human-numeric-sort":
					if ( !TrySelectComparisonMode( ComparisonMode.HumanNumeric, "-h", ref selectedGlobalMode, out error ) ) {
						return false;
					}
					settings.GlobalModifiers.Mode = ComparisonMode.HumanNumeric;
					break;
				case "ignore-nonprinting":
					settings.GlobalModifiers.IgnoreNonprinting = true;
					break;
				case "key": {
					var result = SortKeyParser.Parse( option.Value ?? string.Empty );
					if ( !result.IsSuccess ) {
						error = string.Concat( "invalid field specification '", option.Value, "': ", result.ErrorMessage );
						return false;
					}
					if ( HasIncompatibleKeyModes( result.Definition! ) ) {
						error = string.Concat( "incompatible ordering options in field specification '", option.Value, "'" );
						return false;
					}
					settings.Keys.Add( result.Definition! );
					break;
				}
				case "merge":
					settings.MergeMode = true;
					break;
				case "month-sort":
					if ( !TrySelectComparisonMode( ComparisonMode.Month, "-M", ref selectedGlobalMode, out error ) ) {
						return false;
					}
					settings.GlobalModifiers.Mode = ComparisonMode.Month;
					break;
				case "numeric-sort":
					if ( !TrySelectComparisonMode( ComparisonMode.Numeric, "-n", ref selectedGlobalMode, out error ) ) {
						return false;
					}
					settings.GlobalModifiers.Mode = ComparisonMode.Numeric;
					break;
				case "output":
					settings.OutputFile = option.Value;
					break;
				case "random-sort":
					if ( !TrySelectComparisonMode( ComparisonMode.Random, "-R", ref selectedGlobalMode, out error ) ) {
						return false;
					}
					settings.GlobalModifiers.Mode = ComparisonMode.Random;
					break;
				case "reverse":
					settings.GlobalModifiers.Reverse = true;
					break;
				case "stable":
					settings.Stable = true;
					break;
				case "buffer-size":
					if ( !TryParseMemorySize( option.Value, out var memoryLimit ) ) {
						error = string.Concat( "invalid --buffer-size argument '", option.Value, "'" );
						return false;
					}
					settings.MemoryLimit = memoryLimit;
					break;
				case "field-separator":
					if (
						string.IsNullOrEmpty( option.Value )
						|| !Rune.TryGetRuneAt( option.Value, 0, out var separatorRune )
						|| separatorRune.Utf16SequenceLength != option.Value.Length
					) {
						error = "multi-character tab is not supported";
						return false;
					}
					if ( null != selectedFieldSeparator && !string.Equals( selectedFieldSeparator, option.Value, StringComparison.Ordinal ) ) {
						error = "incompatible field separators";
						return false;
					}
					selectedFieldSeparator = option.Value;
					settings.FieldSeparator = Utf8.GetBytes( option.Value );
					break;
				case "temporary-directory":
					settings.TemporaryDirectories.Add( option.Value! );
					break;
				case "unique":
					settings.Unique = true;
					break;
				case "version-sort":
					if ( !TrySelectComparisonMode( ComparisonMode.Version, "-V", ref selectedGlobalMode, out error ) ) {
						return false;
					}
					settings.GlobalModifiers.Mode = ComparisonMode.Version;
					break;
				case "zero-terminated":
					settings.ZeroTerminated = true;
					break;
				case "sort":
					if ( !TryParseSortMode( option.Value, out var mode ) ) {
						error = string.Concat( "invalid argument '", option.Value, "' for '--sort'" );
						return false;
					}
					if ( !TrySelectComparisonMode( mode, string.Concat( "--sort=", option.Value ), ref selectedGlobalMode, out error ) ) {
						return false;
					}
					settings.GlobalModifiers.Mode = mode;
					break;
				case "batch-size":
					if ( !int.TryParse( option.Value, NumberStyles.None, CultureInfo.InvariantCulture, out var fanIn ) || 2 > fanIn ) {
						error = string.Concat( "invalid --batch-size argument '", option.Value, "'" );
						return false;
					}
					settings.MergeFanIn = fanIn;
					break;
				case "files0-from":
					settings.Files0From = option.Value;
					break;
				case "random-source":
					settings.RandomSource = option.Value;
					break;
			}
		}
		if ( HasIncompatibleCharacterFiltering( settings.GlobalModifiers ) ) {
			error = "dictionary-order and ignore-nonprinting are incompatible with numeric and month ordering";
			return false;
		}
		if ( settings.CheckMode && null != settings.OutputFile ) {
			error = "the --output option is incompatible with --check";
			return false;
		}
		if ( settings.CheckMode && 1 < settings.InputFiles.Count ) {
			error = "extra operand; --check accepts at most one input file";
			return false;
		}
		if ( null != settings.Files0From && 0 < parsed.Operands.Count ) {
			error = "extra operand is not permitted with --files0-from";
			return false;
		}
		if ( null != settings.Files0From ) {
			settings.InputFiles.Clear();
		}
		if ( 1 < settings.TemporaryDirectories.Count ) {
			error = "multiple --temporary-directory options are not supported";
			return false;
		}
		if ( 0 < settings.TemporaryDirectories.Count ) {
			foreach ( var directory in settings.TemporaryDirectories ) {
				if ( !Directory.Exists( directory ) ) {
					error = string.Concat( "temporary directory does not exist: ", directory );
					return false;
				}
			}
		}
		return true;
	}

	private static bool TrySelectCheckMode(
		bool diagnose,
		ref bool? selectedQuiet,
		out string? error
	) {
		var quiet = !diagnose;
		if ( selectedQuiet.HasValue && selectedQuiet.Value != quiet ) {
			error = "diagnosing and quiet check modes are incompatible";
			return false;
		}
		selectedQuiet = quiet;
		error = null;
		return true;
	}

	private static bool TrySelectComparisonMode(
		ComparisonMode mode,
		string optionName,
		ref ComparisonMode? selectedMode,
		out string? error
	) {
		if ( selectedMode.HasValue && selectedMode.Value != mode ) {
			error = string.Concat( "ordering option '", optionName, "' is incompatible with an earlier ordering option" );
			return false;
		}
		selectedMode = mode;
		error = null;
		return true;
	}

	private static bool HasIncompatibleKeyModes( SortKeyDefinition definition ) {
		var count = 0;
		foreach ( var option in "ghMnRV" ) {
			if ( 0 <= definition.Options.IndexOf( option ) ) {
				count++;
			}
		}
		var hasCharacterFiltering = 0 <= definition.Options.IndexOf( 'd' )
			|| 0 <= definition.Options.IndexOf( 'i' );
		var hasNumericOrMonthOrdering = definition.Options.Any( option => option is 'g' or 'h' or 'M' or 'n' );
		return 1 < count || ( hasCharacterFiltering && hasNumericOrMonthOrdering );
	}

	private static bool HasIncompatibleCharacterFiltering( ComparisonModifiers modifiers ) {
		return ( modifiers.DictionaryOrder || modifiers.IgnoreNonprinting )
			&& modifiers.Mode is ComparisonMode.GeneralNumeric
				or ComparisonMode.HumanNumeric
				or ComparisonMode.Month
				or ComparisonMode.Numeric;
	}

	private static async Task ExpandFileListAsync(
		SortSettings settings,
		CommandContext context
	) {
		if ( null == settings.Files0From ) {
			return;
		}
		await using var source = InputSource.OpenBinary(
			InputOperand.Create( settings.Files0From ),
			context
		);
		using var reader = new ByteRecordReader( source.BinaryStream!, RecordSeparator.Null );
		var files = new List<string>();
		while ( true ) {
			var record = await reader.ReadAsync( context.CancellationToken ).ConfigureAwait( false );
			if ( null == record ) {
				break;
			}
			if ( record.Content.IsEmpty ) {
				throw new ArgumentException( "file name list contains an empty file name" );
			}
			files.Add( Utf8.GetString( record.Content.Span ) );
		}
		if ( "-" == settings.Files0From && files.Any( file => "-" == file ) ) {
			throw new ArgumentException( "when reading file names from standard input, no input file may be '-'" );
		}
		if ( settings.CheckMode && 1 < files.Count ) {
			throw new ArgumentException( "extra operand; --check accepts at most one input file" );
		}
		settings.InputFiles = files;
	}

	private static async Task<int> SortAsync(
		SortSettings settings,
		CommandContext context
	) {
		var collation = ResolveCollation();
		var randomSeed = UsesRandomComparison( settings )
			? await ReadRandomSeedAsync( settings, context ).ConfigureAwait( false )
			: Array.Empty<byte>();
		var comparer = new SortRecordComparer(
			settings,
			collation,
			randomSeed,
			ResolveLocaleCategory( "LC_CTYPE" ),
			ResolveLocaleCategory( "LC_NUMERIC" ),
			ResolveLocaleCategory( "LC_TIME" )
		);
		if ( null == settings.OutputFile ) {
			await using var destination = new ByteOutputStream(
				context.StandardOutput,
				context.StandardOutputStream
			);
			await OrderToStreamAsync( settings, context, comparer, destination ).ConfigureAwait( false );
			await destination.CompleteAsync( context.CancellationToken ).ConfigureAwait( false );
			return CommandExitCodes.Success;
		}
		var outputPath = Path.GetFullPath( settings.OutputFile );
		var outputDirectory = Path.GetDirectoryName( outputPath )
			?? throw new IOException( "cannot determine the output directory" );
		await using var workspace = TemporaryWorkspace.Create(
			outputDirectory,
			"sort-output.XXXXXXXX",
			cancellationToken: context.CancellationToken
		);
		var spoolPath = workspace.CreateFile( "output-XXXXXXXX.tmp", context.CancellationToken );
		var streamOptions = new FileStreamOptions {
			Mode = FileMode.Open,
			Access = FileAccess.Write,
			Share = FileShare.None,
			Options = FileOptions.Asynchronous | FileOptions.SequentialScan
		};
		await using ( var destination = new FileStream( spoolPath, streamOptions ) ) {
			destination.SetLength( 0 );
			await OrderToStreamAsync( settings, context, comparer, destination ).ConfigureAwait( false );
			await destination.FlushAsync( context.CancellationToken ).ConfigureAwait( false );
		}
		var inputOptions = new FileStreamOptions {
			Mode = FileMode.Open,
			Access = FileAccess.Read,
			Share = FileShare.Read,
			Options = FileOptions.Asynchronous | FileOptions.SequentialScan
		};
		var outputOptions = new FileStreamOptions {
			Mode = FileMode.Create,
			Access = FileAccess.Write,
			Share = FileShare.None,
			Options = FileOptions.Asynchronous | FileOptions.SequentialScan
		};
		await using ( var input = new FileStream( spoolPath, inputOptions ) )
		await using ( var output = new FileStream( outputPath, outputOptions ) ) {
			await input.CopyToAsync( output, context.CancellationToken ).ConfigureAwait( false );
			await output.FlushAsync( context.CancellationToken ).ConfigureAwait( false );
		}
		return CommandExitCodes.Success;
	}

	private static async Task OrderToStreamAsync(
		SortSettings settings,
		CommandContext context,
		SortRecordComparer comparer,
		Stream destination
	) {
		if ( settings.MergeMode ) {
			await MergeToStreamAsync( settings, context, comparer, destination ).ConfigureAwait( false );
			return;
		}
		var separator = settings.ZeroTerminated ? RecordSeparator.Null : RecordSeparator.LineFeed;
		var writer = new DelimitedByteRecordWriter( destination, separator );
		var options = new ExternalOrderingOptions<ByteRecord>(
			settings.MemoryLimit,
			record => record.Content.Length,
			mergeFanIn: settings.MergeFanIn
		);
		var temporaryDirectory = ResolveTemporaryDirectory( settings );
		var engine = new ExternalOrderingEngine<ByteRecord>(
			comparer,
			new ByteRecordRunCodec(),
			options,
			token => TemporaryWorkspace.Create(
				temporaryDirectory,
				cancellationToken: token
			)
		);
		ByteRecord? previous = null;
		await engine.OrderAsync(
			ReadInputRecordsAsync( settings, context, separator ),
			async ( record, token ) => {
				if ( settings.Unique && null != previous && 0 == comparer.CompareEquivalent( previous, record ) ) {
					return;
				}
				await writer.WriteRecordAsync( record.Content, terminate: true, token ).ConfigureAwait( false );
				previous = record;
			},
			context.CancellationToken
		).ConfigureAwait( false );
		await writer.FlushAsync( context.CancellationToken ).ConfigureAwait( false );
	}

	private static async Task MergeToStreamAsync(
		SortSettings settings,
		CommandContext context,
		SortRecordComparer comparer,
		Stream destination
	) {
		var separator = settings.ZeroTerminated ? RecordSeparator.Null : RecordSeparator.LineFeed;
		var inputs = GetInputFiles( settings )
			.Select( path => new MergeInput( path, IsTemporary: false ) )
			.ToList();
		if ( inputs.Count <= settings.MergeFanIn ) {
			await MergeInputsAsync(
				inputs,
				context,
				comparer,
				destination,
				separator,
				settings.Unique
			).ConfigureAwait( false );
			return;
		}
		await using var workspace = TemporaryWorkspace.Create(
			ResolveTemporaryDirectory( settings ),
			cancellationToken: context.CancellationToken
		);
		while ( inputs.Count > settings.MergeFanIn ) {
			var nextPass = new List<MergeInput>();
			for ( var offset = 0; offset < inputs.Count; offset += settings.MergeFanIn ) {
				var count = Math.Min( settings.MergeFanIn, inputs.Count - offset );
				var group = inputs.GetRange( offset, count );
				var path = workspace.CreateFile( "merge-XXXXXXXX.run", context.CancellationToken );
				var streamOptions = new FileStreamOptions {
					Mode = FileMode.Open,
					Access = FileAccess.Write,
					Share = FileShare.None,
					Options = FileOptions.Asynchronous | FileOptions.SequentialScan
				};
				await using ( var stream = new FileStream( path, streamOptions ) ) {
					stream.SetLength( 0 );
					await MergeInputsAsync(
						group,
						context,
						comparer,
						stream,
						separator,
						settings.Unique
					).ConfigureAwait( false );
				}
				nextPass.Add( new MergeInput( path, IsTemporary: true ) );
				foreach ( var input in group.Where( input => input.IsTemporary ) ) {
					workspace.DeleteFile( input.Path );
				}
			}
			inputs = nextPass;
		}
		await MergeInputsAsync(
			inputs,
			context,
			comparer,
			destination,
			separator,
			settings.Unique
		).ConfigureAwait( false );
	}

	private static async Task MergeInputsAsync(
		IReadOnlyList<MergeInput> inputs,
		CommandContext context,
		SortRecordComparer comparer,
		Stream destination,
		RecordSeparator separator,
		bool unique
	) {
		var writer = new DelimitedByteRecordWriter( destination, separator );
		if ( 0 == inputs.Count ) {
			await writer.FlushAsync( context.CancellationToken ).ConfigureAwait( false );
			return;
		}
		var queue = new PriorityQueue<MergeCursor, MergeCursor>(
			new MergeCursorComparer( comparer )
		);
		var cursors = new List<MergeCursor>( inputs.Count );
		try {
			for ( var index = 0; index < inputs.Count; index++ ) {
				var source = InputSource.OpenBinary( InputOperand.Create( inputs[ index ].Path ), context );
				var cursor = new MergeCursor( source, separator, index );
				cursors.Add( cursor );
				if ( await cursor.AdvanceAsync( context.CancellationToken ).ConfigureAwait( false ) ) {
					queue.Enqueue( cursor, cursor );
				}
			}
			ByteRecord? previous = null;
			while ( queue.TryDequeue( out var cursor, out _ ) ) {
				context.CancellationToken.ThrowIfCancellationRequested();
				var record = cursor.Current!;
				if ( !unique || null == previous || 0 != comparer.CompareEquivalent( previous, record ) ) {
					await writer.WriteRecordAsync( record.Content, terminate: true, context.CancellationToken ).ConfigureAwait( false );
					previous = record;
				}
				if ( await cursor.AdvanceAsync( context.CancellationToken ).ConfigureAwait( false ) ) {
					queue.Enqueue( cursor, cursor );
				}
			}
			await writer.FlushAsync( context.CancellationToken ).ConfigureAwait( false );
		} finally {
			foreach ( var cursor in cursors ) {
				await cursor.DisposeAsync().ConfigureAwait( false );
			}
		}
	}

	private static string? ResolveTemporaryDirectory( SortSettings settings ) {
		return settings.TemporaryDirectories.FirstOrDefault()
			?? Environment.GetEnvironmentVariable( "TMPDIR" );
	}

	private static async Task<int> CheckAsync(
		SortSettings settings,
		CommandContext context
	) {
		var collation = ResolveCollation();
		var randomSeed = UsesRandomComparison( settings )
			? await ReadRandomSeedAsync( settings, context ).ConfigureAwait( false )
			: Array.Empty<byte>();
		var comparer = new SortRecordComparer(
			settings,
			collation,
			randomSeed,
			ResolveLocaleCategory( "LC_CTYPE" ),
			ResolveLocaleCategory( "LC_NUMERIC" ),
			ResolveLocaleCategory( "LC_TIME" )
		);
		var separator = settings.ZeroTerminated ? RecordSeparator.Null : RecordSeparator.LineFeed;
		ByteRecord? previous = null;
		long lineNumber = 0;
		await foreach ( var current in ReadInputRecordsAsync( settings, context, separator ).WithCancellation( context.CancellationToken ) ) {
			lineNumber++;
			var disorder = null != previous && 0 > comparer.Compare( current, previous );
			var duplicate = settings.Unique && null != previous && 0 == comparer.CompareEquivalent( previous, current );
			if ( disorder || duplicate ) {
				if ( !settings.CheckQuiet ) {
					var sourceName = settings.InputFiles.Count == 0 || "-" == settings.InputFiles[ 0 ]
						? "-"
						: settings.InputFiles[ 0 ];
					await context.Diagnostics.ErrorAsync(
						string.Concat(
							sourceName,
							":",
							lineNumber.ToString( CultureInfo.InvariantCulture ),
							": disorder: ",
							EscapeDiagnosticRecord( current.Content.Span )
						),
						context.CancellationToken
					).ConfigureAwait( false );
				}
				return CommandExitCodes.Failure;
			}
			previous = current;
		}
		return CommandExitCodes.Success;
	}

	private static async IAsyncEnumerable<ByteRecord> ReadInputRecordsAsync(
		SortSettings settings,
		CommandContext context,
		RecordSeparator separator,
		[EnumeratorCancellation] CancellationToken cancellationToken = default
	) {
		var files = GetInputFiles( settings );
		foreach ( var file in files ) {
			cancellationToken.ThrowIfCancellationRequested();
			await using var source = InputSource.OpenBinary( InputOperand.Create( file ), context );
			using var reader = new ByteRecordReader( source.BinaryStream!, separator );
			while ( true ) {
				var record = await reader.ReadAsync( cancellationToken ).ConfigureAwait( false );
				if ( null == record ) {
					break;
				}
				yield return record;
			}
		}
	}

	private static IReadOnlyList<string> GetInputFiles( SortSettings settings ) {
		return settings.InputFiles.Count == 0 && null == settings.Files0From
			? new[] { "-" }
			: settings.InputFiles;
	}

	private static ICollationProvider ResolveCollation() {
		var resolution = CollationEnvironment.ResolveCurrent();
		if ( !resolution.IsSuccess ) {
			throw new NotSupportedException( resolution.ErrorMessage );
		}
		return new SystemCollationProvider( resolution.Profile! );
	}

	private static LocaleCategoryProfile ResolveLocaleCategory( string categoryVariable ) {
		var lcAll = Environment.GetEnvironmentVariable( "LC_ALL" );
		var category = Environment.GetEnvironmentVariable( categoryVariable );
		var lang = Environment.GetEnvironmentVariable( "LANG" );
		var resolution = CollationEnvironment.Resolve(
			lcAll,
			category,
			lang,
			CultureInfo.CurrentCulture
		);
		if ( !resolution.IsSuccess ) {
			throw new NotSupportedException( resolution.ErrorMessage );
		}
		var profile = resolution.Profile!;
		var culture = profile.Culture ?? CultureInfo.InvariantCulture;
		var isBytewise = profile.IsBytewise;
		var localeName = FirstNonemptyLocaleName( lcAll, category, lang );
		if (
			isBytewise
			&& string.Equals( categoryVariable, "LC_CTYPE", StringComparison.Ordinal )
			&& IsUtf8CLocale( localeName )
		) {
			isBytewise = false;
		}
		if ( profile.IsBytewise && string.Equals( categoryVariable, "LC_NUMERIC", StringComparison.Ordinal ) ) {
			var posixNumericCulture = (CultureInfo)CultureInfo.InvariantCulture.Clone();
			posixNumericCulture.NumberFormat.NumberGroupSeparator = string.Empty;
			culture = posixNumericCulture;
		}
		return new LocaleCategoryProfile( culture, isBytewise );
	}

	private static string? FirstNonemptyLocaleName( params string?[] values ) {
		foreach ( var value in values ) {
			if ( !string.IsNullOrWhiteSpace( value ) ) {
				return value.Trim();
			}
		}
		return null;
	}

	private static bool IsUtf8CLocale( string? value ) {
		if ( string.IsNullOrEmpty( value ) || !value.StartsWith( "C.", StringComparison.OrdinalIgnoreCase ) ) {
			return false;
		}
		var encoding = value[ 2.. ];
		var modifier = encoding.IndexOf( '@' );
		if ( 0 <= modifier ) {
			encoding = encoding[..modifier];
		}
		encoding = encoding.Replace( "-", string.Empty, StringComparison.Ordinal )
			.Replace( "_", string.Empty, StringComparison.Ordinal );
		return string.Equals( encoding, "UTF8", StringComparison.OrdinalIgnoreCase );
	}

	private static bool UsesRandomComparison( SortSettings settings ) {
		if ( 0 == settings.Keys.Count ) {
			return ComparisonMode.Random == settings.GlobalModifiers.Mode;
		}
		foreach ( var definition in settings.Keys ) {
			var hasLocalOrdering = 0 < definition.Options.Length
				|| definition.Start.SkipLeadingBlanks
				|| ( definition.End?.SkipLeadingBlanks ?? false );
			if ( hasLocalOrdering ) {
				if ( 0 <= definition.Options.IndexOf( 'R' ) ) {
					return true;
				}
			} else if ( ComparisonMode.Random == settings.GlobalModifiers.Mode ) {
				return true;
			}
		}
		return false;
	}

	private static async Task<byte[]> ReadRandomSeedAsync(
		SortSettings settings,
		CommandContext context
	) {
		if ( null == settings.RandomSource ) {
			return RandomNumberGenerator.GetBytes( 32 );
		}
		await using var source = InputSource.OpenBinary(
			InputOperand.Create( settings.RandomSource ),
			context
		);
		using var hash = IncrementalHash.CreateHash( HashAlgorithmName.SHA256 );
		var buffer = new byte[ 81920 ];
		while ( true ) {
			var count = await source.BinaryStream!.ReadAsync(
				buffer.AsMemory(),
				context.CancellationToken
			).ConfigureAwait( false );
			if ( 0 == count ) {
				break;
			}
			hash.AppendData( buffer, 0, count );
		}
		return hash.GetHashAndReset();
	}

	private static bool TryParseSortMode(
		string? value,
		out ComparisonMode mode
	) {
		mode = value switch {
			"general-numeric" => ComparisonMode.GeneralNumeric,
			"human-numeric" => ComparisonMode.HumanNumeric,
			"month" => ComparisonMode.Month,
			"numeric" => ComparisonMode.Numeric,
			"random" => ComparisonMode.Random,
			"version" => ComparisonMode.Version,
			_ => ComparisonMode.Lexical
		};
		return value is "general-numeric" or "human-numeric" or "month" or "numeric" or "random" or "version";
	}

	private static bool TryParseMemorySize(
		string? value,
		out long bytes
	) {
		bytes = 0;
		if ( string.IsNullOrWhiteSpace( value ) ) {
			return false;
		}
		value = value.Trim();
		if ( value.EndsWith( '%' ) ) {
			if ( !int.TryParse( value.AsSpan( 0, value.Length - 1 ), NumberStyles.None, CultureInfo.InvariantCulture, out var percent ) || 0 >= percent ) {
				return false;
			}
			try {
				bytes = checked( GC.GetGCMemoryInfo().TotalAvailableMemoryBytes * percent / 100 );
				return 0 < bytes;
			} catch ( OverflowException ) {
				return false;
			}
		}
		var index = 0;
		while ( index < value.Length && char.IsAsciiDigit( value[ index ] ) ) {
			index++;
		}
		if ( 0 == index || !long.TryParse( value.AsSpan( 0, index ), NumberStyles.None, CultureInfo.InvariantCulture, out var number ) || 0 >= number ) {
			return false;
		}
		var suffix = value[ index.. ];
		long multiplier;
		if ( 0 == suffix.Length || string.Equals( suffix, "B", StringComparison.Ordinal ) ) {
			multiplier = 1;
		} else if ( string.Equals( suffix, "b", StringComparison.Ordinal ) ) {
			multiplier = 512;
		} else {
			var unit = char.ToUpperInvariant( suffix[ 0 ] );
			var exponent = "KMGTPEZYRQ".IndexOf( unit ) + 1;
			if ( 0 >= exponent ) {
				return false;
			}
			long radix;
			if ( 1 == suffix.Length ) {
				radix = 1024;
			} else if ( 2 == suffix.Length && 'B' == suffix[ 1 ] ) {
				radix = 1000;
			} else if ( 3 == suffix.Length && ( suffix[ 1 ] is 'i' or 'I' ) && 'B' == suffix[ 2 ] ) {
				radix = 1024;
			} else {
				return false;
			}
			multiplier = 1;
			try {
				for ( var power = 0; power < exponent; power++ ) {
					multiplier = checked( multiplier * radix );
				}
			} catch ( OverflowException ) {
				return false;
			}
		}
		try {
			bytes = checked( number * multiplier );
			return 0 < bytes;
		} catch ( OverflowException ) {
			return false;
		}
	}

	private static string EscapeDiagnosticRecord( ReadOnlySpan<byte> content ) {
		var builder = new StringBuilder();
		foreach ( var value in content ) {
			if ( 0x20 <= value && 0x7e >= value ) {
				builder.Append( (char)value );
			} else {
				builder.Append( "\\x" );
				builder.Append( value.ToString( "x2", CultureInfo.InvariantCulture ) );
			}
		}
		return builder.ToString();
	}

	private static async Task WriteHelpAsync( CommandContext context ) {
		const string help = """
Usage: sort [OPTION]... [FILE]...
Write sorted concatenation of all FILE(s) to standard output.

Ordering options:
  -b, --ignore-leading-blanks  ignore leading blanks
  -d, --dictionary-order      consider only blanks and alphanumeric characters
  -f, --ignore-case           fold lower case to upper case
  -g, --general-numeric-sort  compare according to general numerical value
  -h, --human-numeric-sort    compare human readable numbers
  -i, --ignore-nonprinting    consider only printable characters
  -M, --month-sort            compare month abbreviations
  -n, --numeric-sort          compare exact numeric prefixes
  -R, --random-sort           shuffle while grouping equal keys
  -r, --reverse               reverse comparison results
  -V, --version-sort          natural sort of version numbers

Other options:
  -c, --check[=MODE]          check for sorted input; MODE is diagnose-first, quiet, or silent
  -C, --check=quiet           check without diagnostics
  -k, --key=KEYDEF            compare by a field key
  -m, --merge                 merge already sorted inputs
  -o, --output=FILE           write to FILE
  -s, --stable                disable last-resort record comparison
  -S, --buffer-size=SIZE      set the in-memory run budget
  -t, --field-separator=SEP   use SEP instead of blank transitions
  -T, --temporary-directory=DIR use DIR for secure temporary runs
  -u, --unique                output only the first equal key
  -z, --zero-terminated       end records with NUL instead of newline
      --batch-size=N          merge at most N runs at once
      --files0-from=FILE      read NUL-terminated input names from FILE
      --random-source=FILE    obtain random-sort seed bytes from FILE
      --sort=WORD             compare by general-numeric, human-numeric, month,
                              numeric, random, or version ordering
      --help                  display this help and exit
      --version               output version information and exit
""";
		await context.StandardOutput.WriteAsync(
			help.AsMemory(),
			context.CancellationToken
		).ConfigureAwait( false );
	}

	private sealed record LocaleCategoryProfile(
		CultureInfo Culture,
		bool IsBytewise
	);

	private enum ComparisonMode {
		Lexical,
		Numeric,
		GeneralNumeric,
		HumanNumeric,
		Month,
		Random,
		Version
	}

	private sealed class ComparisonModifiers {
		/// <summary>Gets or sets whether dictionary-order filtering is applied.</summary>
		public bool DictionaryOrder { get; set; }
		/// <summary>Gets or sets whether case is folded before comparison.</summary>
		public bool IgnoreCase { get; set; }
		/// <summary>Gets or sets whether leading blanks are excluded from the comparison key.</summary>
		public bool IgnoreLeadingBlanks { get; set; }
		/// <summary>Gets or sets whether nonprinting characters are excluded.</summary>
		public bool IgnoreNonprinting { get; set; }
		/// <summary>Gets or sets the comparison family.</summary>
		public ComparisonMode Mode { get; set; }
		/// <summary>Gets or sets whether comparison results are reversed.</summary>
		public bool Reverse { get; set; }
		/// <summary>Creates an independent copy of the modifiers.</summary>
		/// <returns>The copied modifier set.</returns>
		public ComparisonModifiers Clone() => new() {
			DictionaryOrder = this.DictionaryOrder,
			IgnoreCase = this.IgnoreCase,
			IgnoreLeadingBlanks = this.IgnoreLeadingBlanks,
			IgnoreNonprinting = this.IgnoreNonprinting,
			Mode = this.Mode,
			Reverse = this.Reverse
		};
	}

	private sealed class SortSettings {
		/// <summary>Gets or sets the maximum merge fan-in.</summary>
		public int MergeFanIn { get; set; } = ExternalOrderingOptions<ByteRecord>.DefaultMergeFanIn;
		/// <summary>Gets or sets whether input order is checked rather than emitted.</summary>
		public bool CheckMode { get; set; }
		/// <summary>Gets or sets whether check-mode diagnostics are suppressed.</summary>
		public bool CheckQuiet { get; set; }
		/// <summary>Gets or sets the UTF-8 field-separator bytes.</summary>
		public byte[]? FieldSeparator { get; set; }
		/// <summary>Gets or sets the source of NUL-delimited input pathnames.</summary>
		public string? Files0From { get; set; }
		/// <summary>Gets the global comparison modifiers.</summary>
		public ComparisonModifiers GlobalModifiers { get; } = new();
		/// <summary>Gets or sets the input pathname operands.</summary>
		public List<string> InputFiles { get; set; } = new();
		/// <summary>Gets the explicit key definitions.</summary>
		public List<SortKeyDefinition> Keys { get; } = new();
		/// <summary>Gets or sets the approximate in-memory run budget.</summary>
		public long MemoryLimit { get; set; } = DefaultMemoryLimit;
		/// <summary>Gets or sets whether already sorted inputs are merged.</summary>
		public bool MergeMode { get; set; }
		/// <summary>Gets or sets the output pathname.</summary>
		public string? OutputFile { get; set; }
		/// <summary>Gets or sets the random-seed source pathname.</summary>
		public string? RandomSource { get; set; }
		/// <summary>Gets or sets whether the last-resort whole-record comparison is disabled.</summary>
		public bool Stable { get; set; }
		/// <summary>Gets the requested temporary directories.</summary>
		public List<string> TemporaryDirectories { get; } = new();
		/// <summary>Gets or sets whether only the first record from each equal-key group is emitted.</summary>
		public bool Unique { get; set; }
		/// <summary>Gets or sets whether records use NUL termination.</summary>
		public bool ZeroTerminated { get; set; }
	}

	private sealed class SortRecordComparer : IComparer<ByteRecord> {
		private readonly ICollationProvider collation;
		private readonly LocaleCategoryProfile characterCategory;
		private readonly IReadOnlyList<KeyPlan> keyPlans;
		private readonly CultureInfo numericCulture;
		private readonly byte[] randomSeed;
		private readonly SortSettings settings;
		private readonly CultureInfo timeCulture;

		/// <summary>Initializes a record comparer from the complete command settings.</summary>
		/// <param name="settings">The validated command settings.</param>
		/// <param name="collation">The resolved collation provider.</param>
		/// <param name="randomSeed">The random-comparison seed, or an empty array when unused.</param>
		/// <param name="characterCategory">The resolved <c>LC_CTYPE</c> category.</param>
		/// <param name="numericCategory">The resolved <c>LC_NUMERIC</c> category.</param>
		/// <param name="timeCategory">The resolved <c>LC_TIME</c> category.</param>
		public SortRecordComparer(
			SortSettings settings,
			ICollationProvider collation,
			byte[] randomSeed,
			LocaleCategoryProfile characterCategory,
			LocaleCategoryProfile numericCategory,
			LocaleCategoryProfile timeCategory
		) {
			this.settings = settings;
			this.collation = collation;
			this.randomSeed = randomSeed;
			this.characterCategory = characterCategory;
			this.numericCulture = numericCategory.Culture;
			this.timeCulture = timeCategory.Culture;
			this.keyPlans = settings.Keys
				.Select( definition => CreateKeyPlan( definition, settings.GlobalModifiers ) )
				.ToArray();
		}

		/// <summary>Compares two records, including the GNU last-resort comparison when enabled.</summary>
		/// <param name="x">The first record.</param>
		/// <param name="y">The second record.</param>
		/// <returns>A signed ordering result.</returns>
		public int Compare( ByteRecord? x, ByteRecord? y ) {
			if ( ReferenceEquals( x, y ) ) {
				return 0;
			}
			if ( null == x ) {
				return -1;
			}
			if ( null == y ) {
				return 1;
			}
			var result = this.CompareEquivalent( x, y );
			if ( 0 != result || this.settings.Stable || this.settings.Unique ) {
				return result;
			}
			result = this.CompareLexical(
				this.DecodeWholeRecord( x.Content.Span ),
				this.DecodeWholeRecord( y.Content.Span )
			);
			return this.settings.GlobalModifiers.Reverse ? -result : result;
		}

		/// <summary>Compares only the selected sort keys.</summary>
		/// <param name="x">The first record.</param>
		/// <param name="y">The second record.</param>
		/// <returns>A signed key-ordering result.</returns>
		public int CompareEquivalent( ByteRecord x, ByteRecord y ) {
			if ( 0 == this.keyPlans.Count ) {
				return this.CompareValue(
					this.Decode( ExtractDefaultKey( x.Content.Span, this.settings.GlobalModifiers ), this.settings.GlobalModifiers ),
					this.Decode( ExtractDefaultKey( y.Content.Span, this.settings.GlobalModifiers ), this.settings.GlobalModifiers ),
					this.settings.GlobalModifiers
				);
			}
			foreach ( var plan in this.keyPlans ) {
				var result = this.CompareValue(
					this.Decode( ExtractKey( x.Content.Span, plan, this.settings.FieldSeparator ), plan.Modifiers ),
					this.Decode( ExtractKey( y.Content.Span, plan, this.settings.FieldSeparator ), plan.Modifiers ),
					plan.Modifiers
				);
				if ( 0 != result ) {
					return result;
				}
			}
			return 0;
		}

		private string Decode(
			ReadOnlySpan<byte> value,
			ComparisonModifiers modifiers
		) {
			var hasCharacterTransformation = modifiers.DictionaryOrder
				|| modifiers.IgnoreCase
				|| modifiers.IgnoreNonprinting;
			var rawByteComparison = modifiers.Mode is ComparisonMode.Version or ComparisonMode.Random
				|| ( ComparisonMode.Lexical == modifiers.Mode && this.collation.Profile.IsBytewise );
			return rawByteComparison && ( !hasCharacterTransformation || this.characterCategory.IsBytewise )
				? Encoding.Latin1.GetString( value )
				: Utf8.GetString( value );
		}

		private string DecodeWholeRecord( ReadOnlySpan<byte> value ) {
			return this.collation.Profile.IsBytewise
				? Encoding.Latin1.GetString( value )
				: Utf8.GetString( value );
		}

		private int CompareValue(
			string left,
			string right,
			ComparisonModifiers modifiers
		) {
			left = Transform( left, modifiers, this.characterCategory );
			right = Transform( right, modifiers, this.characterCategory );
			var result = modifiers.Mode switch {
				ComparisonMode.Numeric => CompareNumeric( left, right, this.numericCulture ),
				ComparisonMode.GeneralNumeric => CompareGeneralNumeric( left, right, this.numericCulture ),
				ComparisonMode.HumanNumeric => CompareHumanNumeric( left, right, this.numericCulture ),
				ComparisonMode.Month => CompareMonth( left, right, this.timeCulture ),
				ComparisonMode.Random => this.CompareRandom( left, right ),
				ComparisonMode.Version => CompareVersions( left, right ),
				_ => this.CompareLexical( left, right )
			};
			return modifiers.Reverse ? -result : result;
		}

		private int CompareLexical( string left, string right ) => this.collation.Compare( left, right );

		private int CompareRandom( string left, string right ) {
			if ( string.Equals( left, right, StringComparison.Ordinal ) ) {
				return 0;
			}
			var leftHash = HashKey( this.randomSeed, left );
			var rightHash = HashKey( this.randomSeed, right );
			var result = leftHash.AsSpan().SequenceCompareTo( rightHash );
			return 0 != result ? result : string.CompareOrdinal( left, right );
		}

		private static byte[] HashKey( byte[] seed, string value ) {
			using var hash = IncrementalHash.CreateHash( HashAlgorithmName.SHA256 );
			hash.AppendData( seed );
			hash.AppendData( Utf8.GetBytes( value ) );
			return hash.GetHashAndReset();
		}

		private static KeyPlan CreateKeyPlan(
			SortKeyDefinition definition,
			ComparisonModifiers global
		) {
			var localHasOrdering = 0 < definition.Options.Length
				|| definition.Start.SkipLeadingBlanks
				|| ( definition.End?.SkipLeadingBlanks ?? false );
			var modifiers = localHasOrdering ? new ComparisonModifiers() : global.Clone();
			foreach ( var option in definition.Options ) {
				switch ( option ) {
					case 'd': modifiers.DictionaryOrder = true; break;
					case 'f': modifiers.IgnoreCase = true; break;
					case 'g': modifiers.Mode = ComparisonMode.GeneralNumeric; break;
					case 'h': modifiers.Mode = ComparisonMode.HumanNumeric; break;
					case 'i': modifiers.IgnoreNonprinting = true; break;
					case 'M': modifiers.Mode = ComparisonMode.Month; break;
					case 'n': modifiers.Mode = ComparisonMode.Numeric; break;
					case 'R': modifiers.Mode = ComparisonMode.Random; break;
					case 'r': modifiers.Reverse = true; break;
					case 'V': modifiers.Mode = ComparisonMode.Version; break;
				}
			}
			return new KeyPlan(
				definition,
				modifiers,
				definition.Start.SkipLeadingBlanks || ( !localHasOrdering && global.IgnoreLeadingBlanks ),
				( definition.End?.SkipLeadingBlanks ?? false ) || ( !localHasOrdering && global.IgnoreLeadingBlanks )
			);
		}
	}

	private sealed record MergeInput(
		string Path,
		bool IsTemporary
	);

	private sealed class MergeCursor : IAsyncDisposable {
		private readonly ByteRecordReader reader;
		private readonly InputSource source;

		/// <summary>Initializes a cursor over one sorted input.</summary>
		/// <param name="source">The owned input source.</param>
		/// <param name="separator">The record separator.</param>
		/// <param name="sourceIndex">The stable input-source ordinal.</param>
		public MergeCursor(
			InputSource source,
			RecordSeparator separator,
			int sourceIndex
		) {
			this.source = source;
			this.reader = new ByteRecordReader( source.BinaryStream!, separator );
			this.SourceIndex = sourceIndex;
		}

		/// <summary>Gets the cursor's current record.</summary>
		public ByteRecord? Current { get; private set; }

		/// <summary>Gets the stable input-source ordinal.</summary>
		public int SourceIndex { get; }

		/// <summary>Advances to the next record.</summary>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <returns><see langword="true"/> when a current record is available.</returns>
		public async ValueTask<bool> AdvanceAsync( CancellationToken cancellationToken ) {
			this.Current = await this.reader.ReadAsync( cancellationToken ).ConfigureAwait( false );
			return null != this.Current;
		}

		/// <summary>Disposes the reader and its owned input source.</summary>
		/// <returns>A value task representing disposal.</returns>
		public async ValueTask DisposeAsync() {
			this.reader.Dispose();
			await this.source.DisposeAsync().ConfigureAwait( false );
		}
	}

	private sealed class MergeCursorComparer : IComparer<MergeCursor> {
		private readonly SortRecordComparer comparer;

		/// <summary>Initializes a merge-cursor comparer.</summary>
		/// <param name="comparer">The record comparer.</param>
		public MergeCursorComparer( SortRecordComparer comparer ) {
			this.comparer = comparer;
		}

		/// <summary>Compares cursor records and then their stable source ordinals.</summary>
		/// <param name="x">The first cursor.</param>
		/// <param name="y">The second cursor.</param>
		/// <returns>A signed ordering result.</returns>
		public int Compare( MergeCursor? x, MergeCursor? y ) {
			if ( ReferenceEquals( x, y ) ) {
				return 0;
			}
			if ( null == x ) {
				return -1;
			}
			if ( null == y ) {
				return 1;
			}
			var result = this.comparer.Compare( x.Current, y.Current );
			return 0 != result ? result : x.SourceIndex.CompareTo( y.SourceIndex );
		}
	}

	private sealed record KeyPlan(
		SortKeyDefinition Definition,
		ComparisonModifiers Modifiers,
		bool SkipStartBlanks,
		bool SkipEndBlanks
	);

	private readonly record struct FieldRange( int Start, int End );

	private readonly record struct DecimalNumber( BigInteger Significand, int Scale ) : IComparable<DecimalNumber> {
		/// <summary>Compares exact decimal values after scale normalization.</summary>
		/// <param name="other">The other exact decimal value.</param>
		/// <returns>A signed ordering result.</returns>
		public int CompareTo( DecimalNumber other ) {
			if ( this.Scale == other.Scale ) {
				return this.Significand.CompareTo( other.Significand );
			}
			var scale = Math.Max( this.Scale, other.Scale );
			var left = this.Significand * BigInteger.Pow( 10, scale - this.Scale );
			var right = other.Significand * BigInteger.Pow( 10, scale - other.Scale );
			return left.CompareTo( right );
		}
	}

	private static ReadOnlySpan<byte> ExtractDefaultKey(
		ReadOnlySpan<byte> value,
		ComparisonModifiers modifiers
	) {
		if ( !modifiers.IgnoreLeadingBlanks ) {
			return value;
		}
		var start = SkipBlanks( value, 0, value.Length );
		return value[ start.. ];
	}

	private static ReadOnlySpan<byte> ExtractKey(
		ReadOnlySpan<byte> value,
		KeyPlan plan,
		byte[]? separator
	) {
		var fields = GetFields( value, separator );
		var start = ResolveStart( value, fields, plan.Definition.Start, plan.SkipStartBlanks );
		var end = null == plan.Definition.End
			? value.Length
			: ResolveEnd( value, fields, plan.Definition.End, plan.SkipEndBlanks );
		return end < start ? ReadOnlySpan<byte>.Empty : value[ start..end ];
	}

	private static IReadOnlyList<FieldRange> GetFields(
		ReadOnlySpan<byte> value,
		byte[]? separator
	) {
		var fields = new List<FieldRange>();
		if ( null != separator ) {
			var separatorSpan = separator.AsSpan();
			var start = 0;
			while ( true ) {
				var relativeEnd = value[ start.. ].IndexOf( separatorSpan );
				if ( 0 > relativeEnd ) {
					fields.Add( new FieldRange( start, value.Length ) );
					break;
				}
				var end = start + relativeEnd;
				fields.Add( new FieldRange( start, end ) );
				start = end + separatorSpan.Length;
				if ( start == value.Length ) {
					fields.Add( new FieldRange( start, start ) );
					break;
				}
			}
			return fields;
		}
		if ( value.IsEmpty ) {
			fields.Add( new FieldRange( 0, 0 ) );
			return fields;
		}
		var position = 0;
		while ( position < value.Length ) {
			var start = position;
			position = SkipBlanks( value, position, value.Length );
			while ( position < value.Length && !IsBlank( value[ position ] ) ) {
				position++;
			}
			if ( position == value.Length ) {
				fields.Add( new FieldRange( start, position ) );
				break;
			}
			var blankStart = position;
			var afterBlanks = SkipBlanks( value, position, value.Length );
			if ( afterBlanks == value.Length ) {
				fields.Add( new FieldRange( start, value.Length ) );
				break;
			}
			fields.Add( new FieldRange( start, blankStart ) );
			position = blankStart;
		}
		return fields;
	}

	private static int ResolveStart(
		ReadOnlySpan<byte> value,
		IReadOnlyList<FieldRange> fields,
		SortKeyPosition position,
		bool skipBlanks
	) {
		if ( position.FieldNumber > fields.Count ) {
			return value.Length;
		}
		var field = fields[ position.FieldNumber - 1 ];
		var start = skipBlanks ? SkipBlanks( value, field.Start, field.End ) : field.Start;
		var character = position.CharacterOffset ?? 1;
		return AdvanceBytes( start, field.End, character - 1 );
	}

	private static int ResolveEnd(
		ReadOnlySpan<byte> value,
		IReadOnlyList<FieldRange> fields,
		SortKeyPosition position,
		bool skipBlanks
	) {
		if ( position.FieldNumber > fields.Count ) {
			return value.Length;
		}
		var field = fields[ position.FieldNumber - 1 ];
		var start = skipBlanks ? SkipBlanks( value, field.Start, field.End ) : field.Start;
		var character = position.CharacterOffset;
		if ( !character.HasValue || 0 == character.Value ) {
			return field.End;
		}
		return AdvanceBytes( start, field.End, character.Value );
	}

	private static int AdvanceBytes(
		int start,
		int end,
		int count
	) {
		return count >= end - start ? end : start + count;
	}

	private static int SkipBlanks(
		ReadOnlySpan<byte> value,
		int start,
		int end
	) {
		while ( start < end && IsBlank( value[ start ] ) ) {
			start++;
		}
		return start;
	}

	private static int SkipBlanks(
		string value,
		int start,
		int end
	) {
		while ( start < end && IsBlank( value[ start ] ) ) {
			start++;
		}
		return start;
	}

	private static bool IsBlank( byte value ) => value is 0x20 or 0x09;

	private static bool IsBlank( char value ) => value is ' ' or '\t';

	private static string Transform(
		string value,
		ComparisonModifiers modifiers,
		LocaleCategoryProfile characterCategory
	) {
		if ( modifiers.DictionaryOrder ) {
			value = new string(
				value.Where( character => IsBlank( character ) || IsLetterOrDigit( character, characterCategory.IsBytewise ) ).ToArray()
			);
		}
		if ( modifiers.IgnoreNonprinting ) {
			value = new string(
				value.Where( character => IsPrinting( character, characterCategory.IsBytewise ) ).ToArray()
			);
		}
		if ( modifiers.IgnoreCase ) {
			value = characterCategory.IsBytewise
				? FoldAsciiCase( value )
				: value.ToUpper( characterCategory.Culture );
		}
		return value;
	}

	private static bool IsLetterOrDigit(
		char value,
		bool bytewise
	) {
		return bytewise
			? value is >= '0' and <= '9' or >= 'A' and <= 'Z' or >= 'a' and <= 'z'
			: char.IsLetterOrDigit( value );
	}

	private static bool IsPrinting(
		char value,
		bool bytewise
	) {
		return bytewise
			? value is >= ' ' and <= '~'
			: !char.IsControl( value );
	}

	private static string FoldAsciiCase( string value ) {
		var characters = value.ToCharArray();
		for ( var index = 0; index < characters.Length; index++ ) {
			if ( characters[ index ] is >= 'a' and <= 'z' ) {
				characters[ index ] = (char)( characters[ index ] - 'a' + 'A' );
			}
		}
		return new string( characters );
	}

	private static int CompareNumeric(
		string left,
		string right,
		CultureInfo culture
	) {
		var leftNumber = ParseDecimalPrefix( left, culture, allowPlus: false );
		var rightNumber = ParseDecimalPrefix( right, culture, allowPlus: false );
		return leftNumber.CompareTo( rightNumber );
	}

	private static int CompareGeneralNumeric(
		string left,
		string right,
		CultureInfo culture
	) {
		var leftValue = ParseGeneralNumber( left, culture );
		var rightValue = ParseGeneralNumber( right, culture );
		var category = leftValue.Category.CompareTo( rightValue.Category );
		return 0 != category ? category : leftValue.Value.CompareTo( rightValue.Value );
	}

	private static (int Category, double Value) ParseGeneralNumber(
		string value,
		CultureInfo culture
	) {
		var start = SkipBlanks( value, 0, value.Length );
		var index = start;
		var negative = false;
		if ( index < value.Length && value[ index ] is '+' or '-' ) {
			negative = '-' == value[ index ];
			index++;
		}
		if ( StartsWithAsciiIgnoreCase( value, index, "nan" ) ) {
			return ( 1, 0 );
		}
		if ( StartsWithAsciiIgnoreCase( value, index, "inf" ) ) {
			return negative ? ( 2, 0 ) : ( 4, 0 );
		}
		if ( TryParseHexadecimalNumber( value, start, index, negative, culture, out var hexadecimal ) ) {
			return double.IsNegativeInfinity( hexadecimal )
				? ( 2, 0 )
				: double.IsPositiveInfinity( hexadecimal )
					? ( 4, 0 )
					: ( 3, hexadecimal );
		}
		var numberFormat = culture.NumberFormat;
		var decimalSeparator = numberFormat.NumberDecimalSeparator;
		var groupSeparator = numberFormat.NumberGroupSeparator;
		var anyDigit = false;
		var afterDecimal = false;
		while ( index < value.Length ) {
			if ( char.IsAsciiDigit( value[ index ] ) ) {
				anyDigit = true;
				index++;
				continue;
			}
			if ( !afterDecimal && MatchesAt( value, index, groupSeparator ) ) {
				index += groupSeparator.Length;
				continue;
			}
			if ( !afterDecimal && MatchesAt( value, index, decimalSeparator ) ) {
				afterDecimal = true;
				index += decimalSeparator.Length;
				continue;
			}
			break;
		}
		if ( !anyDigit ) {
			return ( 0, 0 );
		}
		if ( index < value.Length && value[ index ] is 'e' or 'E' ) {
			var exponentEnd = index + 1;
			if ( exponentEnd < value.Length && value[ exponentEnd ] is '+' or '-' ) {
				exponentEnd++;
			}
			var exponentDigits = exponentEnd;
			while ( exponentEnd < value.Length && char.IsAsciiDigit( value[ exponentEnd ] ) ) {
				exponentEnd++;
			}
			if ( exponentDigits < exponentEnd ) {
				index = exponentEnd;
			}
		}
		if ( !double.TryParse(
			value.AsSpan( start, index - start ),
			NumberStyles.Float | NumberStyles.AllowThousands,
			culture,
			out var number
		) ) {
			return ( 0, 0 );
		}
		if ( double.IsNaN( number ) ) {
			return ( 1, 0 );
		}
		if ( double.IsNegativeInfinity( number ) ) {
			return ( 2, 0 );
		}
		if ( double.IsPositiveInfinity( number ) ) {
			return ( 4, 0 );
		}
		return ( 3, number );
	}

	private static bool TryParseHexadecimalNumber(
		string value,
		int signedStart,
		int digitsStart,
		bool negative,
		CultureInfo culture,
		out double number
	) {
		number = 0;
		if (
			digitsStart + 2 > value.Length
			|| '0' != value[ digitsStart ]
			|| value[ digitsStart + 1 ] is not ( 'x' or 'X' )
		) {
			return false;
		}
		var index = digitsStart + 2;
		var significand = 0.0;
		var fractionalDigits = 0;
		var anyDigit = false;
		var afterDecimal = false;
		var decimalSeparator = culture.NumberFormat.NumberDecimalSeparator;
		while ( index < value.Length ) {
			var digit = HexadecimalDigitValue( value[ index ] );
			if ( 0 <= digit ) {
				anyDigit = true;
				significand = ( significand * 16 ) + digit;
				if ( afterDecimal ) {
					fractionalDigits++;
				}
				index++;
				continue;
			}
			if ( !afterDecimal && MatchesAt( value, index, decimalSeparator ) ) {
				afterDecimal = true;
				index += decimalSeparator.Length;
				continue;
			}
			break;
		}
		if ( !anyDigit ) {
			return false;
		}
		var exponent = 0;
		if ( index < value.Length && value[ index ] is 'p' or 'P' ) {
			var exponentIndex = index + 1;
			var exponentNegative = false;
			if ( exponentIndex < value.Length && value[ exponentIndex ] is '+' or '-' ) {
				exponentNegative = '-' == value[ exponentIndex ];
				exponentIndex++;
			}
			var exponentStart = exponentIndex;
			while ( exponentIndex < value.Length && char.IsAsciiDigit( value[ exponentIndex ] ) ) {
				if ( exponent < 1_000_000 ) {
					exponent = ( exponent * 10 ) + ( value[ exponentIndex ] - '0' );
				}
				exponentIndex++;
			}
			if ( exponentStart < exponentIndex && exponentNegative ) {
				exponent = -exponent;
			}
		}
		var binaryExponent = exponent - ( fractionalDigits * 4 );
		number = Math.ScaleB( significand, binaryExponent );
		if ( negative || ( signedStart < digitsStart && '-' == value[ signedStart ] ) ) {
			number = -number;
		}
		return true;
	}

	private static int HexadecimalDigitValue( char value ) {
		if ( value is >= '0' and <= '9' ) {
			return value - '0';
		}
		if ( value is >= 'A' and <= 'F' ) {
			return value - 'A' + 10;
		}
		if ( value is >= 'a' and <= 'f' ) {
			return value - 'a' + 10;
		}
		return -1;
	}

	private static bool StartsWithAsciiIgnoreCase(
		string value,
		int index,
		string token
	) {
		return index + token.Length <= value.Length
			&& value.AsSpan( index, token.Length ).Equals( token.AsSpan(), StringComparison.OrdinalIgnoreCase );
	}

	private static int CompareHumanNumeric(
		string left,
		string right,
		CultureInfo culture
	) {
		return ParseHumanNumber( left, culture ).CompareTo( ParseHumanNumber( right, culture ) );
	}

	private static DecimalNumber ParseHumanNumber(
		string value,
		CultureInfo culture
	) {
		var number = ParseDecimalPrefix( value, culture, allowPlus: true, out var consumed );
		var suffixText = value[ Math.Min( consumed, value.Length ).. ].Trim();
		if ( 0 == suffixText.Length ) {
			return number;
		}
		var suffix = char.ToUpperInvariant( suffixText[ 0 ] );
		var exponent = "KMGTPEZYRQ".IndexOf( suffix ) + 1;
		if ( 0 >= exponent ) {
			return number;
		}
		return new DecimalNumber(
			number.Significand * BigInteger.Pow( 1024, exponent ),
			number.Scale
		);
	}

	private static int CompareMonth(
		string left,
		string right,
		CultureInfo culture
	) {
		return MonthNumber( left, culture ).CompareTo( MonthNumber( right, culture ) );
	}

	private static int MonthNumber(
		string value,
		CultureInfo culture
	) {
		value = value.TrimStart( ' ', '\t' );
		var date = culture.DateTimeFormat;
		for ( var month = 1; month <= 13; month++ ) {
			var abbreviation = date.GetAbbreviatedMonthName( month );
			var name = date.GetMonthName( month );
			if (
				( 0 < abbreviation.Length && value.StartsWith( abbreviation, true, culture ) )
				|| ( 0 < name.Length && value.StartsWith( name, true, culture ) )
			) {
				return month;
			}
		}
		return 0;
	}

	private static int CompareVersions(
		string left,
		string right
	) {
		if ( 0 == left.Length ) {
			return 0 == right.Length ? 0 : -1;
		}
		if ( 0 == right.Length ) {
			return 1;
		}
		var special = CompareVersionDotPrefixes( left, right );
		if ( special.HasValue ) {
			return special.Value;
		}
		var leftPrefixLength = GetVersionPrefixLength( left );
		var rightPrefixLength = GetVersionPrefixLength( right );
		var onePassOnly = leftPrefixLength == left.Length && rightPrefixLength == right.Length;
		var result = CompareVersionParts(
			left,
			leftPrefixLength,
			right,
			rightPrefixLength
		);
		return 0 != result || onePassOnly
			? result
			: CompareVersionParts( left, left.Length, right, right.Length );
	}

	private static int? CompareVersionDotPrefixes(
		string left,
		string right
	) {
		if ( '.' == left[ 0 ] ) {
			if ( '.' != right[ 0 ] ) {
				return -1;
			}
			var leftIsDot = 1 == left.Length;
			var rightIsDot = 1 == right.Length;
			if ( leftIsDot || rightIsDot ) {
				return leftIsDot == rightIsDot ? 0 : leftIsDot ? -1 : 1;
			}
			var leftIsDotDot = 2 == left.Length && '.' == left[ 1 ];
			var rightIsDotDot = 2 == right.Length && '.' == right[ 1 ];
			if ( leftIsDotDot || rightIsDotDot ) {
				return leftIsDotDot == rightIsDotDot ? 0 : leftIsDotDot ? -1 : 1;
			}
		} else if ( '.' == right[ 0 ] ) {
			return 1;
		}
		return null;
	}

	private static int GetVersionPrefixLength( string value ) {
		for ( var index = 1; index + 1 < value.Length; index++ ) {
			if ( '.' == value[ index ] && IsVersionSuffixStart( value[ index + 1 ] ) && IsVersionSuffix( value, index ) ) {
				return index;
			}
		}
		return value.Length;
	}

	private static bool IsVersionSuffix(
		string value,
		int index
	) {
		while ( index < value.Length ) {
			if ( index + 1 >= value.Length || '.' != value[ index ] || !IsVersionSuffixStart( value[ index + 1 ] ) ) {
				return false;
			}
			index += 2;
			while ( index < value.Length && IsVersionSuffixContinuation( value[ index ] ) ) {
				index++;
			}
		}
		return true;
	}

	private static bool IsVersionSuffixStart( char value ) {
		return '~' == value || value is >= 'A' and <= 'Z' or >= 'a' and <= 'z';
	}

	private static bool IsVersionSuffixContinuation( char value ) {
		return '~' == value
			|| value is >= '0' and <= '9'
				or >= 'A' and <= 'Z'
				or >= 'a' and <= 'z';
	}

	private static int CompareVersionParts(
		string left,
		int leftLength,
		string right,
		int rightLength
	) {
		var leftIndex = 0;
		var rightIndex = 0;
		while ( leftIndex < leftLength || rightIndex < rightLength ) {
			var firstDifference = 0;
			while (
				( leftIndex < leftLength && !char.IsAsciiDigit( left[ leftIndex ] ) )
				|| ( rightIndex < rightLength && !char.IsAsciiDigit( right[ rightIndex ] ) )
			) {
				var leftOrder = VersionCharacterOrder( left, leftIndex, leftLength );
				var rightOrder = VersionCharacterOrder( right, rightIndex, rightLength );
				if ( leftOrder != rightOrder ) {
					return leftOrder.CompareTo( rightOrder );
				}
				leftIndex++;
				rightIndex++;
			}
			while ( leftIndex < leftLength && '0' == left[ leftIndex ] ) {
				leftIndex++;
			}
			while ( rightIndex < rightLength && '0' == right[ rightIndex ] ) {
				rightIndex++;
			}
			while (
				leftIndex < leftLength
				&& rightIndex < rightLength
				&& char.IsAsciiDigit( left[ leftIndex ] )
				&& char.IsAsciiDigit( right[ rightIndex ] )
			) {
				if ( 0 == firstDifference ) {
					firstDifference = left[ leftIndex ].CompareTo( right[ rightIndex ] );
				}
				leftIndex++;
				rightIndex++;
			}
			if ( leftIndex < leftLength && char.IsAsciiDigit( left[ leftIndex ] ) ) {
				return 1;
			}
			if ( rightIndex < rightLength && char.IsAsciiDigit( right[ rightIndex ] ) ) {
				return -1;
			}
			if ( 0 != firstDifference ) {
				return firstDifference;
			}
		}
		return 0;
	}

	private static int VersionCharacterOrder(
		string value,
		int index,
		int length
	) {
		if ( index >= length ) {
			return -1;
		}
		var character = value[ index ];
		if ( '~' == character ) {
			return -2;
		}
		if ( char.IsAsciiDigit( character ) ) {
			return 0;
		}
		if ( character is >= 'A' and <= 'Z' or >= 'a' and <= 'z' ) {
			return character;
		}
		return character + byte.MaxValue + 1;
	}

	private static DecimalNumber ParseDecimalPrefix(
		string value,
		CultureInfo culture,
		bool allowPlus
	) => ParseDecimalPrefix( value, culture, allowPlus, out _ );

	private static DecimalNumber ParseDecimalPrefix(
		string value,
		CultureInfo culture,
		bool allowPlus,
		out int consumed
	) {
		var numberFormat = culture.NumberFormat;
		var decimalSeparator = numberFormat.NumberDecimalSeparator;
		var groupSeparator = numberFormat.NumberGroupSeparator;
		var index = SkipBlanks( value, 0, value.Length );
		var negative = false;
		if ( index < value.Length && '-' == value[ index ] ) {
			negative = true;
			index++;
		} else if ( allowPlus && index < value.Length && '+' == value[ index ] ) {
			index++;
		}
		var digits = new StringBuilder();
		var scale = 0;
		var afterDecimal = false;
		var anyDigit = false;
		while ( index < value.Length ) {
			if ( char.IsAsciiDigit( value[ index ] ) ) {
				digits.Append( value[ index ] );
				anyDigit = true;
				if ( afterDecimal ) {
					scale++;
				}
				index++;
				continue;
			}
			if ( !afterDecimal && MatchesAt( value, index, groupSeparator ) ) {
				index += groupSeparator.Length;
				continue;
			}
			if ( !afterDecimal && MatchesAt( value, index, decimalSeparator ) ) {
				afterDecimal = true;
				index += decimalSeparator.Length;
				continue;
			}
			break;
		}
		consumed = index;
		if ( !anyDigit ) {
			return new DecimalNumber( BigInteger.Zero, 0 );
		}
		var significand = BigInteger.Parse( digits.ToString(), NumberStyles.None, CultureInfo.InvariantCulture );
		if ( negative ) {
			significand = -significand;
		}
		while ( 0 < scale && !significand.IsZero && BigInteger.Remainder( significand, 10 ).IsZero ) {
			significand /= 10;
			scale--;
		}
		return new DecimalNumber( significand, scale );
	}

	private static bool MatchesAt(
		string value,
		int index,
		string token
	) {
		return 0 < token.Length
			&& index + token.Length <= value.Length
			&& value.AsSpan( index, token.Length ).SequenceEqual( token.AsSpan() );
	}
}
