// Original behavior/reference: GNU coreutils
// Ported to .NET by Timothy J. Bruce <uniblab@hotmail.com>

namespace Icod.CoreUtils.Base32;

using System;
using System.IO;
using System.Linq;
using System.Text;

/// <summary>
/// base32: encode/decode (supports -d decode).
/// Encoded output uses the RFC 4648 alphabet (A-Z2-7) with '=' padding.
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
			if (restStart >= args.Length)
			{
				try
				{
					stdin ??= Console.In;
					using var ms = new MemoryStream();
					Console.OpenStandardInput().CopyTo(ms);
					var input = Encoding.ASCII.GetString(ms.ToArray());
					var cleaned = CleanBase32Input(input);
					var decoded = DecodeBase32(cleaned);
					Console.OpenStandardOutput().Write(decoded, 0, decoded.Length);
					return 0;
				}
				catch (Exception ex)
				{
					stderr.WriteLine($"base32: {ex.Message}");
					return 1;
				}
			}

			var exitDecode = 0;
			for (var i = restStart; i < args.Length; i++)
			{
				try
				{
					var text = File.ReadAllText(args[i], Encoding.ASCII);
					var cleaned = CleanBase32Input(text);
					var decoded = DecodeBase32(cleaned);
					File.WriteAllBytes(args[i] + ".out", decoded);
					Console.OpenStandardOutput().Write(decoded, 0, decoded.Length);
				}
				catch (Exception ex)
				{
					stderr.WriteLine($"base32: {args[i]}: {ex.Message}");
					exitDecode = 1;
				}
			}

			return exitDecode;
		}
		else
		{
			if (restStart >= args.Length)
			{
				try
				{
					stdin ??= Console.In;
					using var ms = new MemoryStream();
					Console.OpenStandardInput().CopyTo(ms);
					var data = ms.ToArray();
					var encoded = EncodeBase32(data);
					stdout.WriteLine(encoded);
					return 0;
				}
				catch (Exception ex)
				{
					stderr.WriteLine($"base32: {ex.Message}");
					return 1;
				}
			}

			var exit = 0;
			for (var i = restStart; i < args.Length; i++)
			{
				try
				{
					var bytes = File.ReadAllBytes(args[i]);
					var encoded = EncodeBase32(bytes);
					stdout.WriteLine(encoded);
				}
				catch (Exception ex)
				{
					stderr.WriteLine($"base32: {args[i]}: {ex.Message}");
					exit = 1;
				}
			}

			return exit;
		}
	}

	private const string Alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";

	private static string EncodeBase32(byte[] data)
	{
		if (data.Length == 0)
		{
			return string.Empty;
		}

		var sb = new StringBuilder();
		var bits = 0;
		var value = 0;

		foreach (var b in data)
		{
			value = (value << 8) | b;
			bits += 8;

			while (bits >= 5)
			{
				var index = (value >> (bits - 5)) & 0x1F;
				sb.Append(Alphabet[index]);
				bits -= 5;
			}
		}

		if (bits > 0)
		{
			var index = (value << (5 - bits)) & 0x1F;
			sb.Append(Alphabet[index]);
		}

		// Pad output to a multiple of 8 characters with '='
		while (sb.Length % 8 != 0)
		{
			sb.Append('=');
		}

		return sb.ToString();
	}

	private static byte[] DecodeBase32(string input)
	{
		if (string.IsNullOrEmpty(input))
		{
			return Array.Empty<byte>();
		}

		var cleaned = input.TrimEnd('=');
		var bits = 0;
		var value = 0;
		var output = new MemoryStream();

		foreach (var ch in cleaned)
		{
			var idx = Alphabet.IndexOf(ch);
			if (idx < 0)
			{
				throw new FormatException("Invalid base32 character encountered.");
			}

			value = (value << 5) | idx;
			bits += 5;

			if (bits >= 8)
			{
				var b = (byte)((value >> (bits - 8)) & 0xFF);
				output.WriteByte(b);
				bits -= 8;
			}
		}

		return output.ToArray();
	}

	private static string CleanBase32Input(string input)
	{
		if (input is null)
		{
			return string.Empty;
		}

		// Remove whitespace and convert to upper-case
		var cleaned = new string(input.Where(c => !char.IsWhiteSpace(c)).ToArray()).ToUpperInvariant();
		return cleaned;
	}
}
