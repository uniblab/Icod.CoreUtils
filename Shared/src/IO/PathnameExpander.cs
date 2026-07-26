namespace Icod.CoreUtils.Shared.IO;

using System.IO.Enumeration;

/// <summary>
/// Expands pathname operands containing <c>*</c>, <c>?</c>, and recursive
/// <c>**</c> path segments.
/// </summary>
/// <remarks>
/// A single asterisk or question mark never crosses a directory separator.
/// A segment consisting of two asterisks matches zero or more directory
/// levels. Results are returned in deterministic ordinal order within each
/// operand. Unmatched patterns are preserved by default, matching the common
/// shell behavior when nullglob is disabled.
/// </remarks>
public static class PathnameExpander {

	/// <summary>
	/// Expands a sequence of pathname operands.
	/// </summary>
	public static IReadOnlyList<string> Expand(
		IEnumerable<string> operands,
		PathnameExpansionOptions? options = null
	) {
		ArgumentNullException.ThrowIfNull(
			operands
		);
		options ??= new PathnameExpansionOptions();
		if (
			!options.IncludeFiles
			&& !options.IncludeDirectories
		) {
			return Array.Empty<string>();
		}

		var output = new List<string>();
		foreach ( var operand in operands ) {
			ArgumentNullException.ThrowIfNull(
				operand
			);
			if (
				"-" == operand
				|| !ContainsWildcard(
					operand
				)
			) {
				output.Add(
					operand
				);
				continue;
			}

			var matches = ExpandOne(
				operand,
				options
			);
			if ( 0 == matches.Count ) {
				if ( options.PreserveUnmatchedPatterns ) {
					output.Add(
						operand
					);
				}
			} else {
				output.AddRange(
					matches
				);
			}
		}
		return output;
	}

	/// <summary>
	/// Returns whether a pathname contains a supported wildcard.
	/// </summary>
	public static bool ContainsWildcard(
		string value
	) {
		ArgumentNullException.ThrowIfNull(
			value
		);
		return value.IndexOfAny(
			new char[] {
				'*',
				'?'
			}
		) >= 0;
	}

	private static IReadOnlyList<string> ExpandOne(
		string operand,
		PathnameExpansionOptions options
	) {
		var normalized = NormalizeSeparators(
			operand
		);
		var rooted = Path.IsPathRooted(
			normalized
		);
		var root = Path.GetPathRoot(
			normalized
		) ?? string.Empty;
		var remainder = rooted
			? normalized.Substring(
				root.Length
			)
			: normalized
		;
		var segments = remainder.Split(
			Path.DirectorySeparatorChar,
			StringSplitOptions.RemoveEmptyEntries
		);
		if ( 0 == segments.Length ) {
			return Array.Empty<string>();
		}

		var firstWildcard = Array.FindIndex(
			segments,
			ContainsWildcard
		);
		if ( firstWildcard < 0 ) {
			return Array.Empty<string>();
		}

		var baseDirectory = Path.GetFullPath(
			options.BaseDirectory
		);
		var searchRoot = rooted
			? root
			: baseDirectory
		;
		for (
			var index = 0;
			index < firstWildcard;
			index++
		) {
			searchRoot = Path.Combine(
				searchRoot,
				segments[ index ]
			);
		}
		if ( !Directory.Exists( searchRoot ) ) {
			return Array.Empty<string>();
		}

		var matches = new List<string>();
		MatchSegments(
			searchRoot,
			segments,
			firstWildcard,
			options,
			matches
		);

		var comparer = OperatingSystem.IsWindows()
			? StringComparer.OrdinalIgnoreCase
			: StringComparer.Ordinal
		;
		matches.Sort(
			comparer
		);

		if ( rooted ) {
			return matches;
		}

		var preserveDotPrefix = operand.StartsWith(
			string.Concat(
				".",
				Path.DirectorySeparatorChar
			),
			StringComparison.Ordinal
		) || operand.StartsWith(
			"./",
			StringComparison.Ordinal
		);
		return matches.Select(
			match => {
				var relative = Path.GetRelativePath(
					baseDirectory,
					match
				);
				return preserveDotPrefix
					? Path.Combine(
						".",
						relative
					)
					: relative
				;
			}
		).ToArray();
	}

