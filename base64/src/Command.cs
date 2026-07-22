namespace Icod.CoreUtils.Base64;

using System;
using System.IO;
using System.Text;

/// <summary>
/// base64: encode/decode (supports -d decode).
/// </summary>
public static class Command
{
	public static int Run(string[] args, TextReader? stdin = null, TextWriter? stdout = null, TextWriter? stderr = null)
	{
		stdout ??= Console.Out;
		stderr ??= Console.Error;

		var decode = false;
		var restStart = 0;
		if (args.Length > 0 && args[0] == "-d")
		{
			decode = true;
			restStart = 1;
		}

		if (decode)
		{
			#nullable disable
			var input = Console.OpenStandardInput();
			using var ms = new MemoryStream();
			input.CopyTo(ms);
			var bytes = Convert.FromBase64String(Encoding.UTF8.GetString(ms.ToArray()));
			Console.OpenStandardOutput().Write(bytes, 0, bytes.Length);
			return 0;
		}

		if (restStart >= args.Length)
		{
			using var ms = new MemoryStream();
			Console.OpenStandardInput().CopyTo(ms);
			var encoded = Convert.ToBase64String(ms.ToArray());
			stdout.WriteLine(encoded);
			return 0;
		}

		var exit = 0;
		for (var i = restStart; i < args.Length; i++)
		{
			try
			{
				var bytes = File.ReadAllBytes(args[i]);
				var encoded = Convert.ToBase64String(bytes);
				stdout.WriteLine(encoded);
			}
			catch (Exception ex)
			{
				stderr.WriteLine($"base64: {args[i]}: {ex.Message}");
				exit = 1;
			}
		}

		return exit;
	}
}
