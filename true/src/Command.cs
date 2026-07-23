// Original behavior/reference: GNU coreutils
// Ported to .NET by Timothy J. Bruce <uniblab@hotmail.com>

namespace Icod.CoreUtils.True;

using System;
using System.IO;

/// <summary>
/// true: do nothing, successful exit.
/// </summary>
public static class Command {
	public static int Run( string[] args, TextReader? stdin = null, TextWriter? stdout = null, TextWriter? stderr = null ) {
		return 0;
	}
}
