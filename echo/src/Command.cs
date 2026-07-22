// Original behavior/reference: GNU coreutils
// Ported to .NET by Timothy J. Bruce <uniblab@hotmail.com>

namespace Icod.CoreUtils.Echo;

using System;
using System.IO;
using System.Text;

/// <summary>
/// echo: display a line of text
/// Supported options: -n (no trailing newline), -e (enable backslash escapes)
/// </summary>
public static class Command
{
	public static int Run(string[] args, TextReader? stdin = null, TextWriter? stdout = null, TextWriter? stderr = null)
	{
		stdout ??= Console.Out;
		stderr ??= Console.Error;

		var noNewline = false;
		var enableEscapes = false;
		var i = 0;
		for (; i < args.Length; i++)
		{
			if (!args[i].StartsWith('-'))
			{
				break;
			}

			if (args[i] == "-n")
			{
				noNewline = true;
			}
			else if (args[i] == "-e")
			{
				enableEscapes = true;
			}
			else
			{
				break;
			}
		}

		var rem = new System.Collections.Generic.List<string>();
		for (; i < args.Length; i++)
		{
			rem.Add(args[i]);
		}

		var output = string.Join(" ", rem);
		if (enableEscapes)
		{
			output = UnescapeCStyle(output);
		}

		if (noNewline)
		{
			stdout.Write(output);
		}
		else
		{
			stdout.WriteLine(output);
		}

		return 0;
	}

	private static string UnescapeCStyle(string s)
	{
		var sb = new StringBuilder(s.Length);
		for (var i = 0; i < s.Length; i++)
		{
			var c = s[i];
			if (c == '\\' && i + 1 < s.Length)
			{
				i++;
				var n = s[i];
				if (n == 'n')
				{
					sb.Append('\n');
				}
				else if (n == 't')
				{
					sb.Append('\t');
				}
				else if (n == 'r')
				{
					sb.Append('\r');
				}
				else if (n == '0')
				{
					sb.Append('\0');
				}
				else if (n == '\\')
				{
					sb.Append('\\');
				}
				else
				{
					sb.Append(n);
				}
			}
			else
			{
				sb.Append(c);
			}
		}

		return sb.ToString();
	}
}
