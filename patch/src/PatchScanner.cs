namespace Icod.Patch;

using System.Text;

/// <summary>Classifies structural patch lines without interpreting hunk contents.</summary>
internal enum PatchProbeKind {
	/// <summary>An ordinary non-structural line.</summary>
	Other,
	/// <summary>An empty line.</summary>
	Empty,
	/// <summary>A line beginning with the unified/context dash-header marker.</summary>
	DashHeader,
	/// <summary>A unified new-file header.</summary>
	UnifiedNewHeader,
	/// <summary>A context old-file header.</summary>
	ContextOldHeader,
	/// <summary>A unified hunk header.</summary>
	UnifiedHunk,
	/// <summary>A context separator line.</summary>
	ContextSeparator,
	/// <summary>A context hunk range line.</summary>
	ContextRange,
	/// <summary>A normal-diff or ed command line.</summary>
	NumericDirective,
	/// <summary>A normal-diff old-data line.</summary>
	NormalOldData,
	/// <summary>A normal-diff new-data line.</summary>
	NormalNewData,
	/// <summary>The normal-diff change separator.</summary>
	NormalSeparator,
	/// <summary>An incomplete-final-line marker.</summary>
	NoNewlineMarker,
	/// <summary>A single dot terminating ed input text.</summary>
	EdDot
}

/// <summary>Contains the bounded structural classification of one source record.</summary>
internal readonly struct PatchLineProbe {
	/// <summary>Initializes a line probe.</summary>
	/// <param name="kind">The structural kind.</param>
	/// <param name="fileName">A parsed file name, when present.</param>
	/// <param name="firstByte">The first content byte, or zero for an empty line.</param>
	/// <param name="secondByte">The second content byte, or zero when absent.</param>
	public PatchLineProbe( PatchProbeKind kind, string? fileName, byte firstByte, byte secondByte ) {
		this.Kind = kind;
		this.FileName = fileName;
		this.FirstByte = firstByte;
		this.SecondByte = secondByte;
	}

	/// <summary>Gets the structural kind.</summary>
	public PatchProbeKind Kind { get; }

	/// <summary>Gets the parsed header file name, when present.</summary>
	public string? FileName { get; }

	/// <summary>Gets the first content byte, or zero for an empty line.</summary>
	public byte FirstByte { get; }

	/// <summary>Gets the second content byte, or zero when absent.</summary>
	public byte SecondByte { get; }
}

/// <summary>Detects patch-section candidates while preserving parsing for later phases.</summary>
internal static class PatchScanner {
	private readonly struct SectionSeed {
		/// <summary>Initializes a detected-section seed.</summary>
		/// <param name="format">The candidate patch format.</param>
		/// <param name="start">The first record index.</param>
		/// <param name="oldFileName">The old-file name, when present.</param>
		/// <param name="newFileName">The new-file name, when present.</param>
		public SectionSeed(
			PatchFormat format,
			int start,
			string? oldFileName,
			string? newFileName
		) {
			this.Format = format;
			this.Start = start;
			this.OldFileName = oldFileName;
			this.NewFileName = newFileName;
		}

		/// <summary>Gets the candidate patch format.</summary>
		public PatchFormat Format { get; }

		/// <summary>Gets the first record index.</summary>
		public int Start { get; }

		/// <summary>Gets the old-file name, when present.</summary>
		public string? OldFileName { get; }

		/// <summary>Gets the new-file name, when present.</summary>
		public string? NewFileName { get; }
	}

