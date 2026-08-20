namespace Icod.CoreUtils.Cut;

using System.Buffers;
using Icod.CoreUtils.Shared.Records;
using Icod.CommandFramework.Text;

/// <summary>Performs streaming field selection, retaining only a potentially ambiguous first field.</summary>
/// <remarks>
/// GNU field mode must defer the first field when an undelimited record may need to be copied or suppressed.
/// Later fields are streamed as soon as their delimiter has established field mode.
/// </remarks>
internal sealed class CutFieldProcessor {
	private readonly CutOptions myOptions;

	/// <summary>Initializes a field processor.</summary>
	/// <param name="options">The validated field-mode options.</param>
	internal CutFieldProcessor( CutOptions options ) {
		this.myOptions = options ?? throw new ArgumentNullException( nameof( options ) );
	}

	/// <summary>Processes one input stream.</summary>
	/// <param name="input">The source stream.</param>
	/// <param name="output">The destination stream.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>A task representing processing.</returns>
	internal Task ProcessAsync( Stream input, Stream output, CancellationToken cancellationToken ) {
		if ( this.myOptions.WhitespaceDelimited ) {
			return this.ProcessWhitespaceAsync( input, output, cancellationToken );
		}
		if ( this.myOptions.FieldDelimiter is { Length: 1 } delimiter && delimiter[0] == this.myOptions.RecordSeparator ) {
			return this.ProcessRecordSeparatorFieldsAsync( input, output, cancellationToken );
		}
		return this.ProcessExplicitAsync( input, output, cancellationToken );
	}

	private async Task ProcessExplicitAsync( Stream input, Stream output, CancellationToken cancellationToken ) {
		var reader = new TextUnitReader( input, this.myOptions.LocaleProvider.DecodingMode, InvalidEncodingPolicy.PreserveBytes );
		var bytes = new byte[4];
		var first = new ArrayBufferWriter<byte>();
		ulong field = 1;
		var selected = this.myOptions.Ranges.Contains( field );
		var lineHasDelimiter = false;
		var recordStarted = false;
		var outputHasField = selected && !this.myOptions.SuppressUndelimited;
		var bufferFirst = (!this.myOptions.SuppressUndelimited && !selected) || (this.myOptions.SuppressUndelimited && selected);

		while ( await reader.ReadAsync( cancellationToken ).ConfigureAwait( false ) is TextUnit unit ) {
			if ( IsSingleByte( unit, this.myOptions.RecordSeparator ) ) {
				await FinalizeRecordAsync(
				output,
				first,
				lineHasDelimiter,
				recordStarted: true,
				bufferFirst: bufferFirst,
				isTerminated: true,
				cancellationToken: cancellationToken
			).ConfigureAwait( false );
				Reset( ref field, ref selected, ref lineHasDelimiter, ref recordStarted, ref outputHasField, ref bufferFirst, first );
				continue;
			}
			recordStarted = true;
			if ( Matches( unit, this.myOptions.FieldDelimiter! ) ) {
				if ( !lineHasDelimiter ) {
					lineHasDelimiter = true;
					if ( bufferFirst && selected ) {
						await output.WriteAsync( first.WrittenMemory, cancellationToken ).ConfigureAwait( false );
						outputHasField = true;
					}
					first.Clear();
				}
				field = checked(field + 1);
				selected = this.myOptions.Ranges.Contains( field );
				bufferFirst = false;
				if ( selected ) {
					if ( outputHasField ) {
						await output.WriteAsync( this.myOptions.OutputDelimiter!, cancellationToken ).ConfigureAwait( false );
					}
					outputHasField = true;
				}
				continue;
			}
			var count = unit.CopyBytesTo( bytes );
			if ( !lineHasDelimiter && bufferFirst ) {
				first.Write( bytes.AsSpan( 0, count ) );
			} else if ( selected ) {
				await output.WriteAsync( bytes.AsMemory( 0, count ), cancellationToken ).ConfigureAwait( false );
			}
		}
		if ( recordStarted ) {
			await FinalizeRecordAsync(
				output,
				first,
				lineHasDelimiter,
				recordStarted,
				bufferFirst,
				isTerminated: false,
				cancellationToken: cancellationToken
			).ConfigureAwait( false );
		}
	}

