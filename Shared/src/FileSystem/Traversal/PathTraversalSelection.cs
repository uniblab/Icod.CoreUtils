using Path = global::System.IO.Path;
namespace Icod.CoreUtils.Shared.FileSystem.Traversal;

/// <summary>
/// Represents independent yield and descend decisions for one traversal entry.
/// </summary>
/// <param name="Yield">Whether the entry's ordinary event phases are exposed.</param>
/// <param name="Descend">Whether a directory's children are enumerated.</param>
public readonly record struct PathTraversalSelection( bool Yield, bool Descend ) {
	/// <summary>Gets a selection that yields entries and descends into directories.</summary>
	public static PathTraversalSelection IncludeAll { get; } = new( true, true );

	/// <summary>Gets a selection that suppresses entries and prunes directories.</summary>
	public static PathTraversalSelection ExcludeAll { get; } = new( false, false );
}

/// <summary>
/// Selects independent yield and descend behavior for traversal entries.
/// </summary>
public interface IPathTraversalSelector {
	/// <summary>
	/// Selects behavior for one entry.
	/// </summary>
	/// <param name="entry">The entry.</param>
	/// <returns>The selection.</returns>
	PathTraversalSelection Select( PathTraversalEntry entry );
}

/// <summary>
/// Identifies the pathname surface tested by a traversal filter rule.
/// </summary>
public enum PathMatchScope {
	/// <summary>Tests only the basename.</summary>
	BaseName = 0,
	/// <summary>Tests the path relative to the root.</summary>
	RootRelativePath = 1,
	/// <summary>Tests the complete operational pathname.</summary>
	WholePath = 2,
	/// <summary>Tests the display path and every separator-delimited suffix of that name.</summary>
	MatchingNameSuffix = 3
}

/// <summary>
/// Identifies which decisions a filter rule changes.
/// </summary>
[Flags]
public enum PathTraversalRuleTarget {
	/// <summary>The rule changes whether an entry is yielded.</summary>
	Yield = 1,
	/// <summary>The rule changes whether a directory is descended into.</summary>
	Descend = 2,
	/// <summary>The rule changes both decisions.</summary>
	YieldAndDescend = Yield | Descend
}

/// <summary>
/// Identifies the selection action applied by a rule.
/// </summary>
public enum PathTraversalRuleAction {
	/// <summary>Enables the targeted decisions.</summary>
	Include = 0,
	/// <summary>Disables the targeted decisions.</summary>
	Exclude = 1
}

/// <summary>
/// Restricts a rule to selected entry kinds.
/// </summary>
public enum PathTraversalRuleEntryKind {
	/// <summary>Matches every entry kind.</summary>
	Any = 0,
	/// <summary>Matches directories only.</summary>
	Directories = 1,
	/// <summary>Matches nondirectories only.</summary>
	NonDirectories = 2
}

/// <summary>
/// Represents one ordered pathname-based traversal selection rule.
/// </summary>
public sealed class PathTraversalFilterRule {
	/// <summary>
	/// Initializes a filter rule.
	/// </summary>
	/// <param name="pattern">The parsed pattern.</param>
	/// <param name="scope">The pathname matching scope.</param>
	/// <param name="action">The action.</param>
	/// <param name="targets">The decisions changed by the rule.</param>
	/// <param name="entryKind">The entry-kind restriction.</param>
	public PathTraversalFilterRule(
		PathnamePattern pattern,
		PathMatchScope scope,
		PathTraversalRuleAction action,
		PathTraversalRuleTarget targets = PathTraversalRuleTarget.YieldAndDescend,
		PathTraversalRuleEntryKind entryKind = PathTraversalRuleEntryKind.Any
	) {
		ArgumentNullException.ThrowIfNull( pattern );
		if ( !Enum.IsDefined( typeof( PathMatchScope ), scope ) ) {
			throw new ArgumentOutOfRangeException( nameof( scope ) );
		}
		if ( !Enum.IsDefined( typeof( PathTraversalRuleAction ), action ) ) {
			throw new ArgumentOutOfRangeException( nameof( action ) );
		}
		if ( !Enum.IsDefined( typeof( PathTraversalRuleEntryKind ), entryKind ) ) {
			throw new ArgumentOutOfRangeException( nameof( entryKind ) );
		}
		if ( targets == 0 || (targets & ~PathTraversalRuleTarget.YieldAndDescend) != 0 ) {
			throw new ArgumentOutOfRangeException( nameof( targets ) );
		}
		Pattern = pattern;
		Scope = scope;
		Action = action;
		Targets = targets;
		EntryKind = entryKind;
	}

