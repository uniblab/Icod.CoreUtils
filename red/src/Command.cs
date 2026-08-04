namespace Icod.LineEditor.Red;

using System;
using System.IO;

/// <summary>
/// Provides the public command facade for the restricted line editor.
/// </summary>
/// <remarks>
/// Phase LE0 establishes the final public identity without implementing the
/// restricted editor engine. Phase LE8 replaces this seed behavior with the
/// shared Ed engine under the Red security profile.
/// </remarks>
public static class Command {
	/// <summary>
	/// Runs the current Red seed command.
	/// </summary>
	/// <param name="args">The command-line arguments.</param>
	/// <param name="stdin">The optional standard-input reader.</param>
	/// <param name="stdout">The optional standard-output writer.</param>
	/// <param name="stderr">The optional standard-error writer.</param>
	/// <returns>The process exit status.</returns>
	public static int Run(
		string[] args,
		TextReader? stdin = null,
		TextWriter? stdout = null,
		TextWriter? stderr = null
	) {
		ArgumentNullException.ThrowIfNull( args );
		_ = stdin;
		_ = stderr;
		stdout ??= Console.Out;
		stdout.WriteLine( "Hello, World!" );
		return 0;
	}
}
