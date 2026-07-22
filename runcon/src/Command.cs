// Original behavior/reference: GNU coreutils
// Ported to .NET by Timothy J. Bruce <uniblab@hotmail.com>

namespace Icod.CoreUtils.Runcon;

using System;
using System.IO;

/// <summary>
/// runcon: run program with specified SELinux context — not implementable portably with BCL-only.
/// </summary>
public static partial class Command {
	public static int Run( string[] args, TextReader? stdin = null, TextWriter? stdout = null, TextWriter? stderr = null ) {
		stderr ??= Console.Error;
		stderr.WriteLine( "runcon: not implemented with BCL-only implementation" );
		throw new NotImplementedException( "runcon requires platform-specific SELinux APIs and is not implemented in this portable port." );
	}
}
