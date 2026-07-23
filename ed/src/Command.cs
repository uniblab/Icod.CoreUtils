// Original behavior/reference: ed (Ken Thompson)
// Ported to .NET by Timothy J. Bruce <uniblab@hotmail.com>

namespace Icod.CoreUtils.Ed;

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Runtime.CompilerServices; // Needed for GeneratedRegexAttribute

public static partial class Command
{
	private static readonly char[] SpaceSeparator = new[] { ' ' };

	[GeneratedRegex(@"^s/(?<old>.*?)/(?<new>.*?)/(?<flags>.*)$", RegexOptions.None, 100)]
	private static partial Regex SubstitutionWithFlagsRegex();

	[GeneratedRegex(@"^s/(?<old>.*?)/(?<new>.*)$", RegexOptions.None, 100)]
	private static partial Regex SubstitutionNoFlagsRegex();

	public static int Run(string[] args, TextReader? stdin = null, TextWriter? stdout = null, TextWriter? stderr = null)
	{
		stdin ??= Console.In;
		stdout ??= Console.Out;
		stderr ??= Console.Error;

		string? filename = null;
		if (args.Length > 0)
		{
			filename = args[0];
		}

		var buffer = new List<string>();
		try
		{
			if (!string.IsNullOrEmpty(filename))
			{
				if (File.Exists(filename))
				{
					buffer.AddRange(File.ReadAllLines(filename, Encoding.UTF8));
				}
			}
		}
		catch (Exception ex)
		{
			stderr.WriteLine($"ed: open '{filename}': {ex.Message}");
			return 1;
		}

		while (true)
		{
			stdout.WriteLine(":"); // prompt
			var line = stdin.ReadLine();
			if (line is null)
			{
				break;
			}

			line = line.Trim();
			if (line.Length == 0)
			{
				continue;
			}

			try
			{
				if (line == "q")
				{
					return 0;
				}

				if (line == "p")
				{
					for (var i = 0; i < buffer.Count; i++)
					{
						stdout.WriteLine(buffer[i]);
					}

					continue;
				}

				if (line == "n")
				{
					for (var i = 0; i < buffer.Count; i++)
					{
						stdout.WriteLine($"{i + 1}\t{buffer[i]}");
					}

					continue;
				}

				if (line == "d")
				{
					buffer.Clear();
					continue;
				}

				if (line == "a")
				{
					// append after end
					while (true)
					{
						var t = stdin.ReadLine();
						if (t is null)
						{
							break;
						}

						if (t == ".")
						{
							break;
						}

						buffer.Add(t);
					}

					continue;
				}

				if (line == "i")
				{
					var insert = new List<string>();
					while (true)
					{
						var t = stdin.ReadLine();
						if (t is null)
						{
							break;
						}

						if (t == ".")
						{
							break;
						}

						insert.Add(t);
					}

					if (insert.Count > 0)
					{
						buffer.InsertRange(0, insert);
					}

					continue;
				}

				if (line.StartsWith('w'))
				{
					var parts = line.Split(SpaceSeparator, 2, StringSplitOptions.RemoveEmptyEntries);
					var outName = parts.Length > 1 ? parts[1] : filename;
					if (string.IsNullOrEmpty(outName))
					{
						stderr.WriteLine("ed: no filename");
						continue;
					}

					File.WriteAllLines(outName, buffer, Encoding.UTF8);
					stdout.WriteLine($"{buffer.Count}");
					continue;
				}

				// substitution: s/old/new/[g]
				if (line.StartsWith("s/"))
				{
					// parse s/old/new/flags
					var m = SubstitutionWithFlagsRegex().Match(line);
					if (!m.Success)
					{
						// try without trailing slash
						m = SubstitutionNoFlagsRegex().Match(line);
					}

					if (m.Success)
					{
						var oldText = m.Groups["old"].Value;
						var newText = m.Groups["new"].Value;
						var flags = m.Groups["flags"].Success ? m.Groups["flags"].Value : string.Empty;
						var global = flags.Contains('g');
						for (var idx = 0; idx < buffer.Count; idx++)
						{
							if (global)
							{
								buffer[idx] = Regex.Replace(buffer[idx], Regex.Escape(oldText), newText);
							}
							else
							{
								buffer[idx] = Regex.Replace(buffer[idx], Regex.Escape(oldText), newText, RegexOptions.None, TimeSpan.FromMilliseconds(100));
								// Replace only the first occurrence manually since Regex.Replace(string, string, string, int) does not exist
								// Workaround for single replacement:
								var input = buffer[idx];
								var pattern = Regex.Escape(oldText);
								var replaced = false;
								buffer[idx] = Regex.Replace(
									input,
									pattern,
									m => {
										if (!replaced)
										{
											replaced = true;
											return newText;
										}
										return m.Value;
									}
								);
							}
						}

						continue;
					}

					stderr.WriteLine(".");
					continue;
				}

				stderr.WriteLine(".");
			}
			catch (Exception ex)
			{
				stderr.WriteLine($"ed: {ex.Message}");
			}
		}

		return 0;
	}
}
