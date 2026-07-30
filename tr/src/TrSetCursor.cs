namespace Icod.CoreUtils.Tr;

/// <summary>Traverses one expanded <c>tr</c> set expression without materializing it.</summary>
internal sealed class TrSetCursor {
	private readonly TrSetExpression myExpression;
	private readonly TrByteLocale myLocale;
	private int myElementIndex;
	private ulong myOffset;
	private ulong myPosition;

	/// <summary>Initializes a cursor at the first nonempty construct.</summary>
	/// <param name="expression">The parsed expression.</param>
	/// <param name="locale">The byte-character locale.</param>
	public TrSetCursor( TrSetExpression expression, TrByteLocale locale ) {
		this.myExpression = expression ?? throw new ArgumentNullException( nameof( expression ) );
		this.myLocale = locale ?? throw new ArgumentNullException( nameof( locale ) );
		this.Normalize();
	}

	/// <summary>Gets whether every construct has been consumed.</summary>
	public bool IsComplete => this.myExpression.Elements.Count <= this.myElementIndex;

	/// <summary>Gets whether the cursor is at the beginning of its current construct.</summary>
	public bool IsAtElementStart => !this.IsComplete && 0 == this.myOffset;

	/// <summary>Gets the current expanded position.</summary>
	public ulong Position => this.myPosition;

	/// <summary>Gets the current construct.</summary>
	/// <exception cref="InvalidOperationException">The cursor is complete.</exception>
	public TrSetElement CurrentElement => !this.IsComplete
		? this.myExpression.Elements[this.myElementIndex]
		: throw new InvalidOperationException( "The set cursor is complete." );

	/// <summary>Gets the number of bytes remaining in the current construct.</summary>
	/// <exception cref="InvalidOperationException">The cursor is complete.</exception>
	public ulong RemainingInElement => checked(
		TrSetExpression.GetElementLength( this.CurrentElement, this.myLocale ) - this.myOffset
	);

	/// <summary>Gets a byte relative to the current cursor position.</summary>
	/// <param name="relativeIndex">The zero-based relative position within the current construct.</param>
	/// <returns>The expanded byte.</returns>
	/// <exception cref="ArgumentOutOfRangeException">The position lies beyond the current construct.</exception>
	public byte GetByteAt( ulong relativeIndex ) {
		if ( this.IsComplete || this.RemainingInElement <= relativeIndex ) {
			throw new ArgumentOutOfRangeException( nameof( relativeIndex ) );
		}
		return this.myExpression.GetByteAt( checked( this.myPosition + relativeIndex ), this.myLocale );
	}

	/// <summary>Advances within, or exactly past, the current construct.</summary>
	/// <param name="count">The number of expanded bytes to consume.</param>
	/// <exception cref="ArgumentOutOfRangeException">The count exceeds the current construct.</exception>
	public void Advance( ulong count ) {
		if ( this.IsComplete || this.RemainingInElement < count ) {
			throw new ArgumentOutOfRangeException( nameof( count ) );
		}
		this.myOffset = checked( this.myOffset + count );
		this.myPosition = checked( this.myPosition + count );
		if ( this.myOffset == TrSetExpression.GetElementLength( this.CurrentElement, this.myLocale ) ) {
			this.myElementIndex++;
			this.myOffset = 0;
			this.Normalize();
		}
	}

	/// <summary>Consumes the remainder of the current construct.</summary>
	/// <exception cref="InvalidOperationException">The cursor is complete.</exception>
	public void SkipCurrentElement() {
		if ( this.IsComplete ) {
			throw new InvalidOperationException( "The set cursor is complete." );
		}
		this.Advance( this.RemainingInElement );
	}

	private void Normalize() {
		while ( !this.IsComplete ) {
			var length = TrSetExpression.GetElementLength(
				this.myExpression.Elements[this.myElementIndex],
				this.myLocale
			);
			if ( 0 < length ) {
				break;
			}
			this.myElementIndex++;
			this.myOffset = 0;
		}
	}
}
