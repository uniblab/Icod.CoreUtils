namespace Icod.CoreUtils.Tr;

/// <summary>Contains the compiled byte tables used by the streaming transformer.</summary>
internal sealed class TrTransformPlan {
	/// <summary>Initializes compiled transformation tables.</summary>
	/// <param name="translation">The 256-entry translation table.</param>
	/// <param name="deletion">The 256-entry deletion membership table.</param>
	/// <param name="squeezing">The 256-entry squeeze membership table.</param>
	public TrTransformPlan( byte[] translation, bool[] deletion, bool[] squeezing ) {
		if ( translation is null || translation.Length != byte.MaxValue + 1 ) {
			throw new ArgumentException( "A complete translation table is required.", nameof( translation ) );
		}
		if ( deletion is null || deletion.Length != byte.MaxValue + 1 ) {
			throw new ArgumentException( "A complete deletion table is required.", nameof( deletion ) );
		}
		if ( squeezing is null || squeezing.Length != byte.MaxValue + 1 ) {
			throw new ArgumentException( "A complete squeeze table is required.", nameof( squeezing ) );
		}
		this.Translation = translation;
		this.Deletion = deletion;
		this.Squeezing = squeezing;
	}

	/// <summary>Gets the translation table.</summary>
	public byte[] Translation { get; }

	/// <summary>Gets the deletion membership table.</summary>
	public bool[] Deletion { get; }

	/// <summary>Gets the squeeze membership table.</summary>
	public bool[] Squeezing { get; }
}

/// <summary>Validates parsed set expressions and compiles byte transformation tables.</summary>
internal static class TrTransformPlanBuilder {
	/// <summary>Builds a transformation plan.</summary>
	/// <param name="options">The validated command options.</param>
	/// <param name="string1">The parsed first expression.</param>
	/// <param name="string2">The parsed optional second expression.</param>
	/// <param name="locale">The byte-character locale.</param>
	/// <returns>The transformation plan.</returns>
	/// <exception cref="InvalidOperationException">The expressions are invalid for the selected operation.</exception>
	public static TrTransformPlan Build(
		TrOptions options,
		TrSetExpression string1,
		TrSetExpression? string2,
		TrByteLocale locale
	) {
		ArgumentNullException.ThrowIfNull( options );
		ArgumentNullException.ThrowIfNull( string1 );
		ArgumentNullException.ThrowIfNull( locale );
		if ( 0 < string1.IndefiniteRepeatCount ) {
			throw new InvalidOperationException( "the [c*] repeat construct may not appear in string1" );
		}
		var sourceLength = options.Complement
			? CountMembers( string1.CreateMembership( locale, complement: true ) )
			: string1.GetLength( locale );
		TrSetExpression? resolvedString2 = null;
		if ( null != string2 ) {
			if ( 1 < string2.IndefiniteRepeatCount ) {
				throw new InvalidOperationException( "only one [c*] repeat construct may appear in string2" );
			}
			if ( !options.Translating && 0 < string2.IndefiniteRepeatCount ) {
				throw new InvalidOperationException( "the [c*] construct may appear in string2 only when translating" );
			}
			resolvedString2 = string2.ResolveIndefiniteRepeat( sourceLength, locale );
		}
		if ( options.Translating ) {
			ValidateTranslation( options, string1, resolvedString2!, sourceLength, locale );
		}
		var translation = CreateIdentityTranslation();
		if ( options.Translating ) {
			CompileTranslation( options, string1, resolvedString2!, locale, translation );
		}
		var deletion = options.Delete
			? string1.CreateMembership( locale, options.Complement )
			: new bool[byte.MaxValue + 1];
		var squeezing = new bool[byte.MaxValue + 1];
		if ( options.SqueezeRepeats ) {
			if ( null != resolvedString2 ) {
				squeezing = resolvedString2.CreateMembership( locale, complement: false );
			} else {
				squeezing = string1.CreateMembership( locale, options.Complement );
			}
		}
		return new TrTransformPlan( translation, deletion, squeezing );
	}

