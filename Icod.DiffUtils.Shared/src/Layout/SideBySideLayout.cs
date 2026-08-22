namespace Icod.DiffUtils.Shared.Layout;

using System.Globalization;
using System.Text;
using Icod.DiffUtils.Shared.Edits;

/// <summary>Builds reusable side-by-side rows and display-column constrained text.</summary>
public static class SideBySideLayout {
	/// <summary>Builds logical rows from an edit script.</summary>
	/// <param name="script">The two-way edit script to align.</param>
	/// <returns>The logical side-by-side rows in source order.</returns>
	public static IReadOnlyList<SideBySideRow> BuildRows( EditScript script ) {
		ArgumentNullException.ThrowIfNull( script );
		var rows = new List<SideBySideRow>();
		var operations = script.Operations;
		var index = 0;
		while ( index < operations.Count ) {
			if ( EditOperationKind.Equal == operations[index].Kind ) {
				rows.Add( new SideBySideRow( operations[index].Line.Content, ' ', operations[index].Line.Content, true ) );
				index++;
				continue;
			}
			var deletes = new List<string>();
			var inserts = new List<string>();
			while ( index < operations.Count && EditOperationKind.Equal != operations[index].Kind ) {
				var operation = operations[index++];
				if ( EditOperationKind.Delete == operation.Kind ) {
					deletes.Add( operation.Line.Content );
				} else {
					inserts.Add( operation.Line.Content );
				}
			}
			var count = Math.Max( deletes.Count, inserts.Count );
			for ( var row = 0; row < count; row++ ) {
				var left = row < deletes.Count ? deletes[row] : null;
				var right = row < inserts.Count ? inserts[row] : null;
				rows.Add( new SideBySideRow(
					left,
					null == left ? '>' : null == right ? '<' : '|',
					right,
					false
				) );
			}
		}
		return rows.AsReadOnly();
	}

	/// <summary>Calculates GNU-compatible field, separator, and right-column positions.</summary>
	/// <param name="width">The maximum total display width.</param>
	/// <param name="tabSize">The output tab interval.</param>
	/// <param name="expandTabs">Whether alignment is performed entirely with spaces.</param>
	/// <returns>The calculated side-by-side geometry.</returns>
	public static SideBySideGeometry CalculateGeometry( int width, int tabSize, bool expandTabs ) {
		ArgumentOutOfRangeException.ThrowIfNegativeOrZero( width );
		ArgumentOutOfRangeException.ThrowIfNegativeOrZero( tabSize );
		var alignment = expandTabs ? 1L : tabSize;
		var alignmentPlusGutter = alignment + 3L;
		var unalignedOffset = ( width >> 1 )
			+ ( alignmentPlusGutter >> 1 )
			+ ( 0L != ( width & alignmentPlusGutter & 1L ) ? 1L : 0L );
		var column2Offset = unalignedOffset - ( unalignedOffset % alignment );
		var halfWidth = Math.Max( 0L, Math.Min( column2Offset - 3L, (long)width - column2Offset ) );
		if ( 0L == halfWidth ) {
			column2Offset = width;
		}
		var separatorColumn = ( halfWidth + column2Offset - 1L ) >> 1;
		return new SideBySideGeometry(
			checked( (int)halfWidth ),
			checked( (int)column2Offset ),
			checked( (int)separatorColumn )
		);
	}

	/// <summary>Formats one logical row within the requested total display width.</summary>
	/// <param name="left">The optional left-side text.</param>
	/// <param name="marker">The gutter marker, or a space for a common two-column row.</param>
	/// <param name="right">The optional right-side text.</param>
	/// <param name="width">The maximum total display width.</param>
	/// <param name="tabSize">The output tab interval.</param>
	/// <param name="expandTabs">Whether all layout padding and input tabs are emitted as spaces.</param>
	/// <returns>The formatted row without a line terminator.</returns>
	public static string FormatRow(
		string? left,
		char marker,
		string? right,
		int width,
		int tabSize,
		bool expandTabs
	) {
		var geometry = CalculateGeometry( width, tabSize, expandTabs );
		var leftText = FitText( left ?? string.Empty, geometry.HalfWidth, tabSize, expandTabs );
		var rightText = FitText( right ?? string.Empty, geometry.HalfWidth, tabSize, expandTabs );
		var builder = new StringBuilder( Math.Max( 16, leftText.Length + rightText.Length + 3 ) );
		builder.Append( leftText );
		var column = MeasureDisplayWidth( leftText, tabSize );
		if ( ' ' == marker ) {
			if ( null != right ) {
				AppendPadding( builder, ref column, geometry.Column2Offset, tabSize, expandTabs );
				builder.Append( rightText );
			}
			return builder.ToString();
		}

		AppendPadding( builder, ref column, geometry.SeparatorColumn, tabSize, expandTabs );
		builder.Append( marker );
		column++;
		if ( null != right ) {
			AppendPadding( builder, ref column, geometry.Column2Offset, tabSize, expandTabs );
			builder.Append( rightText );
		}
		return builder.ToString();
	}

