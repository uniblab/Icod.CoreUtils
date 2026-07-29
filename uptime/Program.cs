namespace Icod.CoreUtils.Uptime;

/// <summary>
/// Provides the executable entry point for the GNU-compatible <c>uptime</c> command for reporting system uptime and load information.
/// </summary>
public static class Program
{
    /// <summary>
    /// Runs the <c>uptime</c> command with the supplied command-line arguments.
    /// </summary>
    /// <param name="args">The command-line arguments supplied to <c>uptime</c>.</param>
    /// <returns>A task whose result is the command exit status.</returns>
    public static Task<int> Main(string[] args) => Command.RunAsync(args);
}
