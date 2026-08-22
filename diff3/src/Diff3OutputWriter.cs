namespace Icod.DiffUtils.Diff3;

using System.Globalization;
using Icod.DiffUtils.Shared.Lines;
using Icod.DiffUtils.Shared.Merge;

/// <summary>Serializes GNU <c>diff3</c> reports, edit scripts, and merged output.</summary>
internal static class Diff3OutputWriter {
	private const string NoNewlineMarker = "\\ No newline at end of file";

	private sealed class NormalSection {
		/// <summary>Initializes one normal-report section.</summary>
		public NormalSection(
			int fileNumber,
			int start,
			IReadOnlyList<ComparisonLine> lines,
			bool writeContent
		) {
			ArgumentOutOfRangeException.ThrowIfNegativeOrZero( fileNumber );
			ArgumentOutOfRangeException.ThrowIfNegative( start );
			ArgumentNullException.ThrowIfNull( lines );
			this.FileNumber = fileNumber;
			this.Start = start;
			this.Lines = lines;
			this.WriteContent = writeContent;
		}

		/// <summary>Gets the external file number.</summary>
		public int FileNumber { get; }
		/// <summary>Gets the zero-based range start.</summary>
		public int Start { get; }
		/// <summary>Gets the lines in the range.</summary>
		public IReadOnlyList<ComparisonLine> Lines { get; }
		/// <summary>Gets whether the section body is written.</summary>
		public bool WriteContent { get; }
	}

	/// <summary>Writes the default human-readable three-file report.</summary>
	public static async Task WriteNormalAsync(
		ThreeWayComparison comparison,
		Diff3Options options,
		TextWriter output,
		CancellationToken cancellationToken
	) {
		ArgumentNullException.ThrowIfNull( comparison );
		ArgumentNullException.ThrowIfNull( options );
		ArgumentNullException.ThrowIfNull( output );
		foreach ( var region in comparison.Regions ) {
			await WriteLineAsync( output, GetHeader( region.Kind ), cancellationToken ).ConfigureAwait( false );
			foreach ( var section in GetNormalSections( region ) ) {
				await WriteLineAsync(
					output,
					FormatRange( section.FileNumber, section.Start, section.Lines.Count ),
					cancellationToken
				).ConfigureAwait( false );
				if ( section.WriteContent ) {
					await WriteNormalLinesAsync(
						output,
						section.Lines,
						options.InitialTab,
						options.StripTrailingCarriageReturn,
						cancellationToken
					).ConfigureAwait( false );
				}
			}
		}
	}

	/// <summary>Writes the selected merge directly to standard output.</summary>
	public static async Task<bool> WriteMergedAsync(
		ThreeWayComparison comparison,
		Diff3Input mine,
		Diff3Options options,
		TextWriter output,
		CancellationToken cancellationToken
	) {
		ArgumentNullException.ThrowIfNull( comparison );
		ArgumentNullException.ThrowIfNull( mine );
		ArgumentNullException.ThrowIfNull( options );
		ArgumentNullException.ThrowIfNull( output );
		var minePosition = 0;
		var wroteConflict = false;
		foreach ( var region in comparison.Regions ) {
			await WriteSourceRangeAsync(
				output,
				mine.Document.Lines,
				minePosition,
				region.MineStart - minePosition,
				stripTrailingCarriageReturn: false,
				cancellationToken: cancellationToken
			).ConfigureAwait( false );
			var replacement = BuildReplacement( region, options, out var markedConflict );
			wroteConflict |= markedConflict;
			await WriteLinesAsync( output, replacement, cancellationToken ).ConfigureAwait( false );
			minePosition = checked( region.MineStart + region.MineLines.Count );
		}
		await WriteSourceRangeAsync(
			output,
			mine.Document.Lines,
			minePosition,
			mine.Document.Lines.Count - minePosition,
			stripTrailingCarriageReturn: false,
			cancellationToken: cancellationToken
		).ConfigureAwait( false );
		return wroteConflict;
	}

