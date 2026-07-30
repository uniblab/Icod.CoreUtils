namespace Icod.CoreUtils.Cut;

using System.Buffers;
using Icod.CoreUtils.Shared.Diagnostics;
using Icod.CoreUtils.Shared.IO;
using Icod.CoreUtils.Shared.Records;
using Icod.CoreUtils.Shared.Text;

/// <summary>Performs byte-preserving positional extraction for <c>cut</c>.</summary>
internal sealed class CutProcessor {
	private readonly CutOptions myOptions;

	/// <summary>Initializes a processor with validated options.</summary>
	/// <param name="options">The validated options.</param>
	internal CutProcessor( CutOptions options ) {
		this.myOptions = options ?? throw new ArgumentNullException( nameof( options ) );
	}

	/// <summary>Processes all operands in order.</summary>
	/// <param name="context">The command context.</param>
	/// <param name="output">The byte-preserving output stream.</param>
	/// <returns><see langword="true"/> when every operand was processed successfully.</returns>
	internal async Task<bool> ProcessAsync( CommandContext context, Stream output ) {
		ArgumentNullException.ThrowIfNull( context );
		ArgumentNullException.ThrowIfNull( output );
		var success = true;
		var operands = 0 == this.myOptions.Operands.Count ? new[] { "-" } : this.myOptions.Operands;
		foreach ( var value in operands ) {
			var operand = InputOperand.Create( value );
			InputSource source;
			try {
				source = InputSource.OpenBinary( operand, context );
			} catch ( Exception exception ) when ( IsInputException( exception ) ) {
				success = false;
				await WriteInputErrorAsync( context, operand, exception ).ConfigureAwait( false );
				continue;
			}
			try {
				using var input = new CutInputStream( source.BinaryStream!, operand.DisplayName );
				try {
					await this.ProcessSourceAsync( input, output, context.CancellationToken ).ConfigureAwait( false );
				} catch ( CutInputException exception ) {
					success = false;
					await WriteInputErrorAsync( context, exception.DisplayName, exception.InnerException ?? exception ).ConfigureAwait( false );
				}
			} finally {
				try {
					await source.DisposeAsync().ConfigureAwait( false );
				} catch ( Exception exception ) when ( IsInputException( exception ) ) {
					success = false;
					await WriteInputErrorAsync( context, operand, exception ).ConfigureAwait( false );
				}
			}
		}
		return success;
	}

	private Task ProcessSourceAsync( Stream input, Stream output, CancellationToken cancellationToken ) {
		return this.myOptions.Mode switch {
			CutMode.Bytes when this.myOptions.NoPartialCharacters
				&& TextDecodingMode.Utf8 == this.myOptions.LocaleProvider.DecodingMode
				=> this.ProcessUnitsAsync( input, output, countBytesWithoutSplitting: true, cancellationToken: cancellationToken ),
			CutMode.Bytes => this.ProcessBytesAsync( input, output, cancellationToken ),
			CutMode.Characters => this.ProcessUnitsAsync( input, output, countBytesWithoutSplitting: false, cancellationToken: cancellationToken ),
			CutMode.Fields => new CutFieldProcessor( this.myOptions ).ProcessAsync( input, output, cancellationToken ),
			_ => throw new InvalidOperationException( "Unsupported cut mode." )
		};
	}

	private async Task ProcessBytesAsync( Stream input, Stream output, CancellationToken cancellationToken ) {
		using var reader = new DelimitedByteRecordSegmentReader( input, this.myOptions.RecordSeparator );
		var cursor = this.myOptions.Ranges.CreateCursor();
		ulong position = 0;
		var wroteSelection = false;
		while ( await reader.ReadAsync( cancellationToken ).ConfigureAwait( false ) is { } segment ) {
			var builder = new ArrayBufferWriter<byte>( Math.Max( 1, segment.Data.Length ) );
			foreach ( var value in segment.Data.Span ) {
				position = checked(position + 1);
				var match = cursor.Match( position );
				if ( !match.IsSelected ) {
					continue;
				}
				if ( wroteSelection && match.IsRangeStart && this.myOptions.OutputDelimiter is { } delimiter ) {
					builder.Write( delimiter );
				}
				builder.GetSpan( 1 )[0] = value;
				builder.Advance( 1 );
				wroteSelection = true;
			}
			if ( 0 < builder.WrittenCount ) {
				await output.WriteAsync( builder.WrittenMemory, cancellationToken ).ConfigureAwait( false );
			}
			if ( segment.EndsRecord ) {
				if ( segment.IsTerminated ) {
					await WriteByteAsync( output, this.myOptions.RecordSeparator, cancellationToken ).ConfigureAwait( false );
				} else {
					await output.WriteAsync( this.myOptions.GeneratedRecordSeparator, cancellationToken ).ConfigureAwait( false );
				}
				position = 0;
				wroteSelection = false;
				cursor.Reset();
			}
		}
	}

