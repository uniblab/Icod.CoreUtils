// Original behavior/reference: GNU coreutils
// Ported to .NET by Timothy J. Bruce <uniblab@hotmail.com>

namespace Icod.CoreUtils.Shuf;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

/// <summary>
/// shuf: shuffle input lines.
/// Supported options:
///   -n N	output at most N lines
/// If files provided, read lines from files else from stdin.
/// </summary>
public static class Command
{
	public static int Run(string[] args, TextReader? stdin = null, TextWriter? stdout = null, TextWriter? stderr = null)
	{
		stdin ??= Console.In;
		stdout ??= Console.Out;
		stderr ??= Console.Error;

		int? limit = null;
		var rem = new List<string>();
		var i = 0;
		for (; i < args.Length; i++)
		{
			if (!args[i].StartsWith( '-' ))
			{
				break;
			}

			if (args[i] == "-n" && i + 1 < args.Length)
			{
				i++;
				if (int.TryParse(args[i], out var n))
				{
					limit = Math.Max(0, n);
				}
				else
				{
					stderr.WriteLine($"shuf: invalid number '{args[i]}'");
					return 1;
				}
			}
			else
			{
				break;
			}
		}

		for (; i < args.Length; i++)
		{
			rem.Add(args[i]);
		}

		try
		{
			var lines = new List<string>();
			if (rem.Count == 0)
			{
				string? line;
				while ((line = stdin.ReadLine()) is not null)
				{
					lines.Add(line);
				}
			}
			else
			{
				foreach (var path in rem)
				{
					if (path == "-")
					{
						string? line;
						while ((line = stdin.ReadLine()) is not null)
						{
							lines.Add(line);
						}
					}
					else
					{
						using var sr = new StreamReader(path, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
						string? line;
						while ((line = sr.ReadLine()) is not null)
						{
							lines.Add(line);
						}
					}
				}
			}

			using var rng = RandomNumberGenerator.Create();
			var shuffled = lines.Select((v, idx) => (v, key: RandomKey(rng))).OrderBy(x => x.key).Select(x => x.v).ToList();

			if (limit.HasValue)
			{
				foreach (var outLine in shuffled.Take(limit.Value))
				{
					stdout.WriteLine(outLine);
				}
			}
			else
			{
				foreach (var outLine in shuffled)
				{
					stdout.WriteLine(outLine);
				}
			}

			return 0;
		}
		catch (Exception ex)
		{
			stderr.WriteLine($"shuf: {ex.Message}");
			return 1;
		}
	}

	private static ulong RandomKey(RandomNumberGenerator rng)
	{
		var buf = new byte[8];
		rng.GetBytes(buf);
		return BitConverter.ToUInt64(buf, 0);
	}
}
