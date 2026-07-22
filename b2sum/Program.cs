// Port of the standard UNIX `b2sum` utility to .NET
namespace Icod.CoreUtils.B2Sum;

using System;

internal static class Program
{
    public static int Main(string[] args)
    {
        return Command.Run(args, Console.In, Console.Out, Console.Error);
    }
}