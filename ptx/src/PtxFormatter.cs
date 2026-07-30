namespace Icod.CoreUtils.Ptx;

using System.Buffers;
using System.Text;

/// <summary>Plans and writes GNU-compatible dumb, roff, and TeX output fields.</summary>
internal sealed class PtxFormatter {
	private static readonly byte[] NewLine = Encoding.UTF8.GetBytes( Environment.NewLine );
	private readonly PtxSettings settings;
	private readonly PtxProcessingState state;
	private readonly PtxPatterns patterns;
	private readonly PtxContextStore store;
	private readonly Stream destination;
	private bool initialized;
	private int referenceMaximumWidth;
	private int halfLineWidth;
	private int beforeMaximumWidth;
	private int keyAfterMaximumWidth;

	/// <summary>Initializes an occurrence formatter.</summary>
	/// <param name="settings">The effective command settings.</param>
	/// <param name="state">The completed processing statistics.</param>
	/// <param name="patterns">The compiled word-pattern policy.</param>
	/// <param name="store">The sealed context store.</param>
	/// <param name="destination">The caller-owned output stream.</param>
	internal PtxFormatter(
		PtxSettings settings,
		PtxProcessingState state,
		PtxPatterns patterns,
		PtxContextStore store,
		Stream destination
	) {
		this.settings = settings;
		this.state = state;
		this.patterns = patterns;
		this.store = store;
		this.destination = destination;
	}

	/// <summary>Writes one sorted occurrence.</summary>
	/// <param name="occurrence">The occurrence.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>A task representing output.</returns>
	internal async ValueTask WriteAsync(
		PtxOccurrence occurrence,
		CancellationToken cancellationToken
	) {
		this.EnsureInitialized();
		var context = await this.store.ReadAsync(
			occurrence.ContextOffset,
			occurrence.ContextLength,
			cancellationToken
		).ConfigureAwait( false );
		var fields = this.DefineFields( occurrence, context, cancellationToken );
		var line = new ArrayBufferWriter<byte>( Math.Clamp( this.settings.LineWidth, 128, 1_048_576 ) );
		switch ( this.settings.OutputFormat ) {
			case PtxOutputFormat.Roff:
				this.WriteRoffLine( line, context, occurrence.Reference, fields );
				break;
			case PtxOutputFormat.Tex:
				this.WriteTexLine( line, context, occurrence.Reference, fields, cancellationToken );
				break;
			default:
				this.WriteDumbLine( line, context, occurrence.Reference, fields );
				break;
		}
		WriteBytes( line, NewLine );
		await this.destination.WriteAsync( line.WrittenMemory, cancellationToken ).ConfigureAwait( false );
	}

	private void EnsureInitialized() {
		if ( this.initialized ) {
			return;
		}
		this.initialized = true;
		if ( this.settings.AutoReference ) {
			var maximum = 0;
			foreach ( var file in this.state.Files ) {
				var width = Encoding.UTF8.GetByteCount( file.ReferenceName )
					+ file.LineCount.ToString( System.Globalization.CultureInfo.InvariantCulture ).Length;
				maximum = Math.Max( maximum, width );
			}
			this.referenceMaximumWidth = checked( maximum + 1 );
		} else if ( this.settings.InputReference ) {
			this.referenceMaximumWidth = this.state.InputReferenceMaximumWidth;
		}
		var lineWidth = this.settings.LineWidth;
		if (
			( this.settings.AutoReference || this.settings.InputReference )
			&& !this.settings.RightReference
		) {
			lineWidth = Math.Max( 0, lineWidth - ( this.referenceMaximumWidth + this.settings.GapSize ) );
		}
		this.halfLineWidth = lineWidth >> 1;
		this.beforeMaximumWidth = this.halfLineWidth - this.settings.GapSize;
		this.keyAfterMaximumWidth = this.halfLineWidth;
		var truncationLength = this.settings.TruncationString.Length;
		if ( this.settings.GnuExtensions ) {
			this.beforeMaximumWidth = Math.Max( 0, this.beforeMaximumWidth - ( 2 * truncationLength ) );
			this.keyAfterMaximumWidth -= 2 * truncationLength;
		} else {
			this.keyAfterMaximumWidth -= ( 2 * truncationLength ) + 1;
		}
	}