	private async Task ProcessUnitsAsync(
		Stream input,
		Stream output,
		bool countBytesWithoutSplitting,
		CancellationToken cancellationToken
	) {
		var reader = new TextUnitReader(
			input,
			this.myOptions.LocaleProvider.DecodingMode,
			InvalidEncodingPolicy.PreserveBytes
		);
		var cursor = this.myOptions.Ranges.CreateCursor();
		ulong position = 0;
		var recordStarted = false;
		var wroteSelection = false;
		var unitBytes = new byte[4];
		while ( await reader.ReadAsync( cancellationToken ).ConfigureAwait( false ) is TextUnit unit ) {
			if ( IsSingleByte( unit, this.myOptions.RecordSeparator ) ) {
				await WriteByteAsync( output, this.myOptions.RecordSeparator, cancellationToken ).ConfigureAwait( false );
				position = 0;
				recordStarted = false;
				wroteSelection = false;
				cursor.Reset();
				continue;
			}
			recordStarted = true;
			if ( countBytesWithoutSplitting ) {
				var selected = false;
				var unselectedAfterSelection = false;
				var startsRange = false;
				for ( var index = 0; index < unit.ByteCount; index++ ) {
					position = checked(position + 1);
					var match = cursor.Match( position );
					if ( match.IsSelected ) {
						if ( !selected ) {
							startsRange = match.IsRangeStart;
						}
						selected = true;
					} else if ( selected ) {
						unselectedAfterSelection = true;
					}
				}
				if ( selected && !unselectedAfterSelection ) {
					if ( wroteSelection && startsRange && this.myOptions.OutputDelimiter is { } delimiter ) {
						await output.WriteAsync( delimiter, cancellationToken ).ConfigureAwait( false );
					}
					await WriteUnitAsync( output, unit, unitBytes, cancellationToken ).ConfigureAwait( false );
					wroteSelection = true;
				}
				continue;
			}

			position = checked(position + 1);
			var characterMatch = cursor.Match( position );
			if ( !characterMatch.IsSelected ) {
				continue;
			}
			if ( wroteSelection && characterMatch.IsRangeStart && this.myOptions.OutputDelimiter is { } outputDelimiter ) {
				await output.WriteAsync( outputDelimiter, cancellationToken ).ConfigureAwait( false );
			}
			await WriteUnitAsync( output, unit, unitBytes, cancellationToken ).ConfigureAwait( false );
			wroteSelection = true;
		}
		if ( recordStarted ) {
			await output.WriteAsync( this.myOptions.GeneratedRecordSeparator, cancellationToken ).ConfigureAwait( false );
		}
	}

	private static bool IsSingleByte( TextUnit unit, byte value ) => 1 == unit.ByteCount && value == unit.GetByte( 0 );

	private static async ValueTask WriteUnitAsync( Stream output, TextUnit unit, byte[] buffer, CancellationToken cancellationToken ) {
		var count = unit.CopyBytesTo( buffer );
		await output.WriteAsync( buffer.AsMemory( 0, count ), cancellationToken ).ConfigureAwait( false );
	}

	private static ValueTask WriteByteAsync( Stream output, byte value, CancellationToken cancellationToken ) {
		return output.WriteAsync( new[] { value }, cancellationToken );
	}

	private static bool IsInputException( Exception exception ) => exception is IOException or UnauthorizedAccessException or System.Security.SecurityException;

	private static ValueTask WriteInputErrorAsync( CommandContext context, InputOperand operand, Exception exception ) {
		return WriteInputErrorAsync( context, operand.DisplayName, exception );
	}

	private static ValueTask WriteInputErrorAsync( CommandContext context, string displayName, Exception exception ) {
		return context.Diagnostics.ErrorAsync(
			string.Concat( displayName, ": ", exception.Message ),
			context.CancellationToken
		);
	}
}
