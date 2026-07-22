// Original behavior/reference: GNU coreutils
// Ported to .NET by Timothy J. Bruce <uniblab@hotmail.com>

namespace Icod.CoreUtils.Ls;

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text;

/// <summary>
/// ls: list directory contents (best-effort POSIX behavior using only BCL).
/// Supported options: -a, -A, -l, -h, -R, -r, -t, -S, -1, -d
/// POSIX semantics are approximated where not directly available from BCL.
/// Where true POSIX semantics are impossible to provide with BCL alone, best-effort fallbacks are used.
/// </summary>
public static partial class Command {
	public static int Run( string[] args, TextReader? stdin = null, TextWriter? stdout = null, TextWriter? stderr = null ) {
		stdout ??= Console.Out;
		stderr ??= Console.Error;

		// Parse options. Use SharedUtils.ParseOptions semantics: option spec string where ':' indicates value
		var (flags, _, rest) = Icod.CoreUtils.Shared.SharedUtils.ParseOptions( args, "aAlhrtS1dR" );

		var showAll = flags.Contains( 'a' );
		var almostAll = flags.Contains( 'A' );
		var longListing = flags.Contains( 'l' );
		var humanReadable = flags.Contains( 'h' );
		var recursive = flags.Contains( 'R' );
		var reverse = flags.Contains( 'r' );
		var sortByTime = flags.Contains( 't' );
		var sortBySize = flags.Contains( 'S' );
		var onePerLine = flags.Contains( '1' );
		var listDirectoriesSelf = flags.Contains( 'd' );

		// If no paths, default to current directory
		if ( rest.Length == 0 ) {
			rest = new[] { "." };
		}

		var exit = 0;
		try {
			var entriesToList = new List<PathEntry>();

			foreach ( var path in rest ) {
				if ( path == "-" ) {
					stderr.WriteLine( "ls: '-' (stdin) is not supported as a path in this implementation" );
					exit = 1;
					continue;
				}

				try {
					var attr = File.GetAttributes( path );
					var isDir = ( attr & FileAttributes.Directory ) != 0;

					if ( isDir && !listDirectoriesSelf ) {
						// list contents of directory
						var dirInfo = new DirectoryInfo( path );
						var children = dirInfo.EnumerateFileSystemInfos();
						foreach ( var fi in children ) {
							if ( !showAll ) {
								if ( IsHiddenOrDot( fi ) || ( almostAll && IsDotOnly( fi ) ) ) {
									continue;
								}
							}

							entriesToList.Add( new PathEntry( fi.FullName, fi ) );
						}

						// If multiple arguments, print header for each dir
						if ( rest.Length > 1 ) {
							// Separator header will be printed during output using entries grouped by directory
						}
					} else {
						// list the path itself
						FileSystemInfo? fsi = isDir ? (FileSystemInfo)new DirectoryInfo( path ) : new FileInfo( path );
						if ( !showAll ) {
							if ( IsHiddenOrDot( fsi ) || ( almostAll && IsDotOnly( fsi ) ) ) {
								continue;
							}
						}

						entriesToList.Add( new PathEntry( path, fsi ) );
					}
				} catch ( Exception ex ) {
					stderr.WriteLine( $"ls: cannot access '{path}': {ex.Message}" );
					exit = 1;
				}
			}

			// If input included directories and multiple args, we should print directory headers and contents grouped by directory.
			// For simplicity, if more than one input and any input was a directory (and not -d), we will display per-directory groupings.
			var groups = GroupEntriesByRequestedPath( rest, listDirectoriesSelf );

			var firstGroup = true;
			foreach ( var group in groups ) {
				if ( groups.Count > 1 ) {
					if ( !firstGroup ) {
						stdout.WriteLine();
					}

					stdout.WriteLine( $"{group.Key}:" );
				}

				var items = GetEntriesForRequestedPath( group.Key, group.Value, listDirectoriesSelf );
				// Sort
				var sorted = SortEntries( items, sortByTime, sortBySize, reverse );

				// Output
				if ( longListing ) {
					var rows = new List<string>();
					var maxLinks = 0;
					var maxOwner = 0;
					var maxGroup = 0;
					var maxSize = 0;

					// Gather formatted fields to compute padding
					var infos = new List<LongInfo>();
					foreach ( var pe in sorted ) {
						var li = BuildLongInfo( pe );
						infos.Add( li );
						maxLinks = Math.Max( maxLinks, li.Links.Length );
						maxOwner = Math.Max( maxOwner, li.Owner.Length );
						maxGroup = Math.Max( maxGroup, li.Group.Length );
						maxSize = Math.Max( maxSize, li.Size.Length );
					}

					foreach ( var li in infos ) {
						var sb = new StringBuilder();
						sb.Append( li.Mode );
						sb.Append( ' ' );
						sb.Append( li.Links.PadLeft( maxLinks ) );
						sb.Append( ' ' );
						sb.Append( li.Owner.PadRight( maxOwner ) );
						sb.Append( ' ' );
						sb.Append( li.Group.PadRight( maxGroup ) );
						sb.Append( ' ' );
						var sizeField = li.Size.PadLeft( maxSize );
						if ( humanReadable ) {
							sizeField = FormatSizeHumanReadable( li.RawSize ).PadLeft( maxSize );
						}

						sb.Append( sizeField );
						sb.Append( ' ' );
						sb.Append( li.Mtime );
						sb.Append( ' ' );
						sb.Append( li.Name );
						if ( !string.IsNullOrEmpty( li.LinkTarget ) ) {
							sb.Append( " -> " );
							sb.Append( li.LinkTarget );
						}

						rows.Add( sb.ToString() );
					}

					foreach ( var r in rows ) {
						stdout.WriteLine( r );
					}
				} else {
					// Simple listing: one per line (we avoid column layout for simplicity and reliability)
					foreach ( var pe in sorted ) {
						stdout.WriteLine( pe.DisplayName );
					}
				}

				firstGroup = false;
			}

			// If recursive, perform depth-first recursion over directories that were not printed as single entries
			if ( recursive ) {
				// Re-run but descending directories
				var toRecurse = new Queue<string>( GetInitialDirectoriesToRecurse( rest, listDirectoriesSelf ) );
				while ( toRecurse.Count > 0 ) {
					var dir = toRecurse.Dequeue();
					try {
						var children = new DirectoryInfo( dir ).EnumerateFileSystemInfos();
						stdout.WriteLine();
						stdout.WriteLine( $"{dir}:" );
						var list = new List<PathEntry>();
						foreach ( var fi in children ) {
							if ( !showAll ) {
								if ( IsHiddenOrDot( fi ) || ( almostAll && IsDotOnly( fi ) ) ) {
									continue;
								}
							}

							list.Add( new PathEntry( fi.FullName, fi ) );
							if ( ( fi.Attributes & FileAttributes.Directory ) != 0 ) {
								toRecurse.Enqueue( fi.FullName );
							}
						}

						var sorted = SortEntries( list, sortByTime, sortBySize, reverse );
						foreach ( var pe in sorted ) {
							if ( longListing ) {
								var li = BuildLongInfo( pe );
								var sizeField = humanReadable ? FormatSizeHumanReadable( li.RawSize ) : li.Size;
								stdout.WriteLine( $"{li.Mode} {li.Links} {li.Owner} {li.Group} {sizeField} {li.Mtime} {li.Name}" );
							} else {
								stdout.WriteLine( pe.DisplayName );
							}
						}
					} catch ( Exception ex ) {
						stderr.WriteLine( $"ls: cannot open directory '{dir}': {ex.Message}" );
						exit = 1;
					}
				}
			}

			return exit;
		} catch ( NotImplementedException ) {
			throw;
		} catch ( Exception ex ) {
			stderr.WriteLine( $"ls: {ex.Message}" );
			return 1;
		}
	}