	/// <summary>Writes a reverse-order <c>ed</c> script for the selected merge policy.</summary>
	public static async Task<bool> WriteEdScriptAsync(
		ThreeWayComparison comparison,
		Diff3Input mine,
		Diff3Options options,
		TextWriter output,
		TextWriter error,
		CancellationToken cancellationToken
	) {
		ArgumentNullException.ThrowIfNull( comparison );
		ArgumentNullException.ThrowIfNull( mine );
		ArgumentNullException.ThrowIfNull( options );
		ArgumentNullException.ThrowIfNull( output );
		ArgumentNullException.ThrowIfNull( error );
		var wroteConflict = false;
		var warnedIncompleteLine = false;
		foreach ( var region in comparison.Regions.Reverse() ) {
			if ( !RequiresEdit( region, options ) ) {
				continue;
			}
			if ( IsMarkedConflict( region, options ) ) {
				wroteConflict = true;
				await WriteMarkedConflictEdAsync(
					output,
					region,
					options,
					cancellationToken
				).ConfigureAwait( false );
				warnedIncompleteLine |= HasIncompleteEdPayload( region );
			} else {
				var replacement = BuildReplacement( region, options, out _ );
				await WriteEdEditAsync(
					output,
					region.MineStart,
					region.MineLines.Count,
					replacement,
					options.StripTrailingCarriageReturn,
					cancellationToken
				).ConfigureAwait( false );
				warnedIncompleteLine |= replacement.Any( line => !line.HasLineTerminator );
			}
		}
		if ( warnedIncompleteLine ) {
			await WriteLineAsync(
				error,
				"diff3: No newline at end of file",
				CancellationToken.None
			).ConfigureAwait( false );
		}
		if ( options.AppendWriteAndQuit ) {
			await WriteLineAsync( output, "w", cancellationToken ).ConfigureAwait( false );
			await WriteLineAsync( output, "q", cancellationToken ).ConfigureAwait( false );
		}
		return wroteConflict;
	}

	private static IReadOnlyList<ComparisonLine> BuildReplacement(
		ThreeWayChangeRegion region,
		Diff3Options options,
		out bool markedConflict
	) {
		markedConflict = false;
		switch ( options.Mode ) {
			case Diff3OutputMode.Ed:
				return ThreeWayChangeKind.Overlap == region.Kind || ThreeWayChangeKind.YoursOnly == region.Kind
					? NormalizeLines( region.YoursLines, options.StripTrailingCarriageReturn )
					: NormalizeLines( region.MineLines, options.StripTrailingCarriageReturn );
			case Diff3OutputMode.EasyOnly:
				return ThreeWayChangeKind.YoursOnly == region.Kind
					? NormalizeLines( region.YoursLines, options.StripTrailingCarriageReturn )
					: NormalizeLines( region.MineLines, options.StripTrailingCarriageReturn );
			case Diff3OutputMode.OverlapOnly:
				return ThreeWayChangeKind.Overlap == region.Kind
					? NormalizeLines( region.YoursLines, options.StripTrailingCarriageReturn )
					: NormalizeLines( region.MineLines, options.StripTrailingCarriageReturn );
			case Diff3OutputMode.ShowOverlap:
				if ( ThreeWayChangeKind.Overlap == region.Kind ) {
					markedConflict = true;
					return BuildTwoWayConflict( region, options );
				}
				return ThreeWayChangeKind.YoursOnly == region.Kind
					? NormalizeLines( region.YoursLines, options.StripTrailingCarriageReturn )
					: NormalizeLines( region.MineLines, options.StripTrailingCarriageReturn );
			case Diff3OutputMode.MarkedOverlapOnly:
				if ( ThreeWayChangeKind.Overlap == region.Kind ) {
					markedConflict = true;
					return BuildTwoWayConflict( region, options );
				}
				return NormalizeLines( region.MineLines, options.StripTrailingCarriageReturn );
			case Diff3OutputMode.ShowAll:
				if ( ThreeWayChangeKind.Overlap == region.Kind ) {
					markedConflict = true;
					return BuildThreeWayConflict( region, options );
				}
				if ( ThreeWayChangeKind.OlderOnly == region.Kind ) {
					markedConflict = true;
					return BuildAncestorConflict( region, options );
				}
				return ThreeWayChangeKind.YoursOnly == region.Kind
					? NormalizeLines( region.YoursLines, options.StripTrailingCarriageReturn )
					: NormalizeLines( region.MineLines, options.StripTrailingCarriageReturn );
			default:
				return NormalizeLines( region.MineLines, options.StripTrailingCarriageReturn );
		}
	}