	private OutputFields DefineFields(
		PtxOccurrence occurrence,
		byte[] context,
		CancellationToken cancellationToken
	) {
		var memory = context.AsMemory();
		var keyStart = occurrence.KeywordStart;
		var keyEnd = checked( keyStart + occurrence.KeywordLength );
		var rightContextEnd = context.Length;
		var keyAfterEnd = keyEnd;
		var cursor = keyEnd;
		var keyAfterLimit = (long)keyStart + this.keyAfterMaximumWidth;
		while (
			cursor < rightContextEnd
			&& cursor <= keyAfterLimit
		) {
			keyAfterEnd = cursor;
			cursor = this.patterns.SkipSomething( memory, cursor, rightContextEnd, cancellationToken );
		}
		if ( cursor <= keyAfterLimit ) {
			keyAfterEnd = cursor;
		}
		var keyAfterTruncation = 0 < this.settings.TruncationString.Length && keyAfterEnd < rightContextEnd;
		keyAfterEnd = SkipWhiteBackwards( context, keyAfterEnd, keyStart );

		var leftFieldStart = 0;
		var secureDistance = (long)this.halfLineWidth + this.state.MaximumWordLength;
		if ( secureDistance < keyStart ) {
			leftFieldStart = checked( keyStart - (int)secureDistance );
			leftFieldStart = this.patterns.SkipSomething(
				memory,
				leftFieldStart,
				keyStart,
				cancellationToken
			);
		}
		var beforeStart = leftFieldStart;
		var beforeEnd = SkipWhiteBackwards( context, keyStart, beforeStart );
		while ( (long)beforeStart + this.beforeMaximumWidth < beforeEnd ) {
			beforeStart = this.patterns.SkipSomething( memory, beforeStart, beforeEnd, cancellationToken );
		}
		var beforeProbe = SkipWhiteBackwards( context, beforeStart, 0 );
		var beforeTruncation = 0 < this.settings.TruncationString.Length && 0 < beforeProbe;
		beforeStart = SkipWhite( context, beforeStart, context.Length );

		var tailStart = 0;
		var tailEnd = 0;
		var tailTruncation = false;
		var tailMaximumWidth = this.beforeMaximumWidth - ( beforeEnd - beforeStart ) - this.settings.GapSize;
		if ( 0 < tailMaximumWidth ) {
			tailStart = SkipWhite( context, keyAfterEnd, context.Length );
			tailEnd = tailStart;
			cursor = tailEnd;
			var tailLimit = (long)tailStart + tailMaximumWidth;
			while ( cursor < rightContextEnd && cursor < tailLimit ) {
				tailEnd = cursor;
				cursor = this.patterns.SkipSomething( memory, cursor, rightContextEnd, cancellationToken );
			}
			if ( cursor < tailLimit ) {
				tailEnd = cursor;
			}
			if ( tailEnd > tailStart ) {
				keyAfterTruncation = false;
				tailTruncation = 0 < this.settings.TruncationString.Length && tailEnd < rightContextEnd;
			}
			tailEnd = SkipWhiteBackwards( context, tailEnd, tailStart );
		}

		var headStart = 0;
		var headEnd = 0;
		var headTruncation = false;
		var headMaximumWidth = this.keyAfterMaximumWidth - ( keyAfterEnd - keyStart ) - this.settings.GapSize;
		if ( 0 < headMaximumWidth ) {
			headEnd = SkipWhiteBackwards( context, beforeStart, 0 );
			headStart = leftFieldStart;
			while ( (long)headStart + headMaximumWidth < headEnd ) {
				headStart = this.patterns.SkipSomething( memory, headStart, headEnd, cancellationToken );
			}
			if ( headEnd > headStart ) {
				beforeTruncation = false;
				headTruncation = 0 < this.settings.TruncationString.Length && 0 < headStart;
			}
			headStart = SkipWhite( context, headStart, headEnd );
		}
		return new OutputFields(
			new Field( tailStart, tailEnd ), tailTruncation,
			new Field( beforeStart, beforeEnd ), beforeTruncation,
			new Field( keyStart, keyAfterEnd ), keyAfterTruncation,
			new Field( headStart, headEnd ), headTruncation
		);
	}

