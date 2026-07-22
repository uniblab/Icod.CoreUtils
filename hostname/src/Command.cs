namespace Icod.CoreUtils.Hostname;

using System;

/// <summary>
/// hostname: print or set the system host name. Minimal: prints machine name.
/// Credit: Bill Joy and the BSD team.
/// Usage: hostname
/// </summary>
public static class Command
{
	public static int Run(string[] args, System.IO.TextReader? stdin = null, System.IO.TextWriter? stdout = null, System.IO.TextWriter? stderr = null)
	{
		stdout ??= Console.Out;
		stdout.WriteLine(Environment.MachineName);
		return 0;
	}
}
