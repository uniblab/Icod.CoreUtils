// Original behavior/reference: GNU coreutils pr 9.11
// Ported to .NET by Timothy J. Bruce <uniblab@hotmail.com>

namespace Icod.CoreUtils.Pr;

using System.Globalization;
using System.Text;
using Icod.CoreUtils.Shared.CommandLine;
using Icod.CoreUtils.Shared.Diagnostics;
using Icod.CoreUtils.Shared.Time;

/// <summary>Implements GNU-compatible pagination and multi-column presentation.</summary>
public static class Command {
	private const string VersionText = "pr (Icod.CoreUtils) 1.0";
	private const int DefaultPageLength = 66;
	private const int DefaultPageWidth = 72;
	private const int DefaultTabWidth = 8;
	private const int HeaderAndTrailerLines = 10;
	private const int BufferSize = 4096;

	private sealed class PrUsageException : Exception {
		public PrUsageException( string message )
			: base( message ) {
		}
	}

	private sealed class TabSpecification {
		public char Character { get; init; } = '\t';
		public int Width { get; init; } = DefaultTabWidth;
	}

	private sealed class PageSelection {
		public int First { get; init; } = 1;
		public int? Last { get; init; }

		public bool Includes( int pageNumber ) {
			return this.First <= pageNumber && ( !this.Last.HasValue || pageNumber <= this.Last.Value );
		}

		public bool IsPastLast( int pageNumber ) {
			return this.Last.HasValue && this.Last.Value < pageNumber;
		}
	}

	private sealed class PrOptions {
		public int Columns { get; set; } = 1;
		public bool Across { get; set; }
		public bool ShowControlCharacters { get; set; }
		public bool ShowNonprinting { get; set; }
		public bool DoubleSpace { get; set; }
		public string? DateFormat { get; set; }
		public TabSpecification? ExpandTabs { get; set; }
		public bool FormFeed { get; set; }
		public string? Header { get; set; }
		public TabSpecification? OutputTabs { get; set; }
		public bool JoinLines { get; set; }
		public int PageLength { get; set; } = DefaultPageLength;
		public bool Merge { get; set; }
		public bool NumberLines { get; set; }
		public char NumberSeparator { get; set; } = '\t';
		public int NumberDigits { get; set; } = 5;
		public long? FirstLineNumber { get; set; }
		public int Margin { get; set; }
		public bool SuppressFileWarnings { get; set; }
		public string? Separator { get; set; }
		public bool SeparatorOptionSpecified { get; set; }
		public bool SeparatorStringOptionSpecified { get; set; }
		public bool OmitHeader { get; set; }
		public bool OmitPagination { get; set; }
		public int PageWidth { get; set; } = DefaultPageWidth;
		public bool WidthSpecified { get; set; }
		public bool PageWidthSpecified { get; set; }
		public PageSelection Pages { get; set; } = new();
		public List<string> Files { get; } = new();

		public bool Paginated => !this.OmitHeader && !this.OmitPagination;
		public int Spacing => this.DoubleSpace ? 2 : 1;
		public int BodyLineCapacity {
			get {
				var physicalLines = this.Paginated
					? this.PageLength - HeaderAndTrailerLines
					: this.PageLength;
				return Math.Max( 1, physicalLines / this.Spacing );
			}
		}
	}

	private sealed class PageChunk {
		public List<string> Lines { get; } = new();
		public bool EndedByFormFeed { get; set; }
		public bool EndOfInput { get; set; }
	}

	private sealed class InputCursor : IAsyncDisposable {
		private readonly TextReader reader;
		private readonly bool ownsReader;
		private readonly bool eliminateFormFeeds;
		private readonly char[] buffer = new char[BufferSize];
		private int bufferIndex;
		private int bufferCount;
		private int? pushedCharacter;
		private bool endOfInput;
		private bool consumeDelimiterAfterFullPage;
		private bool suppressLineBreakAfterEliminatedFormFeed;
		private long nextInputLineNumber = 1;

		public string DisplayName { get; }
		public DateTime HeaderDate { get; }
		public long NextInputLineNumber => this.nextInputLineNumber;
		public bool IsExhausted => this.endOfInput && !this.pushedCharacter.HasValue;

		public InputCursor(
			string displayName,
			TextReader reader,
			bool ownsReader,
			bool eliminateFormFeeds,
			DateTime headerDate
		) {
			this.DisplayName = displayName;
			this.reader = reader;
			this.ownsReader = ownsReader;
			this.eliminateFormFeeds = eliminateFormFeeds;
			this.HeaderDate = headerDate;
		}

		public ValueTask DisposeAsync() {
			if ( this.ownsReader ) {
				this.reader.Dispose();
			}
			return ValueTask.CompletedTask;
		}

