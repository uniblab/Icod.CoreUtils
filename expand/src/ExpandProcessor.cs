namespace Icod.CoreUtils.Expand;

using Icod.CommandFramework.Diagnostics;
using Icod.CommandFramework.IO;
using Icod.CommandFramework.Text;

/// <summary>Performs the byte-preserving <c>expand</c> transformation.</summary>
internal sealed class ExpandProcessor {
	private static readonly byte[] ourSpaces = Enumerable.Repeat( (byte)' ', 4096 ).ToArray();
	private readonly IDisplayWidthProvider myDisplayWidthProvider;
	private readonly ITextLocaleProvider myLocaleProvider;
	private readonly ExpandOptions myOptions;

	/// <summary>Initializes an expansion processor.</summary>
	/// <param name="options">The validated command options.</param>
	/// <param name="localeProvider">The locale and decoding provider.</param>
	/// <param name="displayWidthProvider">The display-width provider.</param>
	internal ExpandProcessor(
		ExpandOptions options,
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
	internal async Task<bool> ProcessAsync(
		CommandContext context,
		Stream output
	) {
		ArgumentNullException.ThrowIfNull( context );
		ArgumentNullException.ThrowIfNull( output );
		var success = true;
		var convert = true;
		var columns = new DisplayColumnState();
		var operands = 0 == this.myOptions.Operands.Count
			? new[] { "-" }
			: this.myOptions.Operands;
		var unitBytes = new byte[4];

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

					if ( convert ) {
						convert = !this.myOptions.InitialOnly || this.myLocaleProvider.IsBlank( unit );
						if ( IsAscii( unit, (byte)'\t' ) ) {
							var next = this.myOptions.TabStops.GetNextStop( columns.Column )
								?? checked(columns.Column + 1);
							await WriteSpacesAsync(
								output,
								next - columns.Column,
								context.CancellationToken
							).ConfigureAwait( false );
							columns.Reset( next );
							continue;
						}
						if ( IsAscii( unit, 0x08 ) ) {
							columns.Backspace();
						} else {
							var width = TextUnitDisplayWidth.GetWidth( unit, this.myDisplayWidthProvider );
							columns.Advance( width < 0 ? 1 : width );
						}
					}
					await WriteUnitAsync( output, unit, unitBytes, context.CancellationToken ).ConfigureAwait( false );
					if ( IsAscii( unit, (byte)'\n' ) ) {
						convert = true;
						columns.Reset();
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
		return success;
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

	private static async ValueTask WriteSpacesAsync(
		Stream output,
		ulong count,
		CancellationToken cancellationToken
	) {
		while ( 0 < count ) {
			var current = (int)Math.Min( (ulong)ourSpaces.Length, count );
			await output.WriteAsync( ourSpaces.AsMemory( 0, current ), cancellationToken ).ConfigureAwait( false );
			count -= (ulong)current;
		}
	}

	private static ValueTask WriteUnitAsync(
		Stream output,
		TextUnit unit,
		byte[] buffer,
		CancellationToken cancellationToken
	) {
		var count = unit.CopyBytesTo( buffer );
		return output.WriteAsync( buffer.AsMemory( 0, count ), cancellationToken );
	}
}
