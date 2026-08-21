namespace Icod.CoreUtils.Unexpand;

using Icod.CommandFramework.Diagnostics;
using Icod.CommandFramework.IO;
using Icod.CommandFramework.Text;

/// <summary>Performs the byte-preserving <c>unexpand</c> transformation.</summary>
internal sealed class UnexpandProcessor {
	private static readonly byte[] ourTab = new[] { (byte)'\t' };
	private readonly IDisplayWidthProvider myDisplayWidthProvider;
	private readonly ITextLocaleProvider myLocaleProvider;
	private readonly UnexpandOptions myOptions;

	/// <summary>Initializes an unexpansion processor.</summary>
	/// <param name="options">The validated command options.</param>
	/// <param name="localeProvider">The locale and decoding provider.</param>
	/// <param name="displayWidthProvider">The display-width provider.</param>
	internal UnexpandProcessor(
		UnexpandOptions options,
		ITextLocaleProvider localeProvider,
		IDisplayWidthProvider displayWidthProvider
	) {
		this.myOptions = options ?? throw new ArgumentNullException( nameof( options ) );
		this.myLocaleProvider = localeProvider ?? throw new ArgumentNullException( nameof( localeProvider ) );
		this.myDisplayWidthProvider = displayWidthProvider ?? throw new ArgumentNullException( nameof( displayWidthProvider ) );
	}

	/// <summary>Processes all operands, preserving logical-line state across operand boundaries.</summary>
	/// <param name="context">The command context.</param>
	/// <param name="output">The byte output stream.</param>
	/// <returns><see langword="true"/> when every input was processed successfully.</returns>
	internal async Task<bool> ProcessAsync( CommandContext context, Stream output ) {
		ArgumentNullException.ThrowIfNull( context );
		ArgumentNullException.ThrowIfNull( output );
		var success = true;
		var convert = true;
		var column = 0UL;
		var previousBlank = true;
		var oneBlankBeforeTabStop = false;
		var pending = new PendingBlankBuffer();
		var scratch = new byte[4];
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
				var reader = new TextUnitReader(
					source.BinaryStream!,
					this.myLocaleProvider.DecodingMode,
					InvalidEncodingPolicy.PreserveBytes
				);
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

					var suppressCurrent = false;
					if ( convert ) {
						var blank = this.myLocaleProvider.IsBlank( unit );
						if ( blank ) {
							var nextColumn = this.myOptions.TabStops.GetNextStop( column );
							if ( nextColumn is null ) {
								convert = false;
							} else if ( IsAscii( unit, (byte)'\t' ) ) {
								column = nextColumn.Value;
								pending.ReplaceFirstWithTab();
								pending.KeepFirstOrClear( oneBlankBeforeTabStop );
							} else {
								column = checked(column + GetDisplayWidth( unit, this.myDisplayWidthProvider ));
								if ( !(previousBlank && column == nextColumn.Value) ) {
									if ( column == nextColumn.Value ) {
										oneBlankBeforeTabStop = true;
									}
									pending.Add( unit );
									previousBlank = true;
									continue;
								}
								suppressCurrent = true;
								await output.WriteAsync( ourTab.AsMemory(), context.CancellationToken ).ConfigureAwait( false );
								pending.ReplaceFirstWithTab();
								pending.KeepFirstOrClear( oneBlankBeforeTabStop );
							}
						} else if ( IsAscii( unit, 0x08 ) ) {
							column = 0 == column ? 0 : column - 1;
						} else {
							column = checked(column + GetDisplayWidth( unit, this.myDisplayWidthProvider ));
						}

						if ( !pending.IsEmpty ) {
							if ( 1 < pending.Count && oneBlankBeforeTabStop ) {
								pending.ReplaceFirstWithTab();
							}
							await pending.WriteAsync( output, scratch, context.CancellationToken ).ConfigureAwait( false );
							oneBlankBeforeTabStop = false;
						}
						previousBlank = blank;
						convert = convert && (this.myOptions.ConvertAll || blank);
					}

					if ( !suppressCurrent ) {
						await WriteUnitAsync( output, unit, scratch, context.CancellationToken ).ConfigureAwait( false );
					}
					if ( IsAscii( unit, (byte)'\n' ) ) {
						convert = true;
						column = 0;
						previousBlank = true;
						oneBlankBeforeTabStop = false;
						pending.Clear();
					}
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

		if ( !pending.IsEmpty ) {
			if ( 1 < pending.Count && oneBlankBeforeTabStop ) {
				pending.ReplaceFirstWithTab();
			}
			await pending.WriteAsync( output, scratch, context.CancellationToken ).ConfigureAwait( false );
		}
		return success;
	}

	private static ulong GetDisplayWidth( TextUnit unit, IDisplayWidthProvider provider ) {
		var width = TextUnitDisplayWidth.GetWidth( unit, provider );
		return (ulong)(width < 0 ? 1 : width);
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

	private static ValueTask WriteUnitAsync(
		Stream output,
		TextUnit unit,
		byte[] scratch,
		CancellationToken cancellationToken
	) {
		var count = unit.CopyBytesTo( scratch );
		return output.WriteAsync( scratch.AsMemory( 0, count ), cancellationToken );
	}
}
