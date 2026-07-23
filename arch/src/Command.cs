// Ported to .NET by Timothy J. Bruce <uniblab@hotmail.com>

namespace Icod.CoreUtils.Arch;

using System;
using System.IO;
using System.Runtime.InteropServices;

public static class Command
{
	public static int Run(string[] args, TextReader? stdin = null, TextWriter? stdout = null, TextWriter? stderr = null)
	{
		stdin ??= Console.In;
		stdout ??= Console.Out;
		stderr ??= Console.Error;

		try
		{
			// Prefer uname-style names for compatibility with typical `arch`/`uname -m` outputs.
			var mapped = RuntimeInformation.OSArchitecture switch
			{
				Architecture.X64 => "x86_64",
				Architecture.X86 => "i386",
				Architecture.Arm64 => "aarch64",
				Architecture.Arm => "arm",
#if NET9_0_OR_GREATER
				Architecture.S390x => "s390x",
#endif
				Architecture.Wasm => "wasm32",
				_ => RuntimeInformation.OSArchitecture.ToString().ToLowerInvariant(),
			};

			stdout.WriteLine(mapped);
			return 0;
		}
		catch (Exception ex)
		{
			stderr.WriteLine($"arch: {ex.Message}");
			return 1;
		}
	}
}
