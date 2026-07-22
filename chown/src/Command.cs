// Original behavior/reference: GNU coreutils
// Ported to .NET by Timothy J. Bruce <uniblab@hotmail.com>

namespace Icod.CoreUtils.Chown;

using System;
using System.IO;

/// <summary>
/// chown: change owner of files.
/// Best-effort implementation: Not implemented with BCL-only approach.
/// True POSIX ownership changes require platform-specific APIs; this implementation throws NotImplementedException.
/// </summary>
public static class Command
{
	public static int Run(string[] args, TextReader? stdin = null, TextWriter? stdout = null, TextWriter? stderr = null)
	{
		stderr ??= Console.Error;
		if (args.Length < 2)
		{
			stderr.WriteLine("chown: missing operand");
			return 1;
		}

		// First arg is owner (or owner:group), remaining are files
		throw new NotImplementedException("chown: changing owner is not implemented using BCL-only; requires platform-specific APIs.");
	}
}
