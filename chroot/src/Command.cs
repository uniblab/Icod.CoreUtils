// Original behavior/reference: GNU coreutils
// Ported to .NET by Timothy J. Bruce <uniblab@hotmail.com>

namespace Icod.CoreUtils.Chroot;

using System;
using System.IO;

/// <summary>
/// chroot: change root directory (not implemented).
/// Changing root requires OS-level privileges and platform-specific APIs; BCL-only approach cannot implement this safely.
/// </summary>
public static class Command {
	public static int Run( string[] args, TextReader? stdin = null, TextWriter? stdout = null, TextWriter? stderr = null ) {
		stderr ??= Console.Error;
		stderr.WriteLine( "chroot: not implemented with BCL-only implementation" );
		throw new NotImplementedException( "chroot: requires platform-specific APIs and elevated privileges." );
	}
}