	private static void ValidateTranslation(
		TrOptions options,
		TrSetExpression string1,
		TrSetExpression string2,
		ulong sourceLength,
		TrByteLocale locale
	) {
		if ( string2.HasEquivalenceClass ) {
			throw new InvalidOperationException( "[=c=] expressions may not appear in string2 when translating" );
		}
		if ( string2.HasRestrictedCharacterClass ) {
			throw new InvalidOperationException(
				"when translating, the only character classes that may appear in string2 are 'upper' and 'lower'"
			);
		}
		var lengths = ValidateCaseClassAlignment( options, string1, string2, sourceLength, locale );
		var translationSourceLength = lengths.SourceLength;
		var targetLength = lengths.TargetLength;
		if ( translationSourceLength > targetLength && !options.TruncateSet1 ) {
			if ( 0 == targetLength ) {
				throw new InvalidOperationException( "when not truncating set1, string2 must be non-empty" );
			}
			if ( string2.EndsWithCharacterClass( locale ) ) {
				throw new InvalidOperationException(
					"when translating with string1 longer than string2, string2 must not end with a character class"
				);
			}
		}
		var effectiveTargetLength = targetLength;
		if ( !options.TruncateSet1 && translationSourceLength > targetLength && 0 < targetLength ) {
			effectiveTargetLength = translationSourceLength;
		}
		if ( options.Complement && string1.HasCharacterClass
			&& !( effectiveTargetLength == translationSourceLength && string2.IsHomogeneous( locale ) ) ) {
			throw new InvalidOperationException(
				"when translating with complemented character classes, string2 must map all characters in the domain to one"
			);
		}
	}

	private static (ulong SourceLength, ulong TargetLength) ValidateCaseClassAlignment(
		TrOptions options,
		TrSetExpression string1,
		TrSetExpression string2,
		ulong sourceLength,
		TrByteLocale locale
	) {
		var targetLength = string2.GetLength( locale );
		if ( options.Complement || !string2.Elements.Any( IsCaseClass ) ) {
			return ( sourceLength, targetLength );
		}
		var source = new TrSetCursor( string1, locale );
		var target = new TrSetCursor( string2, locale );
		while ( !source.IsComplete && !target.IsComplete ) {
			if ( target.IsAtElementStart && IsCaseClass( target.CurrentElement ) ) {
				if ( !source.IsAtElementStart || !IsCaseClass( source.CurrentElement ) ) {
					throw new InvalidOperationException( "misaligned [:upper:] and/or [:lower:] construct" );
				}
				sourceLength = checked( sourceLength - source.RemainingInElement + 1 );
				targetLength = checked( targetLength - target.RemainingInElement + 1 );
				source.SkipCurrentElement();
				target.SkipCurrentElement();
				continue;
			}
			var count = Math.Min( source.RemainingInElement, target.RemainingInElement );
			source.Advance( count );
			target.Advance( count );
		}
		if ( source.IsComplete
			&& !target.IsComplete
			&& target.IsAtElementStart
			&& IsCaseClass( target.CurrentElement ) ) {
			throw new InvalidOperationException( "misaligned [:upper:] and/or [:lower:] construct" );
		}
		return ( sourceLength, targetLength );
	}

	private static bool IsCaseClass( TrSetElement element ) =>
		TrSetElementKind.CharacterClass == element.Kind
		&& element.CharacterClass is TrCharacterClass.Lower or TrCharacterClass.Upper;

