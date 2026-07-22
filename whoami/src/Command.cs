// Original behavior/reference: GNU coreutils
// Ported to .NET by Timothy J. Bruce <uniblab@hotmail.com>

namespace Icod.CoreUtils.Whoami;

using System;
using System.IO;

/// <summary>
/// whoami: print effective user name.
/// </summary>
public static class Command
{
	public static int Run(string[] args, TextReader? stdin = null, TextWriter? stdout = null, TextWriter? stderr = null)
	{
		stdout ??= Console.Out;
		stderr ??= Console.Error;

		try
		{
			stdout.WriteLine(Environment.UserName);
			return 0;
		}
		catch (Exception ex)
		{
			stderr.WriteLine($"whoami: {ex.Message}");
			return 1;
		}
	}
}