	/// <summary>Expands tabs when requested and truncates text to a display-column width.</summary>
	/// <param name="value">The source text.</param>
	/// <param name="width">The maximum display width.</param>
	/// <param name="tabSize">The tab-stop interval.</param>
	/// <param name="expandTabs">Whether tabs are emitted as spaces.</param>
	/// <returns>The fitted text.</returns>
	public static string FitText( string value, int width, int tabSize, bool expandTabs ) {
		ArgumentNullException.ThrowIfNull( value );
		ArgumentOutOfRangeException.ThrowIfNegative( width );
		ArgumentOutOfRangeException.ThrowIfNegativeOrZero( tabSize );
		var builder = new StringBuilder( Math.Min( value.Length, width ) );
		long column = 0;
		foreach ( var rune in value.EnumerateRunes() ) {
			if ( '\t' == rune.Value ) {
				var count = (long)tabSize - ( column % tabSize );
				if ( expandTabs ) {
					count = Math.Min( count, Math.Max( 0L, width - column ) );
					builder.Append( ' ', checked( (int)count ) );
				} else if ( column + count < width ) {
					builder.Append( '\t' );
				}
				column += count;
				continue;
			}
			if ( '\b' == rune.Value ) {
				if ( column <= width ) {
					builder.Append( '\b' );
				}
				column = Math.Max( 0L, column - 1L );
				continue;
			}
			var runeWidth = GetRuneWidth( rune );
			if ( 0 == runeWidth ) {
				if ( column <= width ) {
					builder.Append( rune.ToString() );
				}
				continue;
			}
			if ( width < column + runeWidth ) {
				column += runeWidth;
				continue;
			}
			builder.Append( rune.ToString() );
			column += runeWidth;
		}
		return builder.ToString();
	}

	/// <summary>Measures text in terminal display columns using the selected tab interval.</summary>
	/// <param name="value">The text to measure.</param>
	/// <param name="tabSize">The tab-stop interval.</param>
	/// <returns>The terminal display width.</returns>
	public static int MeasureDisplayWidth( string value, int tabSize ) {
		ArgumentNullException.ThrowIfNull( value );
		ArgumentOutOfRangeException.ThrowIfNegativeOrZero( tabSize );
		long column = 0;
		foreach ( var rune in value.EnumerateRunes() ) {
			if ( '\t' == rune.Value ) {
				column += (long)tabSize - ( column % tabSize );
			} else if ( '\b' == rune.Value ) {
				column = Math.Max( 0L, column - 1L );
			} else {
				column += GetRuneWidth( rune );
			}
			if ( int.MaxValue <= column ) {
				return int.MaxValue;
			}
		}
		return (int)column;
	}

	/// <summary>Pads text with spaces until it occupies the requested display width.</summary>
	/// <param name="value">The text to pad.</param>
	/// <param name="width">The requested display width.</param>
	/// <param name="tabSize">The tab-stop interval used to measure existing text.</param>
	/// <returns>The original or space-padded text.</returns>
	public static string PadRight( string value, int width, int tabSize ) {
		ArgumentNullException.ThrowIfNull( value );
		ArgumentOutOfRangeException.ThrowIfNegative( width );
		var current = MeasureDisplayWidth( value, tabSize );
		return current < width ? string.Concat( value, new string( ' ', width - current ) ) : value;
	}

	/// <summary>Creates padding that advances from one display column to another.</summary>
	/// <param name="fromColumn">The current zero-based display column.</param>
	/// <param name="toColumn">The target zero-based display column.</param>
	/// <param name="tabSize">The output tab interval.</param>
	/// <param name="expandTabs">Whether to emit spaces instead of tabs.</param>
	/// <returns>The required tab-and-space padding.</returns>
	public static string CreatePadding(
		int fromColumn,
		int toColumn,
		int tabSize,
		bool expandTabs
	) {
		ArgumentOutOfRangeException.ThrowIfNegative( fromColumn );
		ArgumentOutOfRangeException.ThrowIfNegative( toColumn );
		ArgumentOutOfRangeException.ThrowIfNegativeOrZero( tabSize );
		if ( toColumn <= fromColumn ) {
			return string.Empty;
		}
		var builder = new StringBuilder( toColumn - fromColumn );
		AppendPadding( builder, ref fromColumn, toColumn, tabSize, expandTabs );
		return builder.ToString();
	}

	private static void AppendPadding(
		StringBuilder builder,
		ref int column,
		int targetColumn,
		int tabSize,
		bool expandTabs
	) {
		if ( !expandTabs ) {
			var nextTabStop = (long)column + tabSize - ( column % tabSize );
			while ( nextTabStop <= targetColumn ) {
				builder.Append( '\t' );
				column = checked( (int)nextTabStop );
				nextTabStop += tabSize;
			}
		}
		if ( column < targetColumn ) {
			builder.Append( ' ', targetColumn - column );
			column = targetColumn;
		}
	}

	private static int GetRuneWidth( Rune rune ) {
		var category = Rune.GetUnicodeCategory( rune );
		return category is UnicodeCategory.NonSpacingMark
			or UnicodeCategory.EnclosingMark
			or UnicodeCategory.Format
			or UnicodeCategory.Control
			? 0
			: IsWide( rune.Value ) ? 2 : 1;
	}

	private static bool IsWide( int value ) {
		return value is >= 0x1100 and <= 0x115F
			or >= 0x2329 and <= 0x232A
			or >= 0x2E80 and <= 0xA4CF
			or >= 0xAC00 and <= 0xD7A3
			or >= 0xF900 and <= 0xFAFF
			or >= 0xFE10 and <= 0xFE19
			or >= 0xFE30 and <= 0xFE6F
			or >= 0xFF00 and <= 0xFF60
			or >= 0xFFE0 and <= 0xFFE6
			or >= 0x1F300 and <= 0x1FAFF
			or >= 0x20000 and <= 0x3FFFD;
	}
}
