namespace Icod.CoreUtils.Tr;

/// <summary>Identifies one parsed construct in a <c>tr</c> set expression.</summary>
internal enum TrSetElementKind {
	/// <summary>A single byte.</summary>
	Literal,
	/// <summary>An inclusive ascending byte range.</summary>
	Range,
	/// <summary>A locale-sensitive character class.</summary>
	CharacterClass,
	/// <summary>A GNU equivalence class, represented by its single byte.</summary>
	EquivalenceClass,
	/// <summary>A repeated byte.</summary>
	Repeat
}

/// <summary>Represents one parsed construct in a <c>tr</c> set expression.</summary>
internal sealed class TrSetElement {
	private TrSetElement(
		TrSetElementKind kind,
		byte first,
		byte last,
		TrCharacterClass characterClass,
		ulong repeatCount,
		bool indefinite
	) {
		this.Kind = kind;
		this.First = first;
		this.Last = last;
		this.CharacterClass = characterClass;
		this.RepeatCount = repeatCount;
		this.IsIndefiniteRepeat = indefinite;
	}

	/// <summary>Gets the construct kind.</summary>
	public TrSetElementKind Kind { get; }

	/// <summary>Gets the literal, range-start, equivalence, or repeated byte.</summary>
	public byte First { get; }

	/// <summary>Gets the inclusive range-end byte.</summary>
	public byte Last { get; }

	/// <summary>Gets the character class for a class construct.</summary>
	public TrCharacterClass CharacterClass { get; }

	/// <summary>Gets the explicit repeat count.</summary>
	public ulong RepeatCount { get; }

	/// <summary>Gets whether a repeat count is filled from the first array length.</summary>
	public bool IsIndefiniteRepeat { get; }

	/// <summary>Creates a literal-byte construct.</summary>
	/// <param name="value">The literal byte.</param>
	/// <returns>The construct.</returns>
	public static TrSetElement Literal( byte value ) => new(
		TrSetElementKind.Literal,
		value,
		value,
		default,
		1,
		false
	);

	/// <summary>Creates an inclusive ascending range construct.</summary>
	/// <param name="first">The first byte.</param>
	/// <param name="last">The last byte.</param>
	/// <returns>The construct.</returns>
	public static TrSetElement Range( byte first, byte last ) => new(
		TrSetElementKind.Range,
		first,
		last,
		default,
		checked( (ulong)( last - first ) + 1UL ),
		false
	);

	/// <summary>Creates a character-class construct.</summary>
	/// <param name="value">The character class.</param>
	/// <returns>The construct.</returns>
	public static TrSetElement Class( TrCharacterClass value ) => new(
		TrSetElementKind.CharacterClass,
		0,
		0,
		value,
		0,
		false
	);

	/// <summary>Creates an equivalence-class construct.</summary>
	/// <param name="value">The equivalence-class byte.</param>
	/// <returns>The construct.</returns>
	public static TrSetElement Equivalence( byte value ) => new(
		TrSetElementKind.EquivalenceClass,
		value,
		value,
		default,
		1,
		false
	);

	/// <summary>Creates a repeated-byte construct.</summary>
	/// <param name="value">The repeated byte.</param>
	/// <param name="count">The explicit repeat count.</param>
	/// <param name="indefinite">Whether the count is resolved from the first array.</param>
	/// <returns>The construct.</returns>
	public static TrSetElement Repeat( byte value, ulong count, bool indefinite ) => new(
		TrSetElementKind.Repeat,
		value,
		value,
		default,
		count,
		indefinite
	);

	/// <summary>Creates a copy with a resolved repeat count.</summary>
	/// <param name="count">The resolved repeat count.</param>
	/// <returns>The resolved construct.</returns>
	public TrSetElement ResolveRepeat( ulong count ) {
		if ( TrSetElementKind.Repeat != this.Kind || !this.IsIndefiniteRepeat ) {
			throw new InvalidOperationException( "Only an indefinite repeat can be resolved." );
		}
		return Repeat( this.First, count, false );
	}
}