	private static bool RequiresEdit( ThreeWayChangeRegion region, Diff3Options options ) {
		return options.Mode switch {
			Diff3OutputMode.Ed => region.Kind is ThreeWayChangeKind.YoursOnly or ThreeWayChangeKind.Overlap,
			Diff3OutputMode.EasyOnly => ThreeWayChangeKind.YoursOnly == region.Kind,
			Diff3OutputMode.OverlapOnly => ThreeWayChangeKind.Overlap == region.Kind,
			Diff3OutputMode.MarkedOverlapOnly => ThreeWayChangeKind.Overlap == region.Kind,
			Diff3OutputMode.ShowOverlap => region.Kind is ThreeWayChangeKind.YoursOnly or ThreeWayChangeKind.Overlap,
			Diff3OutputMode.ShowAll => ThreeWayChangeKind.MineOnly != region.Kind,
			_ => false
		};
	}

	private static bool IsMarkedConflict( ThreeWayChangeRegion region, Diff3Options options ) {
		return ThreeWayChangeKind.Overlap == region.Kind
			? options.Mode is Diff3OutputMode.ShowAll or Diff3OutputMode.ShowOverlap or Diff3OutputMode.MarkedOverlapOnly
			: ThreeWayChangeKind.OlderOnly == region.Kind && Diff3OutputMode.ShowAll == options.Mode;
	}

	private static bool HasIncompleteEdPayload( ThreeWayChangeRegion region ) {
		return region.MineLines.Any( line => !line.HasLineTerminator )
			|| region.OlderLines.Any( line => !line.HasLineTerminator )
			|| region.YoursLines.Any( line => !line.HasLineTerminator );
	}

	private static async Task WriteMarkedConflictEdAsync(
		TextWriter output,
		ThreeWayChangeRegion region,
		Diff3Options options,
		CancellationToken cancellationToken
	) {
		var highAddress = checked( region.MineStart + region.MineLines.Count );
		await WriteLineAsync(
			output,
			string.Concat( highAddress.ToString( CultureInfo.InvariantCulture ), "a" ),
			cancellationToken
		).ConfigureAwait( false );
		var leadingDot = false;
		if ( ThreeWayChangeKind.Overlap == region.Kind ) {
			if ( Diff3OutputMode.ShowAll == options.Mode ) {
				await WriteLineAsync(
					output,
					string.Concat( "||||||| ", options.Labels[1] ),
					cancellationToken
				).ConfigureAwait( false );
				leadingDot |= await WriteEdSourceLinesAsync(
					output,
					region.OlderLines,
					options.StripTrailingCarriageReturn,
					cancellationToken
				).ConfigureAwait( false );
			}
			await WriteLineAsync( output, "=======", cancellationToken ).ConfigureAwait( false );
			leadingDot |= await WriteEdSourceLinesAsync(
				output,
				region.YoursLines,
				options.StripTrailingCarriageReturn,
				cancellationToken
			).ConfigureAwait( false );
		}
		await WriteLineAsync(
			output,
			string.Concat( ">>>>>>> ", options.Labels[2] ),
			cancellationToken
		).ConfigureAwait( false );
		await WriteEdTerminatorAsync(
			output,
			leadingDot,
			checked( highAddress + 2 ),
			checked( region.OlderLines.Count + region.YoursLines.Count + 1 ),
			cancellationToken
		).ConfigureAwait( false );

		await WriteLineAsync(
			output,
			string.Concat( region.MineStart.ToString( CultureInfo.InvariantCulture ), "a" ),
			cancellationToken
		).ConfigureAwait( false );
		await WriteLineAsync(
			output,
			string.Concat(
				"<<<<<<< ",
				ThreeWayChangeKind.Overlap == region.Kind ? options.Labels[0] : options.Labels[1]
			),
			cancellationToken
		).ConfigureAwait( false );
		leadingDot = false;
		if ( ThreeWayChangeKind.OlderOnly == region.Kind ) {
			leadingDot = await WriteEdSourceLinesAsync(
				output,
				region.OlderLines,
				options.StripTrailingCarriageReturn,
				cancellationToken
			).ConfigureAwait( false );
			await WriteLineAsync( output, "=======", cancellationToken ).ConfigureAwait( false );
		}
		await WriteEdTerminatorAsync(
			output,
			leadingDot,
			checked( region.MineStart + 2 ),
			region.OlderLines.Count,
			cancellationToken
		).ConfigureAwait( false );
	}

