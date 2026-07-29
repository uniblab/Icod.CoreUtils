namespace Icod.CoreUtils.Date;

/// <summary>
/// Provides the executable entry point for the GNU-compatible <c>date</c> command for displaying and formatting system date and time values.
/// </summary>
public static class Program
{
    /// <summary>
    /// Runs the <c>date</c> command with the supplied command-line arguments.
    /// </summary>
    /// <param name="args">The command-line arguments supplied to <c>date</c>.</param>
    /// <returns>A task whose result is the command exit status.</returns>
    public static Task<int> Main(string[] args) => Command.RunAsync(args);
}