	/// <summary>Classifies one byte-oriented source record.</summary>
	/// <param name="line">The record bytes excluding its terminator.</param>
	/// <param name="location">The source location.</param>
	/// <returns>The bounded structural probe.</returns>
	public static PatchLineProbe ClassifyLine(
		ReadOnlySpan<byte> line,
		PatchSourceLocation location
	) {
		var first = 0 < line.Length ? line[0] : (byte)0;
		var second = 1 < line.Length ? line[1] : (byte)0;
		if ( 0 == line.Length ) {
			return new PatchLineProbe( PatchProbeKind.Empty, null, first, second );
		}
		var containsNul = 0 <= line.IndexOf( (byte)0 );
		if (
			containsNul
			&& ( LooksHeaderDirectiveLike( line ) || LooksNumericDirectiveLikeWithNul( line ) )
		) {
			throw new PatchInputException( "NUL byte in patch directive", location );
		}
		if ( IsNoNewlineMarker( line ) ) {
			return new PatchLineProbe( PatchProbeKind.NoNewlineMarker, null, first, second );
		}
		if ( IsContextSeparator( line ) ) {
			return new PatchLineProbe( PatchProbeKind.ContextSeparator, null, first, second );
		}
		if ( StartsWithAscii( line, "@@" ) ) {
			return new PatchLineProbe( PatchProbeKind.UnifiedHunk, null, first, second );
		}
		if ( IsContextRange( line, location ) ) {
			return new PatchLineProbe( PatchProbeKind.ContextRange, null, first, second );
		}
		if ( StartsHeader( line, "+++" ) ) {
			return new PatchLineProbe(
				PatchProbeKind.UnifiedNewHeader,
				ParseHeaderFileName( line[3..], location ),
				first,
				second
			);
		}
		if ( StartsHeader( line, "***" ) ) {
			return new PatchLineProbe(
				PatchProbeKind.ContextOldHeader,
				ParseHeaderFileName( line[3..], location ),
				first,
				second
			);
		}
		if ( StartsHeader( line, "---" ) ) {
			return new PatchLineProbe(
				PatchProbeKind.DashHeader,
				ParseHeaderFileName( line[3..], location ),
				first,
				second
			);
		}
		if ( TryParseNumericDirective( line, location, out var candidate ) ) {
			return new PatchLineProbe( PatchProbeKind.NumericDirective, null, first, second );
		}
		if ( candidate ) {
			if ( containsNul ) {
				throw new PatchInputException( "NUL byte in patch directive", location );
			}
			throw new PatchInputException( "malformed patch directive", location );
		}
		if ( StartsWithAscii( line, "< " ) ) {
			return new PatchLineProbe( PatchProbeKind.NormalOldData, null, first, second );
		}
		if ( StartsWithAscii( line, "> " ) ) {
			return new PatchLineProbe( PatchProbeKind.NormalNewData, null, first, second );
		}
		if ( line.SequenceEqual( "---"u8 ) ) {
			return new PatchLineProbe( PatchProbeKind.NormalSeparator, null, first, second );
		}
		if ( line.SequenceEqual( "."u8 ) ) {
			return new PatchLineProbe( PatchProbeKind.EdDot, null, first, second );
		}
		return new PatchLineProbe( PatchProbeKind.Other, null, first, second );
	}

	/// <summary>Builds patch sections and adjacent text regions from source probes.</summary>
	/// <param name="records">The source records.</param>
	/// <param name="probes">The structural probes.</param>
	/// <returns>The completed scan result.</returns>
	public static PatchScanResult Detect(
		IReadOnlyList<PatchRecord> records,
		IReadOnlyList<PatchLineProbe> probes
	) {
		ArgumentNullException.ThrowIfNull( records );
		ArgumentNullException.ThrowIfNull( probes );
		if ( records.Count != probes.Count ) {
			throw new ArgumentException( "record and probe counts differ", nameof( probes ) );
		}
		var seeds = FindSectionSeeds( probes );
		if ( 0 == seeds.Count ) {
			return new PatchScanResult( records, Array.Empty<PatchSection>(), null, null );
		}
		var sections = new List<PatchSection>( seeds.Count );
		for ( var index = 0; index < seeds.Count; index++ ) {
			var seed = seeds[index];
			var nextSeed = index + 1 < seeds.Count ? seeds[index + 1].Start : probes.Count;
			var end = FindSectionEnd( seed, probes, nextSeed );
			sections.Add(
				new PatchSection(
					seed.Format,
					seed.Start,
					Math.Max( 1, end - seed.Start ),
					seed.OldFileName,
					seed.NewFileName
				)
			);
		}
		var first = sections[0].FirstRecordIndex;
		var lastSection = sections[^1];
		var afterLast = checked( lastSection.FirstRecordIndex + lastSection.RecordCount );
		var leading = 0 < first ? new PatchTextRegion( 0, first ) : null;
		var trailing = afterLast < records.Count
			? new PatchTextRegion( afterLast, records.Count - afterLast )
			: null;
		return new PatchScanResult( records, sections, leading, trailing );
	}

