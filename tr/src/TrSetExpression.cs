namespace Icod.CoreUtils.Tr;

/// <summary>Represents a parsed sequence of GNU <c>tr</c> set constructs.</summary>
internal sealed class TrSetExpression {
	private const ulong MaximumExpandedLength = ulong.MaxValue - 1;
	private readonly IReadOnlyList<TrSetElement> myElements;

	/// <summary>Initializes a parsed set expression.</summary>
	/// <param name="elements">The constructs in source order.</param>
	public TrSetExpression( IEnumerable<TrSetElement> elements ) {
		ArgumentNullException.ThrowIfNull( elements );
		this.myElements = Array.AsReadOnly( elements.ToArray() );
	}

	/// <summary>Gets the constructs in source order.</summary>
	public IReadOnlyList<TrSetElement> Elements => this.myElements;

	/// <summary>Gets the number of unresolved fill repeats.</summary>
	public int IndefiniteRepeatCount => this.myElements.Count( value => value.IsIndefiniteRepeat );

	/// <summary>Gets whether the expression contains a character class.</summary>
	public bool HasCharacterClass => this.myElements.Any( value => TrSetElementKind.CharacterClass == value.Kind );

	/// <summary>Gets whether the expression contains an equivalence class.</summary>
	public bool HasEquivalenceClass => this.myElements.Any( value => TrSetElementKind.EquivalenceClass == value.Kind );

	/// <summary>Gets whether the expression contains a character class other than lower or upper.</summary>
	public bool HasRestrictedCharacterClass => this.myElements.Any(
		value => TrSetElementKind.CharacterClass == value.Kind
			&& value.CharacterClass is not TrCharacterClass.Lower and not TrCharacterClass.Upper
	);

	/// <summary>Gets the expanded sequence length.</summary>
	/// <param name="locale">The byte-character locale.</param>
	/// <returns>The number of bytes in the sequence.</returns>
	/// <exception cref="InvalidOperationException">An unresolved fill repeat remains.</exception>
	/// <exception cref="OverflowException">The expanded length exceeds <see cref="ulong.MaxValue"/>.</exception>
	public ulong GetLength( TrByteLocale locale ) {
		ArgumentNullException.ThrowIfNull( locale );
		ulong length = 0;
		foreach ( var element in this.myElements ) {
			length = AddLength( length, GetElementLength( element, locale ) );
		}
		return length;
	}

	/// <summary>Resolves one fill repeat so the expression reaches the requested target length when possible.</summary>
	/// <param name="targetLength">The first array length.</param>
	/// <param name="locale">The byte-character locale.</param>
	/// <returns>A resolved expression.</returns>
	/// <exception cref="InvalidOperationException">More than one fill repeat is present.</exception>
	public TrSetExpression ResolveIndefiniteRepeat( ulong targetLength, TrByteLocale locale ) {
		ArgumentNullException.ThrowIfNull( locale );
		if ( 0 == this.IndefiniteRepeatCount ) {
			return this;
		}
		if ( 1 != this.IndefiniteRepeatCount ) {
			throw new InvalidOperationException( "only one [c*] repeat construct may appear in string2" );
		}
		ulong fixedLength = 0;
		foreach ( var element in this.myElements ) {
			if ( !element.IsIndefiniteRepeat ) {
				fixedLength = AddLength( fixedLength, GetElementLength( element, locale ) );
			}
		}
		var count = targetLength > fixedLength ? targetLength - fixedLength : 0;
		return new TrSetExpression(
			this.myElements.Select( value => value.IsIndefiniteRepeat ? value.ResolveRepeat( count ) : value )
		);
	}

	/// <summary>Creates byte-membership flags for the expression.</summary>
	/// <param name="locale">The byte-character locale.</param>
	/// <param name="complement">Whether to invert the resulting set.</param>
	/// <returns>A 256-entry membership array.</returns>
	public bool[] CreateMembership( TrByteLocale locale, bool complement ) {
		ArgumentNullException.ThrowIfNull( locale );
		var membership = new bool[byte.MaxValue + 1];
		foreach ( var element in this.myElements ) {
			switch ( element.Kind ) {
				case TrSetElementKind.Literal:
				case TrSetElementKind.EquivalenceClass:
				case TrSetElementKind.Repeat:
					membership[element.First] = true;
					break;
				case TrSetElementKind.Range:
					for ( var value = (int)element.First; value <= element.Last; value++ ) {
						membership[value] = true;
					}
					break;
				case TrSetElementKind.CharacterClass:
					for ( var value = 0; value <= byte.MaxValue; value++ ) {
						if ( locale.IsMember( (byte)value, element.CharacterClass ) ) {
							membership[value] = true;
						}
					}
					break;
				default:
					throw new InvalidOperationException( "Unknown tr set element." );
			}
		}
		if ( complement ) {
			for ( var index = 0; index < membership.Length; index++ ) {
				membership[index] = !membership[index];
			}
		}
		return membership;
	}

	/// <summary>Gets the byte at an expanded zero-based position.</summary>
	/// <param name="index">The expanded position.</param>
	/// <param name="locale">The byte-character locale.</param>
	/// <returns>The byte at the position.</returns>
	/// <exception cref="ArgumentOutOfRangeException">The position lies beyond the sequence.</exception>
	public byte GetByteAt( ulong index, TrByteLocale locale ) {
		ArgumentNullException.ThrowIfNull( locale );
		foreach ( var element in this.myElements ) {
			var length = GetElementLength( element, locale );
			if ( index < length ) {
				return GetElementByteAt( element, index, locale );
			}
			index -= length;
		}
		throw new ArgumentOutOfRangeException( nameof( index ) );
	}

