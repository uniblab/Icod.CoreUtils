// Original behavior/reference: GNU coreutils
// Ported to .NET by Timothy J. Bruce <uniblab@hotmail.com>

namespace Icod.CoreUtils.Sha1sum;

using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

/// <summary>
/// sha1sum: compute SHA-1 checksum and file length. Outputs: "&lt;sha1&gt; &lt;length&gt; &lt;filename&gt;"."
/// </summary>
public static class Command
{
	public static int Run(string[] args, TextReader? stdin = null, TextWriter? stdout = null, TextWriter? stderr = null)
	{
		stdout ??= Console.Out;
		stderr ??= Console.Error;

		if (args.Length == 0)
		{
			try
			{
				using var ms = new MemoryStream();
				Console.OpenStandardInput().CopyTo(ms);
				var data = ms.ToArray();
				var hash = SHA1.HashData(data);
				stdout.WriteLine($"{BytesToHex(hash)} {data.Length} -");
				return 0;
			}
			catch (Exception ex)
			{
				stderr.WriteLine($"sha1sum: {ex.Message}");
				return 1;
			}
		}

		var exit = 0;
		foreach (var path in args)
		{
			if (path == "-")
			{
				try
				{
					using var ms = new MemoryStream();
					Console.OpenStandardInput().CopyTo(ms);
					var data = ms.ToArray();
					var hash = SHA1.HashData(data);
					stdout.WriteLine($"{BytesToHex(hash)} {data.Length} -");
				}
				catch (Exception ex)
				{
					stderr.WriteLine($"sha1sum: -: {ex.Message}");
					exit = 1;
				}

				continue;
			}

			try
			{
				var bytes = File.ReadAllBytes(path);
				var hash = SHA1.HashData(bytes);
				stdout.WriteLine($"{BytesToHex(hash)} {bytes.Length} {path}");
			}
			catch (Exception ex)
			{
				stderr.WriteLine($"sha1sum: {path}: {ex.Message}");
				exit = 1;
			}
		}

		return exit;
	}

	private static string BytesToHex(byte[] bytes)
	{
		var sb = new StringBuilder(bytes.Length * 2);
		foreach (var b in bytes)
		{
			_ = sb.Append(b.ToString("x2"));
		}

		return sb.ToString();
	}
}
