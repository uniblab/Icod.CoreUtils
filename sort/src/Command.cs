// Original behavior/reference: GNU coreutils
// Ported to .NET by Timothy J. Bruce <uniblab@hotmail.com>

namespace Icod.CoreUtils.Sort;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Globalization;

/// <summary>
/// sort: sort lines of text files.
/// Supported options:
///   -r	reverse
///   -n	numeric sort
///   -u	unique
/// </summary>
public static class Command
{
	public static int Run(string[] args, TextReader? stdin = null, TextWriter? stdout = null, TextWriter? stderr = null)
	{
		stdin ??= Console.In;
		stdout ??= Console.Out;
		stderr ??= Console.Error;

		var reverse = false;
		var numeric = false;
		var unique = false;
		var i = 0;
		for (; i < args.Length; i++)
		{
			if (!args[i].StartsWith('-'))
			{
				break;
			}

			if (args[i].Contains('r'))
			{
				reverse = true;
			}

			if (args[i].Contains('n'))
			{
				numeric = true;
			}

			if (args[i].Contains('u'))
			{
				unique = true;
			}
		}

		var files = new List<string>();
		for (; i < args.Length; i++)
		{
			files.Add(args[i]);
		}

		try
		{
			var lines = new List<string>();
			if (files.Count == 0)
			{
				string? line;
				while ((line = stdin.ReadLine()) is not null)
				{
					lines.Add(line);
				}
			}
			else
			{
				foreach (var f in files)
				{
					if (f == "-")
					{
						string? line;
						while ((line = stdin.ReadLine()) is not null)
						{
							lines.Add(line);
						}
					}
					else
					{
						using var sr = new StreamReader(f, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
						string? line;
						while ((line = sr.ReadLine()) is not null)
						{
							lines.Add(line);
						}
					}
				}
			}

			IOrderedEnumerable<string> ordered;
			if (numeric)
			{
				ordered = lines.OrderBy(s =>
				{
					if (double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var v))
					{
						return v;
					}

					return double.NaN;
				});
			}
			else
			{
				ordered = lines.OrderBy(s => s, StringComparer.Ordinal);
			}

			var result = reverse ? ordered.Reverse().ToList() : ordered.ToList();
			if (unique)
			{
				result = result.Distinct(StringComparer.Ordinal).ToList();
			}

			foreach (var l in result)
			{
				stdout.WriteLine(l);
			}

			return 0;
		}
		catch (Exception ex)
		{
			stderr.WriteLine($"sort: {ex.Message}");
			return 1;
		}
	}
}