	// --- Helpers and internal types ---

	private static bool IsHiddenOrDot( FileSystemInfo fi ) {
		// Best-effort hidden: dotfile on Unix, Hidden attribute on Windows
		if ( fi.Name.Length > 0 && fi.Name[ 0 ] == '.' ) {
			return true;
		}

		try {
			if ( ( fi.Attributes & FileAttributes.Hidden ) != 0 ) {
				return true;
			}
		} catch {
			// ignore attribute failures
		}

		return false;
	}

	private static bool IsDotOnly( FileSystemInfo fi ) {
		if ( fi.Name == "." || fi.Name == ".." ) {
			return true;
		}

		return false;
	}

	private sealed class PathEntry {
		public string Path {
			get;
		}
		public FileSystemInfo Info {
			get;
		}
		public string DisplayName => Info.Name;

		public PathEntry( string path, FileSystemInfo info ) {
			Path = path;
			Info = info;
		}
	}

	private sealed class LongInfo {
		// Mutable properties so BuildLongInfo can assign fields after construction.
		public string Mode { get; set; } = "";
		public string Links { get; set; } = "1";
		public string Owner { get; set; } = "-";
		public string Group { get; set; } = "-";
		public string Size { get; set; } = "0";
		public long RawSize {
			get; set;
		}
		public string Mtime { get; set; } = "";
		public string Name { get; set; } = "";
		public string LinkTarget { get; set; } = "";
	}

