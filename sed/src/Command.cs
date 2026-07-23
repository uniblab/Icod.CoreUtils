// Original behavior/reference: sed (Lee E. McMahon)
// Ported to .NET by Timothy J. Bruce <uniblab@hotmail.com>

namespace Icod.CoreUtils.Sed;

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using System.Text;
using System.Diagnostics.CodeAnalysis;

/// <summary>
/// sed: stream editor (simplified).
/// Supported features:
///   -n		suppress automatic printing
///   -e script add script to the commands to run
/// Script forms supported (simple subset):
///   s/old/new/g   substitute (supports regex) with optional g flag
/// Multiple -e accepted; first script applied if none specified.
/// This is a small, portable subset for common use cases.
/// </summary>
public static class Command
{
	[GeneratedRegex(@"^s/(?<old>.*?)/(?<new>.*?)/(?<flags>.*)$")]
	private static partial Regex SubstWithFlagsRegex();

	[GeneratedRegex(@"^s/(?<old>.*?)/(?<new>.*)$")]
	private static partial Regex SubstNoFlagsRegex();

	public static int Run(string[] args, TextReader? stdin = null, TextWriter? stdout = null, TextWriter? stderr = null)
	{
		stdin ??= Console.In;
		stdout ??= Console.Out;
		stderr ??= Console.Error;

		var scripts = new List<string>();
		var suppress = false;
		var files = new List<string>();

		var i = 0;
		for (; i < args.Length; i++)
		{
			var a = args[i];
			if (!a.StartsWith( '-' ))
			{
				break;
			}

			if (a == "-n")
			{
				suppress = true;
			}
			else if (a == "-e")
			{
				if (i + 1 < args.Length)
				{
					i++;
					scripts.Add(args[i]);
				}
			}
			else
			{
				// ignore other options
			}
		}

		for (; i < args.Length; i++)
		{
			files.Add(args[i]);
		}

		if (scripts.Count == 0)
		{
			stderr.WriteLine("sed: no scripts provided");
			return 2;
		}

		if (files.Count == 0)
		{
			files.Add("-");
		}

		try
		{
			foreach (var path in files)
			{
				TextReader reader;
				if (path == "-")
				{
					reader = stdin;
				}
				else
				{
					reader = new StreamReader(path, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
				}

				using (reader)
				{
					string? line;
					while ((line = reader.ReadLine()) is not null)
					{
						var outLine = line;
						foreach (var script in scripts)
						{
							if (script.StartsWith("s/"))
							{
								// parse s/old/new/flags
								var m = SubstWithFlagsRegex().Match(script);
								if (!m.Success)
								{
									m = SubstNoFlagsRegex().Match(script);
								}

								if (m.Success)
								{
									var oldPat = m.Groups["old"].Value;
									var newText = m.Groups["new"].Value;
									var flags = m.Groups["flags"].Success ? m.Groups["flags"].Value : string.Empty;
									var regexOptions = RegexOptions.None;
									var replaceCount = flags.Contains('g') ? -1 : 1;
									outLine = RegexReplace(outLine, oldPat, newText, regexOptions, replaceCount);
								}
							}
							else
							{
								// unsupported script: ignore
							}
						}

						if (!suppress)
						{
							stdout.WriteLine(outLine);
						}
					}
				}
			}

			return 0;
		}
		catch (Exception ex)
		{
			stderr.WriteLine($"sed: {ex.Message}");
			return 1;
		}
	}

	private static string RegexReplace(string input, string pattern, string replacement, RegexOptions options, int maxReplacements)
	{
		try
		{
			if (maxReplacements == -1)
			{
				return Regex.Replace(input, pattern, replacement, options);
			}

			var regex = new Regex(pattern, options);
			var count = 0;
			return regex.Replace(input, m =>
			{
				count++;
				if (count <= maxReplacements)
				{
					return replacement;
				}

				return m.Value;
			});
		}
		catch
		{
			return input;
		}
	}
}