	private static async Task<bool> WriteEdSourceLinesAsync(
		TextWriter output,
		IReadOnlyList<ComparisonLine> lines,
		bool stripTrailingCarriageReturn,
		CancellationToken cancellationToken
	) {
		var leadingDot = false;
		foreach ( var line in lines ) {
			var content = GetOutputContent( line, stripTrailingCarriageReturn );
			if ( content.StartsWith( ".", StringComparison.Ordinal ) ) {
				content = string.Concat( ".", content );
				leadingDot = true;
			}
			await WriteLineAsync( output, content, cancellationToken ).ConfigureAwait( false );
		}
		return leadingDot;
	}

	private static async Task WriteEdTerminatorAsync(
		TextWriter output,
		bool leadingDot,
		int firstOutputLine,
		int lineCount,
		CancellationToken cancellationToken
	) {
		await WriteLineAsync( output, ".", cancellationToken ).ConfigureAwait( false );
		if ( !leadingDot ) {
			return;
		}
		var lastOutputLine = checked( firstOutputLine + lineCount - 1 );
		var address = 1 == lineCount
			? firstOutputLine.ToString( CultureInfo.InvariantCulture )
			: string.Concat(
				firstOutputLine.ToString( CultureInfo.InvariantCulture ),
				",",
				lastOutputLine.ToString( CultureInfo.InvariantCulture )
			);
		await WriteLineAsync(
			output,
			string.Concat( address, "s/^\\.//" ),
			cancellationToken
		).ConfigureAwait( false );
	}

	private static async Task WriteEdEditAsync(
		TextWriter output,
		int start,
		int oldLength,
		IReadOnlyList<ComparisonLine> replacement,
		bool stripTrailingCarriageReturn,
		CancellationToken cancellationToken
	) {
		if ( 0 == oldLength ) {
			await WriteLineAsync(
				output,
				string.Concat( start.ToString( CultureInfo.InvariantCulture ), "a" ),
				cancellationToken
			).ConfigureAwait( false );
		} else if ( 0 == replacement.Count ) {
			await WriteLineAsync( output, FormatEdAddress( start, oldLength, "d" ), cancellationToken ).ConfigureAwait( false );
			return;
		} else {
			await WriteLineAsync( output, FormatEdAddress( start, oldLength, "c" ), cancellationToken ).ConfigureAwait( false );
		}
		await WriteEdPayloadAsync(
			output,
			replacement,
			checked( start + 1 ),
			stripTrailingCarriageReturn,
			cancellationToken
		).ConfigureAwait( false );
	}

	private static async Task WriteEdPayloadAsync(
		TextWriter output,
		IReadOnlyList<ComparisonLine> payload,
		int firstOutputLine,
		bool stripTrailingCarriageReturn,
		CancellationToken cancellationToken
	) {
		var leadingDot = await WriteEdSourceLinesAsync(
			output,
			payload,
			stripTrailingCarriageReturn,
			cancellationToken
		).ConfigureAwait( false );
		await WriteEdTerminatorAsync(
			output,
			leadingDot,
			firstOutputLine,
			payload.Count,
			cancellationToken
		).ConfigureAwait( false );
	}

