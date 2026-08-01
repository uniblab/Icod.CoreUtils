namespace Icod.DiffUtils.Shared.Lines;

using System.Text;

/// <summary>Applies GNU Diffutils line-comparison transformations without changing output text.</summary>
public static class LineNormalizer {
	/// <summary>Produces the comparison key for one source line.</summary>
	public static string Normalize( ComparisonLine line, LineComparisonPolicy policy ) {
		ArgumentNullException.ThrowIfNull( line );
		ArgumentNullException.ThrowIfNull( policy );
		if ( policy.TabSize <= 0 ) {
			throw new ArgumentOutOfRangeException( nameof( policy ), "TabSize must be positive." );
		}
		var value = line.Content;
		if ( policy.StripTrailingCarriageReturn && 0 < value.Length && '\r' == value[^1] ) {
			value = value[..^1];
		}
		if ( policy.IgnoreTabExpansion && value.Contains( '\t' ) ) {
			value = ExpandTabs( value, policy.TabSize );
		}
		if ( policy.IgnoreAllSpace ) {
			var builder = new StringBuilder( value.Length );
			foreach ( var character in value ) {
				if ( !IsComparisonWhiteSpace( character ) ) {
					builder.Append( character );
				}
			}
			value = builder.ToString();
		} else if ( policy.IgnoreSpaceChange ) {
			value = CollapseWhiteSpace( value );
		} else if ( policy.IgnoreTrailingSpace ) {
			var end = value.Length;
			while ( 0 < end && IsComparisonWhiteSpace( value[end - 1] ) ) {
				end--;
			}
			if ( end != value.Length ) {
				value = value[..end];
			}
		}
		if ( policy.IgnoreCase ) {
			value = value.ToUpperInvariant();
		}
		return string.Concat( value, line.HasLineTerminator ? "\n" : "\0" );
	}

	/// <summary>Determines whether a line is blank under the selected white-space interpretation.</summary>
	public static bool IsBlank( ComparisonLine line, LineComparisonPolicy policy ) {
		ArgumentNullException.ThrowIfNull( line );
		ArgumentNullException.ThrowIfNull( policy );
		var value = line.Content;
		if ( policy.StripTrailingCarriageReturn && 0 < value.Length && '\r' == value[^1] ) {
			value = value[..^1];
		}
		return value.All( IsComparisonWhiteSpace );
	}

	private static string CollapseWhiteSpace( string value ) {
		var builder = new StringBuilder( value.Length );
		var pendingSpace = false;
		foreach ( var character in value ) {
			if ( IsComparisonWhiteSpace( character ) ) {
				pendingSpace = true;
				continue;
			}
			if ( pendingSpace ) {
				builder.Append( ' ' );
				pendingSpace = false;
			}
			builder.Append( character );
		}
		return builder.ToString();
	}

	private static string ExpandTabs( string value, int tabSize ) {
		var builder = new StringBuilder( value.Length );
		var column = 0;
		foreach ( var character in value ) {
			if ( '\t' == character ) {
				var count = tabSize - ( column % tabSize );
				builder.Append( ' ', count );
				column += count;
				continue;
			}
			builder.Append( character );
			column = '\b' == character ? Math.Max( 0, column - 1 ) : column + 1;
		}
		return builder.ToString();
	}

	private static bool IsComparisonWhiteSpace( char character ) {
		return character is ' ' or '\t' or '\r' or '\v' or '\f';
	}
}
