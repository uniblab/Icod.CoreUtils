using Path = global::System.IO.Path;
using System.Collections.ObjectModel;

namespace Icod.CoreUtils.Shared.FileSystem.Traversal;

/// <summary>
/// Represents a parsed pathname pattern with segment-aware <c>**</c> semantics.
/// </summary>
public sealed class PathnamePattern {
	private readonly ReadOnlyCollection<PathPatternSegment> _segments;

	private PathnamePattern(
		string pattern,
		PathnamePatternOptions options,
		string root,
		IList<PathPatternSegment> segments,
		bool requiresDirectory
	) {
		Pattern = pattern;
		Options = options;
		Root = root;
		_segments = new ReadOnlyCollection<PathPatternSegment>( segments );
		RequiresDirectory = requiresDirectory;
		HasMetacharacters = segments.Any( static segment => segment.HasMetacharacters );
	}

	/// <summary>
	/// Gets the source pattern.
	/// </summary>
	public string Pattern { get; }

	/// <summary>
	/// Gets the options used to parse and match the pattern.
	/// </summary>
	public PathnamePatternOptions Options { get; }

	/// <summary>
	/// Gets whether the pattern contains an unquoted wildcard or bracket expression.
	/// </summary>
	public bool HasMetacharacters { get; }

	/// <summary>
	/// Gets whether a trailing pathname separator requires a directory result.
	/// </summary>
	public bool RequiresDirectory { get; }

	/// <summary>
	/// Gets the parsed pathname root, or an empty string for a relative pattern.
	/// </summary>
	internal string Root { get; }

	/// <summary>
	/// Gets the parsed pathname segments.
	/// </summary>
	internal IReadOnlyList<PathPatternSegment> Segments => _segments;

	/// <summary>
	/// Parses a pathname pattern.
	/// </summary>
	/// <param name="pattern">The pathname pattern.</param>
	/// <param name="options">The optional parsing and matching options.</param>
	/// <returns>The parsed pattern.</returns>
	/// <exception cref="ArgumentNullException"><paramref name="pattern"/> is <see langword="null"/>.</exception>
	/// <exception cref="ArgumentException">The pathname root is invalid for the current platform.</exception>
	public static PathnamePattern Parse(
		string pattern,
		PathnamePatternOptions? options = null
	) {
		ArgumentNullException.ThrowIfNull( pattern );
		options ??= PathnamePatternOptions.Default;
		options.Validate();

		string root;
		try {
			root = System.IO.Path.GetPathRoot( pattern ) ?? string.Empty;
		} catch ( Exception exception ) when (
			exception is ArgumentException
			or NotSupportedException
			or PathTooLongException
		) {
			throw new ArgumentException( "The pathname pattern has an invalid platform root.", nameof( pattern ), exception );
		}

		var remainder = pattern[root.Length..];
		var separators = GetSeparators();
		var requiresDirectory = pattern.Length > root.Length && separators.Contains( pattern[^1] );
		var rawSegments = remainder.Split( separators, StringSplitOptions.RemoveEmptyEntries );
		var segments = new List<PathPatternSegment>( rawSegments.Length );
		foreach ( var rawSegment in rawSegments ) {
			segments.Add( ParseSegment( rawSegment, options ) );
		}
		return new PathnamePattern( pattern, options, root, segments, requiresDirectory );
	}

	/// <summary>
	/// Reconstructs the operational literal pathname represented by a pattern that has no metacharacters.
	/// </summary>
	/// <returns>The unquoted literal pathname.</returns>
	/// <exception cref="InvalidOperationException">The pattern contains metacharacters.</exception>
	internal string GetLiteralPath() {
		if ( HasMetacharacters ) {
			throw new InvalidOperationException( "A pathname pattern with metacharacters is not a literal pathname." );
		}
		var result = Root;
		foreach ( var segment in _segments ) {
			var literalSegment = new string( segment.Tokens.Select( static token => token.Literal ).ToArray() );
			result = result.Length == 0 ? literalSegment : System.IO.Path.Combine( result, literalSegment );
		}
		if ( RequiresDirectory && result.Length > 0 && !System.IO.Path.EndsInDirectorySeparator( result ) ) {
			result = string.Concat( result, System.IO.Path.DirectorySeparatorChar );
		}
		return result;
	}

