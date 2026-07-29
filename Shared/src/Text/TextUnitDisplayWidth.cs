namespace Icod.CoreUtils.Shared.Text;

/// <summary>Calculates display widths for byte-preserving text units.</summary>
public static class TextUnitDisplayWidth {
	/// <summary>Gets a display width for a text unit.</summary>
	/// <param name="unit">The unit to measure.</param>
	/// <param name="provider">The decoded-scalar width provider.</param>
	/// <param name="opaqueByteWidth">The width assigned to opaque or invalid byte units.</param>
	/// <returns>The provider result for scalar units, or <paramref name="opaqueByteWidth"/> otherwise.</returns>
	/// <exception cref="ArgumentNullException">The provider is <see langword="null"/>.</exception>
	/// <exception cref="ArgumentOutOfRangeException">The opaque-byte width is negative.</exception>
	public static int GetWidth(
		TextUnit unit,
		IDisplayWidthProvider provider,
		int opaqueByteWidth = 1
	) {
		ArgumentNullException.ThrowIfNull( provider );
		if ( opaqueByteWidth < 0 ) {
			throw new ArgumentOutOfRangeException( nameof( opaqueByteWidth ) );
		}
		return unit.Scalar is { } scalar
			? provider.GetWidth( scalar )
			: opaqueByteWidth;
	}
}
