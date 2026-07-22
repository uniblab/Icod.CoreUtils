// Original behavior/reference: GNU coreutils
// Ported to .NET by Timothy J. Bruce <uniblab@hotmail.com>

namespace Icod.CoreUtils.Cp;

using System;
using System.IO;

/// <summary>
/// cp: simple copy. Supports -r for recursive directories.
/// </summary>
public static class Command
{
	public static int Run(string[] args, TextReader? stdin = null, TextWriter? stdout = null, TextWriter? stderr = null)
	{
		stdout ??= Console.Out;
		stderr ??= Console.Error;

		var recursive = false;
		var i = 0;
		for (; i < args.Length; i++)
		{
			if (!args[i].StartsWith('-'))
			{
				break;
			}

			if (args[i] == "-r" || args[i] == "-R")
			{
				recursive = true;
			}
		}

		var rem = new System.Collections.Generic.List<string>();
		for (; i < args.Length; i++)
		{
			rem.Add(args[i]);
		}

		if (rem.Count < 2)
		{
			stderr.WriteLine("cp: missing file operand");
			return 1;
		}

		var dest = rem[^1];
		var sources = rem.GetRange(0, rem.Count - 1);

		try
		{
			if (sources.Count > 1 || Directory.Exists(dest))
			{
				Directory.CreateDirectory(dest);
				foreach (var s in sources)
				{
					var name = Path.GetFileName(s);
					var targ = Path.Combine(dest, name);
					if (Directory.Exists(s))
					{
						if (!recursive)
						{
							stderr.WriteLine($"cp: -r not specified; omitting directory '{s}'");
							continue;
						}

						CopyDirectory(s, targ);
					}
					else
					{
						File.Copy(s, targ, overwrite: true);
					}
				}
			}
			else
			{
				var src = sources[0];
				if (Directory.Exists(src))
				{
					if (!recursive)
					{
						stderr.WriteLine($"cp: -r not specified; omitting directory '{src}'");
						return 1;
					}

					CopyDirectory(src, dest);
				}
				else
				{
					File.Copy(src, dest, overwrite: true);
				}
			}

			return 0;
		}
		catch (Exception ex)
		{
			stderr.WriteLine($"cp: {ex.Message}");
			return 1;
		}
	}

	private static void CopyDirectory(string sourceDir, string destDir)
	{
		Directory.CreateDirectory(destDir);
		foreach (var file in Directory.GetFiles(sourceDir))
		{
			var dest = Path.Combine(destDir, Path.GetFileName(file));
			File.Copy(file, dest, overwrite: true);
		}

		foreach (var dir in Directory.GetDirectories(sourceDir))
		{
			CopyDirectory(dir, Path.Combine(destDir, Path.GetFileName(dir)));
		}
	}
}
