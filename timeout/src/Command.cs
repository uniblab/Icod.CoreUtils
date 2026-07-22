// Original behavior/reference: GNU coreutils
// Ported to .NET by Timothy J. Bruce <uniblab@hotmail.com>

namespace Icod.CoreUtils.Timeout;

using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Text;
using System.Globalization;

/// <summary>
/// timeout: run a command with a time limit (basic implementation).
/// Usage: timeout SECONDS COMMAND [ARG...]
/// Returns 124 if the command times out, otherwise the command's exit code.
/// </summary>
public static class Command
{
	public static int Run(string[] args, TextReader? stdin = null, TextWriter? stdout = null, TextWriter? stderr = null)
	{
		stdin ??= Console.In;
		stdout ??= Console.Out;
		stderr ??= Console.Error;

		if (args.Length < 2)
		{
			stderr.WriteLine("timeout: missing operand");
			return 1;
		}

		if (!double.TryParse(args[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var seconds) || seconds < 0)
		{
			stderr.WriteLine($"timeout: invalid duration '{args[0]}'");
			return 1;
		}

		var cmd = args[1];
		var cmdArgs = args.Length > 2 ? string.Join(" ", args.Skip(2).Select(a => QuoteArg(a))) : string.Empty;

		try
		{
			using var proc = new Process();
			proc.StartInfo.FileName = cmd;
			proc.StartInfo.Arguments = cmdArgs;
			proc.StartInfo.UseShellExecute = false;
			proc.StartInfo.RedirectStandardOutput = true;
			proc.StartInfo.RedirectStandardError = true;
			proc.StartInfo.RedirectStandardInput = true;

			proc.Start();

			var stdOutTask = Task.Run(async () =>
			{
				char[] buffer = new char[4096];
				while (!proc.HasExited)
				{
					var read = await proc.StandardOutput.ReadAsync(buffer, 0, buffer.Length);
					if (read > 0)
					{
						stdout.Write(new string(buffer, 0, read));
					}
					else
					{
						await Task.Delay(10);
					}
				}

				while (!proc.StandardOutput.EndOfStream)
				{
					var line = proc.StandardOutput.ReadLine();
					if (line is not null)
					{
						stdout.WriteLine(line);
					}
					else
					{
						break;
					}
				}
			});

			var stdErrTask = Task.Run(async () =>
			{
				char[] buffer = new char[4096];
				while (!proc.HasExited)
				{
					var read = await proc.StandardError.ReadAsync(buffer, 0, buffer.Length);
					if (read > 0)
					{
						stderr.Write(new string(buffer, 0, read));
					}
					else
					{
						await Task.Delay(10);
					}
				}

				while (!proc.StandardError.EndOfStream)
				{
					var line = proc.StandardError.ReadLine();
					if (line is not null)
					{
						stderr.WriteLine(line);
					}
					else
					{
						break;
					}
				}
			});

			var finished = proc.WaitForExit((int)Math.Ceiling(seconds * 1000.0));
			if (!finished)
			{
				try
				{
					proc.Kill(entireProcessTree: true);
				}
				catch
				{
				}

				return 124;
			}

			stdOutTask.Wait();
			stdErrTask.Wait();
			return proc.ExitCode;
		}
		catch (Exception ex)
		{
			stderr.WriteLine($"timeout: failed to run '{cmd}': {ex.Message}");
			return 1;
		}
	}

	private static string QuoteArg(string s)
	{
		if (s.Contains(' ') || s.Contains('"'))
		{
			return $"\"{s.Replace("\"", "\\\"")}\"";
		}

		return s;
	}
}
