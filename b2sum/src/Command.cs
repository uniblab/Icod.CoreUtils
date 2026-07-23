// Ported to .NET by Timothy J. Bruce <uniblab@hotmail.com>

namespace Icod.CoreUtils.B2Sum;

using System;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using Konscious.Security.Cryptography;

/// <summary>
/// b2sum: compute BLAKE2b (512-bit) checksums for files or stdin.
/// Usage:
///   b2sum [file ...]
///   b2sum -? | --help
/// If no file arguments are provided the utility reads from standard input.
/// Output format: hexadecimal-digest two-spaces filename (or '-' for stdin).
/// </summary>
public static class Command {
	public static int Run(string[] args, TextReader? stdin = null, TextWriter? stdout = null, TextWriter? stderr = null) {
		stdout ??= Console.Out;
		stderr ??= Console.Error;

		var files = new List<string>();
		foreach (var a in args) {
			if (a == "-?" || a == "--help") {
				PrintUsage(stdout);
				return 0;
			}

			// ignore other options for now; treat as filenames
			files.Add(a);
		}

		if (files.Count == 0) {
			files.Add("-");
		}

		var exitCode = 0;
		foreach (var path in files) {
			try {
				Stream stream;
				if (path == "-") {
					// If stdin is a StreamReader, use its underlying BaseStream so we can compute binary hash.
					if (stdin is StreamReader sr) {
						stream = sr.BaseStream;
					} else {
						// fallback to standard input stream
						stream = Console.OpenStandardInput();
					}
				} else {
					stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
				}

				using (stream) {
					// Konscious.Security.Cryptography.Blake2b is used to compute a 64-byte (512-bit) digest.
					using var hasher = new Konscious.Security.Cryptography.HMACBlake2B(64);
					// ComputeHash(Stream) is provided by HashAlgorithm base class
					var hash = hasher.ComputeHash(stream);
					var hex = ToHexLower(hash);
					stdout.WriteLine($"{hex}  {path}");
				}
			} catch (Exception ex) {
				stderr.WriteLine($"b2sum: {path}: {ex.Message}");
				exitCode = 1;
			}
		}

		return exitCode;
	}

	private static void PrintUsage(TextWriter stdout) {
		stdout.WriteLine("Usage: b2sum [file ...]");
		stdout.WriteLine("  -?, --help    display this help and exit");
	}

	private static string ToHexLower(byte[] data) {
		var sb = new StringBuilder(data.Length * 2);
		foreach (var b in data) {
			sb.Append(b.ToString("x2"));
		}
		return sb.ToString();
	}
}