		public async Task<PageChunk?> ReadPageAsync(
			int maximumLines,
			CancellationToken cancellationToken
		) {
			if ( maximumLines <= 0 ) {
				throw new ArgumentOutOfRangeException( nameof( maximumLines ) );
			}
			if ( this.IsExhausted ) {
				return null;
			}
			if ( this.consumeDelimiterAfterFullPage ) {
				this.consumeDelimiterAfterFullPage = false;
				var delimiter = await this.ReadCharacterAsync( cancellationToken ).ConfigureAwait( false );
				if ( '\f' != delimiter ) {
					this.PushCharacter( delimiter );
				} else if ( this.eliminateFormFeeds ) {
					this.suppressLineBreakAfterEliminatedFormFeed = true;
				}
			}
			if ( this.IsExhausted ) {
				return null;
			}

			var chunk = new PageChunk();
			var line = new StringBuilder();
			while ( true ) {
				cancellationToken.ThrowIfCancellationRequested();
				var character = await this.ReadCharacterAsync( cancellationToken ).ConfigureAwait( false );
				if ( character < 0 ) {
					if ( 0 < line.Length ) {
						chunk.Lines.Add( line.ToString() );
						this.nextInputLineNumber++;
					}
					if ( 0 == chunk.Lines.Count ) {
						return null;
					}
					chunk.EndOfInput = true;
					return chunk;
				}

				if ( '\f' == character ) {
					if ( this.eliminateFormFeeds ) {
						if ( 0 < line.Length ) {
							chunk.Lines.Add( line.ToString() );
							line.Clear();
							this.nextInputLineNumber++;
						}
						this.suppressLineBreakAfterEliminatedFormFeed = true;
						if ( maximumLines <= chunk.Lines.Count ) {
							return chunk;
						}
						continue;
					}
					if ( 0 < line.Length ) {
						chunk.Lines.Add( line.ToString() );
						this.nextInputLineNumber++;
					}
					chunk.EndedByFormFeed = true;
					return chunk;
				}

				if ( '\r' == character ) {
					var next = await this.ReadCharacterAsync( cancellationToken ).ConfigureAwait( false );
					if ( '\n' != next ) {
						this.PushCharacter( next );
					}
					if ( this.suppressLineBreakAfterEliminatedFormFeed && 0 == line.Length ) {
						this.suppressLineBreakAfterEliminatedFormFeed = false;
						continue;
					}
					chunk.Lines.Add( line.ToString() );
					line.Clear();
					this.suppressLineBreakAfterEliminatedFormFeed = false;
					this.nextInputLineNumber++;
				} else if ( '\n' == character ) {
					if ( this.suppressLineBreakAfterEliminatedFormFeed && 0 == line.Length ) {
						this.suppressLineBreakAfterEliminatedFormFeed = false;
						continue;
					}
					chunk.Lines.Add( line.ToString() );
					line.Clear();
					this.suppressLineBreakAfterEliminatedFormFeed = false;
					this.nextInputLineNumber++;
				} else {
					this.suppressLineBreakAfterEliminatedFormFeed = false;
					line.Append( (char)character );
					continue;
				}

				if ( maximumLines <= chunk.Lines.Count ) {
					this.consumeDelimiterAfterFullPage = true;
					return chunk;
				}
			}
		}

		private async ValueTask<int> ReadCharacterAsync( CancellationToken cancellationToken ) {
			if ( this.pushedCharacter.HasValue ) {
				var result = this.pushedCharacter.Value;
				this.pushedCharacter = null;
				return result;
			}
			if ( this.endOfInput ) {
				return -1;
			}
			if ( this.bufferCount <= this.bufferIndex ) {
				this.bufferCount = await this.reader.ReadAsync(
					this.buffer.AsMemory(),
					cancellationToken
				).ConfigureAwait( false );
				this.bufferIndex = 0;
				if ( 0 == this.bufferCount ) {
					this.endOfInput = true;
					return -1;
				}
			}
			return this.buffer[this.bufferIndex++];
		}

		private void PushCharacter( int character ) {
			if ( character < 0 ) {
				return;
			}
			if ( this.pushedCharacter.HasValue ) {
				throw new InvalidOperationException( "input pushback buffer is already occupied" );
			}
			this.pushedCharacter = character;
		}

	}


	private sealed class SharedInputDistributor {
		private sealed class SlotBuffer {
			private readonly Queue<string> segments = new();
			private string? current;
			private int index;

			public void Enqueue( string value ) {
				this.segments.Enqueue( value );
			}

			public int CopyTo( Memory<char> destination ) {
				while ( null == this.current && 0 < this.segments.Count ) {
					this.current = this.segments.Dequeue();
					this.index = 0;
				}
				if ( null == this.current ) {
					return 0;
				}
				var count = Math.Min( destination.Length, this.current.Length - this.index );
				this.current.AsMemory( this.index, count ).CopyTo( destination );
				this.index += count;
				if ( this.current.Length <= this.index ) {
					this.current = null;
					this.index = 0;
				}
				return count;
			}
		}

		private readonly TextReader source;
		private readonly SlotBuffer[] slots;
		private readonly SemaphoreSlim gate = new( 1, 1 );
		private readonly char[] sourceBuffer = new char[BufferSize];
		private int sourceBufferIndex;
		private int sourceBufferCount;
		private int nextSlot;
		private bool endOfInput;

		public SharedInputDistributor( TextReader source, int slotCount ) {
			this.source = source;
			this.slots = Enumerable.Range( 0, slotCount )
				.Select( _ => new SlotBuffer() )
				.ToArray();
		}

		public async ValueTask<int> ReadAsync(
			int slot,
			Memory<char> destination,
			CancellationToken cancellationToken
		) {
			if ( destination.IsEmpty ) {
				return 0;
			}
			await this.gate.WaitAsync( cancellationToken ).ConfigureAwait( false );
			try {
				var written = 0;
				while ( written < destination.Length ) {
					var copied = this.slots[slot].CopyTo( destination[written..] );
					if ( 0 < copied ) {
						written += copied;
						continue;
					}
					if ( this.endOfInput ) {
						break;
					}
					var record = await this.ReadRecordAsync( cancellationToken ).ConfigureAwait( false );
					if ( null == record ) {
						this.endOfInput = true;
						break;
					}
					this.slots[this.nextSlot].Enqueue( record );
					this.nextSlot = ( this.nextSlot + 1 ) % this.slots.Length;
				}
				return written;
			} finally {
				this.gate.Release();
			}
		}

		private async ValueTask<string?> ReadRecordAsync( CancellationToken cancellationToken ) {
			var result = new StringBuilder();
			while ( true ) {
				var character = await this.ReadSourceCharacterAsync( cancellationToken ).ConfigureAwait( false );
				if ( character < 0 ) {
					return 0 == result.Length ? null : result.ToString();
				}
				result.Append( (char)character );
				if ( character is '\n' or '\f' ) {
					return result.ToString();
				}
			}
		}

		private async ValueTask<int> ReadSourceCharacterAsync( CancellationToken cancellationToken ) {
			if ( this.sourceBufferCount <= this.sourceBufferIndex ) {
				this.sourceBufferCount = await this.source.ReadAsync(
					this.sourceBuffer.AsMemory(),
					cancellationToken
				).ConfigureAwait( false );
				this.sourceBufferIndex = 0;
				if ( 0 == this.sourceBufferCount ) {
					return -1;
				}
			}
			return this.sourceBuffer[this.sourceBufferIndex++];
		}
	}