	private static LongInfo BuildLongInfo( PathEntry pe ) {
		var info = pe.Info;
		var li = new LongInfo();
		// Mode string: best-effort
		li.Mode = BuildModeString( info );

		// Links: best-effort set to 1 (BCL doesn't expose link count reliably)
		li.Links = "1";

		// Owner: best-effort using FileSecurity on Windows; otherwise fallback to '-'
		try {
			if ( RuntimeInformation.IsOSPlatform( OSPlatform.Windows ) ) {
				try {
					var acl = GetFileSecurity( pe.Path );
					if ( acl is not null ) {
						var owner = acl.GetOwner( typeof( NTAccount ) )?.ToString();
						if ( !string.IsNullOrEmpty( owner ) ) {
							li.Owner = owner;
						}
					}
				} catch {
					// best-effort: leave as '-'
				}
			}
		} catch {
			// ignore
		}

		// Group: best-effort not available on standard BCL -> leave '-'

		// Size
		try {
			if ( info is FileInfo fi ) {
				li.RawSize = fi.Length;
				li.Size = fi.Length.ToString( CultureInfo.InvariantCulture );
			} else {
				li.RawSize = 0;
				li.Size = "0";
			}
		} catch {
			li.RawSize = 0;
			li.Size = "0";
		}

		// Mtime
		try {
			var mtime = info.LastWriteTime;
			li.Mtime = FormatMTime( mtime );
		} catch {
			li.Mtime = "";
		}

		// Name and link target
		li.Name = pe.DisplayName;
		try {
			// Try to detect symlink target using LinkTarget if available
			var linkTargetProp = info.GetType().GetProperty( "LinkTarget" );
			if ( linkTargetProp is not null ) {
				var val = linkTargetProp.GetValue( info ) as string;
				if ( !string.IsNullOrEmpty( val ) ) {
					li.LinkTarget = val;
				}
			}
		} catch {
			// ignore
		}

		return li;
	}

	private static string BuildModeString( FileSystemInfo info ) {
		// Format like "drwxr-xr-x" best-effort:
		var sb = new StringBuilder( 10 );

		var isDir = ( info.Attributes & FileAttributes.Directory ) != 0;
		if ( isDir ) {
			sb.Append( 'd' );
		} else {
			sb.Append( '-' );
		}

		// Owner permissions: read/write/execute best-effort
		sb.Append( 'r' ); // read (best-effort assume readable)
		if ( ( info.Attributes & FileAttributes.ReadOnly ) == 0 ) {
			sb.Append( 'w' );
		} else {
			sb.Append( '-' );
		}

		// Execute best-effort:
		if ( IsExecutable( info ) ) {
			sb.Append( 'x' );
		} else {
			sb.Append( '-' );
		}

		// Group permissions: best-effort mirror owner for readability, but no true POSIX group bits available
		sb.Append( 'r' );
		if ( ( info.Attributes & FileAttributes.ReadOnly ) == 0 ) {
			sb.Append( 'w' );
		} else {
			sb.Append( '-' );
		}

		if ( IsExecutable( info ) ) {
			sb.Append( 'x' );
		} else {
			sb.Append( '-' );
		}

		// Others
		sb.Append( 'r' );
		if ( ( info.Attributes & FileAttributes.ReadOnly ) == 0 ) {
			sb.Append( 'w' );
		} else {
			sb.Append( '-' );
		}

		if ( IsExecutable( info ) ) {
			sb.Append( 'x' );
		} else {
			sb.Append( '-' );
		}

		return sb.ToString();
	}

	private static bool IsExecutable( FileSystemInfo info ) {
		// Best-effort heuristics:
		if ( ( info.Attributes & FileAttributes.Directory ) != 0 ) {
			return true;
		}

		var name = info.Name;
		var ext = Path.GetExtension( name ).ToLowerInvariant();
		if ( Environment.OSVersion.Platform == PlatformID.Win32NT ) {
			// On Windows, common executable extensions
			if ( ext is ".exe" or ".com" or ".bat" or ".cmd" ) {
				return true;
			}

			return false;
		}

		// On Unix-like systems, we cannot read exec bit reliably without additional APIs.
		// Attempt simple heuristic: files with no extension might be scripts; treat as not executable.
		return false;
	}

