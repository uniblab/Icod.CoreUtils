ï»¿// Ported to .NET by Timothy J. Bruce <uniblab@hotmail.com>

namespace Icod.CoreUtils.Users;

using System;
using System.IO;

/// <summary>
/// $u: placeholder stub. Prints usage and supports -?/--help.
/// Replace the implementation with the actual utility behavior.
/// </summary>
public static class Command {
    public static int Run(string[] args, TextReader? stdin = null, TextWriter? stdout = null, TextWriter? stderr = null) {
        stdout ??= Console.Out;
        stderr ??= Console.Error;

        foreach (var a in args) {
            if (a == "-?" || a == "--help") {
                PrintUsage(stdout);
                return 0;
            }
        }

        // TODO: implement users behavior here.
        PrintUsage(stdout);
        return 0;
    }

    private static void PrintUsage(TextWriter stdout) {
        stdout.WriteLine($"Usage: users [-?]");
        stdout.WriteLine("  -?    display this help and exit");
    }
}
