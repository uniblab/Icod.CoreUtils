namespace Icod.CoreUtils.Df;

using System;
using System.IO;
using System.Linq;

/// <summary>
/// df: show disk space usage for mounted drives or a specific path.
/// Credit: Dennis Ritchie and Ken Thompson.
/// Usage: df [path]
/// </summary>
public static class Command {
	public static int Run( string[] args, System.IO.TextReader? stdin = null, System.IO.TextWriter? stdout = null, System.IO.TextWriter? stderr = null ) {
		stdout ??= Console.Out;
		var path = args.Length > 0 ? args[ 0 ] : null;

		try {
			if ( path is null ) {
				var drives = DriveInfo.GetDrives().Where( d => d.IsReady );
				foreach ( var d in drives ) {
					var total = d.TotalSize;
					var free = d.TotalFreeSpace;
					var used = total - free;
					var pct = total == 0 ? 0 : (int)( ( used * 100L ) / total );
					stdout.WriteLine( $"{d.Name}\t{Bytes( total )}\t{Bytes( free )}\t{pct}%" );
				}
			} else {
				var di = new DriveInfo( Path.GetPathRoot( Path.GetFullPath( path ) ) ?? path );
				var total = di.TotalSize;
				var free = di.TotalFreeSpace;
				var used = total - free;
				var pct = total == 0 ? 0 : (int)( ( used * 100L ) / total );
				stdout.WriteLine( $"{di.Name}\t{Bytes( total )}\t{Bytes( free )}\t{pct}%" );
			}

			return 0;
		} catch ( Exception ex ) {
			stderr?.WriteLine( $"df: {ex.Message}" );
			return 1;
		}
	}

	private static string Bytes( long v ) {
		if ( v < 1024 )
			return v + "B";
		if ( v < 1024L * 1024 )
			return ( v / 1024 ) + "K";
		if ( v < 1024L * 1024 * 1024 )
			return ( v / ( 1024 * 1024 ) ) + "M";
		return ( v / ( 1024L * 1024 * 1024 ) ) + "G";
	}
}