	private async Task ProcessWhitespaceAsync( Stream input, Stream output, CancellationToken cancellationToken ) {
		var reader = new TextUnitReader( input, this.myOptions.LocaleProvider.DecodingMode, InvalidEncodingPolicy.PreserveBytes );
		var first = new ArrayBufferWriter<byte>();
		var bytes = new byte[4];
		ulong field = 1;
		var selected = this.myOptions.Ranges.Contains( field );
		var lineHasDelimiter = false;
		var recordStarted = false;
		var outputHasField = selected && !this.myOptions.SuppressUndelimited;
		var bufferFirst = (!this.myOptions.SuppressUndelimited && !selected) || (this.myOptions.SuppressUndelimited && selected);
		var atLineStart = true;
		var blankRun = false;

		while ( await reader.ReadAsync( cancellationToken ).ConfigureAwait( false ) is TextUnit unit ) {
			if ( IsSingleByte( unit, this.myOptions.RecordSeparator ) ) {
				await FinalizeWhitespaceRecordAsync( output, first, lineHasDelimiter, bufferFirst, cancellationToken ).ConfigureAwait( false );
				await WriteRecordSeparatorAsync( output, lineHasDelimiter || !this.myOptions.SuppressUndelimited, isTerminated: true, cancellationToken: cancellationToken ).ConfigureAwait( false );
				field = 1;
				selected = this.myOptions.Ranges.Contains( field );
				lineHasDelimiter = false;
				recordStarted = false;
				outputHasField = selected && !this.myOptions.SuppressUndelimited;
				bufferFirst = (!this.myOptions.SuppressUndelimited && !selected) || (this.myOptions.SuppressUndelimited && selected);
				atLineStart = true;
				blankRun = false;
				first.Clear();
				continue;
			}
			recordStarted = true;
			if ( this.myOptions.LocaleProvider.IsBlank( unit ) ) {
				if ( this.myOptions.TrimWhitespace ) {
					if ( !atLineStart ) {
						blankRun = true;
					}
				} else if ( !blankRun ) {
					(field, selected, lineHasDelimiter, outputHasField) = await ConfirmWhitespaceDelimiterAsync(
						output,
						first,
						field,
						selected,
						lineHasDelimiter,
						outputHasField,
						bufferFirst,
						cancellationToken
					).ConfigureAwait( false );
					blankRun = true;
				}
				continue;
			}
			if ( this.myOptions.TrimWhitespace && blankRun ) {
				(field, selected, lineHasDelimiter, outputHasField) = await ConfirmWhitespaceDelimiterAsync(
						output,
						first,
						field,
						selected,
						lineHasDelimiter,
						outputHasField,
						bufferFirst,
						cancellationToken
					).ConfigureAwait( false );
			}
			blankRun = false;
			atLineStart = false;
			var count = unit.CopyBytesTo( bytes );
			if ( !lineHasDelimiter && bufferFirst ) {
				first.Write( bytes.AsSpan( 0, count ) );
			} else if ( selected ) {
				await output.WriteAsync( bytes.AsMemory( 0, count ), cancellationToken ).ConfigureAwait( false );
			}
		}
		if ( recordStarted ) {
			await FinalizeWhitespaceRecordAsync( output, first, lineHasDelimiter, bufferFirst, cancellationToken ).ConfigureAwait( false );
			await WriteRecordSeparatorAsync( output, lineHasDelimiter || !this.myOptions.SuppressUndelimited, isTerminated: false, cancellationToken: cancellationToken ).ConfigureAwait( false );
		}
	}

	private async Task<(ulong Field, bool Selected, bool LineHasDelimiter, bool OutputHasField)> ConfirmWhitespaceDelimiterAsync(
		Stream output,
		ArrayBufferWriter<byte> first,
		ulong field,
		bool selected,
		bool lineHasDelimiter,
		bool outputHasField,
		bool bufferFirst,
		CancellationToken cancellationToken
	) {
		if ( !lineHasDelimiter ) {
			lineHasDelimiter = true;
			if ( bufferFirst && selected ) {
				await output.WriteAsync( first.WrittenMemory, cancellationToken ).ConfigureAwait( false );
				outputHasField = true;
			}
			first.Clear();
		}
		field = checked(field + 1);
		selected = this.myOptions.Ranges.Contains( field );
		if ( selected ) {
			if ( outputHasField ) {
				await output.WriteAsync( this.myOptions.OutputDelimiter!, cancellationToken ).ConfigureAwait( false );
			}
			outputHasField = true;
		}
		return (field, selected, lineHasDelimiter, outputHasField);
	}

