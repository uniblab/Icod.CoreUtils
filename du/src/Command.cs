namespace Icod.CoreUtils.DU;

using System;
using System.IO;
using System.Linq;

/// <summary>
/// du: compute disk usage (recursive). Minimal: prints total bytes for each given path.
/// Credit: Dennis Ritchie.
/// Usage: du [path...]
/// </summary>
public static class Command {
	public static int Run( string[] args, System.IO.TextReader? stdin = null, System.IO.TextWriter? stdout = null, System.IO.TextWriter? stderr = null ) {
		stdout ??= Console.Out;
		stderr ??= Console.Error;

		var paths = args.Length > 0 ? args : new string[] { "." };
		var exit = 0;
		foreach ( var p in paths ) {
			try {
				var size = GetSize( new DirectoryInfo( p ) );
				stdout.WriteLine( $"{size}\t{p}" );
			} catch ( Exception ex ) {
				stderr.WriteLine( $"du: {p}: {ex.Message}" );
				exit = 1;
			}
		}

		return exit;
	}

	private static long GetSize( DirectoryInfo dir ) {
		long total = 0;
		FileInfo[] files = Array.Empty<FileInfo>();
		DirectoryInfo[] subdirs = Array.Empty<DirectoryInfo>();
		try {
			files = dir.GetFiles();
			subdirs = dir.GetDirectories();
		} catch {
			return 0;
		}

		foreach ( var f in files )
			total += f.Length;
		foreach ( var d in subdirs )
			total += GetSize( d );
		return total;
	}
}
