namespace Icod.CoreUtils.Shared.Text;

using System.Text;

/// <summary>Calculates terminal display-column widths for decoded Unicode scalars.</summary>
public interface IDisplayWidthProvider {
	/// <summary>Gets the display-column width of a Unicode scalar.</summary>
	/// <param name="scalar">The scalar to measure.</param>
	/// <returns>
	/// Zero for a zero-column scalar, one or two for printable scalar widths, or a negative value when
	/// the scalar is nonprinting or its width is indeterminate.
	/// </returns>
	int GetWidth( Rune scalar );
}
