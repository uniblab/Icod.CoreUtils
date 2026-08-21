namespace Icod.CoreUtils.Shuf;

using System.Buffers;
using System.Buffers.Text;
using System.Numerics;
using System.Text;
using Icod.CommandFramework.Diagnostics;
using Icod.CommandFramework.IO;

/// <summary>Executes validated <c>shuf</c> operations using bounded memory and owned external storage.</summary>
internal static class ShufEngine {
	private const int MaximumUInt64Digits = 20;
	private const ulong SparseRangeLimit = 1_000_000UL;
	private static readonly Encoding OutputEncoding = new UTF8Encoding(
		encoderShouldEmitUTF8Identifier: false,
		throwOnInvalidBytes: false
	);

	/// <summary>Executes one validated command invocation.</summary>
	/// <param name="options">The validated options.</param>
	/// <param name="context">The command context.</param>
	/// <returns>A task representing the operation.</returns>
	internal static async Task ExecuteAsync( ShufOptions options, CommandContext context ) {
		ArgumentNullException.ThrowIfNull( options );
		ArgumentNullException.ThrowIfNull( context );
		if ( options.HeadCount == BigInteger.Zero ) {
			await using var randomSource = options.Repeat
				? RandomByteSource.Create( options.RandomSourcePath )
				: null;
			await WithOutputAsync(
				options.OutputPath,
				context,
				static ( _, _ ) => Task.CompletedTask
			).ConfigureAwait( false );
			return;
		}
		if ( ShufInputMode.Range == options.InputMode ) {
			await ExecuteRangeAsync( options, context ).ConfigureAwait( false );
			return;
		}
		await ExecuteRecordInputAsync( options, context ).ConfigureAwait( false );
	}

	private static async Task ExecuteRecordInputAsync( ShufOptions options, CommandContext context ) {
		await using var store = SpoolRecordStore.Create();
		if ( ShufInputMode.Echo == options.InputMode ) {
			foreach ( var operand in options.Operands ) {
				await store.AppendRecordAsync(
					OutputEncoding.GetBytes( operand ),
					context.CancellationToken
				).ConfigureAwait( false );
			}
		} else {
			await AppendStandardInputAsync( store, options, context ).ConfigureAwait( false );
		}
		await store.SealAsync( context.CancellationToken ).ConfigureAwait( false );
		var selectionCount = GetSelectionCount( options.HeadCount, store.Count );
		var needsRandomSource = options.Repeat || 0UL < selectionCount;
		await using var randomSource = needsRandomSource
			? RandomByteSource.Create( options.RandomSourcePath )
			: null;
		if ( !options.Repeat ) {
			await store.ShufflePrefixAsync(
				selectionCount,
				randomSource,
				context.CancellationToken
			).ConfigureAwait( false );
		}
		await WithOutputAsync(
			options.OutputPath,
			context,
			( output, cancellationToken ) => options.Repeat
				? store.WriteRepeatedAsync(
					output,
					options.HeadCount,
					randomSource,
					options.Separator,
					cancellationToken
				)
				: store.WritePrefixAsync(
					output,
					selectionCount,
					options.Separator,
					cancellationToken
				)
		).ConfigureAwait( false );
	}

	private static async Task AppendStandardInputAsync(
		SpoolRecordStore store,
		ShufOptions options,
		CommandContext context
	) {
		var operand = 0 == options.Operands.Count ? "-" : options.Operands[0];
		if ( "-" == operand ) {
			if ( null == context.StandardInputStream ) {
				throw new InvalidOperationException( "a binary standard-input stream was not supplied" );
			}
			await store.AppendRecordsAsync(
				context.StandardInputStream,
				options.Separator,
				context.CancellationToken
			).ConfigureAwait( false );
			return;
		}
		await using var input = new FileStream(
			operand,
			FileMode.Open,
			FileAccess.Read,
			FileShare.Read,
			bufferSize: StreamOperations.DefaultBufferSize,
			FileOptions.Asynchronous | FileOptions.SequentialScan
		);
		await store.AppendRecordsAsync(
			input,
			options.Separator,
			context.CancellationToken
		).ConfigureAwait( false );
	}

	private static async Task ExecuteRangeAsync( ShufOptions options, CommandContext context ) {
		var rangeCount = checked( options.RangeHigh - options.RangeLow + 1UL );
		if ( options.Repeat ) {
			await ExecuteRepeatedRangeAsync( options, context, rangeCount ).ConfigureAwait( false );
			return;
		}
		var selectionCount = GetSelectionCount( options.HeadCount, rangeCount );
		if ( 0UL == selectionCount ) {
			await WithOutputAsync(
				options.OutputPath,
				context,
				static ( _, _ ) => Task.CompletedTask
			).ConfigureAwait( false );
			return;
		}
		if ( selectionCount <= SparseRangeLimit && selectionCount < rangeCount ) {
			await ExecuteSparseRangeAsync( options, context, rangeCount, selectionCount ).ConfigureAwait( false );
			return;
		}
		await ExecuteSpoolRangeAsync( options, context, selectionCount ).ConfigureAwait( false );
	}