	private sealed class DistributedTextReader : TextReader {
		private readonly SharedInputDistributor distributor;
		private readonly int slot;

		public DistributedTextReader( SharedInputDistributor distributor, int slot ) {
			this.distributor = distributor;
			this.slot = slot;
		}

		public override ValueTask<int> ReadAsync(
			Memory<char> buffer,
			CancellationToken cancellationToken = default
		) {
			return this.distributor.ReadAsync( this.slot, buffer, cancellationToken );
		}
	}

	private sealed class Engine {
		private readonly PrOptions options;
		private readonly CommandContext context;
		private long generatedLineNumber;

		public Engine( PrOptions options, CommandContext context ) {
			this.options = options;
			this.context = context;
			this.generatedLineNumber = options.FirstLineNumber ?? 1;
		}

		public async Task<int> ExecuteAsync() {
			if ( this.options.Merge ) {
				return await this.ExecuteMergeAsync().ConfigureAwait( false );
			}
			var status = CommandExitCodes.Success;
			foreach ( var file in this.options.Files ) {
				this.context.CancellationToken.ThrowIfCancellationRequested();
				InputCursor? cursor = null;
				try {
					cursor = this.OpenCursor( file );
					this.generatedLineNumber = this.options.FirstLineNumber ?? 1;
					await this.PrintCursorAsync( cursor ).ConfigureAwait( false );
				} catch ( Exception exception ) when ( IsFileException( exception ) ) {
					status = CommandExitCodes.Failure;
					if ( !this.options.SuppressFileWarnings ) {
						await this.context.Diagnostics.ErrorAsync(
							string.Concat( file, ": ", exception.Message ),
							this.context.CancellationToken
						).ConfigureAwait( false );
					}
				} finally {
					if ( null != cursor ) {
						await cursor.DisposeAsync().ConfigureAwait( false );
					}
				}
			}
			return status;
		}

		private async Task<int> ExecuteMergeAsync() {
			var cursors = new List<InputCursor>();
			var status = CommandExitCodes.Success;
			var standardInputCount = this.options.Files.Count( file => "-" == file );
			var distributor = 1 < standardInputCount
				? new SharedInputDistributor( this.context.StandardInput, standardInputCount )
				: null;
			var standardInputSlot = 0;
			try {
				foreach ( var file in this.options.Files ) {
					try {
						var distributedInput = "-" == file && null != distributor
							? new DistributedTextReader( distributor, standardInputSlot++ )
							: null;
						cursors.Add( this.OpenCursor( file, distributedInput ) );
					} catch ( Exception exception ) when ( IsFileException( exception ) ) {
						status = CommandExitCodes.Failure;
						if ( !this.options.SuppressFileWarnings ) {
							await this.context.Diagnostics.ErrorAsync(
								string.Concat( file, ": ", exception.Message ),
								this.context.CancellationToken
							).ConfigureAwait( false );
						}
					}
				}
				if ( 0 == cursors.Count ) {
					return status;
				}

				var pageNumber = 1;
				var headerDate = DateTime.Now;
				while ( true ) {
					this.context.CancellationToken.ThrowIfCancellationRequested();
					var chunks = new PageChunk?[cursors.Count];
					var any = false;
					for ( var index = 0; index < cursors.Count; index++ ) {
						chunks[index] = await cursors[index].ReadPageAsync(
							this.options.BodyLineCapacity,
							this.context.CancellationToken
						).ConfigureAwait( false );
						any |= null != chunks[index];
					}
					if ( !any ) {
						break;
					}
					if ( this.options.Pages.IsPastLast( pageNumber ) ) {
						break;
					}
					if ( this.options.Pages.Includes( pageNumber ) ) {
						var rows = this.FormatMergeRows( chunks );
						await this.WritePageAsync(
							rows,
							pageNumber,
							this.options.Header ?? string.Empty,
							headerDate,
							chunks.Any( chunk => null != chunk && chunk.EndedByFormFeed )
						).ConfigureAwait( false );
					} else if ( !this.options.FirstLineNumber.HasValue ) {
						this.generatedLineNumber += chunks.Max( chunk => chunk?.Lines.Count ?? 0 );
					}
					pageNumber++;
				}
				return status;
			} finally {
				foreach ( var cursor in cursors ) {
					await cursor.DisposeAsync().ConfigureAwait( false );
				}
			}
		}

		private InputCursor OpenCursor( string path, TextReader? alternateStandardInput = null ) {
			if ( "-" == path ) {
				return new InputCursor(
					string.Empty,
					alternateStandardInput ?? this.context.StandardInput,
					ownsReader: null != alternateStandardInput,
					this.options.OmitPagination,
					DateTime.Now
				);
			}
			var headerDate = File.GetLastWriteTime( path );
			var stream = new FileStream(
				path,
				FileMode.Open,
				FileAccess.Read,
				FileShare.ReadWrite | FileShare.Delete,
				BufferSize,
				FileOptions.Asynchronous | FileOptions.SequentialScan
			);
			var reader = new StreamReader(
				stream,
				Encoding.UTF8,
				detectEncodingFromByteOrderMarks: true,
				bufferSize: BufferSize,
				leaveOpen: false
			);
			return new InputCursor(
				path,
				reader,
				ownsReader: true,
				this.options.OmitPagination,
				headerDate
			);
		}

		private async Task PrintCursorAsync( InputCursor cursor ) {
			var pageNumber = 1;
			var maximumInputLines = checked( this.options.BodyLineCapacity * this.options.Columns );
			while ( true ) {
				var pageStartLineNumber = cursor.NextInputLineNumber;
				var chunk = await cursor.ReadPageAsync(
					maximumInputLines,
					this.context.CancellationToken
				).ConfigureAwait( false );
				if ( null == chunk ) {
					break;
				}
				if ( this.options.Pages.IsPastLast( pageNumber ) ) {
					break;
				}
				if ( this.options.Pages.Includes( pageNumber ) ) {
					if ( this.options.FirstLineNumber.HasValue && pageNumber == this.options.Pages.First ) {
						this.generatedLineNumber = this.options.FirstLineNumber.Value;
					} else if ( !this.options.FirstLineNumber.HasValue ) {
						this.generatedLineNumber = pageStartLineNumber;
					}
					var rows = this.FormatRows( chunk.Lines );
					await this.WritePageAsync(
						rows,
						pageNumber,
						this.options.Header ?? cursor.DisplayName,
						cursor.HeaderDate,
						chunk.EndedByFormFeed
					).ConfigureAwait( false );
				}
				pageNumber++;
			}
		}

