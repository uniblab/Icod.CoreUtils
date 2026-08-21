namespace Icod.CoreUtils.Fold;

using System.Text;
using Icod.CommandFramework.Diagnostics;
using Icod.CommandFramework.IO;
using Icod.CommandFramework.Text;

/// <summary>Performs the byte-preserving <c>fold</c> transformation.</summary>
internal sealed class FoldProcessor {
	private const int MaximumBufferedBytes = 65536;
	private readonly IDisplayWidthProvider myDisplayWidthProvider;
	private readonly ITextLocaleProvider myLocaleProvider;
	private readonly FoldOptions myOptions;
	private readonly byte[] myGeneratedNewline = Encoding.UTF8.GetBytes( Environment.NewLine );
	private ulong myLastCharacterWidth;

	/// <summary>Initializes a folding processor.</summary>
	/// <param name="options">The validated command options.</param>
	/// <param name="localeProvider">The locale and decoding provider.</param>
	/// <param name="displayWidthProvider">The display-width provider.</param>
	internal FoldProcessor(
		FoldOptions options,
		ITextLocaleProvider localeProvider,
		IDisplayWidthProvider displayWidthProvider
	) {
		this.myOptions = options ?? throw new ArgumentNullException( nameof( options ) );
		this.myLocaleProvider = localeProvider ?? throw new ArgumentNullException( nameof( localeProvider ) );
		this.myDisplayWidthProvider = displayWidthProvider ?? throw new ArgumentNullException( nameof( displayWidthProvider ) );
	}

