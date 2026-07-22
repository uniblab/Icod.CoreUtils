// Original behavior/reference: GNU coreutils
// Ported to .NET by Timothy J. Bruce <uniblab@hotmail.com>

namespace Icod.CoreUtils.Mv;

using System;
using System.IO;

/// <summary>
/// mv: move (rename) files.
/// Supports -f force (overwrite) and -n no-clobber.
/// </summary>
public static class Command
{
	public static int Run(string[] args, TextReader? stdin = null, TextWriter? stdout = null, TextWriter? stderr = null)
	{
		stdout ??= Console.Out;
		stderr ??= Console.Error;

		var force = false;
		var noClobber = false;
		var i = 0;
		for (; i < args.Length; i++)
		{
			if (!args[i].StartsWith('-'))
			{
				break;
			}

			if (args[i].Contains('f'))
			{
				force = true;
			}

			if (args[i].Contains('n'))
			{
				noClobber = true;
			}
		}

		var rem = new System.Collections.Generic.List<string>();
		for (; i < args.Length; i++)
		{
			rem.Add(args[i]);
		}

		if (rem.Count < 2)
		{
			stderr.WriteLine("mv: missing file operand");
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
					if (File.Exists(targ))
					{
						if (noClobber)
						{
							continue;
						}

						if (force)
						{
							File.Delete(targ);
						}
					}

					File.Move(s, targ);
				}
			}
			else
			{
				var src = sources[0];
				if (File.Exists(dest) || Directory.Exists(dest))
				{
					if (noClobber)
					{
						return 0;
					}

					if (force)
					{
						if (File.Exists(dest))
						{
							File.Delete(dest);
						}
						else if (Directory.Exists(dest))
						{
							Directory.Delete(dest, recursive: true);
						}
					}
				}

				File.Move(src, dest);
			}

			return 0;
		}
		catch (Exception ex)
		{
			stderr.WriteLine($"mv: {ex.Message}");
			return 1;
		}
	}
}