		private List<string> FormatRows( IReadOnlyList<string> lines ) {
			if ( 1 == this.options.Columns ) {
				var rows = new List<string>( lines.Count );
				foreach ( var line in lines ) {
					rows.Add( this.ComposeSingleLine( line, this.NextNumber() ) );
				}
				return rows;
			}

			var result = new List<string>();
			if ( this.options.Across ) {
				for ( var first = 0; first < lines.Count; first += this.options.Columns ) {
					var cells = new List<string>();
					for ( var column = 0; column < this.options.Columns && first + column < lines.Count; column++ ) {
						cells.Add( this.FormatCell( lines[first + column], this.NextNumber() ) );
					}
					result.Add( this.ComposeColumns( cells ) );
				}
				return result;
			}

			var rowCount = ( lines.Count + this.options.Columns - 1 ) / this.options.Columns;
			var shortColumnCount = this.options.Columns * rowCount - lines.Count;
			var fullColumnCount = this.options.Columns - shortColumnCount;
			var columnStarts = new int[this.options.Columns];
			var columnLengths = new int[this.options.Columns];
			var nextStart = 0;
			for ( var column = 0; column < this.options.Columns; column++ ) {
				columnStarts[column] = nextStart;
				columnLengths[column] = column < fullColumnCount ? rowCount : rowCount - 1;
				nextStart += columnLengths[column];
			}
			var numberByIndex = new long[lines.Count];
			for ( var index = 0; index < lines.Count; index++ ) {
				numberByIndex[index] = this.NextNumber();
			}
			for ( var row = 0; row < rowCount; row++ ) {
				var cells = new List<string>();
				for ( var column = 0; column < this.options.Columns; column++ ) {
					if ( columnLengths[column] <= row ) {
						break;
					}
					var index = columnStarts[column] + row;
					cells.Add( this.FormatCell( lines[index], numberByIndex[index] ) );
				}
				result.Add( this.ComposeColumns( cells ) );
			}
			return result;
		}

		private List<string> FormatMergeRows( IReadOnlyList<PageChunk?> chunks ) {
			var rowCount = chunks.Max( chunk => chunk?.Lines.Count ?? 0 );
			if ( 0 == rowCount && chunks.Any( chunk => null != chunk ) ) {
				return new List<string>();
			}
			var result = new List<string>( rowCount );
			for ( var row = 0; row < rowCount; row++ ) {
				var cells = new List<string>();
				for ( var column = 0; column < chunks.Count; column++ ) {
					var line = null != chunks[column] && row < chunks[column]!.Lines.Count
						? chunks[column]!.Lines[row]
						: string.Empty;
					cells.Add( this.FormatCell( line, null ) );
				}
				var composed = this.ComposeColumns( cells );
				if ( this.options.NumberLines ) {
					composed = string.Concat( this.FormatNumberPrefix( this.NextNumber(), int.MaxValue ), composed );
				}
				result.Add( composed );
			}
			return result;
		}

		private long NextNumber() {
			return this.generatedLineNumber++;
		}

		private string ComposeSingleLine( string source, long number ) {
			var transformed = this.TransformInput( source );
			if ( this.options.PageWidthSpecified && !this.options.JoinLines ) {
				transformed = Truncate( transformed, this.options.PageWidth );
			}
			var prefix = this.options.NumberLines
				? this.FormatNumberPrefix( number, int.MaxValue )
				: string.Empty;
			return this.ApplyMarginAndTabs( string.Concat( prefix, transformed ) );
		}

		private string FormatCell( string source, long? number ) {
			var transformed = this.TransformInput( source );
			if ( this.options.NumberLines && number.HasValue && !this.options.Merge ) {
				var columnWidth = this.GetColumnWidth();
				var prefix = this.FormatNumberPrefix( number.Value, columnWidth );
				transformed = string.Concat( prefix, transformed );
			}
			return transformed;
		}

		private string ComposeColumns( IReadOnlyList<string> cells ) {
			if ( 0 == cells.Count ) {
				return this.ApplyMarginAndTabs( string.Empty );
			}
			var separator = this.GetSeparator();
			var aligned = !this.options.JoinLines
				&& (
					!this.options.SeparatorOptionSpecified
					|| this.options.WidthSpecified
					|| this.options.PageWidthSpecified
				);
			var columnWidth = this.GetColumnWidth();
			var builder = new StringBuilder();
			for ( var index = 0; index < cells.Count; index++ ) {
				var cell = cells[index];
				if ( aligned ) {
					cell = Truncate( cell, columnWidth );
					if ( index + 1 < cells.Count ) {
						cell = cell.PadRight( columnWidth );
					}
				}
				builder.Append( cell );
				if ( index + 1 < cells.Count ) {
					builder.Append( separator );
				}
			}
			return this.ApplyMarginAndTabs( builder.ToString() );
		}

		private int GetColumnWidth() {
			var separatorLength = this.GetSeparator().Length;
			var available = this.options.PageWidth - separatorLength * ( this.options.Columns - 1 );
			return Math.Max( 1, available / Math.Max( 1, this.options.Columns ) );
		}

		private string GetSeparator() {
			if ( null != this.options.Separator ) {
				return this.options.Separator;
			}
			if ( this.options.JoinLines ) {
				return "\t";
			}
			return " ";
		}

		private string TransformInput( string source ) {
			var value = source;
			if ( null != this.options.ExpandTabs || 1 < this.options.Columns || this.options.Merge ) {
				value = ExpandTabs( value, this.options.ExpandTabs ?? new TabSpecification() );
			}
			if ( this.options.ShowNonprinting ) {
				value = RenderNonprinting( value, hatNotation: false );
			} else if ( this.options.ShowControlCharacters ) {
				value = RenderNonprinting( value, hatNotation: true );
			}
			return value;
		}

