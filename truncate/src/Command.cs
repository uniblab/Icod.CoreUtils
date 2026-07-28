using System.Numerics;
using Icod.CoreUtils.Shared.CommandLine;
using Icod.CoreUtils.Shared.Diagnostics;
using Icod.CoreUtils.Shared.IO;

namespace Icod.CoreUtils.Truncate;

/// <summary>
/// Implements <c>truncate [OPTION]... FILE...</c> using GNU Coreutils 9.11 semantics.
/// </summary>
public static class Command {

	private const string ProgramName = "truncate";
	private const string Version = "truncate (Icod.CoreUtils) 1.0";

	/// <summary>Runs the command synchronously.</summary>
	public static int Run(
		string[] args,
		TextReader? stdin = null,
		TextWriter? stdout = null,
		TextWriter? stderr = null
	) => RunAsync(
		args,
		stdin,
		stdout,
		stderr
	).GetAwaiter().GetResult();

	/// <summary>Runs the command asynchronously with optional text streams.</summary>
	public static Task<int> RunAsync(
		string[] args,
		TextReader? stdin = null,
		TextWriter? stdout = null,
		TextWriter? stderr = null,
		CancellationToken cancellationToken = default
	) => RunAsync(
		args ?? [],
		new CommandContext(
			ProgramName,
			stdin ?? Console.In,
			stdout ?? Console.Out,
			stderr ?? Console.Error,
			cancellationToken: cancellationToken
		),
		SystemTruncatePlatform.Instance
	);

	/// <summary>Runs the command with an explicit command context.</summary>
	public static Task<int> RunAsync(
		string[] args,
		CommandContext context
	) => RunAsync(
		args,
		context,
		SystemTruncatePlatform.Instance
	);

	/// <summary>Runs the command with an injectable platform provider.</summary>
	public static async Task<int> RunAsync(
		string[] args,
		CommandContext context,
		ITruncatePlatform platform
	) {
		ArgumentNullException.ThrowIfNull(
			context
		);
		ArgumentNullException.ThrowIfNull(
			platform
		);
		args ??= [];
		var cancellationToken = context.CancellationToken;
		try {
			var result = CreateParser().Parse(
				args
			);
			if ( await WriteParseErrorsAsync(
				result,
				context
			).ConfigureAwait( false ) ) {
				return CommandExitCodes.Failure;
			}
			if ( result.HasOption( "help" ) ) {
				await WriteHelpAsync(
					context
				).ConfigureAwait( false );
				return CommandExitCodes.Success;
			}
			if ( result.HasOption( "version" ) ) {
				await context.StandardOutput.WriteLineAsync(
					Version.AsMemory(),
					cancellationToken
				).ConfigureAwait( false );
				return CommandExitCodes.Success;
			}

			var noCreate = result.HasOption( "no-create" );
			var ioBlocks = result.HasOption( "io-blocks" );
			var referencePath = result.GetLastValue( "reference" );
			var gotSize = false;
			var specification = new TruncateSizeSpecification(
				TruncateSizeMode.Absolute,
				0
			);
			foreach ( var occurrence in result.GetOccurrences( "size" ) ) {
				if ( !TruncateSizeParser.TryParse(
					occurrence.Value,
					specification.Mode,
					out specification,
					out var parseError
				) ) {
					await context.Diagnostics.ErrorAsync(
						parseError,
						cancellationToken
					).ConfigureAwait( false );
					return CommandExitCodes.Failure;
				}
				gotSize = true;
			}

			if ( !gotSize && null == referencePath ) {
				await context.Diagnostics.ErrorAsync(
					"you must specify either '--size' or '--reference'",
					cancellationToken
				).ConfigureAwait( false );
				return CommandExitCodes.Failure;
			}
			if ( ioBlocks && !gotSize ) {
				await context.Diagnostics.ErrorAsync(
					"--io-blocks was specified but --size was not",
					cancellationToken
				).ConfigureAwait( false );
				return CommandExitCodes.Failure;
			}
			if (
				null != referencePath
				&& gotSize
				&& TruncateSizeMode.Absolute == specification.Mode
			) {
				await context.Diagnostics.ErrorAsync(
					"--reference cannot be used with an absolute --size",
					cancellationToken
				).ConfigureAwait( false );
				return CommandExitCodes.Failure;
			}
			if ( 0 == result.Operands.Count ) {
				await context.Diagnostics.ErrorAsync(
					"missing file operand",
					cancellationToken
				).ConfigureAwait( false );
				return CommandExitCodes.Failure;
			}

			long? referenceLength = null;
			if ( null != referencePath ) {
				var referenceResult = await GetReferenceLengthAsync(
					referencePath,
					cancellationToken
				).ConfigureAwait( false );
				if ( !referenceResult.Succeeded ) {
					await context.Diagnostics.ErrorAsync(
						String.Concat(
							"cannot stat '",
							referencePath,
							"': ",
							referenceResult.Message
						),
						cancellationToken
					).ConfigureAwait( false );
					return CommandExitCodes.Failure;
				}
				referenceLength = referenceResult.Length;
			}

			var targetPaths = PathnameExpander.Expand(
				result.Operands,
				new PathnameExpansionOptions {
					IncludeDirectories = true,
					IncludeFiles = true,
					PreserveUnmatchedPatterns = true,
				}
			);
			var failed = false;
			foreach ( var path in targetPaths ) {
				cancellationToken.ThrowIfCancellationRequested();
				var targetResult = await ProcessTargetAsync(
					path,
					noCreate,
					ioBlocks,
					gotSize,
					specification,
					referenceLength,
					context,
					platform
				).ConfigureAwait( false );
				failed |= !targetResult;
			}
			return failed
				? CommandExitCodes.Failure
				: CommandExitCodes.Success
			;
		} catch ( OperationCanceledException ) {
			return CommandExitCodes.Canceled;
		} catch ( Exception exception ) when ( IsFileSystemException( exception ) ) {
			await context.Diagnostics.ErrorAsync(
				exception.Message,
				context.CancellationToken
			).ConfigureAwait( false );
			return CommandExitCodes.Failure;
		}
	}

