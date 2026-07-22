// Original behavior/reference: GNU coreutils
// Ported to .NET by Timothy J. Bruce <uniblab@hotmail.com>

namespace Icod.CoreUtils.Printenv;

using System;
using System.IO;
using System.Collections;

/// <summary>
/// printenv: print environment variables. With no arguments prints all variables.
/// With one or more NAME arguments prints the values of those variables.
/// </summary>
public static class Command
{
	public static int Run(string[] args, TextReader? stdin = null, TextWriter? stdout = null, TextWriter? stderr = null)
	{
		stdout ??= Console.Out;
		stderr ??= Console.Error;

		if (args.Length == 0)
		{
			foreach (DictionaryEntry de in Environment.GetEnvironmentVariables())
			{
				stdout.WriteLine($"{de.Key}={de.Value}");
			}

			return 0;
		}

		var exit = 0;
		foreach (var name in args)
		{
			try
			{
				var val = Environment.GetEnvironmentVariable(name);
				if (val is null)
				{
					exit = 1;
					continue;
				}

				stdout.WriteLine(val);
			}
			catch (Exception ex)
			{
				stderr.WriteLine($"printenv: {name}: {ex.Message}");
				exit = 1;
			}
		}

		return exit;
	}
}
