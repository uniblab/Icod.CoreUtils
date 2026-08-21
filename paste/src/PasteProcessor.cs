namespace Icod.CoreUtils.Paste;

using System.Buffers;
using Icod.CommandFramework.Diagnostics;
using Icod.CommandFramework.IO;
using Icod.CommandFramework.Records;

/// <summary>Performs bounded, byte-preserving parallel or serial pasting.</summary>
internal sealed class PasteProcessor {
	private readonly PasteOptions myOptions;

	/// <summary>Initializes a processor with validated options.</summary>
	/// <param name="options">The validated options.</param>
	internal PasteProcessor( PasteOptions options ) {
		this.myOptions = options ?? throw new ArgumentNullException( nameof( options ) );
	}

	/// <summary>Processes all operands.</summary>
	/// <param name="context">The command context.</param>
	/// <param name="output">The byte output stream.</param>
	/// <returns><see langword="true"/> when every operand was processed successfully.</returns>
	internal Task<bool> ProcessAsync( CommandContext context, Stream output ) {
		ArgumentNullException.ThrowIfNull( context );
		ArgumentNullException.ThrowIfNull( output );
		return this.myOptions.Serial
			? this.ProcessSerialAsync( context, output )
			: this.ProcessParallelAsync( context, output );
	}

	private async Task<bool> ProcessParallelAsync( CommandContext context, Stream output ) {
		var success = true;
		var operands = 0 == this.myOptions.Operands.Count ? new[] { "-" } : this.myOptions.Operands;
		var opened = new List<PasteInput>();
		var columns = new List<PasteInput?>( operands.Count );
		PasteInput? standardInput = null;
		try {
			foreach ( var value in operands ) {
				var operand = InputOperand.Create( value );
				if ( operand.IsStandardInput && null != standardInput ) {
					columns.Add( standardInput );
					continue;
				}
				try {
					var input = new PasteInput( InputSource.OpenBinary( operand, context ), this.myOptions.RecordSeparator );
					opened.Add( input );
					columns.Add( input );
					if ( operand.IsStandardInput ) {
						standardInput = input;
					}
				} catch ( Exception exception ) when ( IsInputException( exception ) ) {
					await WriteInputErrorAsync( context, operand.DisplayName, exception ).ConfigureAwait( false );
					return false;
				}
			}

			while ( true ) {
				var anyRecord = false;
				var pending = new ArrayBufferWriter<byte>();
				var delimiters = this.myOptions.Delimiters.CreateCursor();
				for ( var index = 0; index < columns.Count; index++ ) {
					if ( 0 < index ) {
						pending.Write( delimiters.Next().Bytes.Span );
					}
					var column = columns[index];
					if ( null == column ) {
						continue;
					}
					try {
						var first = await column.ReadAsync( context.CancellationToken ).ConfigureAwait( false );
						if ( null == first ) {
							continue;
						}
						anyRecord = true;
						if ( 0 < pending.WrittenCount ) {
							await output.WriteAsync( pending.WrittenMemory, context.CancellationToken ).ConfigureAwait( false );
							pending.Clear();
						}
						await WriteRecordAsync( column, first, output, context.CancellationToken ).ConfigureAwait( false );
					} catch ( PasteInputException exception ) {
						success = false;
						columns[index] = null;
						await WriteInputErrorAsync( context, exception.DisplayName, exception.InnerException ?? exception ).ConfigureAwait( false );
					}
				}
				if ( !anyRecord ) {
					break;
				}
				await output.WriteAsync( this.myOptions.OutputRecordSeparator, context.CancellationToken ).ConfigureAwait( false );
			}
		} finally {
			if ( !await DisposeInputsAsync( opened, context ).ConfigureAwait( false ) ) {
				success = false;
			}
		}
		return success;
	}

	private async Task<bool> ProcessSerialAsync( CommandContext context, Stream output ) {
		var success = true;
		var operands = 0 == this.myOptions.Operands.Count ? new[] { "-" } : this.myOptions.Operands;
		PasteInput? standardInput = null;
		var opened = new List<PasteInput>();
		try {
			foreach ( var value in operands ) {
				var operand = InputOperand.Create( value );
				PasteInput input;
				if ( operand.IsStandardInput && null != standardInput ) {
					input = standardInput;
				} else {
					try {
						input = new PasteInput( InputSource.OpenBinary( operand, context ), this.myOptions.RecordSeparator );
						opened.Add( input );
						if ( operand.IsStandardInput ) {
							standardInput = input;
						}
					} catch ( Exception exception ) when ( IsInputException( exception ) ) {
						success = false;
						await WriteInputErrorAsync( context, operand.DisplayName, exception ).ConfigureAwait( false );
						continue;
					}
				}
				var delimiters = this.myOptions.Delimiters.CreateCursor();
				try {
					var first = await input.ReadAsync( context.CancellationToken ).ConfigureAwait( false );
					if ( null != first ) {
						await WriteRecordAsync( input, first, output, context.CancellationToken ).ConfigureAwait( false );
						while ( await input.ReadAsync( context.CancellationToken ).ConfigureAwait( false ) is { } next ) {
							await output.WriteAsync( delimiters.Next().Bytes, context.CancellationToken ).ConfigureAwait( false );
							await WriteRecordAsync( input, next, output, context.CancellationToken ).ConfigureAwait( false );
						}
					}
				} catch ( PasteInputException exception ) {
					success = false;
					await WriteInputErrorAsync( context, exception.DisplayName, exception.InnerException ?? exception ).ConfigureAwait( false );
				}
				await output.WriteAsync( this.myOptions.OutputRecordSeparator, context.CancellationToken ).ConfigureAwait( false );
			}
		} finally {
			if ( !await DisposeInputsAsync( opened, context ).ConfigureAwait( false ) ) {
				success = false;
			}
		}
		return success;
	}

	private static async Task WriteRecordAsync(
		PasteInput input,
		ByteRecordSegment first,
		Stream output,
		CancellationToken cancellationToken
	) {
		var current = first;
		while ( true ) {
			if ( !current.Data.IsEmpty ) {
				await output.WriteAsync( current.Data, cancellationToken ).ConfigureAwait( false );
			}
			if ( current.EndsRecord ) {
				return;
			}
			current = await input.ReadAsync( cancellationToken ).ConfigureAwait( false )
				?? throw new PasteInputException(
					input.DisplayName,
					new InvalidDataException( "A segmented record ended unexpectedly." )
				);
		}
	}

	private static async Task<bool> DisposeInputsAsync(
		IEnumerable<PasteInput> inputs,
		CommandContext context
	) {
		var success = true;
		foreach ( var input in inputs ) {
			try {
				await input.DisposeAsync().ConfigureAwait( false );
			} catch ( Exception exception ) when ( IsInputException( exception ) ) {
				success = false;
				await WriteInputErrorAsync( context, input.DisplayName, exception ).ConfigureAwait( false );
			}
		}
		return success;
	}


	private static bool IsInputException( Exception exception ) => exception is IOException or UnauthorizedAccessException or System.Security.SecurityException;

	private static ValueTask WriteInputErrorAsync( CommandContext context, string displayName, Exception exception ) {
		return context.Diagnostics.ErrorAsync( string.Concat( displayName, ": ", exception.Message ), context.CancellationToken );
	}
}