	private static async Task ExecuteRepeatedRangeAsync(
		ShufOptions options,
		CommandContext context,
		ulong rangeCount
	) {
		await using var randomSource = RandomByteSource.Create( options.RandomSourcePath );
		await WithOutputAsync(
			options.OutputPath,
			context,
			async ( output, cancellationToken ) => {
				var buffer = ArrayPool<byte>.Shared.Rent( MaximumUInt64Digits + 1 );
				try {
					var written = BigInteger.Zero;
					while ( !options.HeadCount.HasValue || written < options.HeadCount.Value ) {
						cancellationToken.ThrowIfCancellationRequested();
						var offset = 0UL;
						if ( 1UL < rangeCount ) {
							offset = await randomSource.NextInclusiveAsync(
								rangeCount - 1UL,
								cancellationToken
							).ConfigureAwait( false );
						}
						await WriteRangeValueAsync(
							output,
							checked( options.RangeLow + offset ),
							options.Separator,
							buffer,
							cancellationToken
						).ConfigureAwait( false );
						written += BigInteger.One;
					}
				} finally {
					ArrayPool<byte>.Shared.Return( buffer );
				}
			}
		).ConfigureAwait( false );
	}

	private static async Task ExecuteSparseRangeAsync(
		ShufOptions options,
		CommandContext context,
		ulong rangeCount,
		ulong selectionCount
	) {
		await using var randomSource = RandomByteSource.Create( options.RandomSourcePath );
		var substitutions = new Dictionary<ulong, ulong>();
		var selectedValues = ArrayPool<ulong>.Shared.Rent( checked( (int)selectionCount ) );
		try {
			for ( ulong position = 0; position < selectionCount; position++ ) {
				var selectedPosition = checked(
					position + await randomSource.NextInclusiveAsync(
						rangeCount - position - 1UL,
						context.CancellationToken
					).ConfigureAwait( false )
				);
				var positionValue = substitutions.TryGetValue( position, out var current ) ? current : position;
				var selectedValue = substitutions.TryGetValue( selectedPosition, out var selected ) ? selected : selectedPosition;
				substitutions[position] = selectedValue;
				substitutions[selectedPosition] = positionValue;
				selectedValues[(int)position] = selectedValue;
			}
			await WithOutputAsync(
				options.OutputPath,
				context,
				async ( output, cancellationToken ) => {
					var buffer = ArrayPool<byte>.Shared.Rent( MaximumUInt64Digits + 1 );
					try {
						for ( var index = 0; index < (int)selectionCount; index++ ) {
							await WriteRangeValueAsync(
								output,
								checked( options.RangeLow + selectedValues[index] ),
								options.Separator,
								buffer,
								cancellationToken
							).ConfigureAwait( false );
						}
					} finally {
						ArrayPool<byte>.Shared.Return( buffer );
					}
				}
			).ConfigureAwait( false );
		} finally {
			ArrayPool<ulong>.Shared.Return( selectedValues );
		}
	}

	private static async Task ExecuteSpoolRangeAsync(
		ShufOptions options,
		CommandContext context,
		ulong selectionCount
	) {
		await using var store = SpoolRecordStore.Create();
		var buffer = ArrayPool<byte>.Shared.Rent( MaximumUInt64Digits );
		try {
			for ( var value = options.RangeLow; ; value++ ) {
				var length = FormatRangeValue( value, buffer );
				await store.AppendRecordAsync(
					buffer.AsMemory( 0, length ),
					context.CancellationToken
				).ConfigureAwait( false );
				if ( value == options.RangeHigh ) {
					break;
				}
			}
		} finally {
			ArrayPool<byte>.Shared.Return( buffer );
		}
		await store.SealAsync( context.CancellationToken ).ConfigureAwait( false );
		await using var randomSource = RandomByteSource.Create( options.RandomSourcePath );
		await store.ShufflePrefixAsync(
			selectionCount,
			randomSource,
			context.CancellationToken
		).ConfigureAwait( false );
		await WithOutputAsync(
			options.OutputPath,
			context,
			( output, cancellationToken ) => store.WritePrefixAsync(
				output,
				selectionCount,
				options.Separator,
				cancellationToken
			)
		).ConfigureAwait( false );
	}

	private static ulong GetSelectionCount( BigInteger? requested, ulong available ) {
		if ( !requested.HasValue || requested.Value >= available ) {
			return available;
		}
		return (ulong)requested.Value;
	}

	private static int FormatRangeValue( ulong value, Span<byte> destination ) {
		if ( !Utf8Formatter.TryFormat( value, destination, out var bytesWritten ) ) {
			throw new InvalidOperationException( "the range value could not be formatted" );
		}
		return bytesWritten;
	}

	private static async Task WriteRangeValueAsync(
		Stream output,
		ulong value,
		byte separator,
		byte[] buffer,
		CancellationToken cancellationToken
	) {
		var length = FormatRangeValue( value, buffer );
		buffer[length] = separator;
		await output.WriteAsync( buffer.AsMemory( 0, length + 1 ), cancellationToken ).ConfigureAwait( false );
	}

	private static async Task WithOutputAsync(
		string? outputPath,
		CommandContext context,
		Func<Stream, CancellationToken, Task> operation
	) {
		if ( null == outputPath ) {
			await using var output = new ByteOutputStream(
				context.StandardOutput,
				context.StandardOutputStream
			);
			await operation( output, context.CancellationToken ).ConfigureAwait( false );
			await output.CompleteAsync( context.CancellationToken ).ConfigureAwait( false );
			return;
		}
		await using var file = new FileStream(
			outputPath,
			FileMode.Create,
			FileAccess.Write,
			FileShare.Read,
			bufferSize: StreamOperations.DefaultBufferSize,
			FileOptions.Asynchronous | FileOptions.SequentialScan
		);
		await operation( file, context.CancellationToken ).ConfigureAwait( false );
		await file.FlushAsync( context.CancellationToken ).ConfigureAwait( false );
	}
}
