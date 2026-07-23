// Ported to .NET by Timothy J. Bruce <uniblab@hotmail.com>

namespace Icod.CoreUtils.Vdir;

using System;
using System.Globalization;
using System.IO;
using System.Text;

/// <summary>
/// Minimal `vdir`: long listing for files and directories.
/// This is a pragmatic implementation and shows best-effort owner/group and permissions.
/// </summary>
public static class Command {
	public static int Run( string[] args, TextReader? stdin = null, TextWriter? stdout = null, TextWriter? stderr = null ) {
		stdin ??= Console.In;
		stdout ??= Console.Out;
		stderr ??= Console.Error;

		if ( args.Length == 1 && ( args[ 0 ] == "-h" || args[ 0 ] == "--help" ) ) {
			PrintUsage( stdout );
			return 0;
		}

		var targets = args.Length == 0 ? new[] { "." } : args;
		var exitCode = 0;
		foreach ( var t in targets ) {
			try {
				if ( Directory.Exists( t ) ) {
					// print directory header if multiple targets
					if ( targets.Length > 1 )
						stdout.WriteLine( $"{t}:" );
					var di = new DirectoryInfo( t );
					foreach ( var fi in di.EnumerateFileSystemInfos() ) {
						PrintLong( fi, stdout );
					}
				} else if ( File.Exists( t ) ) {
					var fi = new FileInfo( t );
					PrintLong( fi, stdout );
				} else {
					stderr.WriteLine( $"vdir: cannot access '{t}': No such file or directory" );
					exitCode = 1;
				}
			} catch ( Exception ex ) {
				stderr.WriteLine( $"vdir: {t}: {ex.Message}" );
				exitCode = 1;
			}
		}
		return exitCode;
	}

	private static void PrintLong( FileSystemInfo fi, TextWriter stdout ) {
		var perm = BuildPermissions( fi );
		var links = 1;
		var owner = Environment.UserName ?? string.Empty;
		var group = string.Empty;
		long size = 0;
		DateTime mtime = fi.LastWriteTime;

		if ( fi is FileInfo f )
			size = f.Length;
		else
			size = 0;

		var dateStr = FormatDate( mtime );
		var name = fi.Name;

		stdout.WriteLine( $"{perm} {links,3} {owner,8} {group,8} {size,8} {dateStr} {name}" );
	}

	private static string BuildPermissions( FileSystemInfo fi ) {
		var isDir = ( fi.Attributes & FileAttributes.Directory ) != 0;
		var isReadOnly = ( fi.Attributes & FileAttributes.ReadOnly ) != 0;

		var sb = new StringBuilder( 10 );
		sb.Append( isDir ? 'd' : '-' );

		// owner
		sb.Append( 'r' );
		sb.Append( isReadOnly ? '-' : 'w' );
		sb.Append( '-' ); // execute not easily determined; leave '-' for portability

		// group (approximate)
		sb.Append( 'r' );
		sb.Append( '-' );
		sb.Append( '-' );

		// others
		sb.Append( 'r' );
		sb.Append( '-' );
		sb.Append( '-' );

		return sb.ToString();
	}

	private static string FormatDate( DateTime dt ) {
		var now = DateTime.Now;
		var sixMonthsAgo = now.AddMonths( -6 );
		if ( dt < sixMonthsAgo || dt > now.AddMinutes( 1 ) ) {
			// older/newer: show year
			return dt.ToString( "MMM dd  yyyy", CultureInfo.InvariantCulture );
		} else {
			return dt.ToString( "MMM dd HH:mm", CultureInfo.InvariantCulture );
		}
	}

	private static void PrintUsage( TextWriter stdout ) {
		stdout.WriteLine( "Usage: vdir [FILE]..." );
		stdout.WriteLine( "List files in long format (similar to `ls -l`)." );
	}
}