		private string FormatNumberPrefix( long number, int maximumWidth ) {
			var digits = number.ToString( CultureInfo.InvariantCulture );
			var padded = digits.PadLeft( this.options.NumberDigits );
			var prefix = string.Concat( padded, this.options.NumberSeparator );
			if ( maximumWidth != int.MaxValue && maximumWidth <= prefix.Length ) {
				var keep = Math.Max( 0, maximumWidth - 1 );
				prefix = keep <= 0 ? string.Empty : prefix[^keep..];
			}
			return prefix;
		}

		private string ApplyMarginAndTabs( string value ) {
			var result = 0 == this.options.Margin
				? value
				: string.Concat( new string( ' ', this.options.Margin ), value );
			var tabs = this.options.OutputTabs;
			if ( null == tabs && ( 1 < this.options.Columns || this.options.Merge ) ) {
				tabs = new TabSpecification();
			}
			return null == tabs ? result : CompressSpaces( result, tabs );
		}

		private async Task WritePageAsync(
			IReadOnlyList<string> rows,
			int pageNumber,
			string title,
			DateTime date,
			bool endedByInputFormFeed
		) {
			var cancellationToken = this.context.CancellationToken;
			var writtenPhysicalLines = 0;
			if ( this.options.Paginated ) {
				await this.WriteLineAsync( string.Empty ).ConfigureAwait( false );
				await this.WriteLineAsync( string.Empty ).ConfigureAwait( false );
				await this.WriteLineAsync(
					string.Concat(
						new string( ' ', this.options.Margin ),
						BuildHeader( this.FormatDate( date ), title, pageNumber, this.options.PageWidth )
					)
				).ConfigureAwait( false );
				await this.WriteLineAsync( string.Empty ).ConfigureAwait( false );
				await this.WriteLineAsync( string.Empty ).ConfigureAwait( false );
				writtenPhysicalLines = 5;
			}

			foreach ( var row in rows ) {
				await this.WriteLineAsync( row ).ConfigureAwait( false );
				writtenPhysicalLines++;
				if ( this.options.DoubleSpace ) {
					await this.WriteLineAsync( string.Empty ).ConfigureAwait( false );
					writtenPhysicalLines++;
				}
			}

			if ( this.options.Paginated ) {
				if ( this.options.FormFeed ) {
					await this.context.StandardOutput.WriteAsync(
						"\f".AsMemory(),
						cancellationToken
					).ConfigureAwait( false );
				} else {
					while ( writtenPhysicalLines < this.options.PageLength ) {
						await this.WriteLineAsync( string.Empty ).ConfigureAwait( false );
						writtenPhysicalLines++;
					}
				}
			} else if ( this.options.OmitHeader && !this.options.OmitPagination && endedByInputFormFeed ) {
				await this.context.StandardOutput.WriteAsync(
					"\f".AsMemory(),
					cancellationToken
				).ConfigureAwait( false );
			}
		}

		private Task WriteLineAsync( string value ) {
			return this.context.StandardOutput.WriteLineAsync(
				value.AsMemory(),
				this.context.CancellationToken
			);
		}

		private string FormatDate( DateTime value ) {
			return null == this.options.DateFormat
				? value.ToString( "yyyy-MM-dd HH:mm", CultureInfo.CurrentCulture )
				: GnuDateFormatter.Format(
					new DateTimeOffset( value ),
					this.options.DateFormat,
					TimeZoneInfo.Local,
					CultureInfo.CurrentCulture
				);
		}
	}

	/// <summary>Runs <c>pr</c> synchronously.</summary>
	/// <param name="args">The command-line arguments.</param>
	/// <param name="stdin">Optional standard input.</param>
	/// <param name="stdout">Optional standard output.</param>
	/// <param name="stderr">Optional standard error.</param>
	/// <returns>The process exit status.</returns>
	public static int Run(
		string[] args,
		TextReader? stdin = null,
		TextWriter? stdout = null,
		TextWriter? stderr = null
	) {
		return RunAsync( args, stdin, stdout, stderr ).GetAwaiter().GetResult();
	}

	/// <summary>Runs <c>pr</c> asynchronously using text streams.</summary>
	/// <param name="args">The command-line arguments.</param>
	/// <param name="standardInput">Optional standard input.</param>
	/// <param name="standardOutput">Optional standard output.</param>
	/// <param name="standardError">Optional standard error.</param>
	/// <param name="cancellationToken">Cancellation token for the operation.</param>
	/// <returns>A task whose result is the process exit status.</returns>
	public static Task<int> RunAsync(
		string[] args,
		TextReader? standardInput = null,
		TextWriter? standardOutput = null,
		TextWriter? standardError = null,
		CancellationToken cancellationToken = default
	) {
		standardInput ??= Console.In;
		standardOutput ??= Console.Out;
		standardError ??= Console.Error;
		return RunAsync(
			args,
			new CommandContext(
				"pr",
				standardInput,
				standardOutput,
				standardError,
				null,
				null,
				null,
				cancellationToken
			)
		);
	}