	/// <summary>
	/// Determines whether a pathname matches this pattern.
	/// </summary>
	/// <param name="path">The pathname to test.</param>
	/// <returns><see langword="true"/> when the pathname matches.</returns>
	public bool IsMatch( string path ) {
		ArgumentNullException.ThrowIfNull( path );

		string pathRoot;
		try {
			pathRoot = System.IO.Path.GetPathRoot( path ) ?? string.Empty;
		} catch ( Exception exception ) when (
			exception is ArgumentException
			or NotSupportedException
			or PathTooLongException
		) {
			return false;
		}

		if ( (Root.Length == 0) != (pathRoot.Length == 0) ) {
			return false;
		}
		if ( Root.Length > 0 && !RootsEqual( Root, pathRoot, Options ) ) {
			return false;
		}

		var remainder = path[pathRoot.Length..];
		var pathSegments = remainder.Split( GetSeparators(), StringSplitOptions.RemoveEmptyEntries ).ToList();
		IReadOnlyList<PathPatternSegment> matchingSegments = _segments;
		if ( HasMetacharacters ) {
			pathSegments.RemoveAll( static segment => segment == "." );
			matchingSegments = _segments.Where( static segment => segment.LiteralValue != "." ).ToArray();
		}
		return MatchSegments( matchingSegments, pathSegments, Options );
	}

	/// <summary>
	/// Determines whether a single pathname segment matches a parsed segment.
	/// </summary>
	/// <param name="segment">The parsed segment.</param>
	/// <param name="name">The entry name.</param>
	/// <param name="options">The matching options.</param>
	/// <returns><see langword="true"/> when the entry name matches.</returns>
	internal static bool IsSegmentMatch(
		PathPatternSegment segment,
		string name,
		PathnamePatternOptions options
	) {
		ArgumentNullException.ThrowIfNull( name );

		if (
			name.Length > 0
			&& name[0] == '.'
			&& options.LeadingPeriodPolicy == LeadingPeriodPolicy.RequireExplicitPeriod
			&& !segment.ExplicitlyMatchesLeadingPeriod
		) {
			return false;
		}
		if ( segment.IsDoubleStar ) {
			return true;
		}

		var tokens = segment.Tokens;
		var current = new bool[name.Length + 1];
		current[0] = true;
		foreach ( var token in tokens ) {
			var next = new bool[name.Length + 1];
			switch ( token.Kind ) {
				case PathPatternTokenKind.Star:
					next[0] = current[0];
					for ( var index = 1; index <= name.Length; index++ ) {
						next[index] = current[index] || next[index - 1];
					}
					break;
				case PathPatternTokenKind.Question:
					for ( var index = 1; index <= name.Length; index++ ) {
						next[index] = current[index - 1];
					}
					break;
				case PathPatternTokenKind.Literal:
					for ( var index = 1; index <= name.Length; index++ ) {
						next[index] = current[index - 1]
							&& CharactersEqual( token.Literal, name[index - 1], options );
					}
					break;
				case PathPatternTokenKind.CharacterClass:
					for ( var index = 1; index <= name.Length; index++ ) {
						next[index] = current[index - 1]
							&& token.CharacterClass!.Matches( name[index - 1], options );
					}
					break;
				default:
					throw new InvalidOperationException( "The pathname pattern contains an unknown token kind." );
			}
			current = next;
		}

		return current[name.Length];
	}

