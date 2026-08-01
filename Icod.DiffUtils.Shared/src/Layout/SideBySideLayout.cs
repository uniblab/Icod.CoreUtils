namespace Icod.DiffUtils.Shared.Layout;

using System.Text;
using Icod.DiffUtils.Shared.Edits;

/// <summary>Builds reusable side-by-side rows and display-column constrained text.</summary>
public static class SideBySideLayout {
	/// <summary>Builds logical rows from an edit script.</summary>
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

	/// <summary>Expands tabs and truncates a string to a maximum display-column width.</summary>
	public static string FitText( string value, int width, int tabSize, bool expandTabs ) {
		ArgumentNullException.ThrowIfNull( value );
		ArgumentOutOfRangeException.ThrowIfNegative( width );
		ArgumentOutOfRangeException.ThrowIfNegativeOrZero( tabSize );
		var builder = new StringBuilder( Math.Min( value.Length, width ) );
		var column = 0;
		foreach ( var character in value ) {
			if ( width <= column ) {
				break;
			}
			if ( '\t' == character ) {
				var count = tabSize - ( column % tabSize );
				if ( expandTabs ) {
					count = Math.Min( count, width - column );
					builder.Append( ' ', count );
				} else if ( column + count <= width ) {
					builder.Append( '\t' );
				}
				column += count;
				continue;
			}
			builder.Append( character );
			column++;
		}
		return builder.ToString();
	}
}
