namespace Icod.CoreUtils.Echo;

using System.Text;

/// <summary>
/// Implements the echo utility.
/// </summary>
public static class Command {
	private const int BufferSize = 4096;
	private const string VersionText = "echo (Icod.CoreUtils) 1.0";

	/// <summary>
	/// Executes <c>echo</c> synchronously with optional standard-stream substitution.
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
			stderr
		).GetAwaiter().GetResult();
	}

	/// <summary>
	/// Executes <c>echo</c> asynchronously with optional injected standard streams.
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
		stdout ??= Console.Out;
		stderr ??= Console.Error;
		try {
			cancellationToken.ThrowIfCancellationRequested();
			var posixlyCorrect = null != Environment.GetEnvironmentVariable(
				"POSIXLY_CORRECT"
			);
			if (
				!posixlyCorrect
				&& 1 == args.Length
				&& "--help" == args[ 0 ]
			) {
				await PrintUsageAsync(
					stdout,
					cancellationToken
				).ConfigureAwait( false );
				return 0;
			}
			if (
				!posixlyCorrect
				&& 1 == args.Length
				&& "--version" == args[ 0 ]
			) {
				await stdout.WriteLineAsync(
					VersionText.AsMemory(),
					cancellationToken
				).ConfigureAwait( false );
				return 0;
			}

			var noNewline = false;
			var interpretEscapes = posixlyCorrect;
			var operandIndex = 0;
			var scanOptions = !posixlyCorrect
				|| ( 0 < args.Length && "-n" == args[ 0 ] )
			;
			while (
				scanOptions
				&& operandIndex < args.Length
				&& IsShortOptionCluster( args[ operandIndex ] )
			) {
				foreach ( var option in args[ operandIndex ].AsSpan( 1 ) ) {
					switch ( option ) {
						case 'n':
							noNewline = true;
							break;
						case 'e':
							if ( !posixlyCorrect ) {
								interpretEscapes = true;
							}
							break;
						case 'E':
							if ( !posixlyCorrect ) {
								interpretEscapes = false;
							}
							break;
					}
				}
				operandIndex++;
			}

			if ( interpretEscapes ) {
				return await WriteEscapedAsync(
					args,
					operandIndex,
					noNewline,
					stdout,
					cancellationToken
				).ConfigureAwait( false );
			}

			for ( var index = operandIndex; index < args.Length; index++ ) {
				cancellationToken.ThrowIfCancellationRequested();
				if ( index > operandIndex ) {
					await stdout.WriteAsync(
						" ".AsMemory(),
						cancellationToken
					).ConfigureAwait( false );
				}
				await stdout.WriteAsync(
					args[ index ].AsMemory(),
					cancellationToken
				).ConfigureAwait( false );
			}
			if ( !noNewline ) {
				await stdout.WriteLineAsync(
					ReadOnlyMemory<char>.Empty,
					cancellationToken
				).ConfigureAwait( false );
			}
			return 0;
		} catch ( OperationCanceledException ) {
			return 130;
		} catch ( IOException exception ) {
			await TryWriteErrorAsync(
				stderr,
				System.String.Concat(
					"echo: write error: ",
					exception.Message,
					Environment.NewLine
				)
			).ConfigureAwait( false );
			return 1;
		}
	}

	private static bool IsShortOptionCluster(
		string argument
	) {
		if (
			argument.Length < 2
			|| '-' != argument[ 0 ]
		) {
			return false;
		}
		foreach ( var option in argument.AsSpan( 1 ) ) {
			if (
				'n' != option
				&& 'e' != option
				&& 'E' != option
			) {
				return false;
			}
		}
		return true;
	}

	private static async Task<int> WriteEscapedAsync(
		string[] args,
		int operandIndex,
		bool noNewline,
		TextWriter output,
		CancellationToken cancellationToken
	) {
		var buffer = new StringBuilder( BufferSize );
		var stop = false;
		for ( var argumentIndex = operandIndex; argumentIndex < args.Length && !stop; argumentIndex++ ) {
			if ( argumentIndex > operandIndex ) {
				buffer.Append( ' ' );
			}
			var value = args[ argumentIndex ];
			for ( var index = 0; index < value.Length; index++ ) {
				cancellationToken.ThrowIfCancellationRequested();
				var current = value[ index ];
				if (
					'\\' != current
					|| index + 1 >= value.Length
				) {
					buffer.Append( current );
					await FlushWhenFullAsync(
						buffer,
						output,
						cancellationToken
					).ConfigureAwait( false );
					continue;
				}

				var escape = value[ ++index ];
				switch ( escape ) {
					case '\\':
						buffer.Append( '\\' );
						break;
					case 'a':
						buffer.Append( '\a' );
						break;
					case 'b':
						buffer.Append( '\b' );
						break;
					case 'c':
						stop = true;
						break;
					case 'e':
						buffer.Append( '\u001B' );
						break;
					case 'f':
						buffer.Append( '\f' );
						break;
					case 'n':
						buffer.Append( Environment.NewLine );
						break;
					case 'r':
						buffer.Append( '\r' );
						break;
					case 't':
						buffer.Append( '\t' );
						break;
					case 'v':
						buffer.Append( '\v' );
						break;
					case '0':
						buffer.Append(
							ParseByteEscape(
								value,
								ref index,
								8,
								3,
								allowNoDigits: true
							)
						);
						break;
					case 'x': {
						var before = index;
						var parsed = ParseByteEscape(
							value,
							ref index,
							16,
							2,
							allowNoDigits: false
						);
						if ( before == index ) {
							buffer.Append( "\\x" );
						} else {
							buffer.Append( parsed );
						}
						break;
					}
					default:
						buffer.Append( '\\' );
						buffer.Append( escape );
						break;
				}
				if ( stop ) {
					break;
				}
				await FlushWhenFullAsync(
					buffer,
					output,
					cancellationToken
				).ConfigureAwait( false );
			}
		}
		if (
			!stop
			&& !noNewline
		) {
			buffer.Append( Environment.NewLine );
		}
		if ( 0 < buffer.Length ) {
			await output.WriteAsync(
				buffer.ToString().AsMemory(),
				cancellationToken
			).ConfigureAwait( false );
		}
		return 0;
	}

	private static char ParseByteEscape(
		string value,
		ref int index,
		int radix,
		int maximumDigits,
		bool allowNoDigits
	) {
		var parsed = 0;
		var digits = 0;
		while (
			digits < maximumDigits
			&& index + 1 < value.Length
		) {
			var digit = DigitValue( value[ index + 1 ] );
			if (
				digit < 0
				|| digit >= radix
			) {
				break;
			}
			index++;
			parsed = ( parsed * radix ) + digit;
			digits++;
		}
		if (
			0 == digits
			&& !allowNoDigits
		) {
			return '\0';
		}
		return (char)( parsed & byte.MaxValue );
	}

	private static int DigitValue(
		char value
	) {
		if ( value is >= '0' and <= '9' ) {
			return value - '0';
		}
		if ( value is >= 'a' and <= 'f' ) {
			return value - 'a' + 10;
		}
		if ( value is >= 'A' and <= 'F' ) {
			return value - 'A' + 10;
		}
		return -1;
	}

	private static async Task FlushWhenFullAsync(
		StringBuilder buffer,
		TextWriter output,
		CancellationToken cancellationToken
	) {
		if ( buffer.Length < BufferSize ) {
			return;
		}
		await output.WriteAsync(
			buffer.ToString().AsMemory(),
			cancellationToken
		).ConfigureAwait( false );
		buffer.Clear();
	}

	private static async Task TryWriteErrorAsync(
		TextWriter error,
		string message
	) {
		try {
			await error.WriteAsync( message ).ConfigureAwait( false );
		} catch ( IOException ) {
		}
	}

	private static async Task PrintUsageAsync(
		TextWriter output,
		CancellationToken cancellationToken
	) {
		const string text = """
Usage: echo [SHORT-OPTION]... [STRING]...
  or:  echo LONG-OPTION
Echo the STRING(s) to standard output.

  -n             do not output the trailing newline
  -e             enable interpretation of backslash escapes
  -E             disable interpretation of backslash escapes (default)
      --help     display this help and exit
      --version  output version information and exit

If -e is in effect, the following sequences are recognized:
  \\      backslash                 \\a     alert (BEL)
  \\b     backspace                 \\c     produce no further output
  \\e     escape                    \\f     form feed
  \\n     new line                  \\r     carriage return
  \\t     horizontal tab             \\v     vertical tab
  \\0NNN  byte with octal value NNN (1 to 3 digits)
  \\xHH   byte with hexadecimal value HH (1 to 2 digits)
""";
		await output.WriteAsync(
			text.ReplaceLineEndings( Environment.NewLine ).AsMemory(),
			cancellationToken
		).ConfigureAwait( false );
	}
}
