namespace Icod.CoreUtils.NL;

using System.Globalization;
using System.Text;
using Icod.CommandFramework.Diagnostics;
using Icod.CommandFramework.IO;
using Icod.CommandFramework.Text;

/// <summary>Processes all <c>nl</c> operands as one logical document.</summary>
internal sealed class NlProcessor {
	private static readonly byte[] ourSpaces = Enumerable.Repeat( (byte)' ', 4096 ).ToArray();
	private static readonly byte[] ourZeroes = Enumerable.Repeat( (byte)'0', 4096 ).ToArray();
	private readonly ITextLocaleProvider myLocaleProvider;
	private readonly byte[] myNewLine = Encoding.UTF8.GetBytes( Environment.NewLine );
	private readonly NlOptions myOptions;
	private readonly byte[] mySeparatorBytes;

	/// <summary>Initializes a line-number processor.</summary>
	/// <param name="options">The validated command options.</param>
	/// <param name="localeProvider">The locale and decoding provider.</param>
	internal NlProcessor( NlOptions options, ITextLocaleProvider localeProvider ) {
		this.myOptions = options ?? throw new ArgumentNullException( nameof( options ) );
		this.myLocaleProvider = localeProvider ?? throw new ArgumentNullException( nameof( localeProvider ) );
		this.mySeparatorBytes = Encoding.UTF8.GetBytes( options.Separator );
	}