	private static List<SectionSeed> FindSectionSeeds( IReadOnlyList<PatchLineProbe> probes ) {
		var seeds = new List<SectionSeed>();
		var index = 0;
		while ( index < probes.Count ) {
			if (
				PatchProbeKind.DashHeader == probes[index].Kind
				&& index + 1 < probes.Count
				&& PatchProbeKind.UnifiedNewHeader == probes[index + 1].Kind
			) {
				seeds.Add(
					new SectionSeed(
						PatchFormat.Unified,
						index,
						probes[index].FileName,
						probes[index + 1].FileName
					)
				);
				index += 2;
				continue;
			}
			if (
				PatchProbeKind.ContextOldHeader == probes[index].Kind
				&& index + 1 < probes.Count
				&& PatchProbeKind.DashHeader == probes[index + 1].Kind
			) {
				seeds.Add(
					new SectionSeed(
						PatchFormat.Context,
						index,
						probes[index].FileName,
						probes[index + 1].FileName
					)
				);
				index += 2;
				continue;
			}
			if ( PatchProbeKind.NumericDirective == probes[index].Kind ) {
				var format = LooksLikeNormalDiff( probes, index )
					? PatchFormat.Normal
					: PatchFormat.EdScript;
				seeds.Add( new SectionSeed( format, index, null, null ) );
				index = SkipNumericSection( probes, index + 1 );
				continue;
			}
			index++;
		}
		return seeds;
	}

	private static int SkipNumericSection( IReadOnlyList<PatchLineProbe> probes, int index ) {
		for ( ; index < probes.Count; index++ ) {
			if ( IsHeaderPairStart( probes, index ) ) {
				return index;
			}
		}
		return probes.Count;
	}

	private static bool IsHeaderPairStart( IReadOnlyList<PatchLineProbe> probes, int index ) {
		if ( index + 1 >= probes.Count ) {
			return false;
		}
		return (
			PatchProbeKind.DashHeader == probes[index].Kind
			&& PatchProbeKind.UnifiedNewHeader == probes[index + 1].Kind
		) || (
			PatchProbeKind.ContextOldHeader == probes[index].Kind
			&& PatchProbeKind.DashHeader == probes[index + 1].Kind
		);
	}

	private static int FindSectionEnd(
		SectionSeed seed,
		IReadOnlyList<PatchLineProbe> probes,
		int upperBound
	) {
		var minimum = seed.Format is PatchFormat.Unified or PatchFormat.Context
			? Math.Min( upperBound, seed.Start + 2 )
			: seed.Start + 1;
		var lastRecognized = minimum;
		var sawBody = false;
		for ( var index = minimum; index < upperBound; index++ ) {
			if ( IsSectionBodyLine( seed.Format, probes[index] ) ) {
				lastRecognized = index + 1;
				sawBody = true;
				continue;
			}
			if ( sawBody ) {
				break;
			}
		}
		return Math.Max( minimum, lastRecognized );
	}

	private static bool IsSectionBodyLine( PatchFormat format, PatchLineProbe probe ) {
		return format switch {
			PatchFormat.Unified => probe.Kind is
				PatchProbeKind.UnifiedHunk or
				PatchProbeKind.NoNewlineMarker
				|| probe.FirstByte is (byte)' ' or (byte)'+' or (byte)'-',
			PatchFormat.Context => probe.Kind is
				PatchProbeKind.ContextSeparator or
				PatchProbeKind.ContextRange or
				PatchProbeKind.NoNewlineMarker
				|| probe.FirstByte is (byte)' ' or (byte)'+' or (byte)'-' or (byte)'!',
			PatchFormat.Normal => probe.Kind is
				PatchProbeKind.NumericDirective or
				PatchProbeKind.NormalOldData or
				PatchProbeKind.NormalNewData or
				PatchProbeKind.NormalSeparator or
				PatchProbeKind.NoNewlineMarker,
			PatchFormat.EdScript => probe.Kind is
				PatchProbeKind.NumericDirective or
				PatchProbeKind.EdDot or
				PatchProbeKind.NoNewlineMarker or
				PatchProbeKind.Other or
				PatchProbeKind.Empty,
			_ => false
		};
	}

	private static bool LooksLikeNormalDiff( IReadOnlyList<PatchLineProbe> probes, int directiveIndex ) {
		var limit = Math.Min( probes.Count, directiveIndex + 8 );
		for ( var index = directiveIndex + 1; index < limit; index++ ) {
			if ( probes[index].Kind is
				PatchProbeKind.NormalOldData or
				PatchProbeKind.NormalNewData or
				PatchProbeKind.NormalSeparator ) {
				return true;
			}
			if ( PatchProbeKind.NumericDirective == probes[index].Kind || IsHeaderPairStart( probes, index ) ) {
				break;
			}
		}
		return false;
	}

	private static bool LooksHeaderDirectiveLike( ReadOnlySpan<byte> line ) {
		var index = 0;
		while ( index < line.Length && IsHorizontalSpace( line[index] ) ) {
			index++;
		}
		if ( index >= line.Length ) {
			return false;
		}
		var candidate = line[index..];
		return LooksMarkerDirectiveLike( candidate, "---" )
			|| LooksMarkerDirectiveLike( candidate, "+++" )
			|| LooksMarkerDirectiveLike( candidate, "***" )
			|| LooksMarkerDirectiveLike( candidate, "@@" );
	}

