// Original behavior/reference: sed (Lee E. McMahon)
// Ported to .NET by Timothy J. Bruce <uniblab@hotmail.com>

namespace Icod.LineEditor.Sed;

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Icod.CoreUtils.Shared.CommandLine;
using Icod.CoreUtils.Shared.Diagnostics;
using Icod.CoreUtils.Shared.IO;
using Icod.CoreUtils.Shared.Processes;

/// <summary>
/// Implements a portable, BSD-style <c>sed</c> stream editor using the .NET
/// text and regular-expression APIs.
/// </summary>
/// <remarks>
/// <para>
/// The command processor implements addressed commands, inclusive address
/// ranges, negation, command groups, labels and branches, pattern space,
/// hold space, substitution, transliteration, explicit printing, file
/// reads and writes, next-cycle commands, and in-place editing. Primary
/// input, script files, auxiliary files, and output are processed with TAP
/// operations. Input uses one-record lookahead and is never fully materialized.
/// </para>
/// <para>
/// In syntax descriptions, <c>M</c> and <c>N</c> are metavariables for
/// non-negative or positive decimal line numbers as required by the command.
/// They are not literal characters in a sed program.
/// </para>
/// <para>
/// Supported command-line options include <c>-n</c>, <c>-e</c>, <c>-f</c>,
/// <c>-i[SUFFIX]</c>, <c>-E</c>/<c>-r</c>, <c>-s</c>, <c>-u</c>,
/// <c>-z</c>, <c>-l N</c>, <c>--sandbox</c>, <c>--help</c>, and
/// <c>--version</c>.
/// </para>
/// <para>
/// Supported addresses include line numbers, <c>$</c>, regular-expression
/// addresses, GNU-style <c>first~step</c> addresses, and range ends
/// <c>+N</c> and <c>~N</c>. An address or range may be followed by
/// <c>!</c> to negate its selection.
/// </para>
/// <para>
/// Supported commands are <c>= a b c d D e g G h H i l n N p P q Q r R
/// s t T w W x y</c>, labels introduced with <c>:</c>, comments introduced
/// with <c>#</c>, and grouped commands enclosed in braces.
/// </para>
/// <para>
/// Regular expressions are executed by <see cref="Regex"/>. In basic mode,
/// common sed BRE constructs such as <c>\(...\)</c>, <c>\{m,n\}</c>,
/// <c>\+</c>, <c>\?</c>, and <c>\|</c> are translated to their .NET
/// equivalents. This is source-compatible with common sed scripts, but it
/// is not a byte-for-byte implementation of every locale-sensitive POSIX
/// regular-expression rule.
/// </para>
/// </remarks>
public static partial class Command {

	#region fields
	private const int DefaultListWidth = 70;
	private const int ErrorExitCode = CommandExitCodes.Failure;
	private const int UsageExitCode = CommandExitCodes.UsageError;
	private const string VersionText = "Icod.LineEditor.Sed 1.0";
	#endregion fields

	/// <summary>
	/// Executes <c>sed</c> synchronously with optional standard-stream substitution.
	/// </summary>
	/// <remarks>
	/// This compatibility entry point blocks on the TAP implementation. A <see langword="null"/> text stream selects the corresponding <see cref="Console"/> stream; caller-supplied streams remain caller-owned.
	/// </remarks>
	/// <param name="args">The command-line arguments, excluding the executable name.</param>
	/// <param name="stdin">The text reader to use as standard input, or <see langword="null"/> to use <see cref="Console.In"/>.</param>
	/// <param name="stdout">The text writer to use as standard output, or <see langword="null"/> to use <see cref="Console.Out"/>.</param>
	/// <param name="stderr">The text writer to use as standard error, or <see langword="null"/> to use <see cref="Console.Error"/>.</param>
	/// <returns>The GNU-compatible process exit status: zero for successful command execution and nonzero for a usage or operational failure.</returns>
	public static int Run(
		string[] args,
		TextReader? stdin = null,
		TextWriter? stdout = null,
		TextWriter? stderr = null
	) {
		return RunAsync(
			args,
			stdin,
			stdout,
			stderr,
			CancellationToken.None
		).GetAwaiter().GetResult();
	}

