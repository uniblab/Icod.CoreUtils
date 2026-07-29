namespace Icod.ProcPs.Ps;

/// <summary>
/// Provides the executable entry point for the GNU-compatible <c>ps</c> command for reporting process information.
/// </summary>
public static class Program
{
    /// <summary>
    /// Runs the <c>ps</c> command with the supplied command-line arguments.
    /// </summary>
    /// <param name="args">The command-line arguments supplied to <c>ps</c>.</param>
    /// <returns>A task whose result is the command exit status.</returns>
    public static Task<int> Main(string[] args) => Command.RunAsync(args);
}