	private static bool MatchSegments(
		IReadOnlyList<PathPatternSegment> patternSegments,
		IReadOnlyList<string> pathSegments,
		PathnamePatternOptions options
	) {
		var current = new bool[pathSegments.Count + 1];
		current[0] = true;
		foreach ( var segment in patternSegments ) {
			var next = new bool[pathSegments.Count + 1];
			if ( segment.IsDoubleStar ) {
				next[0] = current[0];
				for ( var index = 1; index <= pathSegments.Count; index++ ) {
					next[index] = current[index]
						|| (
							next[index - 1]
							&& CanDoubleStarMatchName( pathSegments[index - 1], options )
						);
				}
			} else {
				for ( var index = 1; index <= pathSegments.Count; index++ ) {
					next[index] = current[index - 1]
						&& IsSegmentMatch( segment, pathSegments[index - 1], options );
				}
			}
			current = next;
		}
		return current[pathSegments.Count];
	}

	/// <summary>
	/// Determines whether a recursive wildcard may consume one pathname segment.
	/// </summary>
	/// <param name="name">The pathname segment.</param>
	/// <param name="options">The matching options.</param>
	/// <returns><see langword="true"/> when the segment may be consumed.</returns>
	internal static bool CanDoubleStarMatchName(
		string name,
		PathnamePatternOptions options
	) => name.Length == 0
		|| name[0] != '.'
		|| options.LeadingPeriodPolicy == LeadingPeriodPolicy.WildcardMayMatch;

	private static PathPatternSegment ParseSegment(
		string segment,
		PathnamePatternOptions options
	) {
		if ( segment == "**" ) {
			return PathPatternSegment.CreateDoubleStar();
		}

		var tokens = new List<PathPatternToken>( segment.Length );
		var index = 0;
		while ( index < segment.Length ) {
			var current = segment[index];
			if (
				options.BackslashEscapes
				&& current == '\\'
				&& index + 1 < segment.Length
			) {
				tokens.Add( PathPatternToken.CreateLiteral( segment[index + 1] ) );
				index += 2;
				continue;
			}

			switch ( current ) {
				case '*':
					tokens.Add( PathPatternToken.CreateStar() );
					index++;
					break;
				case '?':
					tokens.Add( PathPatternToken.CreateQuestion() );
					index++;
					break;
				case '[':
					if ( TryParseCharacterClass( segment, index, options, out var characterClass, out var nextIndex ) ) {
						tokens.Add( PathPatternToken.CreateCharacterClass( characterClass ) );
						index = nextIndex;
					} else {
						tokens.Add( PathPatternToken.CreateLiteral( current ) );
						index++;
					}
					break;
				default:
					tokens.Add( PathPatternToken.CreateLiteral( current ) );
					index++;
					break;
			}
		}

		return new PathPatternSegment( segment, tokens );
	}

	private static bool TryParseCharacterClass(
		string segment,
		int openingIndex,
		PathnamePatternOptions options,
		out PathPatternCharacterClass characterClass,
		out int nextIndex
	) {
		var index = openingIndex + 1;
		var negated = false;
		if ( index < segment.Length && (segment[index] == '!' || segment[index] == '^') ) {
			negated = true;
			index++;
		}

		var ranges = new List<PathPatternCharacterRange>();
		var hasContent = false;
		if ( index < segment.Length && segment[index] == ']' ) {
			ranges.Add( new PathPatternCharacterRange( ']', ']' ) );
			index++;
			hasContent = true;
		}

		while ( index < segment.Length && segment[index] != ']' ) {
			var start = ReadClassCharacter( segment, ref index, options );
			hasContent = true;
			if (
				index < segment.Length - 1
				&& segment[index] == '-'
				&& segment[index + 1] != ']'
			) {
				index++;
				var end = ReadClassCharacter( segment, ref index, options );
				ranges.Add( new PathPatternCharacterRange( start, end ) );
			} else {
				ranges.Add( new PathPatternCharacterRange( start, start ) );
			}
		}

		if ( index >= segment.Length || segment[index] != ']' || !hasContent ) {
			characterClass = null!;
			nextIndex = openingIndex + 1;
			return false;
		}

		characterClass = new PathPatternCharacterClass( negated, ranges );
		nextIndex = index + 1;
		return true;
	}