	private void WriteDumbLine(
		ArrayBufferWriter<byte> line,
		byte[] context,
		byte[] reference,
		OutputFields fields
	) {
		if ( !this.settings.RightReference ) {
			if ( this.settings.AutoReference ) {
				this.WriteField( line, reference, new Field( 0, reference.Length ) );
				WriteByte( line, (byte)':' );
				WriteSpaces( line, this.referenceMaximumWidth + this.settings.GapSize - reference.Length - 1 );
			} else {
				this.WriteField( line, reference, new Field( 0, reference.Length ) );
				WriteSpaces( line, this.referenceMaximumWidth + this.settings.GapSize - reference.Length );
			}
		}
		if ( !fields.Tail.IsEmpty ) {
			this.WriteField( line, context, fields.Tail );
			if ( fields.TailTruncation ) {
				WriteBytes( line, this.settings.TruncationString );
			}
			WriteSpaces(
				line,
				this.halfLineWidth - this.settings.GapSize
				- fields.Before.Length
				- ( fields.BeforeTruncation ? this.settings.TruncationString.Length : 0 )
				- fields.Tail.Length
				- ( fields.TailTruncation ? this.settings.TruncationString.Length : 0 )
			);
		} else {
			WriteSpaces(
				line,
				this.halfLineWidth - this.settings.GapSize
				- fields.Before.Length
				- ( fields.BeforeTruncation ? this.settings.TruncationString.Length : 0 )
			);
		}
		if ( fields.BeforeTruncation ) {
			WriteBytes( line, this.settings.TruncationString );
		}
		this.WriteField( line, context, fields.Before );
		WriteSpaces( line, this.settings.GapSize );
		this.WriteField( line, context, fields.KeyAfter );
		if ( fields.KeyAfterTruncation ) {
			WriteBytes( line, this.settings.TruncationString );
		}
		if ( !fields.Head.IsEmpty ) {
			WriteSpaces(
				line,
				this.halfLineWidth
				- fields.KeyAfter.Length
				- ( fields.KeyAfterTruncation ? this.settings.TruncationString.Length : 0 )
				- fields.Head.Length
				- ( fields.HeadTruncation ? this.settings.TruncationString.Length : 0 )
			);
			if ( fields.HeadTruncation ) {
				WriteBytes( line, this.settings.TruncationString );
			}
			this.WriteField( line, context, fields.Head );
		} else if (
			( this.settings.AutoReference || this.settings.InputReference )
			&& this.settings.RightReference
		) {
			WriteSpaces(
				line,
				this.halfLineWidth
				- fields.KeyAfter.Length
				- ( fields.KeyAfterTruncation ? this.settings.TruncationString.Length : 0 )
			);
		}
		if (
			( this.settings.AutoReference || this.settings.InputReference )
			&& this.settings.RightReference
		) {
			WriteSpaces( line, this.settings.GapSize );
			this.WriteField( line, reference, new Field( 0, reference.Length ) );
		}
	}

	private void WriteRoffLine(
		ArrayBufferWriter<byte> line,
		byte[] context,
		byte[] reference,
		OutputFields fields
	) {
		WriteByte( line, (byte)'.' );
		WriteBytes( line, Encoding.UTF8.GetBytes( this.settings.MacroName ) );
		WriteBytes( line, " \""u8 );
		this.WriteField( line, context, fields.Tail );
		if ( fields.TailTruncation ) {
			WriteBytes( line, this.settings.TruncationString );
		}
		WriteBytes( line, "\" \""u8 );
		if ( fields.BeforeTruncation ) {
			WriteBytes( line, this.settings.TruncationString );
		}
		this.WriteField( line, context, fields.Before );
		WriteBytes( line, "\" \""u8 );
		this.WriteField( line, context, fields.KeyAfter );
		if ( fields.KeyAfterTruncation ) {
			WriteBytes( line, this.settings.TruncationString );
		}
		WriteBytes( line, "\" \""u8 );
		if ( fields.HeadTruncation ) {
			WriteBytes( line, this.settings.TruncationString );
		}
		this.WriteField( line, context, fields.Head );
		WriteByte( line, (byte)'"' );
		if ( this.settings.AutoReference || this.settings.InputReference ) {
			WriteBytes( line, " \""u8 );
			this.WriteField( line, reference, new Field( 0, reference.Length ) );
			WriteByte( line, (byte)'"' );
		}
	}