	private static bool LooksMarkerDirectiveLike(
		ReadOnlySpan<byte> line,
		string marker
	) {
		if ( !StartsWithAscii( line, marker ) || line.Length <= marker.Length ) {
			return false;
		}
		return IsHorizontalSpace( line[marker.Length] ) || 0 == line[marker.Length];
	}

	private static bool LooksNumericDirectiveLikeWithNul( ReadOnlySpan<byte> line ) {
		var index = 0;
		SkipSpaceAndNul( line, ref index );
		if ( index >= line.Length || !IsAsciiDigit( line[index] ) ) {
			return false;
		}
		while ( index < line.Length && IsAsciiDigit( line[index] ) ) {
			index++;
		}
		SkipSpaceAndNul( line, ref index );
		if ( index < line.Length && (byte)',' == line[index] ) {
			index++;
			SkipSpaceAndNul( line, ref index );
			if ( index >= line.Length || !IsAsciiDigit( line[index] ) ) {
				return false;
			}
			while ( index < line.Length && IsAsciiDigit( line[index] ) ) {
				index++;
			}
			SkipSpaceAndNul( line, ref index );
		}
		return index < line.Length
			&& ( line[index] == (byte)'a' || line[index] == (byte)'c' || line[index] == (byte)'d' );
	}

	private static bool TryParseNumericDirective(
		ReadOnlySpan<byte> line,
		PatchSourceLocation location,
		out bool candidate
	) {
		candidate = false;
		var index = 0;
		SkipSpace( line, ref index );
		if ( index >= line.Length || !IsAsciiDigit( line[index] ) ) {
			return false;
		}
		var firstNumberOverflowed = ScanNumber( line, ref index );
		SkipSpace( line, ref index );
		if ( index < line.Length && (byte)',' == line[index] ) {
			candidate = true;
			if ( firstNumberOverflowed ) {
				throw new PatchInputException( "line number is too large", location );
			}
			index++;
			SkipSpace( line, ref index );
			ParseNumber( line, ref index, location );
			SkipSpace( line, ref index );
		}
		if ( index >= line.Length || ( line[index] != (byte)'a' && line[index] != (byte)'c' && line[index] != (byte)'d' ) ) {
			return false;
		}
		candidate = true;
		if ( firstNumberOverflowed ) {
			throw new PatchInputException( "line number is too large", location );
		}
		index++;
		SkipSpace( line, ref index );
		if ( index == line.Length ) {
			return true;
		}
		ParseNumber( line, ref index, location );
		SkipSpace( line, ref index );
		if ( index < line.Length && (byte)',' == line[index] ) {
			index++;
			SkipSpace( line, ref index );
			ParseNumber( line, ref index, location );
			SkipSpace( line, ref index );
		}
		return index == line.Length;
	}

	private static bool ScanNumber( ReadOnlySpan<byte> line, ref int index ) {
		var overflowed = false;
		long value = 0;
		while ( index < line.Length && IsAsciiDigit( line[index] ) ) {
			if ( !overflowed ) {
				try {
					value = checked( value * 10 + line[index] - (byte)'0' );
				} catch ( OverflowException ) {
					overflowed = true;
				}
			}
			index++;
		}
		return overflowed;
	}

	private static long ParseNumber(
		ReadOnlySpan<byte> line,
		ref int index,
		PatchSourceLocation location
	) {
		if ( index >= line.Length || !IsAsciiDigit( line[index] ) ) {
			throw new PatchInputException( "missing line number in patch directive", location );
		}
		long value = 0;
		try {
			while ( index < line.Length && IsAsciiDigit( line[index] ) ) {
				value = checked( value * 10 + line[index] - (byte)'0' );
				index++;
			}
		} catch ( OverflowException ) {
			throw new PatchInputException( "line number is too large", location );
		}
		return value;
	}