	private static char ReadClassCharacter(
		string segment,
		ref int index,
		PathnamePatternOptions options
	) {
		var current = segment[index++];
		if (
			options.BackslashEscapes
			&& current == '\\'
			&& index < segment.Length
		) {
			return segment[index++];
		}
		return current;
	}

	private static bool RootsEqual(
		string first,
		string second,
		PathnamePatternOptions options
	) {
		var normalizedFirst = NormalizeRoot( first );
		var normalizedSecond = NormalizeRoot( second );
		return string.Equals( normalizedFirst, normalizedSecond, options.ResolveStringComparison() );
	}

	private static string NormalizeRoot( string root ) {
		if ( System.IO.Path.AltDirectorySeparatorChar == System.IO.Path.DirectorySeparatorChar ) {
			return root;
		}
		return root.Replace( System.IO.Path.AltDirectorySeparatorChar, System.IO.Path.DirectorySeparatorChar );
	}

	private static bool CharactersEqual(
		char first,
		char second,
		PathnamePatternOptions options
	) => options.ResolveCaseSensitive()
		? first == second
		: char.ToUpperInvariant( first ) == char.ToUpperInvariant( second );

	private static char[] GetSeparators() => System.IO.Path.AltDirectorySeparatorChar == System.IO.Path.DirectorySeparatorChar
		? new[] { System.IO.Path.DirectorySeparatorChar }
		: new[] { System.IO.Path.DirectorySeparatorChar, System.IO.Path.AltDirectorySeparatorChar };
}

/// <summary>
/// Represents one parsed pathname-pattern segment.
/// </summary>
internal sealed class PathPatternSegment {
	private readonly ReadOnlyCollection<PathPatternToken> _tokens;

	/// <summary>
	/// Initializes a parsed ordinary segment.
	/// </summary>
	/// <param name="source">The source segment.</param>
	/// <param name="tokens">The parsed tokens.</param>
	internal PathPatternSegment( string source, IList<PathPatternToken> tokens ) {
		Source = source;
		_tokens = new ReadOnlyCollection<PathPatternToken>( tokens );
		IsDoubleStar = false;
		HasMetacharacters = tokens.Any(
			static token => token.Kind != PathPatternTokenKind.Literal
		);
		LiteralValue = HasMetacharacters
			? null
			: new string( tokens.Select( static token => token.Literal ).ToArray() );
		ExplicitlyMatchesLeadingPeriod = tokens.Count > 0
			&& tokens[0].Kind == PathPatternTokenKind.Literal
			&& tokens[0].Literal == '.';
	}

	private PathPatternSegment() {
		Source = "**";
		_tokens = new ReadOnlyCollection<PathPatternToken>( Array.Empty<PathPatternToken>() );
		IsDoubleStar = true;
		HasMetacharacters = true;
		LiteralValue = null;
		ExplicitlyMatchesLeadingPeriod = false;
	}

	/// <summary>
	/// Gets the source segment.
	/// </summary>
	internal string Source { get; }

	/// <summary>
	/// Gets the parsed tokens.
	/// </summary>
	internal IReadOnlyList<PathPatternToken> Tokens => _tokens;

	/// <summary>
	/// Gets whether this segment is exactly the recursive <c>**</c> segment.
	/// </summary>
	internal bool IsDoubleStar { get; }

	/// <summary>
	/// Gets whether the segment contains unquoted metacharacters.
	/// </summary>
	internal bool HasMetacharacters { get; }

	/// <summary>
	/// Gets the unquoted literal value when the segment contains no metacharacters.
	/// </summary>
	internal string? LiteralValue { get; }

	/// <summary>
	/// Gets whether the segment begins with a literal period.
	/// </summary>
	internal bool ExplicitlyMatchesLeadingPeriod { get; }

	/// <summary>
	/// Creates the recursive segment representation.
	/// </summary>
	/// <returns>The recursive segment.</returns>
	internal static PathPatternSegment CreateDoubleStar() => new();
}