	private static IReadOnlyList<ComparisonLine> BuildTwoWayConflict( ThreeWayChangeRegion region, Diff3Options options ) {
		var lines = new List<ComparisonLine> {
			GeneratedLine( string.Concat( "<<<<<<< ", options.Labels[0] ) )
		};
		lines.AddRange( NormalizeLines( region.MineLines, options.StripTrailingCarriageReturn ) );
		lines.Add( GeneratedLine( "=======" ) );
		lines.AddRange( NormalizeLines( region.YoursLines, options.StripTrailingCarriageReturn ) );
		lines.Add( GeneratedLine( string.Concat( ">>>>>>> ", options.Labels[2] ) ) );
		return lines.AsReadOnly();
	}

	private static IReadOnlyList<ComparisonLine> BuildThreeWayConflict( ThreeWayChangeRegion region, Diff3Options options ) {
		var lines = new List<ComparisonLine> {
			GeneratedLine( string.Concat( "<<<<<<< ", options.Labels[0] ) )
		};
		lines.AddRange( NormalizeLines( region.MineLines, options.StripTrailingCarriageReturn ) );
		lines.Add( GeneratedLine( string.Concat( "||||||| ", options.Labels[1] ) ) );
		lines.AddRange( NormalizeLines( region.OlderLines, options.StripTrailingCarriageReturn ) );
		lines.Add( GeneratedLine( "=======" ) );
		lines.AddRange( NormalizeLines( region.YoursLines, options.StripTrailingCarriageReturn ) );
		lines.Add( GeneratedLine( string.Concat( ">>>>>>> ", options.Labels[2] ) ) );
		return lines.AsReadOnly();
	}

	private static IReadOnlyList<ComparisonLine> BuildAncestorConflict( ThreeWayChangeRegion region, Diff3Options options ) {
		var lines = new List<ComparisonLine> {
			GeneratedLine( string.Concat( "<<<<<<< ", options.Labels[1] ) )
		};
		lines.AddRange( NormalizeLines( region.OlderLines, options.StripTrailingCarriageReturn ) );
		lines.Add( GeneratedLine( "=======" ) );
		lines.AddRange( NormalizeLines( region.YoursLines, options.StripTrailingCarriageReturn ) );
		lines.Add( GeneratedLine( string.Concat( ">>>>>>> ", options.Labels[2] ) ) );
		return lines.AsReadOnly();
	}

	private static IReadOnlyList<ComparisonLine> NormalizeLines(
		IReadOnlyList<ComparisonLine> lines,
		bool stripTrailingCarriageReturn
	) {
		if ( !stripTrailingCarriageReturn ) {
			return lines;
		}
		return lines.Select( line => new ComparisonLine( GetOutputContent( line, true ), line.HasLineTerminator ) ).ToArray();
	}

	private static string FormatEdAddress( int start, int length, string command ) {
		var first = checked( start + 1 );
		if ( 1 == length ) {
			return string.Concat( first.ToString( CultureInfo.InvariantCulture ), command );
		}
		return string.Concat(
			first.ToString( CultureInfo.InvariantCulture ),
			",",
			checked( start + length ).ToString( CultureInfo.InvariantCulture ),
			command
		);
	}

	private static async Task WriteNormalLinesAsync(
		TextWriter output,
		IReadOnlyList<ComparisonLine> lines,
		bool initialTab,
		bool stripTrailingCarriageReturn,
		CancellationToken cancellationToken
	) {
		var prefix = initialTab ? "\t" : "  ";
		foreach ( var line in lines ) {
			await WriteLineAsync(
				output,
				string.Concat( prefix, GetOutputContent( line, stripTrailingCarriageReturn ) ),
				cancellationToken
			).ConfigureAwait( false );
			if ( !line.HasLineTerminator ) {
				await WriteLineAsync( output, NoNewlineMarker, cancellationToken ).ConfigureAwait( false );
			}
		}
	}

	private static async Task WriteSourceRangeAsync(
		TextWriter output,
		IReadOnlyList<ComparisonLine> lines,
		int start,
		int length,
		bool stripTrailingCarriageReturn,
		CancellationToken cancellationToken
	) {
		for ( var index = 0; index < length; index++ ) {
			await WriteLineAsync(
				output,
				NormalizeLine( lines[start + index], stripTrailingCarriageReturn ),
				cancellationToken
			).ConfigureAwait( false );
		}
	}