	/// <summary>Gets the parsed pattern.</summary>
	public PathnamePattern Pattern { get; }

	/// <summary>Gets the pathname matching scope.</summary>
	public PathMatchScope Scope { get; }

	/// <summary>Gets the action.</summary>
	public PathTraversalRuleAction Action { get; }

	/// <summary>Gets the decisions changed by the rule.</summary>
	public PathTraversalRuleTarget Targets { get; }

	/// <summary>Gets the entry-kind restriction.</summary>
	public PathTraversalRuleEntryKind EntryKind { get; }
}

/// <summary>
/// Applies ordered pathname rules with last-matching-rule behavior.
/// </summary>
public sealed class PathTraversalRuleSelector : IPathTraversalSelector {
	private readonly IReadOnlyList<PathTraversalFilterRule> _rules;

	/// <summary>
	/// Gets a selector that yields and descends into every entry.
	/// </summary>
	public static PathTraversalRuleSelector AllowAll { get; } = new(
		Array.Empty<PathTraversalFilterRule>(),
		PathTraversalSelection.IncludeAll
	);

	/// <summary>
	/// Initializes an ordered selector.
	/// </summary>
	/// <param name="rules">The rules in encounter order.</param>
	/// <param name="defaultSelection">The initial selection before rules match.</param>
	public PathTraversalRuleSelector(
		IEnumerable<PathTraversalFilterRule> rules,
		PathTraversalSelection defaultSelection
	) {
		ArgumentNullException.ThrowIfNull( rules );
		var materializedRules = rules.ToArray();
		if ( materializedRules.Any( static rule => rule is null ) ) {
			throw new ArgumentException( "A traversal rule collection cannot contain null entries.", nameof( rules ) );
		}
		_rules = materializedRules;
		DefaultSelection = defaultSelection;
	}

	/// <summary>Gets the initial selection.</summary>
	public PathTraversalSelection DefaultSelection { get; }

	/// <inheritdoc/>
	public PathTraversalSelection Select( PathTraversalEntry entry ) {
		ArgumentNullException.ThrowIfNull( entry );
		var yield = DefaultSelection.Yield;
		var descend = DefaultSelection.Descend;
		foreach ( var rule in _rules ) {
			if ( !KindMatches( rule.EntryKind, entry.Kind ) || !PatternMatches( rule, entry ) ) {
				continue;
			}
			var value = rule.Action == PathTraversalRuleAction.Include;
			if ( (rule.Targets & PathTraversalRuleTarget.Yield) != 0 ) {
				yield = value;
			}
			if ( (rule.Targets & PathTraversalRuleTarget.Descend) != 0 ) {
				descend = value;
			}
		}
		return new PathTraversalSelection( yield, descend );
	}

	private static bool KindMatches(
		PathTraversalRuleEntryKind restriction,
		FileSystemEntryKind kind
	) => restriction switch {
		PathTraversalRuleEntryKind.Directories => kind == FileSystemEntryKind.Directory,
		PathTraversalRuleEntryKind.NonDirectories => kind != FileSystemEntryKind.Directory,
		_ => true
	};

	private static bool PatternMatches(
		PathTraversalFilterRule rule,
		PathTraversalEntry entry
	) {
		if ( rule.Pattern.RequiresDirectory && entry.Kind != FileSystemEntryKind.Directory ) {
			return false;
		}
		var candidate = rule.Scope switch {
			PathMatchScope.BaseName => entry.Name,
			PathMatchScope.RootRelativePath => entry.RelativePath,
			PathMatchScope.WholePath => entry.AccessPath,
			PathMatchScope.MatchingNameSuffix => entry.DisplayPath,
			_ => throw new ArgumentOutOfRangeException( nameof( rule ) )
		};
		if ( rule.Pattern.IsMatch( candidate ) ) {
			return true;
		}
		if ( rule.Scope != PathMatchScope.MatchingNameSuffix ) {
			return false;
		}

		for ( var index = 0; index < candidate.Length; index++ ) {
			if ( !IsSeparator( candidate[index] ) || index + 1 >= candidate.Length ) {
				continue;
			}
			if ( rule.Pattern.IsMatch( candidate[(index + 1)..] ) ) {
				return true;
			}
		}
		return false;
	}

	private static bool IsSeparator( char value ) => value == Path.DirectorySeparatorChar
		|| value == Path.AltDirectorySeparatorChar;
}