	/// <summary>Runs <c>pr</c> asynchronously against an injected command context.</summary>
	/// <param name="args">The command-line arguments.</param>
	/// <param name="context">The command context.</param>
	/// <returns>A task whose result is the process exit status.</returns>
	public static async Task<int> RunAsync( string[] args, CommandContext context ) {
		ArgumentNullException.ThrowIfNull( args );
		ArgumentNullException.ThrowIfNull( context );
		try {
			var parsed = CreateParser().Parse( args );
			if ( !parsed.IsSuccess ) {
				await context.StandardError.WriteLineAsync(
					OptionDiagnosticFormatter.Format( context.ProgramName, parsed.Errors[0] ).AsMemory(),
					context.CancellationToken
				).ConfigureAwait( false );
				return CommandExitCodes.UsageError;
			}
			if ( parsed.HasOption( "help" ) ) {
				await WriteHelpAsync( context.StandardOutput, context.CancellationToken ).ConfigureAwait( false );
				return CommandExitCodes.Success;
			}
			if ( parsed.HasOption( "version" ) ) {
				await context.StandardOutput.WriteLineAsync(
					VersionText.AsMemory(),
					context.CancellationToken
				).ConfigureAwait( false );
				return CommandExitCodes.Success;
			}
			var options = CreateOptions( parsed );
			var engine = new Engine( options, context );
			return await engine.ExecuteAsync().ConfigureAwait( false );
		} catch ( OperationCanceledException ) {
			return CommandExitCodes.Canceled;
		} catch ( Exception exception ) when (
			exception is PrUsageException
			or IOException
			or UnauthorizedAccessException
			or ArgumentException
			or FormatException
			or OverflowException
			or InvalidOperationException
			or System.Security.SecurityException
		) {
			try {
				await context.Diagnostics.ErrorAsync(
					exception.Message,
					CancellationToken.None
				).ConfigureAwait( false );
			} catch {
				// A diagnostic failure must not replace the command status.
			}
			return exception is PrUsageException
				? CommandExitCodes.UsageError
				: CommandExitCodes.Failure;
		}
	}

	private static OptionParser CreateParser() {
		var settings = new OptionParserSettings {
			AllowLongOptionAbbreviations = true,
			Ordering = OptionOrdering.Permute
		};
		settings.TokenRewriteRules.Add( new OptionTokenRewriteRule( RewriteLegacyColumnToken ) );
		settings.TokenRewriteRules.Add( new OptionTokenRewriteRule( RewriteLegacyPageToken ) );
		return new OptionParser(
			new[] {
				new OptionDefinition( "pages", null, new[] { "pages" }, OptionValueArity.Required ),
				new OptionDefinition( "columns", null, new[] { "columns" }, OptionValueArity.Required ),
				new OptionDefinition( "across", 'a', new[] { "across" } ),
				new OptionDefinition( "show-control", 'c', new[] { "show-control-chars" } ),
				new OptionDefinition( "double-space", 'd', new[] { "double-space" } ),
				new OptionDefinition( "date-format", 'D', new[] { "date-format" }, OptionValueArity.Required ),
				new OptionDefinition( "expand-tabs", 'e', new[] { "expand-tabs" }, OptionValueArity.Optional ),
				new OptionDefinition( "form-feed-lower", 'f', new[] { "form-feed" } ),
				new OptionDefinition( "form-feed-upper", 'F' ),
				new OptionDefinition( "header", 'h', new[] { "header" }, OptionValueArity.Required ),
				new OptionDefinition( "output-tabs", 'i', new[] { "output-tabs" }, OptionValueArity.Optional ),
				new OptionDefinition( "join-lines", 'J', new[] { "join-lines" } ),
				new OptionDefinition( "length", 'l', new[] { "length" }, OptionValueArity.Required ),
				new OptionDefinition( "merge", 'm', new[] { "merge" } ),
				new OptionDefinition( "number-lines", 'n', new[] { "number-lines" }, OptionValueArity.Optional ),
				new OptionDefinition( "first-line-number", 'N', new[] { "first-line-number" }, OptionValueArity.Required ),
				new OptionDefinition( "indent", 'o', new[] { "indent" }, OptionValueArity.Required ),
				new OptionDefinition( "no-file-warnings", 'r', new[] { "no-file-warnings" } ),
				new OptionDefinition( "separator", 's', new[] { "separator" }, OptionValueArity.Optional ),
				new OptionDefinition( "separator-string", 'S', new[] { "sep-string" }, OptionValueArity.Optional ),
				new OptionDefinition( "omit-header", 't', new[] { "omit-header" } ),
				new OptionDefinition( "omit-pagination", 'T', new[] { "omit-pagination" } ),
				new OptionDefinition( "show-nonprinting", 'v', new[] { "show-nonprinting" } ),
				new OptionDefinition( "width", 'w', new[] { "width" }, OptionValueArity.Required ),
				new OptionDefinition( "page-width", 'W', new[] { "page-width" }, OptionValueArity.Required ),
				new OptionDefinition( "help", null, new[] { "help" } ),
				new OptionDefinition( "version", null, new[] { "version" } )
			},
			settings
		);
	}

	private static IReadOnlyList<string>? RewriteLegacyColumnToken( string token ) {
		if ( token.Length < 2 || '-' != token[0] || '-' == token[1] ) {
			return null;
		}
		var output = new List<string>();
		var flags = new StringBuilder();
		var foundColumnCount = false;
		for ( var index = 1; index < token.Length; ) {
			var character = token[index];
			if ( char.IsAsciiDigit( character ) ) {
				FlushLegacyFlags( flags, output );
				var start = index;
				while ( index < token.Length && char.IsAsciiDigit( token[index] ) ) {
					index++;
				}
				output.Add( string.Concat( "--columns=", token[start..index] ) );
				foundColumnCount = true;
				continue;
			}
			if ( "DehilNnosSwW".Contains( character ) ) {
				FlushLegacyFlags( flags, output );
				output.Add( string.Concat( "-", token[index..] ) );
				return foundColumnCount ? output : null;
			}
			flags.Append( character );
			index++;
		}
		FlushLegacyFlags( flags, output );
		return foundColumnCount ? output : null;
	}

	private static void FlushLegacyFlags( StringBuilder flags, ICollection<string> output ) {
		if ( 0 == flags.Length ) {
			return;
		}
		output.Add( string.Concat( "-", flags.ToString() ) );
		flags.Clear();
	}

	private static IReadOnlyList<string>? RewriteLegacyPageToken( string token ) {
		if ( token.Length < 2 || '+' != token[0] ) {
			return null;
		}
		var value = token.AsSpan( 1 );
		var colon = value.IndexOf( ':' );
		var first = colon < 0 ? value : value[..colon];
		var last = colon < 0 ? ReadOnlySpan<char>.Empty : value[( colon + 1 )..];
		if ( first.IsEmpty || !first.ToString().All( char.IsAsciiDigit ) ) {
			return null;
		}
		if ( 0 <= colon && ( last.IsEmpty || !last.ToString().All( char.IsAsciiDigit ) ) ) {
			return null;
		}
		return new[] { string.Concat( "--pages=", value.ToString() ) };
	}