	/// <summary>Processes each operand with an independent folding state.</summary>
	/// <param name="context">The command context.</param>
	/// <param name="output">The byte output stream.</param>
	/// <returns><see langword="true"/> when every input was processed successfully.</returns>
	internal async Task<bool> ProcessAsync( CommandContext context, Stream output ) {
		ArgumentNullException.ThrowIfNull( context );
		ArgumentNullException.ThrowIfNull( output );
		var success = true;
		var operands = 0 == this.myOptions.Operands.Count
			? new[] { "-" }
			: this.myOptions.Operands;
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
				if ( !await this.ProcessOneAsync(
					source.BinaryStream!,
					output,
					context,
					operand
				).ConfigureAwait( false ) ) {
					success = false;
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

	private async Task<bool> ProcessOneAsync(
		Stream input,
		Stream output,
		CommandContext context,
		InputOperand operand
	) {
		var reader = new TextUnitReader(
			input,
			this.myLocaleProvider.DecodingMode,
			InvalidEncodingPolicy.PreserveBytes
		);
		var buffer = new FoldBuffer();
		var scratch = new byte[4];
		var state = new ColumnState { LastCharacterWidth = this.myLastCharacterWidth };
		var success = true;
		while ( true ) {
			TextUnit? valueUnit;
			try {
				valueUnit = await reader.ReadAsync( context.CancellationToken ).ConfigureAwait( false );
			} catch ( Exception exception ) when ( IsInputException( exception ) ) {
				success = false;
				await WriteInputErrorAsync( context, operand, exception ).ConfigureAwait( false );
				break;
			}
			if ( valueUnit is not TextUnit unit ) {
				break;
			}

			if ( IsAscii( unit, (byte)'\n' ) ) {
				await buffer.WritePrefixAsync( output, buffer.Count, scratch, context.CancellationToken ).ConfigureAwait( false );
				buffer.Clear();
				await WriteUnitAsync( output, unit, scratch, context.CancellationToken ).ConfigureAwait( false );
				state.ResetColumn();
				continue;
			}

			var accepted = false;
			while ( !accepted ) {
				var candidate = this.CalculateNextState( state, unit );
				if ( candidate.Column <= this.myOptions.Width ) {
					if ( MaximumBufferedBytes < checked(buffer.ByteCount + unit.ByteCount) && 0 < buffer.Count ) {
						await buffer.WritePrefixAsync( output, buffer.Count, scratch, context.CancellationToken ).ConfigureAwait( false );
						buffer.Clear();
					}
					buffer.Add( unit );
					state = candidate;
					accepted = true;
					continue;
				}

				if ( this.myOptions.BreakAtBlanks ) {
					var blankIndex = buffer.FindLastBlank( this.myLocaleProvider );
					if ( 0 <= blankIndex ) {
						await buffer.WritePrefixAsync( output, blankIndex + 1, scratch, context.CancellationToken ).ConfigureAwait( false );
						await this.WriteGeneratedNewlineAsync( output, context.CancellationToken ).ConfigureAwait( false );
						buffer.RemovePrefix( blankIndex + 1 );
						state = this.Recalculate( buffer, candidate.LastCharacterWidth );
						continue;
					}
				}

				if ( 0 == buffer.Count ) {
					buffer.Add( unit );
					state = candidate;
					accepted = true;
				} else {
					await buffer.WritePrefixAsync( output, buffer.Count, scratch, context.CancellationToken ).ConfigureAwait( false );
					await this.WriteGeneratedNewlineAsync( output, context.CancellationToken ).ConfigureAwait( false );
					buffer.Clear();
					state = new ColumnState { LastCharacterWidth = candidate.LastCharacterWidth };
				}
			}
		}
		await buffer.WritePrefixAsync( output, buffer.Count, scratch, context.CancellationToken ).ConfigureAwait( false );
		this.myLastCharacterWidth = state.LastCharacterWidth;
		return success;
	}

	private ColumnState CalculateNextState( ColumnState state, TextUnit unit ) {
		if ( this.myOptions.CountingMode == FoldCountingMode.Bytes ) {
			state.Column = checked(state.Column + (ulong)unit.ByteCount);
			return state;
		}
		if ( IsAscii( unit, 0x08 ) ) {
			if ( 0 < state.Column ) {
				state.Column = state.LastCharacterWidth >= state.Column
					? 0
					: state.Column - state.LastCharacterWidth;
			}
			return state;
		}
		if ( IsAscii( unit, (byte)'\r' ) ) {
			state.Column = 0;
			return state;
		}
		if ( IsAscii( unit, (byte)'\t' ) ) {
			state.Column = checked(((state.Column / 8) + 1) * 8);
			return state;
		}
		var measuredWidth = this.myOptions.CountingMode == FoldCountingMode.Characters
			? 1
			: TextUnitDisplayWidth.GetWidth( unit, this.myDisplayWidthProvider );
		var width = measuredWidth < 0 ? 1 : measuredWidth;
		state.LastCharacterWidth = (ulong)width;
		state.Column = checked(state.Column + state.LastCharacterWidth);
		return state;
	}

	private ColumnState Recalculate( FoldBuffer buffer, ulong lastCharacterWidth ) {
		var state = new ColumnState { LastCharacterWidth = lastCharacterWidth };
		for ( var index = 0; index < buffer.Count; index++ ) {
			state = this.CalculateNextState( state, buffer.GetUnit( index ) );
		}
		return state;
	}

	private ValueTask WriteGeneratedNewlineAsync( Stream output, CancellationToken cancellationToken ) {
		return output.WriteAsync( this.myGeneratedNewline.AsMemory(), cancellationToken );
	}

	private static bool IsAscii( TextUnit unit, byte value ) {
		return 1 == unit.ByteCount && value == unit.GetByte( 0 );
	}

	private static bool IsInputException( Exception exception ) {
		return exception is IOException
			or UnauthorizedAccessException
			or System.Security.SecurityException;
	}

	private static ValueTask WriteInputErrorAsync(
		CommandContext context,
		InputOperand operand,
		Exception exception
	) {
		return context.Diagnostics.ErrorAsync(
			string.Concat( operand.DisplayName, ": ", exception.Message ),
			context.CancellationToken
		);
	}

	private static ValueTask WriteUnitAsync( Stream output, TextUnit unit, byte[] scratch, CancellationToken cancellationToken ) {
		var count = unit.CopyBytesTo( scratch );
		return output.WriteAsync( scratch.AsMemory( 0, count ), cancellationToken );
	}

	private struct ColumnState {
		/// <summary>Stores the current zero-based logical display column.</summary>
		internal ulong Column;

		/// <summary>Stores the width reversed by a following backspace.</summary>
		internal ulong LastCharacterWidth;

		/// <summary>Resets only the current logical-line column, preserving GNU's last-character-width state.</summary>
		internal void ResetColumn() {
			this.Column = 0;
		}
	}
}