	/// <summary>Gets the final byte in a nonempty expanded sequence.</summary>
	/// <param name="locale">The byte-character locale.</param>
	/// <returns>The final byte.</returns>
	/// <exception cref="InvalidOperationException">The sequence is empty.</exception>
	public byte GetLastByte( TrByteLocale locale ) {
		ArgumentNullException.ThrowIfNull( locale );
		for ( var index = this.myElements.Count - 1; index >= 0; index-- ) {
			var length = GetElementLength( this.myElements[index], locale );
			if ( 0 < length ) {
				return GetElementByteAt( this.myElements[index], length - 1, locale );
			}
		}
		throw new InvalidOperationException( "the set is empty" );
	}

	/// <summary>Gets whether the final nonempty construct is a character class.</summary>
	/// <param name="locale">The byte-character locale.</param>
	/// <returns><see langword="true"/> when a character class ends the expression.</returns>
	public bool EndsWithCharacterClass( TrByteLocale locale ) {
		ArgumentNullException.ThrowIfNull( locale );
		for ( var index = this.myElements.Count - 1; index >= 0; index-- ) {
			if ( 0 < GetElementLength( this.myElements[index], locale ) ) {
				return TrSetElementKind.CharacterClass == this.myElements[index].Kind;
			}
		}
		return false;
	}

	/// <summary>Gets whether every expanded byte is identical and the sequence is nonempty.</summary>
	/// <param name="locale">The byte-character locale.</param>
	/// <returns><see langword="true"/> for a homogeneous nonempty expression.</returns>
	public bool IsHomogeneous( TrByteLocale locale ) {
		ArgumentNullException.ThrowIfNull( locale );
		var length = this.GetLength( locale );
		if ( 0 == length ) {
			return false;
		}
		var first = this.GetByteAt( 0, locale );
		foreach ( var element in this.myElements ) {
			if ( !ElementIsHomogeneous( element, first, locale ) ) {
				return false;
			}
		}
		return true;
	}

	/// <summary>Enumerates the expanded bytes without materializing the sequence.</summary>
	/// <param name="locale">The byte-character locale.</param>
	/// <returns>The bytes in expansion order.</returns>
	public IEnumerable<byte> Enumerate( TrByteLocale locale ) {
		ArgumentNullException.ThrowIfNull( locale );
		foreach ( var element in this.myElements ) {
			var length = GetElementLength( element, locale );
			for ( ulong index = 0; index < length; index++ ) {
				yield return GetElementByteAt( element, index, locale );
			}
		}
	}

	/// <summary>Gets the expanded length of one construct.</summary>
	/// <param name="element">The construct.</param>
	/// <param name="locale">The byte-character locale.</param>
	/// <returns>The expanded length.</returns>
	/// <exception cref="InvalidOperationException">The repeat is unresolved.</exception>
	public static ulong GetElementLength( TrSetElement element, TrByteLocale locale ) {
		ArgumentNullException.ThrowIfNull( element );
		ArgumentNullException.ThrowIfNull( locale );
		return element.Kind switch {
			TrSetElementKind.Literal => 1,
			TrSetElementKind.Range => checked( (ulong)( element.Last - element.First ) + 1UL ),
			TrSetElementKind.CharacterClass => CountClass( element.CharacterClass, locale ),
			TrSetElementKind.EquivalenceClass => 1,
			TrSetElementKind.Repeat when element.IsIndefiniteRepeat => throw new InvalidOperationException( "an unresolved [c*] repeat remains" ),
			TrSetElementKind.Repeat => element.RepeatCount,
			_ => throw new InvalidOperationException( "Unknown tr set element." )
		};
	}

	private static ulong AddLength( ulong current, ulong addition ) {
		var result = checked( current + addition );
		if ( MaximumExpandedLength < result ) {
			throw new OverflowException( "too many characters in set" );
		}
		return result;
	}

	private static ulong CountClass( TrCharacterClass characterClass, TrByteLocale locale ) {
		ulong count = 0;
		for ( var value = 0; value <= byte.MaxValue; value++ ) {
			if ( locale.IsMember( (byte)value, characterClass ) ) {
				count++;
			}
		}
		return count;
	}

	private static byte GetElementByteAt( TrSetElement element, ulong index, TrByteLocale locale ) {
		return element.Kind switch {
			TrSetElementKind.Literal or TrSetElementKind.EquivalenceClass or TrSetElementKind.Repeat => element.First,
			TrSetElementKind.Range => checked( (byte)( element.First + index ) ),
			TrSetElementKind.CharacterClass => GetClassByteAt( element.CharacterClass, index, locale ),
			_ => throw new InvalidOperationException( "Unknown tr set element." )
		};
	}

	private static byte GetClassByteAt( TrCharacterClass characterClass, ulong index, TrByteLocale locale ) {
		for ( var value = 0; value <= byte.MaxValue; value++ ) {
			if ( !locale.IsMember( (byte)value, characterClass ) ) {
				continue;
			}
			if ( 0 == index ) {
				return (byte)value;
			}
			index--;
		}
		throw new ArgumentOutOfRangeException( nameof( index ) );
	}

	private static bool ElementIsHomogeneous( TrSetElement element, byte expected, TrByteLocale locale ) {
		var length = GetElementLength( element, locale );
		if ( 0 == length ) {
			return true;
		}
		if ( element.Kind is TrSetElementKind.Literal or TrSetElementKind.EquivalenceClass or TrSetElementKind.Repeat ) {
			return element.First == expected;
		}
		if ( 1 < length ) {
			return false;
		}
		return GetElementByteAt( element, 0, locale ) == expected;
	}
}