	private static PrOptions CreateOptions( OptionParseResult parsed ) {
		var options = new PrOptions();
		var columns = parsed.GetLastValue( "columns" );
		if ( null != columns ) {
			options.Columns = ParsePositiveInt( columns, "number of columns" );
		}
		options.Across = parsed.HasOption( "across" );
		options.ShowControlCharacters = parsed.HasOption( "show-control" );
		options.ShowNonprinting = parsed.HasOption( "show-nonprinting" );
		options.DoubleSpace = parsed.HasOption( "double-space" );
		options.DateFormat = parsed.GetLastValue( "date-format" );
		if ( parsed.HasOption( "expand-tabs" ) ) {
			options.ExpandTabs = ParseTabSpecification( parsed.GetLastValue( "expand-tabs" ) );
		}
		options.FormFeed = parsed.HasOption( "form-feed-lower" ) || parsed.HasOption( "form-feed-upper" );
		options.Header = parsed.GetLastValue( "header" );
		if ( parsed.HasOption( "output-tabs" ) ) {
			options.OutputTabs = ParseTabSpecification( parsed.GetLastValue( "output-tabs" ) );
		}
		options.JoinLines = parsed.HasOption( "join-lines" );
		var length = parsed.GetLastValue( "length" );
		if ( null != length ) {
			options.PageLength = ParsePositiveInt( length, "page length" );
		}
		options.Merge = parsed.HasOption( "merge" );
		if ( parsed.HasOption( "number-lines" ) ) {
			options.NumberLines = true;
			ParseNumberSpecification(
				parsed.GetLastValue( "number-lines" ),
				out var separator,
				out var digits
			);
			options.NumberSeparator = separator;
			options.NumberDigits = digits;
		}
		var firstLine = parsed.GetLastValue( "first-line-number" );
		if ( null != firstLine ) {
			options.FirstLineNumber = ParseLong( firstLine, "first line number" );
		}
		var margin = parsed.GetLastValue( "indent" );
		if ( null != margin ) {
			options.Margin = ParseNonnegativeInt( margin, "indent" );
		}
		options.SuppressFileWarnings = parsed.HasOption( "no-file-warnings" );
		options.SeparatorOptionSpecified = parsed.HasOption( "separator" );
		options.SeparatorStringOptionSpecified = parsed.HasOption( "separator-string" );
		var width = parsed.GetLastValue( "width" );
		if ( null != width ) {
			options.WidthSpecified = true;
			options.PageWidth = ParsePositiveInt( width, "page width" );
		}
		var pageWidth = parsed.GetLastValue( "page-width" );
		if ( null != pageWidth ) {
			options.PageWidthSpecified = true;
			options.PageWidth = ParsePositiveInt( pageWidth, "page width" );
		}
		if ( options.SeparatorStringOptionSpecified ) {
			options.Separator = parsed.GetLastValue( "separator-string" ) ?? string.Empty;
		} else if ( options.SeparatorOptionSpecified ) {
			var text = parsed.GetLastValue( "separator" );
			if ( string.IsNullOrEmpty( text ) ) {
				options.Separator = options.WidthSpecified || options.PageWidthSpecified ? string.Empty : "\t";
			} else {
				options.Separator = text;
			}
		}
		options.OmitHeader = parsed.HasOption( "omit-header" );
		options.OmitPagination = parsed.HasOption( "omit-pagination" );
		if ( options.PageLength <= HeaderAndTrailerLines ) {
			options.OmitHeader = true;
		}
		var pages = parsed.GetLastValue( "pages" );
		if ( null != pages ) {
			options.Pages = ParsePageSelection( pages );
		}
		options.Files.AddRange( parsed.Operands );
		if ( 0 == options.Files.Count ) {
			options.Files.Add( "-" );
		}
		if ( options.Merge && null != columns ) {
			throw new PrUsageException( "cannot specify number of columns when printing in parallel" );
		}
		if ( options.Merge ) {
			options.Columns = options.Files.Count;
		}
		if ( options.Merge && options.Across ) {
			throw new PrUsageException( "cannot specify both --across and --merge" );
		}
		if ( 1 == options.Columns ) {
			options.Across = false;
		} else {
			var separatorLength = options.Separator?.Length ?? 1;
			var minimumWidth = (long)options.Columns
				+ (long)separatorLength * ( options.Columns - 1 );
			if ( options.PageWidth < minimumWidth ) {
				throw new PrUsageException( "page width too narrow" );
			}
		}
		return options;
	}

	private static PageSelection ParsePageSelection( string value ) {
		var pieces = value.Split( ':', 2 );
		var first = ParsePositiveInt( pieces[0], "first page" );
		int? last = null;
		if ( 2 == pieces.Length ) {
			last = ParsePositiveInt( pieces[1], "last page" );
			if ( last.Value < first ) {
				throw new PrUsageException( "the first page cannot exceed the last page" );
			}
		}
		return new PageSelection { First = first, Last = last };
	}

	private static TabSpecification ParseTabSpecification( string? value ) {
		if ( string.IsNullOrEmpty( value ) ) {
			return new TabSpecification();
		}
		if ( value.All( char.IsAsciiDigit ) ) {
			return new TabSpecification { Width = ParsePositiveInt( value, "tab width" ) };
		}
		var character = value[0];
		var width = 1 == value.Length
			? DefaultTabWidth
			: ParsePositiveInt( value[1..], "tab width" );
		return new TabSpecification { Character = character, Width = width };
	}

	private static void ParseNumberSpecification(
		string? value,
		out char separator,
		out int digits
	) {
		separator = '\t';
		digits = 5;
		if ( string.IsNullOrEmpty( value ) ) {
			return;
		}
		if ( value.All( char.IsAsciiDigit ) ) {
			digits = ParsePositiveInt( value, "number width" );
			return;
		}
		separator = value[0];
		if ( 1 < value.Length ) {
			digits = ParsePositiveInt( value[1..], "number width" );
		}
	}