	private static async Task<bool> ProcessTargetAsync(
		string path,
		bool noCreate,
		bool ioBlocks,
		bool gotSize,
		TruncateSizeSpecification specification,
		long? referenceLength,
		CommandContext context,
		ITruncatePlatform platform
	) {
		var cancellationToken = context.CancellationToken;
		FileStream file;
		try {
			file = new FileStream(
				path,
				noCreate
					? FileMode.Open
					: FileMode.OpenOrCreate,
				FileAccess.Write,
				FileShare.ReadWrite | FileShare.Delete,
				4096,
				FileOptions.Asynchronous | FileOptions.RandomAccess
			);
		} catch ( FileNotFoundException ) when ( noCreate ) {
			return true;
		} catch ( DirectoryNotFoundException ) when ( noCreate ) {
			return true;
		} catch ( Exception exception ) when ( IsFileSystemException( exception ) ) {
			await context.Diagnostics.ErrorAsync(
				String.Concat( "cannot open '", path, "' for writing: ", exception.Message ),
				cancellationToken
			).ConfigureAwait( false );
			return false;
		}

		try {
			await using ( file.ConfigureAwait( false ) ) {
				long currentLength;
				try {
					currentLength = file.Length;
				} catch ( Exception exception ) when ( IsFileSystemException( exception ) ) {
					await context.Diagnostics.ErrorAsync(
						String.Concat( "cannot determine size of '", path, "': ", exception.Message ),
						cancellationToken
					).ConfigureAwait( false );
					return false;
				}

				var effectiveSpecification = specification;
				if ( !gotSize ) {
					effectiveSpecification = new TruncateSizeSpecification(
						TruncateSizeMode.Absolute,
						referenceLength!.Value
					);
				} else if ( ioBlocks ) {
					var blockResult = await platform.GetIoBlockSizeAsync(
						file,
						path,
						cancellationToken
					).ConfigureAwait( false );
					if ( !blockResult.Succeeded ) {
						await context.Diagnostics.ErrorAsync(
							String.Concat(
								"cannot determine I/O block size for '",
								path,
								"': ",
								blockResult.Message
							),
							cancellationToken
						).ConfigureAwait( false );
						return false;
					}
					if ( !TryMultiplyByBlockSize(
						effectiveSpecification,
						blockResult.Value,
						out effectiveSpecification
					) ) {
						await context.Diagnostics.ErrorAsync(
							String.Concat( "overflow in block-size multiplication for '", path, "'" ),
							cancellationToken
						).ConfigureAwait( false );
						return false;
					}
				}

				var baseLength = referenceLength ?? currentLength;
				if ( !TryCalculateLength(
					baseLength,
					effectiveSpecification,
					out var desiredLength,
					out var calculationError
				) ) {
					await context.Diagnostics.ErrorAsync(
						String.Concat( "cannot truncate '", path, "': ", calculationError ),
						cancellationToken
					).ConfigureAwait( false );
					return false;
				}
				if ( desiredLength == currentLength ) {
					return true;
				}

				var setResult = await platform.SetLengthAsync(
					file,
					desiredLength,
					cancellationToken
				).ConfigureAwait( false );
				if ( setResult.Succeeded ) {
					return true;
				}
				await context.Diagnostics.ErrorAsync(
					String.Concat(
						"cannot truncate '",
						path,
						"' to ",
						desiredLength,
						" bytes: ",
						setResult.Message
					),
					cancellationToken
				).ConfigureAwait( false );
				return false;
			}
		} catch ( Exception exception ) when ( IsFileSystemException( exception ) ) {
			await context.Diagnostics.ErrorAsync(
				String.Concat( "failed to close '", path, "': ", exception.Message ),
				cancellationToken
			).ConfigureAwait( false );
			return false;
		}
	}

