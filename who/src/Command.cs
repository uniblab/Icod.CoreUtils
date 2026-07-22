// Original behavior/reference: GNU coreutils
// Ported to .NET by Timothy J. Bruce <uniblab@hotmail.com>

namespace Icod.CoreUtils.Who;

using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

/// <summary>
/// who: show who is logged on (best-effort).
/// BCL-only port prints current user and hostname as a minimal approximation.
/// </summary>
public static class Command
{
	public static int Run(string[] args, TextReader? stdin = null, TextWriter? stdout = null, TextWriter? stderr = null)
	{
		stdin ??= Console.In;
		stdout ??= Console.Out;
		stderr ??= Console.Error;

		try
		{
			var user = Environment.UserName;
			var host = Environment.MachineName;
			var tty = "?";
			var time = DateTime.Now.ToString("yyyy-MM-dd HH:mm", System.Globalization.CultureInfo.InvariantCulture);
			stdout.WriteLine($"{user}\t{tty}\t{time}\t({host})");
			return 0;
		}
		catch (Exception ex)
		{
			stderr.WriteLine($"who: {ex.Message}");
			return 1;
		}
	}
}