	/// <summary>
	/// Executes <c>sed</c> asynchronously with optional injected standard streams.
	/// </summary>
	/// <remarks>
	/// A <see langword="null"/> text stream selects the corresponding <see cref="Console"/> stream. Caller-supplied streams remain caller-owned.
	/// </remarks>
	/// <param name="args">The command-line arguments, excluding the executable name.</param>
	/// <param name="stdin">The text reader to use as standard input, or <see langword="null"/> to use <see cref="Console.In"/>.</param>
	/// <param name="stdout">The text writer to use as standard output, or <see langword="null"/> to use <see cref="Console.Out"/>.</param>
	/// <param name="stderr">The text writer to use as standard error, or <see langword="null"/> to use <see cref="Console.Error"/>.</param>
	/// <param name="cancellationToken">The token used to cancel parsing, platform queries, and asynchronous I/O.</param>
	/// <returns>The GNU-compatible process exit status: zero for successful command execution and nonzero for a usage or operational failure.</returns>
	public static async Task<int> RunAsync(
		string[] args,
		TextReader? stdin = null,
		TextWriter? stdout = null,
		TextWriter? stderr = null,
		CancellationToken cancellationToken = default
	) {
		args ??= Array.Empty<string>();
		stdin ??= Console.In;
		stdout ??= Console.Out;
		stderr ??= Console.Error;

		try {
			var options = new Options();
			var scriptFragments = new List<string>();
			var files = new List<string>();
			var argumentResult = await ParseArgumentsAsync(
				args,
				options,
				scriptFragments,
				files,
				stdout,
				stderr,
				cancellationToken
			).ConfigureAwait( false );
			if ( argumentResult.HasValue ) {
				return argumentResult.Value;
			}

			if ( 0 == scriptFragments.Count ) {
				if ( 0 == files.Count ) {
					await stderr.WriteLineAsync(
						"sed: no script was provided"
					).ConfigureAwait( false );
					return UsageExitCode;
				}

				scriptFragments.Add(
					files[ 0 ]
				);
				files.RemoveAt(
					0
				);
			}

			if ( 0 == files.Count ) {
				files.Add(
					"-"
				);
			}

			if ( options.InPlace ) {
				options.Separate = true;
			}

			if (
				options.InPlace
				&& files.Any(
					path => "-" == path
				)
			) {
				await stderr.WriteLineAsync(
					"sed: cannot edit standard input in-place"
				).ConfigureAwait( false );
				return UsageExitCode;
			}

			var scriptText = string.Join(
				Environment.NewLine,
				scriptFragments
			);
			var program = new ScriptParser(
				scriptText,
				options.ExtendedRegularExpressions,
				options.Sandbox,
				options.Posix
			).Parse();

			if ( options.Debug ) {
				await stderr.WriteLineAsync(
					"SED PROGRAM:"
				).ConfigureAwait( false );
				foreach ( var scriptLine in scriptText.Split( '\n' ) ) {
					await stderr.WriteLineAsync(
						$"  {scriptLine.TrimEnd( '\r' )}"
					).ConfigureAwait( false );
				}
			}

			if (
				options.Unbuffered
				&& stdout is StreamWriter streamWriter
			) {
				streamWriter.AutoFlush = true;
			}

			if ( options.InPlace ) {
				foreach ( var path in files ) {
					var result = await ProcessInPlaceAsync(
						path,
						options,
						program,
						stderr,
						cancellationToken
					).ConfigureAwait( false );
					if ( result.Quit ) {
						return result.ExitCode;
					}
				}
				return 0;
			}

			if ( options.Separate ) {
				foreach ( var path in files ) {
					using ( var input = new InputSequence(
						new SourceSpec[ 1 ] {
							new SourceSpec(
								path
							)
						},
						stdin,
						options.NullData
					) ) {
						var environment = new ExecutionEnvironment(
							stdout,
							stderr,
							options.SuppressAutomaticPrint,
							options.NullData,
							options.ListWidth,
							options.Debug
						);
						try {
							var result = await ExecuteAsync(
								program,
								input,
								environment,
								cancellationToken
							).ConfigureAwait( false );
							if ( result.Quit ) {
								return result.ExitCode;
							}
						} finally {
							await environment.DisposeAsync().ConfigureAwait( false );
						}
					}
				}
				return 0;
			}

			var sharedEnvironment = new ExecutionEnvironment(
				stdout,
				stderr,
				options.SuppressAutomaticPrint,
				options.NullData,
				options.ListWidth,
				options.Debug
			);
			try {
				using ( var input = new InputSequence(
					files.Select(
						path => new SourceSpec(
							path
						)
					).ToArray(),
					stdin,
					options.NullData
				) ) {
					return (
						await ExecuteAsync(
							program,
							input,
							sharedEnvironment,
							cancellationToken
						).ConfigureAwait( false )
					).ExitCode;
				}
			} finally {
				await sharedEnvironment.DisposeAsync().ConfigureAwait( false );
			}
		} catch ( ScriptParseException ex ) {
			await stderr.WriteLineAsync(
				$"sed: {ex.Message}"
			).ConfigureAwait( false );
			return UsageExitCode;
		} catch ( OperationCanceledException ) {
			await stderr.WriteLineAsync(
				"sed: operation canceled"
			).ConfigureAwait( false );
			return CommandExitCodes.Canceled;
		} catch ( Exception ex ) {
			await stderr.WriteLineAsync(
				$"sed: {ex.Message}"
			).ConfigureAwait( false );
			return ErrorExitCode;
		}
	}

}
