namespace Icod.CoreUtils.Shared.FileSystem.Traversal;

/// <summary>
/// Provides convenience methods for pathname-pattern matching without retaining a parsed pattern.
/// </summary>
public static class PathnamePatternMatcher {
	/// <summary>
	/// Determines whether a pathname matches a pattern.
	/// </summary>
	/// <param name="pattern">The pathname pattern.</param>
	/// <param name="path">The pathname to test.</param>
	/// <param name="options">The optional pattern options.</param>
	/// <returns><see langword="true"/> when the pathname matches.</returns>
	public static bool IsMatch(
		string pattern,
		string path,
		PathnamePatternOptions? options = null
	) => PathnamePattern.Parse( pattern, options ).IsMatch( path );

	/// <summary>
	/// Determines whether one entry name matches one pattern segment.
	/// </summary>
	/// <param name="patternSegment">The pattern segment.</param>
	/// <param name="name">The entry name.</param>
	/// <param name="options">The optional pattern options.</param>
	/// <returns><see langword="true"/> when the name matches.</returns>
	/// <exception cref="ArgumentException"><paramref name="patternSegment"/> contains a pathname separator.</exception>
	public static bool IsSegmentMatch(
		string patternSegment,
		string name,
		PathnamePatternOptions? options = null
	) {
		ArgumentNullException.ThrowIfNull( patternSegment );
		ArgumentNullException.ThrowIfNull( name );
		if (
			patternSegment.Contains( Path.DirectorySeparatorChar )
			|| (
				Path.AltDirectorySeparatorChar != Path.DirectorySeparatorChar
				&& patternSegment.Contains( Path.AltDirectorySeparatorChar )
			)
		) {
			throw new ArgumentException( "A segment pattern cannot contain a pathname separator.", nameof( patternSegment ) );
		}

		var parsed = PathnamePattern.Parse( patternSegment, options );
		return parsed.Segments.Count == 1
			&& PathnamePattern.IsSegmentMatch( parsed.Segments[0], name, parsed.Options );
	}
}