	private static int ParsePositiveInt( string value, string description ) {
		if (
			string.IsNullOrEmpty( value )
			|| value.Any( character => !char.IsAsciiDigit( character ) )
			|| !int.TryParse( value, NumberStyles.None, CultureInfo.InvariantCulture, out var result )
			|| result <= 0
		) {
			throw new PrUsageException( string.Concat( "invalid ", description, ": '", value, "'" ) );
		}
		return result;
	}

	private static int ParseNonnegativeInt( string value, string description ) {
		if (
			string.IsNullOrEmpty( value )
			|| value.Any( character => !char.IsAsciiDigit( character ) )
			|| !int.TryParse( value, NumberStyles.None, CultureInfo.InvariantCulture, out var result )
		) {
			throw new PrUsageException( string.Concat( "invalid ", description, ": '", value, "'" ) );
		}
		return result;
	}

	private static long ParseLong( string value, string description ) {
		if (
			string.IsNullOrWhiteSpace( value )
			|| !long.TryParse( value, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var result )
		) {
			throw new PrUsageException( string.Concat( "invalid ", description, ": '", value, "'" ) );
		}
		return result;
	}

	private static string ExpandTabs( string value, TabSpecification specification ) {
		var builder = new StringBuilder();
		var column = 0;
		foreach ( var character in value ) {
			if ( character == specification.Character ) {
				var count = specification.Width - column % specification.Width;
				builder.Append( ' ', count );
				column += count;
			} else {
				builder.Append( character );
				column++;
			}
		}
		return builder.ToString();
	}

	private static string CompressSpaces( string value, TabSpecification specification ) {
		var builder = new StringBuilder();
		var column = 0;
		var index = 0;
		while ( index < value.Length ) {
			if ( ' ' != value[index] ) {
				builder.Append( value[index] );
				column++;
				index++;
				continue;
			}
			var start = index;
			while ( index < value.Length && ' ' == value[index] ) {
				index++;
			}
			var remaining = index - start;
			while ( 1 < remaining ) {
				var nextStop = column + ( specification.Width - column % specification.Width );
				var distance = nextStop - column;
				if ( remaining < distance || distance <= 1 ) {
					break;
				}
				builder.Append( specification.Character );
				column = nextStop;
				remaining -= distance;
			}
			builder.Append( ' ', remaining );
			column += remaining;
		}
		return builder.ToString();
	}

	private static string RenderNonprinting( string value, bool hatNotation ) {
		var builder = new StringBuilder();
		foreach ( var character in value ) {
			if ( character < ' ' || '\u007f' == character ) {
				if ( hatNotation ) {
					builder.Append( '^' );
					builder.Append( '\u007f' == character ? '?' : (char)( character + 64 ) );
				} else {
					builder.Append( '\\' );
					builder.Append( Convert.ToString( character, 8 )!.PadLeft( 3, '0' ) );
				}
			} else if ( char.IsControl( character ) ) {
				builder.Append( '\\' );
				builder.Append( Convert.ToString( character, 8 )!.PadLeft( 3, '0' ) );
			} else {
				builder.Append( character );
			}
		}
		return builder.ToString();
	}

	private static string Truncate( string value, int width ) {
		return value.Length <= width ? value : value[..width];
	}

	private static string BuildHeader( string date, string title, int pageNumber, int width ) {
		var page = string.Concat( "Page ", pageNumber.ToString( CultureInfo.InvariantCulture ) );
		var minimum = date.Length + title.Length + page.Length + 2;
		if ( width < minimum ) {
			return string.Join( " ", new[] { date, title, page }.Where( value => 0 < value.Length ) );
		}
		var buffer = new string( ' ', width ).ToCharArray();
		date.CopyTo( 0, buffer, 0, date.Length );
		page.CopyTo( 0, buffer, width - page.Length, page.Length );
		var titleStart = Math.Max( date.Length + 1, ( width - title.Length ) / 2 );
		titleStart = Math.Min( titleStart, width - page.Length - title.Length - 1 );
		if ( 0 < title.Length ) {
			title.CopyTo( 0, buffer, titleStart, title.Length );
		}
		return new string( buffer ).TrimEnd();
	}

	private static bool IsFileException( Exception exception ) {
		return exception is IOException
			or UnauthorizedAccessException
			or ArgumentException
			or NotSupportedException
			or System.Security.SecurityException;
	}

	private static async Task WriteHelpAsync(
		TextWriter output,
		CancellationToken cancellationToken
	) {
		const string help = """
Usage: pr [OPTION]... [FILE]...
Paginate or columnate FILE(s) for printing.

  +FIRST[:LAST], --pages=FIRST[:LAST]  select pages
  -COLUMN, --columns=COLUMN            print COLUMN columns down
  -a, --across                         print columns across
  -c, --show-control-chars             use hat and octal notation
  -d, --double-space                   double-space output
  -D, --date-format=FORMAT             set header date format
  -e[CHAR[WIDTH]], --expand-tabs[=...] expand input tabs
  -F, -f, --form-feed                  separate pages with form feeds
  -h, --header=HEADER                  replace the filename in the header
  -i[CHAR[WIDTH]], --output-tabs[=...] replace spaces with output tabs
  -J, --join-lines                     do not align or truncate columns
  -l, --length=PAGE_LENGTH             set page length
  -m, --merge                          print files in parallel
  -n[SEP[DIGITS]], --number-lines[=...] number lines
  -N, --first-line-number=NUMBER       set first printed line number
  -o, --indent=MARGIN                  indent each output line
  -r, --no-file-warnings               suppress open warnings
  -s[CHAR], --separator[=CHAR]         set one-character separator
  -S[STRING], --sep-string[=STRING]    set separator string
  -t, --omit-header                    omit headers and trailers
  -T, --omit-pagination                omit pagination and input form feeds
  -v, --show-nonprinting               use octal notation
  -w, --width=PAGE_WIDTH               set multi-column width
  -W, --page-width=PAGE_WIDTH          always set and enforce page width
      --help                            display this help and exit
      --version                         output version information and exit
""";
		await output.WriteAsync( help.AsMemory(), cancellationToken ).ConfigureAwait( false );
	}
}