	private static async Task WriteLinesAsync(
		TextWriter output,
		IReadOnlyList<ComparisonLine> lines,
		CancellationToken cancellationToken
	) {
		foreach ( var line in lines ) {
			await WriteLineAsync( output, line, cancellationToken ).ConfigureAwait( false );
		}
	}

	private static async Task WriteLineAsync(
		TextWriter output,
		ComparisonLine line,
		CancellationToken cancellationToken
	) {
		await output.WriteAsync( line.Content.AsMemory(), cancellationToken ).ConfigureAwait( false );
		if ( line.HasLineTerminator ) {
			await output.WriteLineAsync( ReadOnlyMemory<char>.Empty, cancellationToken ).ConfigureAwait( false );
		}
	}

	private static Task WriteLineAsync( TextWriter output, string value, CancellationToken cancellationToken ) {
		return output.WriteLineAsync( value.AsMemory(), cancellationToken );
	}

	private static ComparisonLine NormalizeLine( ComparisonLine line, bool stripTrailingCarriageReturn ) {
		return stripTrailingCarriageReturn
			? new ComparisonLine( GetOutputContent( line, true ), line.HasLineTerminator )
			: line;
	}

	private static string GetOutputContent( ComparisonLine line, bool stripTrailingCarriageReturn ) {
		var content = line.Content;
		return stripTrailingCarriageReturn && 0 < content.Length && '\r' == content[^1]
			? content[..^1]
			: content;
	}

	private static ComparisonLine GeneratedLine( string content ) {
		return new ComparisonLine( content, true );
	}

	private static string GetHeader( ThreeWayChangeKind kind ) {
		return kind switch {
			ThreeWayChangeKind.MineOnly => "====1",
			ThreeWayChangeKind.OlderOnly => "====2",
			ThreeWayChangeKind.YoursOnly => "====3",
			_ => "===="
		};
	}

	private static IReadOnlyList<NormalSection> GetNormalSections( ThreeWayChangeRegion region ) {
		return region.Kind switch {
			ThreeWayChangeKind.MineOnly => new[] {
				new NormalSection( 1, region.MineStart, region.MineLines, true ),
				new NormalSection( 2, region.OlderStart, region.OlderLines, false ),
				new NormalSection( 3, region.YoursStart, region.YoursLines, true )
			},
			ThreeWayChangeKind.OlderOnly => new[] {
				new NormalSection( 1, region.MineStart, region.MineLines, false ),
				new NormalSection( 3, region.YoursStart, region.YoursLines, true ),
				new NormalSection( 2, region.OlderStart, region.OlderLines, true )
			},
			ThreeWayChangeKind.YoursOnly => new[] {
				new NormalSection( 1, region.MineStart, region.MineLines, false ),
				new NormalSection( 2, region.OlderStart, region.OlderLines, true ),
				new NormalSection( 3, region.YoursStart, region.YoursLines, true )
			},
			_ => new[] {
				new NormalSection( 1, region.MineStart, region.MineLines, true ),
				new NormalSection( 2, region.OlderStart, region.OlderLines, true ),
				new NormalSection( 3, region.YoursStart, region.YoursLines, true )
			}
		};
	}

	private static string FormatRange( int fileNumber, int start, int length ) {
		var prefix = string.Concat( fileNumber.ToString( CultureInfo.InvariantCulture ), ":" );
		if ( 0 == length ) {
			return string.Concat( prefix, start.ToString( CultureInfo.InvariantCulture ), "a" );
		}
		var first = checked( start + 1 );
		if ( 1 == length ) {
			return string.Concat( prefix, first.ToString( CultureInfo.InvariantCulture ), "c" );
		}
		return string.Concat(
			prefix,
			first.ToString( CultureInfo.InvariantCulture ),
			",",
			checked( start + length ).ToString( CultureInfo.InvariantCulture ),
			"c"
		);
	}
}