/// <summary>
/// Identifies the kind of a parsed pathname-pattern token.
/// </summary>
internal enum PathPatternTokenKind {
	/// <summary>A literal character.</summary>
	Literal = 0,
	/// <summary>A zero-or-more-character wildcard.</summary>
	Star = 1,
	/// <summary>A one-character wildcard.</summary>
	Question = 2,
	/// <summary>A bracket character class.</summary>
	CharacterClass = 3
}

/// <summary>
/// Represents one parsed pathname-pattern token.
/// </summary>
internal sealed class PathPatternToken {
	private PathPatternToken(
		PathPatternTokenKind kind,
		char literal,
		PathPatternCharacterClass? characterClass
	) {
		Kind = kind;
		Literal = literal;
		CharacterClass = characterClass;
	}

	/// <summary>Gets the token kind.</summary>
	internal PathPatternTokenKind Kind { get; }

	/// <summary>Gets the literal character when <see cref="Kind"/> is literal.</summary>
	internal char Literal { get; }

	/// <summary>Gets the character class when <see cref="Kind"/> is a class.</summary>
	internal PathPatternCharacterClass? CharacterClass { get; }

	/// <summary>Creates a literal token.</summary>
	/// <param name="value">The literal value.</param>
	/// <returns>The token.</returns>
	internal static PathPatternToken CreateLiteral( char value ) => new( PathPatternTokenKind.Literal, value, null );

	/// <summary>Creates a star token.</summary>
	/// <returns>The token.</returns>
	internal static PathPatternToken CreateStar() => new( PathPatternTokenKind.Star, default, null );

	/// <summary>Creates a question token.</summary>
	/// <returns>The token.</returns>
	internal static PathPatternToken CreateQuestion() => new( PathPatternTokenKind.Question, default, null );

	/// <summary>Creates a character-class token.</summary>
	/// <param name="characterClass">The character class.</param>
	/// <returns>The token.</returns>
	internal static PathPatternToken CreateCharacterClass( PathPatternCharacterClass characterClass ) =>
		new( PathPatternTokenKind.CharacterClass, default, characterClass );
}

/// <summary>
/// Represents one inclusive range in a bracket expression.
/// </summary>
/// <param name="Start">The inclusive range start.</param>
/// <param name="End">The inclusive range end.</param>
internal readonly record struct PathPatternCharacterRange( char Start, char End );

/// <summary>
/// Represents a parsed bracket expression.
/// </summary>
internal sealed class PathPatternCharacterClass {
	private readonly ReadOnlyCollection<PathPatternCharacterRange> _ranges;

	/// <summary>
	/// Initializes a character class.
	/// </summary>
	/// <param name="negated">Whether membership is negated.</param>
	/// <param name="ranges">The inclusive character ranges.</param>
	internal PathPatternCharacterClass(
		bool negated,
		IList<PathPatternCharacterRange> ranges
	) {
		Negated = negated;
		_ranges = new ReadOnlyCollection<PathPatternCharacterRange>( ranges );
	}

	/// <summary>Gets whether membership is negated.</summary>
	internal bool Negated { get; }

	/// <summary>
	/// Tests a character against the class.
	/// </summary>
	/// <param name="value">The character.</param>
	/// <param name="options">The comparison options.</param>
	/// <returns><see langword="true"/> when the character matches.</returns>
	internal bool Matches( char value, PathnamePatternOptions options ) {
		var comparisonValue = Fold( value, options );
		var contained = false;
		foreach ( var range in _ranges ) {
			var start = Fold( range.Start, options );
			var end = Fold( range.End, options );
			var lower = Math.Min( start, end );
			var upper = Math.Max( start, end );
			if ( comparisonValue >= lower && comparisonValue <= upper ) {
				contained = true;
				break;
			}
		}
		return Negated ? !contained : contained;
	}

	private static char Fold( char value, PathnamePatternOptions options ) => options.ResolveCaseSensitive()
		? value
		: char.ToUpperInvariant( value );
}
