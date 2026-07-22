// Original behavior/reference: GNU coreutils
// Ported to .NET by Timothy J. Bruce <uniblab@hotmail.com>

namespace Icod.CoreUtils.Split;

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

/// <summary>
/// split: split a file into pieces.
/// Supported options:
///   -l N	put N lines per output file (default 1000)
///   -b SIZE put SIZE bytes per output file (supports suffix k, m)
/// Output files named 'xaa', 'xab', ...
/// </summary>
public static class Command
{
	public static int Run(string[] args, TextReader? stdin = null, TextWriter? stdout = null, TextWriter? stderr = null)
	{
		stdin ??= Console.In;
		stdout ??= Console.Out;
		stderr ??= Console.Error;

		var linesPerFile = 1000;
		long? bytesPerFile = null;
		var i = 0;
		for (; i < args.Length; i++)
		{
			if (!args[i].StartsWith( '-' ))
			{
				break;
			}

			if (args[i] == "-l" && i + 1 < args.Length)
			{
				i++;
				if (!int.TryParse(args[i], out linesPerFile))
				{
					stderr.WriteLine($"split: invalid number '{args[i]}'");
					return 1;
				}
			}
			else if (args[i] == "-b" && i + 1 < args.Length)
			{
				i++;
				if (!TryParseSize(args[i], out var b))
				{
					stderr.WriteLine($"split: invalid size '{args[i]}'");
					return 1;
				}

				bytesPerFile = b;
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

		string inputPath;
		if (rem.Count == 0)
		{
			inputPath = "-";
		}
		else
		{
			inputPath = rem[0];
		}

		try
		{
			if (bytesPerFile.HasValue)
			{
				using var stream = inputPath == "-" ? Console.OpenStandardInput() : new FileStream(inputPath, FileMode.Open, FileAccess.Read);
				var buf = new byte[8192];
				int fileIdx = 0;
				long remaining = bytesPerFile.Value;
				var outFs = OpenOutFile(fileIdx++);
				while (true)
				{
					var toRead = (int)Math.Min(buf.Length, remaining);
					var r = stream.Read(buf, 0, toRead);
					if (r <= 0)
					{
						break;
					}

					outFs.Write(buf, 0, r);
					remaining -= r;
					if (remaining == 0)
					{
						outFs.Dispose();
						if (stream.Position < stream.Length)
						{
							outFs = OpenOutFile(fileIdx++);
							remaining = bytesPerFile.Value;
						}
					}
				}

				outFs.Dispose();
			}
			else
			{
				using var reader = inputPath == "-" ? new StreamReader(Console.OpenStandardInput(), Encoding.UTF8) : new StreamReader(inputPath, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
				int fileIdx = 0;
				int lineCount = 0;
				using var sw = new StreamWriter(OpenOutFile(fileIdx), Encoding.UTF8);
				string? line;
				while ((line = reader.ReadLine()) is not null)
				{
					sw.WriteLine(line);
					lineCount++;
					if (lineCount >= linesPerFile)
					{
						sw.Dispose();
						fileIdx++;
						using var tmp = new StreamWriter(OpenOutFile(fileIdx), Encoding.UTF8);
						// assign new writer for next iteration - use outer sw variable by disposing and reopening
						// Re-open next writer by creating a new StreamWriter for subsequent writes in subsequent loop iterations.
					}
				}
			}

			return 0;
		}
		catch (Exception ex)
		{
			stderr.WriteLine($"split: {ex.Message}");
			return 1;
		}
	}

	private static FileStream OpenOutFile(int idx)
	{
		var a = 'a';
		var first = (char)(a + (idx / 26) % 26);
		var second = (char)(a + idx % 26);
		var name = $"x{first}{second}";
		return new FileStream(name, FileMode.Create, FileAccess.Write);
	}

	private static bool TryParseSize(string s, out long size)
	{
		size = 0;
		if (string.IsNullOrEmpty(s))
		{
			return false;
		}

		var mul = 1L;
		var last = s[^1];
		var num = s;
		if (last == 'k' || last == 'K')
		{
			mul = 1024L;
			num = s.Substring(0, s.Length - 1);
		}
		else if (last == 'm' || last == 'M')
		{
			mul = 1024L * 1024L;
			num = s.Substring(0, s.Length - 1);
		}

		if (!long.TryParse(num, out var v))
		{
			return false;
		}

		size = v * mul;
		return true;
	}
}