	private static ObjectSecurity? GetFileSecurity( string path ) {
		// Use FileInfo/DirectoryInfo.GetAccessControl on Windows only.
		if ( !RuntimeInformation.IsOSPlatform( OSPlatform.Windows ) ) {
			return null;
		}

		try {
			if ( File.Exists( path ) ) {
				var fi = new FileInfo( path );
				return fi.GetAccessControl();
			}

			if ( Directory.Exists( path ) ) {
				var di = new DirectoryInfo( path );
				return di.GetAccessControl();
			}
		} catch {
			// best-effort: return null on any failure
			return null;
		}

		return null;
	}

	private static string FormatMTime( DateTime dt ) {
		// Similar to coreutils: if older than 6 months show year instead of time.
		var now = DateTime.Now;
		var sixMonthsAgo = now.AddMonths( -6 );
		if ( dt < sixMonthsAgo || dt > now.AddMinutes( 1 ) ) {
			return dt.ToString( "MMM dd  yyyy", CultureInfo.InvariantCulture );
		}

		return dt.ToString( "MMM dd HH:mm", CultureInfo.InvariantCulture );
	}

	private static string FormatSizeHumanReadable( long size ) {
		// Simple human-readable formatter using powers of 1024
		if ( size < 1024 ) {
			return $"{size}B";
		}

		var units = new[] { "K", "M", "G", "T", "P", "E" };
		double s = size;
		var i = 0;
		while ( s >= 1024 && i < units.Length - 1 ) {
			s /= 1024;
			i++;
		}

		return $"{s:0.#}{units[ i ]}";
	}

	private static List<PathEntry> SortEntries( IEnumerable<PathEntry> items, bool sortByTime, bool sortBySize, bool reverse ) {
		IOrderedEnumerable<PathEntry> ordered;
		if ( sortByTime ) {
			ordered = items.OrderByDescending( e => GetLastWriteTimeSafe( e.Info ) );
		} else {
			if ( sortBySize ) {
				ordered = items.OrderByDescending( e => GetSizeSafe( e.Info ) );
			} else {
				ordered = items.OrderBy( e => e.DisplayName, StringComparer.Ordinal );
			}
		}

		var result = reverse ? ordered.Reverse().ToList() : ordered.ToList();
		return result;
	}

	private static DateTime GetLastWriteTimeSafe( FileSystemInfo fi ) {
		try {
			return fi.LastWriteTime;
		} catch {
			return DateTime.MinValue;
		}
	}

	private static long GetSizeSafe( FileSystemInfo fi ) {
		try {
			if ( fi is FileInfo f ) {
				return f.Length;
			}

			return 0L;
		} catch {
			return 0L;
		}
	}

	private static Dictionary<string, List<PathEntry>> GroupEntriesByRequestedPath( string[] requestedPaths, bool listDirectoriesSelf ) {
		// Build mapping from requested path to its entries (we will enumerate contents later)
		var map = new Dictionary<string, List<PathEntry>>( StringComparer.Ordinal );
		foreach ( var p in requestedPaths ) {
			map[ p ] = new List<PathEntry>();
		}

		return map;
	}

	private static List<PathEntry> GetEntriesForRequestedPath( string requestedPath, List<PathEntry> dummy, bool listDirectoriesSelf ) {
		// Populate entries for a single requested path.
		var result = new List<PathEntry>();
		try {
			var attr = File.GetAttributes( requestedPath );
			var isDir = ( attr & FileAttributes.Directory ) != 0;
			if ( isDir && !listDirectoriesSelf ) {
				var dirInfo = new DirectoryInfo( requestedPath );
				foreach ( var fi in dirInfo.EnumerateFileSystemInfos() ) {
					result.Add( new PathEntry( fi.FullName, fi ) );
				}
			} else {
				FileSystemInfo? fsi = isDir ? (FileSystemInfo)new DirectoryInfo( requestedPath ) : new FileInfo( requestedPath );
				result.Add( new PathEntry( requestedPath, fsi ) );
			}
		} catch {
			// If we cannot access, return empty list; caller will have reported error earlier.
		}

		return result;
	}

	private static IEnumerable<string> GetInitialDirectoriesToRecurse( string[] requestedPaths, bool listDirectoriesSelf ) {
		var results = new List<string>();
		foreach ( var p in requestedPaths ) {
			try {
				var attr = File.GetAttributes( p );
				var isDir = ( attr & FileAttributes.Directory ) != 0;
				if ( isDir && !listDirectoriesSelf ) {
					results.Add( p );
				}
			} catch {
				// skip
			}
		}

		foreach ( var r in results ) {
			yield return r;
		}
	}
}