	private static void CompileTranslation(
		TrOptions options,
		TrSetExpression string1,
		TrSetExpression string2,
		TrByteLocale locale,
		byte[] translation
	) {
		var targetLength = string2.GetLength( locale );
		var paddedTarget = 0 < targetLength ? string2.GetLastByte( locale ) : (byte)0;
		if ( options.Complement ) {
			var sourceMembership = string1.CreateMembership( locale, complement: true );
			ulong position = 0;
			for ( var value = 0; value <= byte.MaxValue; value++ ) {
				if ( !sourceMembership[value] ) {
					continue;
				}
				if ( !TryGetTarget( string2, position, targetLength, paddedTarget, options.TruncateSet1, locale, out var target ) ) {
					break;
				}
				translation[value] = target;
				position++;
			}
			return;
		}
		var source = new TrSetCursor( string1, locale );
		var targetCursor = new TrSetCursor( string2, locale );
		while ( !source.IsComplete ) {
			if ( targetCursor.IsComplete ) {
				if ( options.TruncateSet1 ) {
					break;
				}
				MapToConstantTarget( source, paddedTarget, translation );
				continue;
			}
			if ( source.IsAtElementStart
				&& targetCursor.IsAtElementStart
				&& IsCaseClass( source.CurrentElement )
				&& IsCaseClass( targetCursor.CurrentElement ) ) {
				ApplyCaseClass(
					translation,
					source.CurrentElement.CharacterClass,
					targetCursor.CurrentElement.CharacterClass,
					locale
				);
				source.SkipCurrentElement();
				targetCursor.SkipCurrentElement();
				continue;
			}
			var count = Math.Min( source.RemainingInElement, targetCursor.RemainingInElement );
			MapCursorRun( source, targetCursor, count, translation );
			source.Advance( count );
			targetCursor.Advance( count );
		}
	}

	private static void MapCursorRun(
		TrSetCursor source,
		TrSetCursor target,
		ulong count,
		byte[] translation
	) {
		if ( TrSetElementKind.Repeat == source.CurrentElement.Kind ) {
			translation[source.CurrentElement.First] = target.GetByteAt( count - 1 );
			return;
		}
		for ( ulong offset = 0; offset < count; offset++ ) {
			translation[source.GetByteAt( offset )] = target.GetByteAt( offset );
		}
	}

	private static void MapToConstantTarget(
		TrSetCursor source,
		byte target,
		byte[] translation
	) {
		if ( TrSetElementKind.Repeat == source.CurrentElement.Kind ) {
			translation[source.CurrentElement.First] = target;
			source.SkipCurrentElement();
			return;
		}
		var count = source.RemainingInElement;
		for ( ulong offset = 0; offset < count; offset++ ) {
			translation[source.GetByteAt( offset )] = target;
		}
		source.Advance( count );
	}

	private static void ApplyCaseClass(
		byte[] translation,
		TrCharacterClass sourceClass,
		TrCharacterClass targetClass,
		TrByteLocale locale
	) {
		for ( var value = 0; value <= byte.MaxValue; value++ ) {
			var source = (byte)value;
			if ( !locale.IsMember( source, sourceClass ) ) {
				continue;
			}
			translation[value] = sourceClass == TrCharacterClass.Lower && targetClass == TrCharacterClass.Upper
				? locale.ToUpper( source )
				: sourceClass == TrCharacterClass.Upper && targetClass == TrCharacterClass.Lower
					? locale.ToLower( source )
					: source;
		}
	}

	private static bool TryGetTarget(
		TrSetExpression target,
		ulong position,
		ulong targetLength,
		byte paddedTarget,
		bool truncate,
		TrByteLocale locale,
		out byte value
	) {
		if ( position < targetLength ) {
			value = target.GetByteAt( position, locale );
			return true;
		}
		if ( truncate ) {
			value = 0;
			return false;
		}
		value = paddedTarget;
		return true;
	}

	private static ulong CountMembers( IReadOnlyList<bool> membership ) {
		ulong count = 0;
		for ( var index = 0; index < membership.Count; index++ ) {
			if ( membership[index] ) {
				count++;
			}
		}
		return count;
	}

	private static byte[] CreateIdentityTranslation() {
		var result = new byte[byte.MaxValue + 1];
		for ( var value = 0; value <= byte.MaxValue; value++ ) {
			result[value] = (byte)value;
		}
		return result;
	}
}
