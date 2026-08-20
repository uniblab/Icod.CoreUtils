namespace Icod.CoreUtils.Fmt;

using Icod.CoreUtils.Shared.Diagnostics;
using Icod.CoreUtils.Shared.IO;
using Icod.CommandFramework.Text;

/// <summary>Processes byte-preserving <c>fmt</c> input operands.</summary>
internal sealed class FmtProcessor {
	private readonly FmtOptions myOptions;

	/// <summary>Initializes an operand processor.</summary>
	/// <param name="options">The validated command options.</param>
	internal FmtProcessor( FmtOptions options ) {
		this.myOptions = options ?? throw new ArgumentNullException( nameof( options ) );
	}

	/// <summary>Processes all operands in encounter order.</summary>
	/// <param name="context">The command context.</param>
	/// <param name="output">The byte destination.</param>
	/// <returns><see langword="true"/> when every operand was processed successfully.</returns>
	internal async Task<bool> ProcessAsync( CommandContext context, Stream output ) {
		ArgumentNullException.ThrowIfNull( context );
		ArgumentNullException.ThrowIfNull( output );
		var operands = 0 == this.myOptions.Operands.Count ? new[] { "-" } : this.myOptions.Operands;
		var success = true;
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
				if ( source.BinaryStream is null ) {
					throw new InputReadException( new IOException( "a binary input stream could not be opened" ) );
				}
				var reader = new TextLineReader(
					new TextUnitReader(
						source.BinaryStream,
						TextDecodingMode.Bytes,
						InvalidEncodingPolicy.PreserveBytes
					)
				);
				await this.ProcessSourceAsync( reader, output, context.CancellationToken ).ConfigureAwait( false );
			} catch ( InputReadException exception ) {
				success = false;
				await WriteInputErrorAsync( context, operand, exception.InnerException ?? exception ).ConfigureAwait( false );
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

	private async Task ProcessSourceAsync(
		TextLineReader reader,
		Stream output,
		CancellationToken cancellationToken
	) {
		FmtInputLine? pending = null;
		var useTabs = false;
		var taggedOtherIndent = 0;
		var formatter = new ParagraphFormatter( this.myOptions );
		while ( true ) {
			var current = pending ?? await this.ReadLineAsync( reader, cancellationToken ).ConfigureAwait( false );
			pending = null;
			if ( current is null ) {
				break;
			}
			useTabs |= current.HasTab;
			if ( !CanStartParagraph( current ) ) {
				await current.WriteUnformattedAsync( output, useTabs, cancellationToken ).ConfigureAwait( false );
				continue;
			}

			var paragraph = new List<FmtInputLine> { current };
			var otherIndent = current.ContentColumn;

			// GNU --split-only changes paragraph recognition, not paragraph optimization.
			// When enabled, this one input line remains a one-line source paragraph, but it
			// is still sent to ParagraphFormatter below and may be split according to the
			// same cost model used for paragraphs assembled from multiple input lines.
			if ( !this.myOptions.SplitOnly ) {
				var next = await this.ReadLineAsync( reader, cancellationToken ).ConfigureAwait( false );
				if ( next is not null ) {
					useTabs |= next.HasTab;
				}
				if ( this.myOptions.CrownMargin ) {
					if ( IsSameParagraphCandidate( current, next ) ) {
						paragraph.Add( next! );
						otherIndent = next!.ContentColumn;
						next = await CollectContinuationAsync(
							reader,
							paragraph,
							current.PrefixColumn,
							otherIndent,
							cancellationToken
						).ConfigureAwait( false );
						useTabs |= paragraph.Any( line => line.HasTab ) || (next?.HasTab ?? false);
					}
				} else if ( this.myOptions.TaggedParagraph ) {
					if ( IsSameParagraphCandidate( current, next ) && next!.ContentColumn != current.ContentColumn ) {
						paragraph.Add( next! );
						otherIndent = next.ContentColumn;
						taggedOtherIndent = otherIndent;
						next = await CollectContinuationAsync(
							reader,
							paragraph,
							current.PrefixColumn,
							otherIndent,
							cancellationToken
						).ConfigureAwait( false );
						useTabs |= paragraph.Any( line => line.HasTab ) || (next?.HasTab ?? false);
					} else {
						if ( taggedOtherIndent == current.ContentColumn ) {
							taggedOtherIndent = 0 == current.ContentColumn ? 3 : 0;
						}
						otherIndent = taggedOtherIndent;
					}
				} else {
					while ( IsSameParagraphCandidate( current, next ) && next!.ContentColumn == current.ContentColumn ) {
						paragraph.Add( next! );
						next = await this.ReadLineAsync( reader, cancellationToken ).ConfigureAwait( false );
						if ( next is not null ) {
							useTabs |= next.HasTab;
						}
					}
				}
				pending = next;
			}
			await formatter.FormatAsync(
				paragraph,
				otherIndent,
				useTabs,
				output,
				cancellationToken
			).ConfigureAwait( false );
		}
	}

	private async Task<FmtInputLine?> CollectContinuationAsync(
		TextLineReader reader,
		ICollection<FmtInputLine> paragraph,
		int prefixColumn,
		int contentColumn,
		CancellationToken cancellationToken
	) {
		while ( true ) {
			var next = await this.ReadLineAsync( reader, cancellationToken ).ConfigureAwait( false );
			if (
				next is null
				|| !CanStartParagraph( next )
				|| next.PrefixColumn != prefixColumn
				|| next.ContentColumn != contentColumn
			) {
				return next;
			}
			paragraph.Add( next! );
		}
	}

	private ValueTask<FmtInputLine?> ReadLineAsync(
		TextLineReader reader,
		CancellationToken cancellationToken
	) {
		return ReadAndAnalyzeAsync( reader, this.myOptions, cancellationToken );
	}

	private static async ValueTask<FmtInputLine?> ReadAndAnalyzeAsync(
		TextLineReader reader,
		FmtOptions options,
		CancellationToken cancellationToken
	) {
		try {
			var source = await reader.ReadAsync( cancellationToken ).ConfigureAwait( false );
			return source is null
				? null
				: FmtInputLine.Analyze( source, options.Prefix, options.UniformSpacing );
		} catch ( Exception exception ) when ( IsInputException( exception ) ) {
			throw new InputReadException( exception );
		}
	}

	private static bool CanStartParagraph( FmtInputLine line ) {
		return line.IsEligible && !line.IsBlank && !line.IsPrefixOnly && 0 < line.Words.Count;
	}

	private static bool IsSameParagraphCandidate( FmtInputLine first, FmtInputLine? candidate ) {
		return candidate is not null
			&& CanStartParagraph( candidate )
			&& candidate.PrefixColumn == first.PrefixColumn;
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

	private sealed class InputReadException : Exception {
		/// <summary>Initializes a wrapper for a source-read exception.</summary>
		/// <param name="innerException">The original source-read exception.</param>
		internal InputReadException( Exception innerException )
			: base( innerException.Message, innerException ) {
		}
	}
}