	/// <summary>Processes the input operands in encounter order as one logical document.</summary>
	/// <param name="context">The command context.</param>
	/// <param name="output">The byte destination.</param>
	/// <returns><see langword="true"/> when every operand was processed successfully.</returns>
	internal async Task<bool> ProcessAsync( CommandContext context, Stream output ) {
		ArgumentNullException.ThrowIfNull( context );
		ArgumentNullException.ThrowIfNull( output );
		var operands = 0 == this.myOptions.Operands.Count ? new[] { "-" } : this.myOptions.Operands;
		var success = true;
		var state = new DocumentState( this.myOptions.StartingNumber );
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
			var continueProcessing = true;
			try {
				if ( source.BinaryStream is null ) {
					throw new IOException( "a binary input stream could not be opened" );
				}
				var reader = new TextLineReader(
					new TextUnitReader(
						source.BinaryStream,
						this.myLocaleProvider.DecodingMode,
						InvalidEncodingPolicy.PreserveBytes
					)
				);
				var result = await this.ProcessSourceAsync(
					reader,
					operand,
					context,
					output,
					state
				).ConfigureAwait( false );
				success &= result.Success;
				continueProcessing = result.ContinueProcessing;
			} finally {
				try {
					await source.DisposeAsync().ConfigureAwait( false );
				} catch ( Exception exception ) when ( IsInputException( exception ) ) {
					success = false;
					await WriteInputErrorAsync( context, operand, exception ).ConfigureAwait( false );
				}
			}
			if ( !continueProcessing ) {
				return false;
			}
		}
		return success;
	}

	private async Task<SourceResult> ProcessSourceAsync(
		TextLineReader reader,
		InputOperand operand,
		CommandContext context,
		Stream output,
		DocumentState state
	) {
		while ( true ) {
			TextLine? line;
			try {
				line = await reader.ReadAsync( context.CancellationToken ).ConfigureAwait( false );
			} catch ( Exception exception ) when ( IsInputException( exception ) ) {
				await WriteInputErrorAsync( context, operand, exception ).ConfigureAwait( false );
				return new SourceResult( true, false );
			}
			if ( line is null ) {
				return new SourceResult( true, true );
			}

			var content = line.ToByteArray( includeLineFeed: false );
			if ( this.myOptions.Delimiter.TryClassify( content, out var section ) ) {
				state.Section = section;
				if ( this.myOptions.RenumberSections ) {
					state.Number = this.myOptions.StartingNumber;
					state.NumberOverflowed = false;
				}
				await output.WriteAsync( this.myNewLine, context.CancellationToken ).ConfigureAwait( false );
				continue;
			}

			var style = this.myOptions.GetStyle( state.Section );
			var decision = await ShouldNumberAsync(
				style,
				line,
				state,
				this.myOptions.BlankJoin,
				context.CancellationToken
			).ConfigureAwait( false );
			if ( !decision.IsSuccess ) {
				await context.Diagnostics.ErrorAsync(
					decision.Diagnostic ?? "regular-expression matching failed",
					context.CancellationToken
				).ConfigureAwait( false );
				return new SourceResult( false, false );
			}
			if ( decision.ShouldNumber ) {
				if ( state.NumberOverflowed ) {
					await context.Diagnostics.ErrorAsync(
						"line number overflow",
						context.CancellationToken
					).ConfigureAwait( false );
					return new SourceResult( false, false );
				}
				await this.WriteNumberAsync( output, state.Number, context.CancellationToken ).ConfigureAwait( false );
				try {
					state.Number = checked(state.Number + this.myOptions.Increment);
				} catch ( OverflowException ) {
					state.NumberOverflowed = true;
				}
			} else {
				await WriteSpacesAsync(
					output,
					checked((long)this.myOptions.NumberWidth + this.mySeparatorBytes.Length),
					context.CancellationToken
				).ConfigureAwait( false );
			}
			await line.WriteAsync( output, cancellationToken: context.CancellationToken ).ConfigureAwait( false );
			if ( !line.HasLineFeed ) {
				await output.WriteAsync( this.myNewLine, context.CancellationToken ).ConfigureAwait( false );
			}
		}
	}

	private static async ValueTask<NumberDecision> ShouldNumberAsync(
		NlNumberingStyle style,
		TextLine line,
		DocumentState state,
		long blankJoin,
		CancellationToken cancellationToken
	) {
		switch ( style.Kind ) {
			case NlNumberingStyleKind.All:
				if ( !line.IsEmpty || 1 == blankJoin ) {
					state.BlankLines = 0;
					return NumberDecision.Numbered;
				}
				state.BlankLines = checked(state.BlankLines + 1);
				if ( state.BlankLines == blankJoin ) {
					state.BlankLines = 0;
					return NumberDecision.Numbered;
				}
				return NumberDecision.Unnumbered;
			case NlNumberingStyleKind.Nonempty:
				return line.IsEmpty ? NumberDecision.Unnumbered : NumberDecision.Numbered;
			case NlNumberingStyleKind.None:
				return NumberDecision.Unnumbered;
			case NlNumberingStyleKind.Pattern:
				var result = await style.Expression!.MatchAsync(
					line.ToDecodedString(),
					cancellationToken: cancellationToken
				).ConfigureAwait( false );
				return result.IsSuccess
					? new NumberDecision( result.IsMatch, true, null )
					: new NumberDecision( false, false, result.Diagnostic?.Message );
			default:
				throw new ArgumentOutOfRangeException( nameof( style ) );
		}
	}

	private async ValueTask WriteNumberAsync(
		Stream output,
		long number,
		CancellationToken cancellationToken
	) {
		var value = number.ToString( CultureInfo.InvariantCulture );
		var padding = Math.Max( 0L, (long)this.myOptions.NumberWidth - value.Length );
		switch ( this.myOptions.NumberFormat ) {
			case NlNumberFormat.Left:
				await output.WriteAsync( Encoding.UTF8.GetBytes( value ), cancellationToken ).ConfigureAwait( false );
				await WriteRepeatedAsync( output, ourSpaces, padding, cancellationToken ).ConfigureAwait( false );
				break;
			case NlNumberFormat.Right:
				await WriteRepeatedAsync( output, ourSpaces, padding, cancellationToken ).ConfigureAwait( false );
				await output.WriteAsync( Encoding.UTF8.GetBytes( value ), cancellationToken ).ConfigureAwait( false );
				break;
			case NlNumberFormat.RightZero:
				if ( value.StartsWith( '-' ) && 0 < padding ) {
					await output.WriteAsync( new byte[] { (byte)'-' }, cancellationToken ).ConfigureAwait( false );
					await WriteRepeatedAsync( output, ourZeroes, padding, cancellationToken ).ConfigureAwait( false );
					await output.WriteAsync( Encoding.UTF8.GetBytes( value[1..] ), cancellationToken ).ConfigureAwait( false );
				} else {
					await WriteRepeatedAsync( output, ourZeroes, padding, cancellationToken ).ConfigureAwait( false );
					await output.WriteAsync( Encoding.UTF8.GetBytes( value ), cancellationToken ).ConfigureAwait( false );
				}
				break;
			default:
				throw new ArgumentOutOfRangeException( nameof( this.myOptions.NumberFormat ) );
		}
		await output.WriteAsync( this.mySeparatorBytes, cancellationToken ).ConfigureAwait( false );
	}

	private static ValueTask WriteSpacesAsync(
		Stream output,
		long count,
		CancellationToken cancellationToken
	) {
		return WriteRepeatedAsync( output, ourSpaces, count, cancellationToken );
	}

	private static async ValueTask WriteRepeatedAsync(
		Stream output,
		byte[] buffer,
		long count,
		CancellationToken cancellationToken
	) {
		while ( 0 < count ) {
			var current = (int)Math.Min( count, buffer.Length );
			await output.WriteAsync( buffer.AsMemory( 0, current ), cancellationToken ).ConfigureAwait( false );
			count -= current;
		}
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

	private sealed class DocumentState {
		/// <summary>Initializes persistent document state.</summary>
		/// <param name="number">The first line number.</param>
		internal DocumentState( long number ) {
			this.Number = number;
		}

		/// <summary>Gets or sets the count of consecutive blank body lines.</summary>
		internal long BlankLines { get; set; }

		/// <summary>Gets or sets the next line number.</summary>
		internal long Number { get; set; }

		/// <summary>Gets or sets a value indicating whether advancing the number overflowed.</summary>
		internal bool NumberOverflowed { get; set; }

		/// <summary>Gets or sets the active logical-page section.</summary>
		internal NlSection Section { get; set; } = NlSection.Body;
	}

	private readonly struct NumberDecision {
		/// <summary>Initializes a numbering decision.</summary>
		/// <param name="shouldNumber">Whether the line should receive a number.</param>
		/// <param name="isSuccess">Whether the decision was evaluated successfully.</param>
		/// <param name="diagnostic">The failure diagnostic, when evaluation failed.</param>
		internal NumberDecision( bool shouldNumber, bool isSuccess, string? diagnostic ) {
			this.ShouldNumber = shouldNumber;
			this.IsSuccess = isSuccess;
			this.Diagnostic = diagnostic;
		}

		/// <summary>Gets the failure diagnostic.</summary>
		internal string? Diagnostic { get; }

		/// <summary>Gets a value indicating whether evaluation succeeded.</summary>
		internal bool IsSuccess { get; }

		/// <summary>Gets a value indicating whether the line should receive a number.</summary>
		internal bool ShouldNumber { get; }

		/// <summary>Gets a successful decision that numbers the line.</summary>
		internal static NumberDecision Numbered { get; } = new( true, true, null );

		/// <summary>Gets a successful decision that leaves the line unnumbered.</summary>
		internal static NumberDecision Unnumbered { get; } = new( false, true, null );
	}

	private readonly struct SourceResult {
		/// <summary>Initializes an operand-processing result.</summary>
		/// <param name="continueProcessing">Whether later operands may be processed.</param>
		/// <param name="success">Whether the operand was processed successfully.</param>
		internal SourceResult( bool continueProcessing, bool success ) {
			this.ContinueProcessing = continueProcessing;
			this.Success = success;
		}

		/// <summary>Gets a value indicating whether later operands may be processed.</summary>
		internal bool ContinueProcessing { get; }

		/// <summary>Gets a value indicating whether the operand was processed successfully.</summary>
		internal bool Success { get; }
	}
}
