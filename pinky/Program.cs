namespace Icod.CoreUtils.Pinky;

/// <summary>
/// Provides the executable entry point for the GNU-compatible <c>pinky</c> command for reporting concise user-session information.
/// </summary>
public static class Program
{
    /// <summary>
    /// Runs the <c>pinky</c> command with the supplied command-line arguments.
    /// </summary>
    /// <param name="args">The command-line arguments supplied to <c>pinky</c>.</param>
    /// <returns>A task whose result is the command exit status.</returns>
    public static Task<int> Main(string[] args) => Command.RunAsync(args);
}
