// Original behavior/reference: GNU coreutils
// Ported to .NET by Timothy J. Bruce <uniblab@hotmail.com>

namespace Icod.CoreUtils.Chgrp;

using System;
using System.IO;

/// <summary>
/// chgrp: change group ownership of files.
/// Best-effort implementation: Not implemented with BCL-only approach.
/// True POSIX group changes require platform-specific APIs; this implementation throws NotImplementedException.
/// </summary>
public static class Command {
	public static int Run( string[] args, TextReader? stdin = null, TextWriter? stdout = null, TextWriter? stderr = null ) {
		stderr ??= Console.Error;
		if ( args.Length < 2 ) {
			stderr.WriteLine( "chgrp: missing operand" );
			return 1;
		}

		// First arg is group, remaining are files
		var group = args[ 0 ];
		var rem = new System.Collections.Generic.List<string>();
		for ( var i = 1; i < args.Length; i++ ) {
			rem.Add( args[ i ] );
		}

		// BCL does not provide a portable way to set file group on all platforms.
		// Following your policy: throw NotImplementedException when true POSIX semantics are impossible.
		throw new NotImplementedException( "chgrp: changing group is not implemented using BCL-only; requires platform-specific APIs." );
	}
}
