// Original behavior/reference: GNU coreutils
// Ported to .NET by Timothy J. Bruce <uniblab@hotmail.com>

namespace Icod.CoreUtils.Uniq;

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

/// <summary>
/// uniq: filter adjacent identical lines.
/// Options:
///   -c   prefix lines by occurrence count
///   -d   only print duplicate lines
///   -u   only print unique lines
/// Reads single file or stdin.
/// </summary>
public static class Command
{
	public static int Run(string[] args, TextReader? stdin = null, TextWriter? stdout = null, TextWriter? stderr = null)
	{
		stdin ??= Console.In;
		stdout ??= Console.Out;
		stderr ??= Console.Error;

		var countFlag = false;
		var dupOnly = false;
		var uniqueOnly = false;
		var rem = new List<string>();
		foreach (var a in args)
		{
			if (a == "-c")
			{
				countFlag = true;
			}
			else if (a == "-d")
			{
				dupOnly = true;
			}
			else if (a == "-u")
			{
				uniqueOnly = true;
			}
			else
			{
				rem.Add(a);
			}
		}

		string? input = "-";
		if (rem.Count > 0)
		{
			input = rem[0];
		}

		try
		{
			using var r = input == "-" ? stdin! : new StreamReader(input, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
			string? prev = null;
			long count = 0;
			string? line;
			var first = true;
			while ((line = r.ReadLine()) is not null)
			{
				if (first)
				{
					prev = line;
					count = 1;
					first = false;
					continue;
				}

				if (line == prev)
				{
					count++;
				}
				else
				{
					Emit(prev!, count, countFlag, dupOnly, uniqueOnly, stdout);
					prev = line;
					count = 1;
				}
			}

			if (!first && prev is not null)
			{
				Emit(prev, count, countFlag, dupOnly, uniqueOnly, stdout);
			}

			return 0;
		}
		catch (Exception ex)
		{
			stderr.WriteLine($"uniq: {ex.Message}");
			return 1;
		}
	}

	private static void Emit(string line, long count, bool countFlag, bool dupOnly, bool uniqueOnly, TextWriter stdout)
	{
		if (dupOnly)
		{
			if (count > 1)
			{
				if (countFlag)
				{
					stdout.WriteLine($"{count,7} {line}");
				}
				else
				{
					stdout.WriteLine(line);
				}
			}
		}
		else if (uniqueOnly)
		{
			if (count == 1)
			{
				if (countFlag)
				{
					stdout.WriteLine($"{count,7} {line}");
				}
				else
				{
					stdout.WriteLine(line);
				}
			}
		}
		else
		{
			if (countFlag)
			{
				stdout.WriteLine($"{count,7} {line}");
			}
			else
			{
				stdout.WriteLine(line);
			}
		}
	}
}
