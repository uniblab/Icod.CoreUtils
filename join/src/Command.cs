// Original behavior/reference: GNU coreutils
// Ported to .NET by Timothy J. Bruce <uniblab@hotmail.com>

namespace Icod.CoreUtils.Join;

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

/// <summary>
/// join: join lines of two files on a common field.
/// Simple, best-effort implementation:
/// - Usage: join [-t delim] file1 file2
/// - Joins on first field (whitespace or specified delim).
/// - If either file name is '-', read from stdin (only one stdin supported reliably).
/// </summary>
public static class Command
{
	public static int Run(string[] args, TextReader? stdin = null, TextWriter? stdout = null, TextWriter? stderr = null)
	{
		stdout ??= Console.Out;
		stderr ??= Console.Error;

		var delim = '\t';
		var i = 0;
		for (; i < args.Length; i++)
		{
			if (!args[i].StartsWith( '-' ))
			{
				break;
			}

			if (args[i] == "-t" && i + 1 < args.Length)
			{
				i++;
				var s = args[i];
				if (!string.IsNullOrEmpty(s))
				{
					delim = s[0];
				}
			}
			else
			{
				break;
			}
		}

		var rem = new List<string>();
		for (; i < args.Length; i++)
		{
			rem.Add(args[i]);
		}

		if (rem.Count < 2)
		{
			stderr.WriteLine("join: missing file operand");
			return 1;
		}

		var file1 = rem[0];
		var file2 = rem[1];

		try
		{
			var map = new Dictionary<string, string>(StringComparer.Ordinal);
			using (var r1 = OpenReader(file1, stdin))
			{
				string? line;
				while ((line = r1.ReadLine()) is not null)
				{
					if (line.Length == 0)
					{
						continue;
					}

					var parts = line.Split(delim);
					var key = parts[0];
					var rest = parts.Length > 1 ? string.Join(delim.ToString(), parts, 1, parts.Length - 1) : string.Empty;
					map[key] = rest;
				}
			}

			using (var r2 = OpenReader(file2, stdin))
			{
				string? line;
				while ((line = r2.ReadLine()) is not null)
				{
					if (line.Length == 0)
					{
						continue;
					}

					var parts = line.Split(delim);
					var key = parts[0];
					var rest2 = parts.Length > 1 ? string.Join(delim.ToString(), parts, 1, parts.Length - 1) : string.Empty;
					if (map.TryGetValue(key, out var rest1))
					{
						if (!string.IsNullOrEmpty(rest1))
						{
							stdout.WriteLine($"{key}{delim}{rest1}{delim}{rest2}");
						}
						else
						{
							stdout.WriteLine($"{key}{delim}{rest2}");
						}
					}
				}
			}

			return 0;
		}
		catch (Exception ex)
		{
			stderr.WriteLine($"join: {ex.Message}");
			return 1;
		}
	}

	private static TextReader OpenReader(string path, TextReader? stdin)
	{
		if (path == "-")
		{
			if (stdin is null)
			{
				return Console.In;
			}

			return stdin;
		}

		return new StreamReader(path, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
	}
}