	private static bool TryCalculateLength(
		long baseLength,
		TruncateSizeSpecification specification,
		out long length,
		out string error
	) {
		var baseValue = new BigInteger(
			baseLength
		);
		var sizeValue = new BigInteger(
			specification.Value
		);
		BigInteger desired = specification.Mode switch {
			TruncateSizeMode.Absolute => sizeValue,
			TruncateSizeMode.Relative => baseValue + sizeValue,
			TruncateSizeMode.AtMost => BigInteger.Min( baseValue, sizeValue ),
			TruncateSizeMode.AtLeast => BigInteger.Max( baseValue, sizeValue ),
			TruncateSizeMode.RoundDown => baseValue - ( baseValue % sizeValue ),
			TruncateSizeMode.RoundUp => (
				( baseValue + sizeValue - BigInteger.One )
				/ sizeValue
			) * sizeValue,
			_ => BigInteger.MinusOne,
		};
		if ( BigInteger.Zero > desired ) {
			desired = BigInteger.Zero;
		}
		if ( desired > long.MaxValue ) {
			length = 0;
			error = "resulting file size is too large";
			return false;
		}
		length = ( long )desired;
		error = string.Empty;
		return true;
	}

	private static bool TryMultiplyByBlockSize(
		TruncateSizeSpecification specification,
		long blockSize,
		out TruncateSizeSpecification multiplied
	) {
		if ( 0 >= blockSize ) {
			multiplied = default;
			return false;
		}
		var value = new BigInteger( specification.Value ) * blockSize;
		if (
			value < long.MinValue
			|| value > long.MaxValue
		) {
			multiplied = default;
			return false;
		}
		multiplied = specification with {
			Value = ( long )value,
		};
		return true;
	}

	private static ValueTask<ReferenceLengthResult> GetReferenceLengthAsync(
		string path,
		CancellationToken cancellationToken
	) {
		cancellationToken.ThrowIfCancellationRequested();
		try {
			var information = new FileInfo(
				path
			);
			var length = information.Length;
			return ValueTask.FromResult(
				new ReferenceLengthResult(
					true,
					length,
					string.Empty
				)
			);
		} catch ( Exception exception ) when ( IsFileSystemException( exception ) ) {
			return ValueTask.FromResult(
				new ReferenceLengthResult(
					false,
					0,
					exception.Message
				)
			);
		}
	}

	private static bool IsFileSystemException(
		Exception exception
	) {
		return exception is IOException
			or ObjectDisposedException
			or UnauthorizedAccessException
			or ArgumentException
			or NotSupportedException
			or System.Security.SecurityException;
	}

	private static OptionParser CreateParser() {
		return new OptionParser(
			[
				new OptionDefinition( "no-create", 'c', [ "no-create" ] ),
				new OptionDefinition( "io-blocks", 'o', [ "io-blocks" ] ),
				new OptionDefinition( "reference", 'r', [ "reference" ], OptionValueArity.Required ),
				new OptionDefinition( "size", 's', [ "size" ], OptionValueArity.Required ),
				new OptionDefinition( "help", null, [ "help" ] ),
				new OptionDefinition( "version", null, [ "version" ] ),
			],
			new OptionParserSettings {
				AllowLongOptionAbbreviations = true,
				Ordering = OptionOrdering.Permute,
			}
		);
	}

	private static async Task<bool> WriteParseErrorsAsync(
		OptionParseResult result,
		CommandContext context
	) {
		if ( result.IsSuccess ) {
			return false;
		}
		foreach ( var error in result.Errors ) {
			await context.StandardError.WriteLineAsync(
				OptionDiagnosticFormatter.Format( context.ProgramName, error ).AsMemory(),
				context.CancellationToken
			).ConfigureAwait( false );
		}
		return true;
	}

	private static async Task WriteHelpAsync(
		CommandContext context
	) {
		const string help = """
Usage: truncate OPTION... FILE...
Shrink or extend the size of each FILE to the specified size.

A FILE argument that does not exist is created unless -c is specified.
A FILE argument that is larger than the specified size loses data.  If a FILE
is shorter, it is extended and the extended part reads as zero bytes.

  -c, --no-create        do not create any files
  -o, --io-blocks        treat SIZE as a number of I/O blocks instead of bytes
  -r, --reference=RFILE  base size on RFILE
  -s, --size=SIZE        set or adjust the file size by SIZE bytes
      --help             display this help and exit
      --version          output version information and exit

SIZE is an integer followed by an optional unit: K, M, G, T, P, E, Z, Y, R,
or Q mean powers of 1024; KB, MB, ... mean powers of 1000; and KiB, MiB, ...
mean powers of 1024.  A leading '+' or '-' makes SIZE relative.  Prefix SIZE
with '<' or '>' to limit the size to at most or at least SIZE, or with '/' or
'%' to round down or up to a multiple of SIZE.
""";
		await context.StandardOutput.WriteAsync(
			help.ReplaceLineEndings( Environment.NewLine ).AsMemory(),
			context.CancellationToken
		).ConfigureAwait( false );
	}

	private readonly record struct ReferenceLengthResult(
		bool Succeeded,
		long Length,
		string Message
	);
}
