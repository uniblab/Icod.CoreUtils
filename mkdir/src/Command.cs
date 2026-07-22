namespace Icod.CoreUtils.Mkdir;

using System;
using System.IO;

/// <summary>
/// mkdir: create directories; supports -p.
/// </summary>
public static class Command
{
	public static int Run(string[] args, TextReader? stdin = null, TextWriter? stdout = null, TextWriter? stderr = null)
	{
		stderr ??= Console.Error;
		var parents = false;
		var i = 0;
		for (; i < args.Length; i++)
		{
			if (!args[i].StartsWith('-'))
			{
				break;
			}
			if (args[i].Contains('p'))
			{
				parents = true;
			}
		}

		var rem = new System.Collections.Generic.List<string>();
		for (; i < args.Length; i++) rem.Add(args[i]);

		if (rem.Count == 0)
		{
			stderr.WriteLine("mkdir: missing operand");
			return 1;
		}

		var exit = 0;
		foreach (var d in rem)
		{
			try
			{
				if (parents)
				{
					Directory.CreateDirectory(d);
				}
				else
				{
					if (Directory.Exists(d))
					{
						stderr.WriteLine($"mkdir: cannot create directory '{d}': File exists");
						exit = 1;
					}
					else
					{
						Directory.CreateDirectory(d);
					}
				}
			}
			catch (Exception ex)
			{
				stderr.WriteLine($"mkdir: { ex.Message}");
				exit = 1;
			}
		}

		return exit;
	}
}
