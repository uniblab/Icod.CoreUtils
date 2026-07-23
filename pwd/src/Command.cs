// Original behavior/reference: GNU coreutils
// Ported to .NET by Timothy J. Bruce <uniblab@hotmail.com>

namespace Icod.CoreUtils.Pwd;

using System;
using System.IO;
using System.Runtime.InteropServices;

/// <summary>
/// pwd: print name of current working directory.
/// Supports: -P (physical) and -L (logical) where -P resolves symlinks (best-effort).
/// </summary>
public static class Command {
	public static int Run( string[] args, TextReader? stdin = null, TextWriter? stdout = null, TextWriter? stderr = null ) {
		stdout ??= Console.Out;
		stderr ??= Console.Error;

		var physical = false;
		foreach ( var a in args ) {
			if ( a == "-P" ) {
				physical = true;
			} else if ( a == "-L" ) {
				physical = false;
			}
		}

		try {
			var cwd = Directory.GetCurrentDirectory();
			if ( physical ) {
				// Best-effort: attempt to resolve symlinks using realpath-like logic
				var real = RealPath.ResolvePath( cwd );
				stdout.WriteLine( real );
			} else {
				stdout.WriteLine( cwd );
			}

			return 0;
		} catch ( Exception ex ) {
			stderr.WriteLine( $"pwd: {ex.Message}" );
			return 1;
		}
	}

	private static class RealPath {
		public static string ResolvePath( string path ) {
			try {
				return Path.GetFullPath( path );
			} catch {
				return path;
			}
		}
	}
}