	private static void MatchSegments(
		string currentPath,
		IReadOnlyList<string> segments,
		int segmentIndex,
		PathnameExpansionOptions options,
		ICollection<string> matches
	) {
		if ( segmentIndex == segments.Count ) {
			if (
				options.IncludeFiles
				&& File.Exists( currentPath )
			) {
				matches.Add(
					Path.GetFullPath(
						currentPath
					)
				);
			} else if (
				options.IncludeDirectories
				&& Directory.Exists( currentPath )
			) {
				matches.Add(
					Path.GetFullPath(
						currentPath
					)
				);
			}
			return;
		}

		var segment = segments[ segmentIndex ];
		if ( "**" == segment ) {
			MatchSegments(
				currentPath,
				segments,
				segmentIndex + 1,
				options,
				matches
			);
			foreach ( var entry in EnumerateEntries( currentPath ) ) {
				if ( File.Exists( entry ) ) {
					if (
						segmentIndex == segments.Count - 1
						&& options.IncludeFiles
					) {
						matches.Add(
							Path.GetFullPath(
								entry
							)
						);
					}
					continue;
				}
				if ( !Directory.Exists( entry ) ) {
					continue;
				}
				if (
					segmentIndex == segments.Count - 1
					&& options.IncludeDirectories
				) {
					matches.Add(
						Path.GetFullPath(
							entry
						)
					);
				}
				if (
					!options.FollowDirectorySymlinks
					&& IsDirectorySymlink(
						entry
					)
				) {
					continue;
				}
				MatchSegments(
					entry,
					segments,
					segmentIndex,
					options,
					matches
				);
			}
			return;
		}

		var lastSegment = segmentIndex == segments.Count - 1;
		if ( !ContainsWildcard( segment ) ) {
			var candidate = Path.Combine(
				currentPath,
				segment
			);
			if (
				lastSegment
				|| Directory.Exists(
					candidate
				)
			) {
				MatchSegments(
					candidate,
					segments,
					segmentIndex + 1,
					options,
					matches
				);
			}
			return;
		}

		foreach ( var entry in EnumerateEntries( currentPath ) ) {
			var name = Path.GetFileName(
				entry
			);
			if (
				!FileSystemName.MatchesSimpleExpression(
					segment,
					name,
					ignoreCase: OperatingSystem.IsWindows()
				)
			) {
				continue;
			}
			if (
				!lastSegment
				&& !Directory.Exists(
					entry
				)
			) {
				continue;
			}
			MatchSegments(
				entry,
				segments,
				segmentIndex + 1,
				options,
				matches
			);
		}
	}

	private static IEnumerable<string> EnumerateEntries(
		string directory
	) {
		try {
			return Directory.EnumerateFileSystemEntries(
				directory,
				"*",
				new EnumerationOptions {
					AttributesToSkip = 0,
					IgnoreInaccessible = true,
					RecurseSubdirectories = false,
					ReturnSpecialDirectories = false
				}
			).ToArray();
		} catch (
			Exception ex
		) when (
			ex is IOException
				or UnauthorizedAccessException
				or DirectoryNotFoundException
		) {
			return Array.Empty<string>();
		}
	}

	private static IEnumerable<string> EnumerateDirectories(
		string directory
	) {
		return EnumerateEntries(
			directory
		).Where(
			Directory.Exists
		);
	}

	private static bool IsDirectorySymlink(
		string path
	) {
		try {
			return 0 != (
				File.GetAttributes(
					path
				) & FileAttributes.ReparsePoint
			);
		} catch (
			Exception ex
		) when (
			ex is IOException
				or UnauthorizedAccessException
		) {
			return true;
		}
	}

	private static string NormalizeSeparators(
		string value
	) {
		if ( Path.DirectorySeparatorChar == '\\' ) {
			return value.Replace(
				'/',
				'\\'
			);
		}
		return value.Replace(
			'\\',
			'/'
		);
	}

}