	private void WriteTexLine(
		ArrayBufferWriter<byte> line,
		byte[] context,
		byte[] reference,
		OutputFields fields,
		CancellationToken cancellationToken
	) {
		WriteByte( line, (byte)'\\' );
		WriteBytes( line, Encoding.UTF8.GetBytes( this.settings.MacroName ) );
		WriteBytes( line, " {"u8 );
		this.WriteField( line, context, fields.Tail );
		WriteBytes( line, "}{"u8 );
		this.WriteField( line, context, fields.Before );
		WriteBytes( line, "}{"u8 );
		var keyEnd = this.patterns.SkipSomething(
			context,
			fields.KeyAfter.Start,
			fields.KeyAfter.End,
			cancellationToken
		);
		this.WriteField( line, context, new Field( fields.KeyAfter.Start, keyEnd ) );
		WriteBytes( line, "}{"u8 );
		this.WriteField( line, context, new Field( keyEnd, fields.KeyAfter.End ) );
		WriteBytes( line, "}{"u8 );
		this.WriteField( line, context, fields.Head );
		WriteByte( line, (byte)'}' );
		if ( this.settings.AutoReference || this.settings.InputReference ) {
			WriteByte( line, (byte)'{' );
			this.WriteField( line, reference, new Field( 0, reference.Length ) );
			WriteByte( line, (byte)'}' );
		}
	}

	private void WriteField(
		ArrayBufferWriter<byte> destination,
		byte[] source,
		Field field
	) {
		for ( var index = field.Start; index < field.End; index++ ) {
			var value = source[ index ];
			if ( PtxText.IsWhiteSpace( value ) ) {
				WriteByte( destination, (byte)' ' );
				continue;
			}
			if ( PtxOutputFormat.Roff == this.settings.OutputFormat && (byte)'"' == value ) {
				WriteBytes( destination, "\"\""u8 );
				continue;
			}
			if ( PtxOutputFormat.Tex == this.settings.OutputFormat ) {
				switch ( value ) {
					case (byte)'$':
					case (byte)'%':
					case (byte)'&':
					case (byte)'#':
					case (byte)'_':
						WriteByte( destination, (byte)'\\' );
						WriteByte( destination, value );
						continue;
					case (byte)'{':
					case (byte)'}':
						WriteBytes( destination, "$\\"u8 );
						WriteByte( destination, value );
						WriteByte( destination, (byte)'$' );
						continue;
					case (byte)'\\':
						WriteBytes( destination, "\\backslash{}"u8 );
						continue;
				}
			}
			WriteByte( destination, value );
		}
	}

	private static int SkipWhite( byte[] value, int index, int limit ) {
		while ( index < limit && PtxText.IsWhiteSpace( value[ index ] ) ) {
			index++;
		}
		return index;
	}

	private static int SkipWhiteBackwards( byte[] value, int index, int start ) {
		while ( start < index && PtxText.IsWhiteSpace( value[ index - 1 ] ) ) {
			index--;
		}
		return index;
	}

	private static void WriteByte( ArrayBufferWriter<byte> destination, byte value ) {
		var span = destination.GetSpan( 1 );
		span[ 0 ] = value;
		destination.Advance( 1 );
	}

	private static void WriteBytes( ArrayBufferWriter<byte> destination, ReadOnlySpan<byte> value ) {
		if ( value.IsEmpty ) {
			return;
		}
		value.CopyTo( destination.GetSpan( value.Length ) );
		destination.Advance( value.Length );
	}

	private static void WriteSpaces( ArrayBufferWriter<byte> destination, int count ) {
		if ( 0 >= count ) {
			return;
		}
		destination.GetSpan( count )[ ..count ].Fill( (byte)' ' );
		destination.Advance( count );
	}

	private readonly record struct Field( int Start, int End ) {
		/// <summary>Gets the field length.</summary>
		internal int Length => this.End - this.Start;
		/// <summary>Gets whether the field is empty.</summary>
		internal bool IsEmpty => this.Start >= this.End;
	}

	private readonly record struct OutputFields(
		Field Tail,
		bool TailTruncation,
		Field Before,
		bool BeforeTruncation,
		Field KeyAfter,
		bool KeyAfterTruncation,
		Field Head,
		bool HeadTruncation
	);
}
