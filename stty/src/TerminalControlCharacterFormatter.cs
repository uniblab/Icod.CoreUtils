namespace Icod.CoreUtils.Stty;

using System.Text;

/// <summary>
/// Formats POSIX terminal control characters using GNU-compatible visible
/// notation without assigning semantic names to native array positions.
/// </summary>
public static class TerminalControlCharacterFormatter {
	/// <summary>
	/// Formats one native control-character byte.
	/// </summary>
	/// <param name="value">The control-character value.</param>
	/// <param name="disabledValue">The host value that disables a control character.</param>
	/// <returns>The visible representation.</returns>
	public static string Format(
		byte value,
		byte disabledValue
	) {
		if ( disabledValue == value ) {
			return "<undef>";
		}
		var builder = new StringBuilder();
		var character = value;
		if ( 0x80 <= character ) {
			builder.Append( "M-" );
			character &= 0x7f;
		}
		if ( 0x20 > character ) {
			builder.Append( '^' );
			builder.Append( (char)( character + 0x40 ) );
		} else if ( 0x7f == character ) {
			builder.Append( "^?" );
		} else {
			builder.Append( (char)character );
		}
		return builder.ToString();
	}
}
