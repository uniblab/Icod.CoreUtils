using Path = global::System.IO.Path;
namespace Icod.CoreUtils.Shared.Temporary;

/// <summary>Represents a parsed temporary-name template and its final replaceable run of <c>X</c> characters.</summary>
public sealed class TemporaryNameTemplate {
	private TemporaryNameTemplate(
		string pattern,
		int replacementStart,
		int replacementLength
	) {
		Pattern = pattern;
		ReplacementStart = replacementStart;
		ReplacementLength = replacementLength;
	}

	/// <summary>Gets the complete pathname pattern.</summary>
	public string Pattern { get; }

	/// <summary>Gets the index of the first replaceable <c>X</c>.</summary>
	public int ReplacementStart { get; }

	/// <summary>Gets the number of replaceable <c>X</c> characters.</summary>
	public int ReplacementLength { get; }

	/// <summary>Parses a GNU-style temporary-name template.</summary>
	/// <param name="template">The original template.</param>
	/// <param name="explicitSuffix">An explicitly supplied suffix, or <see langword="null"/> to infer one.</param>
	/// <param name="result">Receives the parsed template.</param>
	/// <param name="errorMessage">Receives a controlled validation message.</param>
	/// <returns><see langword="true"/> when parsing succeeded.</returns>
	public static bool TryParse(
		string template,
		string? explicitSuffix,
		out TemporaryNameTemplate? result,
		out string? errorMessage
	) {
		ArgumentNullException.ThrowIfNull( template );
		result = null;
		errorMessage = null;

		var pattern = template;
		var replacementEnd = template.Length;
		if ( null != explicitSuffix ) {
			if ( ( 0 == template.Length ) || ( 'X' != template[ ^1 ] ) ) {
				errorMessage = string.Concat(
					"with --suffix, template '",
					template,
					"' must end in X"
				);
				return false;
			}
			if ( ContainsDirectorySeparator( explicitSuffix ) ) {
				errorMessage = string.Concat(
					"invalid suffix '",
					explicitSuffix,
					"': contains a directory separator"
				);
				return false;
			}
			pattern = string.Concat( template, explicitSuffix );
		} else {
			var lastX = template.LastIndexOf( 'X' );
			if ( 0 <= lastX ) {
				replacementEnd = lastX + 1;
				var inferredSuffix = template.AsSpan( replacementEnd );
				if ( ContainsDirectorySeparator( inferredSuffix ) ) {
					errorMessage = string.Concat(
						"invalid suffix in template '",
						template,
						"': contains a directory separator"
					);
					return false;
				}
			}
		}

		var replacementStart = replacementEnd;
		while ( ( 0 < replacementStart ) && ( 'X' == template[ replacementStart - 1 ] ) ) {
			replacementStart--;
		}
		var replacementLength = replacementEnd - replacementStart;
		if ( 3 > replacementLength ) {
			errorMessage = string.Concat(
				"too few X's in template '",
				template,
				"'"
			);
			return false;
		}

		result = new TemporaryNameTemplate(
			pattern,
			replacementStart,
			replacementLength
		);
		return true;
	}

	/// <summary>Returns an equivalent template rooted beneath the supplied directory.</summary>
	/// <param name="directory">The destination directory.</param>
	/// <returns>The combined template.</returns>
	public TemporaryNameTemplate WithDirectory( string directory ) {
		ArgumentNullException.ThrowIfNull( directory );
		var combined = Path.Combine( directory, Pattern );
		var shift = combined.Length - Pattern.Length;
		return new TemporaryNameTemplate(
			combined,
			checked( ReplacementStart + shift ),
			ReplacementLength
		);
	}

	/// <summary>Renders a candidate pathname from replacement characters.</summary>
	/// <param name="replacement">Exactly <see cref="ReplacementLength"/> replacement characters.</param>
	/// <returns>The candidate pathname.</returns>
	public string Render( ReadOnlySpan<char> replacement ) {
		if ( ReplacementLength != replacement.Length ) {
			throw new ArgumentException(
				"The replacement length does not match the template.",
				nameof( replacement )
			);
		}
		var buffer = Pattern.ToCharArray();
		replacement.CopyTo( buffer.AsSpan( ReplacementStart, ReplacementLength ) );
		return new string( buffer );
	}

	private static bool ContainsDirectorySeparator( string value ) {
		return ContainsDirectorySeparator( value.AsSpan() );
	}

	private static bool ContainsDirectorySeparator( ReadOnlySpan<char> value ) {
		return ( 0 <= value.IndexOf( Path.DirectorySeparatorChar ) )
			|| ( 0 <= value.IndexOf( Path.AltDirectorySeparatorChar ) );
	}
}
