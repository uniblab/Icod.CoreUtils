namespace Icod.CoreUtils.Shared.Escapes;

/// <summary>Provides command-neutral scanning of one backslash and its following source position.</summary>
internal static class EscapeSequenceScanner {

	/// <summary>Scans an escape beginning at the current backslash.</summary>
	/// <param name="value">The managed source string.</param>
	/// <param name="index">The backslash offset on entry and designator offset on return.</param>
	/// <param name="sequence">The scanned source offsets.</param>
	/// <returns><see langword="true"/> when the current character was a backslash.</returns>
	internal static bool TryRead(
		string value,
		ref int index,
		out EscapeSequence sequence
	) {
		ArgumentNullException.ThrowIfNull( value );
		if ( index < 0 || value.Length <= index ) {
			throw new ArgumentOutOfRangeException( nameof( index ) );
		}
		if ( '\\' != value[index] ) {
			sequence = default;
			return false;
		}
		var backslash = index;
		index++;
		sequence = new EscapeSequence(
			backslash,
			index,
			value.Length <= index
		);
		return true;
	}

}