	private static string ParseHeaderFileName(
		ReadOnlySpan<byte> remainder,
		PatchSourceLocation location
	) {
		var index = 0;
		SkipSpace( remainder, ref index );
		if ( index >= remainder.Length ) {
			throw new PatchInputException( "missing filename in patch header", location );
		}
		ReadOnlySpan<byte> name;
		if ( (byte)'"' == remainder[index] ) {
			var start = index;
			index++;
			var escaped = false;
			var closed = false;
			for ( ; index < remainder.Length; index++ ) {
				var value = remainder[index];
				if ( escaped ) {
					if ( value is (byte)'n' or (byte)'r' ) {
						throw new PatchInputException( "patch filename contains a newline", location );
					}
					if ( value is >= (byte)'0' and <= (byte)'7' ) {
						var octal = value - (byte)'0';
						var digits = 1;
						while ( digits < 3 && index + 1 < remainder.Length && remainder[index + 1] is >= (byte)'0' and <= (byte)'7' ) {
							index++;
							octal = checked( octal * 8 + remainder[index] - (byte)'0' );
							digits++;
						}
						if ( octal is 10 or 13 ) {
							throw new PatchInputException( "patch filename contains a newline", location );
						}
					}
					escaped = false;
					continue;
				}
				if ( (byte)'\\' == value ) {
					escaped = true;
					continue;
				}
				if ( (byte)'"' == value ) {
					closed = true;
					index++;
					break;
				}
			}
			if ( !closed ) {
				throw new PatchInputException( "unterminated quoted filename in patch header", location );
			}
			name = remainder[start..index];
		} else {
			var start = index;
			while ( index < remainder.Length && (byte)'\t' != remainder[index] ) {
				index++;
			}
			name = TrimEndSpace( remainder[start..index] );
		}
		if ( 0 == name.Length ) {
			throw new PatchInputException( "missing filename in patch header", location );
		}
		return Encoding.UTF8.GetString( name );
	}

	private static ReadOnlySpan<byte> TrimEndSpace( ReadOnlySpan<byte> value ) {
		var length = value.Length;
		while ( 0 < length && IsHorizontalSpace( value[length - 1] ) ) {
			length--;
		}
		return value[..length];
	}

	private static bool StartsHeader( ReadOnlySpan<byte> line, string marker ) {
		if ( !StartsWithAscii( line, marker ) || line.Length <= marker.Length ) {
			return false;
		}
		return IsHorizontalSpace( line[marker.Length] );
	}

	private static bool IsContextSeparator( ReadOnlySpan<byte> line ) {
		if ( line.Length < 8 ) {
			return false;
		}
		foreach ( var value in line ) {
			if ( (byte)'*' != value ) {
				return false;
			}
		}
		return true;
	}

	private static bool IsContextRange(
		ReadOnlySpan<byte> line,
		PatchSourceLocation location
	) {
		var isOldRange = StartsWithAscii( line, "***" );
		var isNewRange = StartsWithAscii( line, "---" );
		if ( !isOldRange && !isNewRange ) {
			return false;
		}
		var index = 3;
		if ( index >= line.Length || !IsHorizontalSpace( line[index] ) ) {
			return false;
		}
		SkipSpace( line, ref index );
		if ( index >= line.Length || !IsAsciiDigit( line[index] ) ) {
			return false;
		}
		var overflowed = ScanNumber( line, ref index );
		SkipSpace( line, ref index );
		if ( index < line.Length && (byte)',' == line[index] ) {
			index++;
			SkipSpace( line, ref index );
			if ( index >= line.Length || !IsAsciiDigit( line[index] ) ) {
				return false;
			}
			overflowed |= ScanNumber( line, ref index );
			SkipSpace( line, ref index );
		}
		if ( !StartsWithAscii( line[index..], isOldRange ? "****" : "----" ) ) {
			return false;
		}
		if ( overflowed ) {
			throw new PatchInputException( "line number is too large", location );
		}
		return true;
	}

	private static bool IsNoNewlineMarker( ReadOnlySpan<byte> line ) {
		return StartsWithAscii( line, "\\ No newline at end of file" );
	}

	private static bool StartsWithAscii( ReadOnlySpan<byte> line, string value ) {
		if ( line.Length < value.Length ) {
			return false;
		}
		for ( var index = 0; index < value.Length; index++ ) {
			if ( line[index] != (byte)value[index] ) {
				return false;
			}
		}
		return true;
	}

	private static void SkipSpace( ReadOnlySpan<byte> line, ref int index ) {
		while ( index < line.Length && IsHorizontalSpace( line[index] ) ) {
			index++;
		}
	}

	private static void SkipSpaceAndNul( ReadOnlySpan<byte> line, ref int index ) {
		while (
			index < line.Length
			&& ( IsHorizontalSpace( line[index] ) || 0 == line[index] )
		) {
			index++;
		}
	}

	private static bool IsHorizontalSpace( byte value ) {
		return value is (byte)' ' or (byte)'\t';
	}

	private static bool IsAsciiDigit( byte value ) {
		return value is >= (byte)'0' and <= (byte)'9';
	}
}
