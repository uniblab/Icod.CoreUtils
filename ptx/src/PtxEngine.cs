namespace Icod.CoreUtils.Ptx;

using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;
using Icod.CoreUtils.Shared.Diagnostics;
using Icod.CoreUtils.Shared.IO;
using Icod.CoreUtils.Shared.Ordering;
using Icod.CoreUtils.Shared.Records;
using Icod.CoreUtils.Shared.Temporary;

/// <summary>Coordinates parameter files, context discovery, stable external ordering, and formatted output.</summary>
internal static class PtxEngine {
	/// <summary>Executes the permuted-index pipeline.</summary>
	/// <param name="settings">The effective settings.</param>
	/// <param name="context">The command context.</param>
	/// <param name="destination">The caller-owned output stream.</param>
	/// <returns>A task representing the complete pipeline.</returns>
	internal static async Task RunAsync(
		PtxSettings settings,
		CommandContext context,
		Stream destination
	) {
		var wordMap = await CreateWordMapAsync( settings, context ).ConfigureAwait( false );
		var patterns = await PtxPatterns.CreateAsync(
			settings,
			wordMap,
			context.CancellationToken
		).ConfigureAwait( false );
		var ignore = await ReadWordTableAsync(
			settings.IgnoreFile,
			settings.IgnoreCase,
			context
		).ConfigureAwait( false );
		var only = await ReadWordTableAsync(
			settings.OnlyFile,
			settings.IgnoreCase,
			context
		).ConfigureAwait( false );
		var state = new PtxProcessingState();
		var workspace = TemporaryWorkspace.Create(
			directoryTemplate: "ptx-work.XXXXXXXX",
			cancellationToken: context.CancellationToken
		);
		var handedToOrderingEngine = false;
		await using var store = new PtxContextStore(
			workspace.CreateFile( "contexts-XXXXXXXX.bin", context.CancellationToken )
		);
		try {
			var orderingOptions = new ExternalOrderingOptions<PtxOccurrence>(
				settings.MemoryLimitBytes,
				occurrence => checked(
					64L + occurrence.Keyword.Length + occurrence.Reference.Length
				),
				runFileTemplate: "ptx-run-XXXXXXXX.bin"
			);
			var ordering = new ExternalOrderingEngine<PtxOccurrence>(
				new PtxOccurrenceComparer( settings.IgnoreCase ),
				new PtxOccurrenceRunCodec(),
				orderingOptions,
				_ => workspace
			);
			var formatter = new PtxFormatter(
				settings,
				state,
				patterns,
				store,
				destination
			);
			handedToOrderingEngine = true;
			await ordering.OrderAsync(
				GenerateOccurrencesAsync(
					settings,
					state,
					patterns,
					ignore,
					only,
					store,
					context,
					context.CancellationToken
				),
				formatter.WriteAsync,
				context.CancellationToken
			).ConfigureAwait( false );
		} finally {
			if ( !handedToOrderingEngine ) {
				await workspace.DisposeAsync().ConfigureAwait( false );
			}
		}
	}

	private static async IAsyncEnumerable<PtxOccurrence> GenerateOccurrencesAsync(
		PtxSettings settings,
		PtxProcessingState state,
		PtxPatterns patterns,
		PtxWordTable? ignore,
		PtxWordTable? only,
		PtxContextStore store,
		CommandContext commandContext,
		[EnumeratorCancellation] CancellationToken cancellationToken
	) {
		try {
			foreach ( var inputName in settings.InputFiles ) {
				cancellationToken.ThrowIfCancellationRequested();
				var operand = CreateInputOperand( inputName );
				var statistics = new PtxFileStatistics(
					operand.IsStandardInput ? string.Empty : inputName
				);
				state.Files.Add( statistics );
				await using var source = InputSource.OpenBinary( operand, commandContext );
				await foreach ( var segment in PtxContextReader.ReadAsync(
					source.BinaryStream!,
					settings,
					patterns,
					statistics,
					cancellationToken
				).WithCancellation( cancellationToken ).ConfigureAwait( false ) ) {
					foreach ( var occurrence in await ProcessContextAsync(
						segment,
						statistics,
						settings,
						state,
						patterns,
						ignore,
						only,
						store,
						cancellationToken
					).ConfigureAwait( false ) ) {
						yield return occurrence;
					}
				}
			}
		} finally {
			await store.SealAsync( CancellationToken.None ).ConfigureAwait( false );
		}
	}