	private async Task FinalizeWhitespaceRecordAsync(
		Stream output,
		ArrayBufferWriter<byte> first,
		bool lineHasDelimiter,
		bool bufferFirst,
		CancellationToken cancellationToken
	) {
		if ( !lineHasDelimiter && !this.myOptions.SuppressUndelimited && bufferFirst ) {
			await output.WriteAsync( first.WrittenMemory, cancellationToken ).ConfigureAwait( false );
		}
	}

	private async Task ProcessRecordSeparatorFieldsAsync( Stream input, Stream output, CancellationToken cancellationToken ) {
		using var reader = new ByteRecordReader( input, this.myOptions.RecordSeparator );
		var current = await reader.ReadAsync( cancellationToken ).ConfigureAwait( false );
		if ( null == current ) {
			return;
		}
		var next = await reader.ReadAsync( cancellationToken ).ConfigureAwait( false );
		if ( null == next && !current.IsTerminated ) {
			if ( !this.myOptions.SuppressUndelimited ) {
				await output.WriteAsync( current.Content, cancellationToken ).ConfigureAwait( false );
				await output.WriteAsync( this.myOptions.GeneratedRecordSeparator, cancellationToken ).ConfigureAwait( false );
			}
			return;
		}

		ulong field = 1;
		var foundSelectedField = false;
		while ( true ) {
			if ( this.myOptions.Ranges.Contains( field ) ) {
				if ( foundSelectedField ) {
					await output.WriteAsync( this.myOptions.OutputDelimiter!, cancellationToken ).ConfigureAwait( false );
				}
				await output.WriteAsync( current.Content, cancellationToken ).ConfigureAwait( false );
				foundSelectedField = true;
			}

			if ( null == next ) {
				// A separator at physical EOF terminates the logical record; unlike an
				// interior separator, it does not create another selectable empty field.
				// Under -s GNU still emits the logical record when an existing field was
				// selected or an interior separator advanced beyond field one.
				if ( foundSelectedField || !this.myOptions.SuppressUndelimited || 1 < field ) {
					await WriteRecordSeparatorAsync( output, true, current.IsTerminated, cancellationToken ).ConfigureAwait( false );
				}
				return;
			}

			field = checked(field + 1);
			current = next;
			next = await reader.ReadAsync( cancellationToken ).ConfigureAwait( false );
		}
	}

	private async Task FinalizeRecordAsync(
		Stream output,
		ArrayBufferWriter<byte> first,
		bool lineHasDelimiter,
		bool recordStarted,
		bool bufferFirst,
		bool isTerminated,
		CancellationToken cancellationToken
	) {
		if ( !recordStarted ) {
			return;
		}
		if ( !lineHasDelimiter && !this.myOptions.SuppressUndelimited && bufferFirst ) {
			await output.WriteAsync( first.WrittenMemory, cancellationToken ).ConfigureAwait( false );
		}
		await WriteRecordSeparatorAsync( output, lineHasDelimiter || !this.myOptions.SuppressUndelimited, isTerminated, cancellationToken ).ConfigureAwait( false );
	}

	private void Reset(
		ref ulong field,
		ref bool selected,
		ref bool lineHasDelimiter,
		ref bool recordStarted,
		ref bool outputHasField,
		ref bool bufferFirst,
		ArrayBufferWriter<byte> first
	) {
		field = 1;
		selected = this.myOptions.Ranges.Contains( field );
		lineHasDelimiter = false;
		recordStarted = false;
		outputHasField = selected && !this.myOptions.SuppressUndelimited;
		bufferFirst = (!this.myOptions.SuppressUndelimited && !selected) || (this.myOptions.SuppressUndelimited && selected);
		first.Clear();
	}

	private static bool IsSingleByte( TextUnit unit, byte value ) => 1 == unit.ByteCount && value == unit.GetByte( 0 );

	private static bool Matches( TextUnit unit, byte[] delimiter ) {
		if ( unit.ByteCount != delimiter.Length ) {
			return false;
		}
		for ( var index = 0; index < delimiter.Length; index++ ) {
			if ( unit.GetByte( index ) != delimiter[index] ) {
				return false;
			}
		}
		return true;
	}

	private ValueTask WriteRecordSeparatorAsync(
		Stream output,
		bool write,
		bool isTerminated,
		CancellationToken cancellationToken
	) {
		if ( !write ) {
			return ValueTask.CompletedTask;
		}
		return isTerminated
			? output.WriteAsync( new[] { this.myOptions.RecordSeparator }, cancellationToken )
			: output.WriteAsync( this.myOptions.GeneratedRecordSeparator, cancellationToken );
	}
}
