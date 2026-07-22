// Original behavior/reference: GNU coreutils
// Ported to .NET by Timothy J. Bruce <uniblab@hotmail.com>

namespace Icod.CoreUtils.Od;

using System;
using System.IO;
using System.Text;

/// <summary>
/// od: octal (and other) dump of files.
/// Supports: -t o (octal bytes), -t x (hex 2-byte), -t c (ASCII chars)
/// Default: octal bytes.
/// </summary>
public static class Command
{
	public static int Run(string[] args, TextReader? stdin = null, TextWriter? stdout = null, TextWriter? stderr = null)
	{
		stdout ??= Console.Out;
		stderr ??= Console.Error;

		var type = "o";
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
				type = args[i];
			}
		}

		var rem = new System.Collections.Generic.List<string>();
		for (; i < args.Length; i++)
		{
			rem.Add(args[i]);
		}

		if (rem.Count == 0)
		{
			rem.Add("-");
		}

		var exit = 0;
		foreach (var path in rem)
		{
			try
			{
				Stream s;
				if (path == "-")
				{
					s = Console.OpenStandardInput();
				}
				else
				{
					s = new FileStream(path, FileMode.Open, FileAccess.Read);
				}

				using (s)
				{
					var buf = new byte[16];
					long offset = 0;
					int read;
					while ((read = s.Read(buf, 0, buf.Length)) > 0)
					{
						if (type == "x")
						{
							var sb = new StringBuilder();
							sb.AppendFormat("{0:X8}  ", offset);
							for (var j = 0; j < read; j += 2)
							{
								if (j + 1 < read)
								{
									var w = (ushort)((buf[j] << 8) | buf[j + 1]);
									sb.AppendFormat("{0:x4} ", w);
								}
								else
								{
									sb.AppendFormat("{0:x2}   ", buf[j]);
								}
							}

							stdout.WriteLine(sb.ToString());
						}
						else if (type == "c")
						{
							var sb = new StringBuilder();
							sb.AppendFormat("{0:X8}  ", offset);
							for (var j = 0; j < read; j++)
							{
								var b = buf[j];
								if (b >= 32 && b <= 126)
								{
									sb.Append((char)b);
								}
								else
								{
									sb.AppendFormat("\\{0:D3}", b);
								}

								if (j + 1 < read)
								{
									sb.Append(' ');
								}
							}

							stdout.WriteLine(sb.ToString());
						}
						else
						{
							var sb = new StringBuilder();
							sb.AppendFormat("{0:X8}  ", offset);
							for (var j = 0; j < read; j++)
							{
								sb.AppendFormat("{0:D3} ", buf[j]);
							}

							stdout.WriteLine(sb.ToString());
						}

						offset += read;
					}
				}
			}
			catch (Exception ex)
			{
				stderr.WriteLine($"od: {path}: {ex.Message}");
				exit = 1;
			}
		}

		return exit;
	}
}