	private static async Task<IReadOnlyList<PtxOccurrence>> ProcessContextAsync(
		PtxContextSegment segment,
		PtxFileStatistics statistics,
		PtxSettings settings,
		PtxProcessingState state,
		PtxPatterns patterns,
		PtxWordTable? ignore,
		PtxWordTable? only,
		PtxContextStore store,
		CancellationToken cancellationToken
	) {
		var source = segment.Content;
		var lineInfos = BuildLineInfos(
			source,
			segment.StartingLineNumber,
			settings.InputReference,
			segment.StartsAtLineStart
		);
		var displayStart = 0;
		if (
			settings.InputReference
			&& segment.StartsAtLineStart
			&& 0 < lineInfos.Count
		) {
			displayStart = lineInfos[ 0 ].ContentStart;
		}
		var display = source.AsMemory( displayStart );
		var words = patterns.FindWords( display, cancellationToken );
		var accepted = new List<PendingOccurrence>();
		foreach ( var word in words ) {
			cancellationToken.ThrowIfCancellationRequested();
			state.MaximumWordLength = Math.Max( state.MaximumWordLength, word.Length );
			var sourceStart = checked( displayStart + word.Start );
			var line = FindLine( lineInfos, sourceStart );
			if (
				settings.InputReference
				&& sourceStart >= line.LineStart
				&& sourceStart < line.ContentStart
			) {
				continue;
			}
			var keyword = source.AsSpan( sourceStart, word.Length );
			if ( null != ignore && !ignore.IsEmpty && ignore.Contains( keyword ) ) {
				continue;
			}
			if ( null != only && !only.IsEmpty && !only.Contains( keyword ) ) {
				continue;
			}
			byte[] reference;
			if ( settings.AutoReference ) {
				reference = Encoding.UTF8.GetBytes( string.Concat(
					statistics.ReferenceName,
					":",
					line.LineNumber.ToString( CultureInfo.InvariantCulture )
				) );
			} else if ( settings.InputReference ) {
				reference = !segment.StartsAtLineStart
					&& line.LineNumber == segment.StartingLineNumber
					? segment.InheritedInputReference
					: source.AsSpan(
						line.LineStart,
						line.ReferenceEnd - line.LineStart
					).ToArray();
				state.InputReferenceMaximumWidth = Math.Max(
					state.InputReferenceMaximumWidth,
					reference.Length
				);
			} else {
				reference = Array.Empty<byte>();
			}
			accepted.Add( new PendingOccurrence(
				keyword.ToArray(),
				word.Start,
				word.Length,
				reference
			) );
		}
		if ( 0 == accepted.Count ) {
			return Array.Empty<PtxOccurrence>();
		}
		var contextOffset = await store.AppendAsync( display, cancellationToken ).ConfigureAwait( false );
		return accepted.Select( value => new PtxOccurrence(
			value.Keyword,
			contextOffset,
			display.Length,
			value.KeywordStart,
			value.KeywordLength,
			value.Reference
		) ).ToArray();
	}

	private static List<LineInfo> BuildLineInfos(
		byte[] context,
		long startingLine,
		bool inputReference,
		bool startsAtLineStart
	) {
		var result = new List<LineInfo>();
		var start = 0;
		var line = startingLine;
		var currentStartsAtLineStart = startsAtLineStart;
		while ( start <= context.Length ) {
			var end = Array.IndexOf( context, (byte)'\n', start );
			if ( 0 > end ) {
				end = context.Length;
			}
			var referenceEnd = start;
			var contentStart = start;
			if ( inputReference && currentStartsAtLineStart ) {
				while ( referenceEnd < end && !PtxText.IsWhiteSpace( context[ referenceEnd ] ) ) {
					referenceEnd++;
				}
				contentStart = referenceEnd;
				while ( contentStart < end && PtxText.IsWhiteSpace( context[ contentStart ] ) ) {
					contentStart++;
				}
			}
			result.Add( new LineInfo( start, end, referenceEnd, contentStart, line ) );
			if ( end == context.Length ) {
				break;
			}
			start = end + 1;
			line++;
			currentStartsAtLineStart = true;
		}
		return result;
	}

	private static LineInfo FindLine( IReadOnlyList<LineInfo> lines, int offset ) {
		for ( var index = lines.Count - 1; 0 <= index; index-- ) {
			if ( lines[ index ].LineStart <= offset ) {
				return lines[ index ];
			}
		}
		return lines[ 0 ];
	}

	private static InputOperand CreateInputOperand( string? path ) =>
		InputOperand.Create( string.IsNullOrEmpty( path ) ? "-" : path );

	private static async Task<bool[]> CreateWordMapAsync(
		PtxSettings settings,
		CommandContext context
	) {
		var map = new bool[ 256 ];
		if ( null != settings.BreakFile ) {
			Array.Fill( map, true );
			await using var source = InputSource.OpenBinary(
				CreateInputOperand( settings.BreakFile ),
				context
			);
			var buffer = new byte[ 65_536 ];
			while ( true ) {
				var count = await source.BinaryStream!.ReadAsync(
					buffer,
					context.CancellationToken
				).ConfigureAwait( false );
				if ( 0 == count ) {
					break;
				}
				for ( var index = 0; index < count; index++ ) {
					map[ buffer[ index ] ] = false;
				}
			}
			if ( !settings.GnuExtensions ) {
				map[ (byte)' ' ] = false;
				map[ (byte)'\t' ] = false;
				map[ (byte)'\n' ] = false;
			}
			return map;
		}
		if ( settings.GnuExtensions ) {
			for ( var value = (byte)'A'; value <= (byte)'Z'; value++ ) {
				map[ value ] = true;
			}
			for ( var value = (byte)'a'; value <= (byte)'z'; value++ ) {
				map[ value ] = true;
			}
		} else {
			Array.Fill( map, true );
			map[ (byte)' ' ] = false;
			map[ (byte)'\t' ] = false;
			map[ (byte)'\n' ] = false;
		}
		return map;
	}

	private static async Task<PtxWordTable?> ReadWordTableAsync(
		string? path,
		bool ignoreCase,
		CommandContext context
	) {
		if ( null == path ) {
			return null;
		}
		await using var source = InputSource.OpenBinary( CreateInputOperand( path ), context );
		using var reader = new ByteRecordReader( source.BinaryStream!, RecordSeparator.LineFeed );
		var values = new List<byte[]>();
		while ( true ) {
			var record = await reader.ReadAsync( context.CancellationToken ).ConfigureAwait( false );
			if ( null == record ) {
				break;
			}
			if ( !record.Content.IsEmpty ) {
				values.Add( record.Content.ToArray() );
			}
		}
		return new PtxWordTable( values, ignoreCase );
	}

	private sealed record PendingOccurrence(
		byte[] Keyword,
		int KeywordStart,
		int KeywordLength,
		byte[] Reference
	);

	private sealed record LineInfo(
		int LineStart,
		int LineEnd,
		int ReferenceEnd,
		int ContentStart,
		long LineNumber
	);
}
