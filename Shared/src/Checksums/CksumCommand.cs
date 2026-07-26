namespace Icod.CoreUtils.Shared.Checksums;

using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Icod.CoreUtils.Shared.CommandLine;
using Icod.CoreUtils.Shared.Diagnostics;
using Icod.CoreUtils.Shared.IO;

internal sealed class CksumOptions {

	public string AlgorithmName {
		get;
		set;
	} = "crc";

	public bool Base64 {
		get;
		set;
	}

	public bool Check {
		get;
		set;
	}

	public bool Debug {
		get;
		set;
	}

	public bool IgnoreMissing {
		get;
		set;
	}

	public int? LengthBits {
		get;
		set;
	}

	public bool Quiet {
		get;
		set;
	}

	public bool Raw {
		get;
		set;
	}

	public bool Status {
		get;
		set;
	}

	public bool Strict {
		get;
		set;
	}

	public bool Tagged {
		get;
		set;
	} = true;

	public bool Warn {
		get;
		set;
	}

	public bool Zero {
		get;
		set;
	}

}

internal sealed record NumericChecksumManifestRecord(
	ChecksumAlgorithmKind Algorithm,
	ulong ExpectedChecksum,
	long ExpectedLengthOrBlocks,
	string FileName
);

/// <summary>
/// Implements the modern multi-algorithm <c>cksum</c> command.
/// </summary>
public static class CksumCommand {

	/// <summary>
	/// Runs the command.
	/// </summary>
	public static async Task<int> RunAsync(
		string[] args,
		CommandContext context,
		Action<TextWriter> printUsage,
		string versionText
	) {
		ArgumentNullException.ThrowIfNull(
			args
		);
		ArgumentNullException.ThrowIfNull(
			context
		);
		try {
			var options = new CksumOptions();
			var operands = new List<string>();
			var parseResult = await ParseAsync(
				args,
				context,
				options,
				operands,
				printUsage,
				versionText
			).ConfigureAwait( false );
			if ( parseResult.HasValue ) {
				return parseResult.Value;
			}

			if (
				options.Raw
				&& options.Base64
			) {
				await context.StandardError.WriteLineAsync(
					"cksum: --raw and --base64 are mutually exclusive"
				).ConfigureAwait( false );
				return CommandExitCodes.Failure;
			}
			if (
				options.Check
				&& (
					options.Raw
					|| options.Base64
					|| options.Zero
				)
			) {
				await context.StandardError.WriteLineAsync(
					"cksum: --check is incompatible with --raw, --base64, and --zero"
				).ConfigureAwait( false );
				return CommandExitCodes.Failure;
			}
			if (
				!options.Check
				&& (
					options.IgnoreMissing
					|| options.Quiet
					|| options.Status
					|| options.Strict
					|| options.Warn
				)
			) {
				await context.StandardError.WriteLineAsync(
					"cksum: verification options require --check"
				).ConfigureAwait( false );
				return CommandExitCodes.Failure;
			}

			var algorithm = ResolveAlgorithm(
				options.AlgorithmName,
				options.LengthBits,
				out var selectedLength
			);
			if ( options.Debug ) {
				await context.StandardError.WriteLineAsync(
					$"cksum: using managed streaming implementation for {ChecksumProcessor.GetDisplayName( algorithm )}"
				).ConfigureAwait( false );
			}

			return options.Check
				? await VerifyAsync(
					operands,
					context,
					options,
					algorithm,
					selectedLength
				).ConfigureAwait( false )
				: await ComputeAsync(
					operands,
					context,
					options,
					algorithm,
					selectedLength
				).ConfigureAwait( false )
			;
		} catch ( OperationCanceledException ) {
			return CommandExitCodes.Canceled;
		} catch ( Exception ex ) {
			await context.StandardError.WriteLineAsync(
				$"cksum: {ex.Message}"
			).ConfigureAwait( false );
			return CommandExitCodes.Failure;
		}
	}

	private static async Task<int?> ParseAsync(
		string[] args,
		CommandContext context,
		CksumOptions options,
		ICollection<string> operands,
		Action<TextWriter> printUsage,
		string versionText
	) {
		var parser = new OptionParser(
			new OptionDefinition[] {
				new OptionDefinition( "algorithm", 'a', new string[] { "algorithm" }, OptionValueArity.Required ),
				new OptionDefinition( "base64", longNames: new string[] { "base64" } ),
				new OptionDefinition( "check", 'c', new string[] { "check" } ),
				new OptionDefinition( "length", 'l', new string[] { "length" }, OptionValueArity.Required ),
				new OptionDefinition( "raw", longNames: new string[] { "raw" } ),
				new OptionDefinition( "tag", longNames: new string[] { "tag" } ),
				new OptionDefinition( "untagged", longNames: new string[] { "untagged" } ),
				new OptionDefinition( "zero", 'z', new string[] { "zero" } ),
				new OptionDefinition( "ignore-missing", longNames: new string[] { "ignore-missing" } ),
				new OptionDefinition( "quiet", longNames: new string[] { "quiet" } ),
				new OptionDefinition( "status", longNames: new string[] { "status" } ),
				new OptionDefinition( "strict", longNames: new string[] { "strict" } ),
				new OptionDefinition( "warn", 'w', new string[] { "warn" } ),
				new OptionDefinition( "debug", longNames: new string[] { "debug" } ),
				new OptionDefinition( "help", longNames: new string[] { "help" } ),
				new OptionDefinition( "version", longNames: new string[] { "version" } )
			},
			new OptionParserSettings {
				AllowLongOptionAbbreviations = true,
				Ordering = OptionOrdering.Permute
			}
		);
		var result = parser.Parse(
			args
		);
		if ( !result.IsSuccess ) {
			foreach ( var error in result.Errors ) {
				await context.StandardError.WriteLineAsync(
					OptionDiagnosticFormatter.Format(
						"cksum",
						error
					)
				).ConfigureAwait( false );
			}
			return CommandExitCodes.Failure;
		}

		foreach ( var occurrence in result.Options ) {
			switch ( occurrence.Definition.Key ) {
				case "algorithm":
					options.AlgorithmName = occurrence.Value ?? string.Empty;
					break;
				case "base64":
					options.Base64 = true;
					break;
				case "check":
					options.Check = true;
					break;
				case "length":
					if (
						!int.TryParse(
							occurrence.Value,
							NumberStyles.None,
							CultureInfo.InvariantCulture,
							out var length
						)
					) {
						await context.StandardError.WriteLineAsync(
							$"cksum: invalid length: '{occurrence.Value}'"
						).ConfigureAwait( false );
						return CommandExitCodes.Failure;
					}
					options.LengthBits = length;
					break;
				case "raw":
					options.Raw = true;
					break;
				case "tag":
					options.Tagged = true;
					break;
				case "untagged":
					options.Tagged = false;
					break;
				case "zero":
					options.Zero = true;
					break;
				case "ignore-missing":
					options.IgnoreMissing = true;
					break;
				case "quiet":
					options.Quiet = true;
					break;
				case "status":
					options.Status = true;
					break;
				case "strict":
					options.Strict = true;
					break;
				case "warn":
					options.Warn = true;
					break;
				case "debug":
					options.Debug = true;
					break;
				case "help":
					printUsage(
						context.StandardOutput
					);
					return CommandExitCodes.Success;
				case "version":
					await context.StandardOutput.WriteLineAsync(
						versionText
					).ConfigureAwait( false );
					return CommandExitCodes.Success;
			}
		}
		foreach ( var operand in result.Operands ) {
			operands.Add(
				operand
			);
		}
		return null;
	}

	private static ChecksumAlgorithmKind ResolveAlgorithm(
		string name,
		int? lengthBits,
		out int? selectedLength
	) {
		selectedLength = lengthBits;
		switch ( name.ToLowerInvariant() ) {
			case "bsd":
				RequireNoLength( name, lengthBits );
				return ChecksumAlgorithmKind.Bsd;
			case "sysv":
				RequireNoLength( name, lengthBits );
				return ChecksumAlgorithmKind.SysV;
			case "crc":
				RequireNoLength( name, lengthBits );
				return ChecksumAlgorithmKind.Crc;
			case "crc32b":
				RequireNoLength( name, lengthBits );
				return ChecksumAlgorithmKind.Crc32b;
			case "md5":
				RequireNoLength( name, lengthBits );
				return ChecksumAlgorithmKind.Md5;
			case "sha1":
				RequireNoLength( name, lengthBits );
				return ChecksumAlgorithmKind.Sha1;
			case "sha2": {
				var length = lengthBits ?? 256;
				selectedLength = length;
				return length switch {
					224 => ChecksumAlgorithmKind.Sha224,
					256 => ChecksumAlgorithmKind.Sha256,
					384 => ChecksumAlgorithmKind.Sha384,
					512 => ChecksumAlgorithmKind.Sha512,
					_ => throw new ChecksumException(
						"SHA2 length must be 224, 256, 384, or 512"
					)
				};
			}
			case "sha3": {
				var length = lengthBits ?? 256;
				selectedLength = length;
				return length switch {
					224 => ChecksumAlgorithmKind.Sha3_224,
					256 => ChecksumAlgorithmKind.Sha3_256,
					384 => ChecksumAlgorithmKind.Sha3_384,
					512 => ChecksumAlgorithmKind.Sha3_512,
					_ => throw new ChecksumException(
						"SHA3 length must be 224, 256, 384, or 512"
					)
				};
			}
			case "blake2b":
				selectedLength = lengthBits ?? 512;
				return ChecksumAlgorithmKind.Blake2b;
			case "sm3":
				RequireNoLength( name, lengthBits );
				return ChecksumAlgorithmKind.Sm3;
			default:
				throw new ChecksumException(
					$"unknown checksum algorithm: {name}"
				);
		}
	}

	private static void RequireNoLength(
		string name,
		int? lengthBits
	) {
		if ( lengthBits.HasValue ) {
			throw new ChecksumException(
				$"--length is not supported for {name}"
			);
		}
	}

	private static async Task<int> ComputeAsync(
		IReadOnlyCollection<string> operands,
		CommandContext context,
		CksumOptions options,
		ChecksumAlgorithmKind algorithm,
		int? lengthBits
	) {
		var explicitOperands = 0 < operands.Count;
		var names = PathnameExpander.Expand(
			explicitOperands
				? operands
				: new string[] { "-" }
		);
		using var output = new ByteOutputStream(
			context.StandardOutput,
			context.StandardOutputStream
		);
		var exitCode = CommandExitCodes.Success;

		foreach ( var name in names ) {
			var operand = InputOperand.Create(
				name
			);
			try {
				await using var source = InputSource.OpenBinary(
					operand,
					context
				);
				var result = await ChecksumProcessor.ComputeAsync(
					source.BinaryStream!,
					algorithm,
					lengthBits,
					context.CancellationToken
				).ConfigureAwait( false );

				if ( options.Raw ) {
					if ( null != result.Digest ) {
						await output.WriteAsync(
							result.Digest,
							context.CancellationToken
						).ConfigureAwait( false );
					} else {
						var numericBytes = BitConverter.GetBytes(
							checked(
								(uint)result.NumericValue!.Value
							)
						);
						if ( BitConverter.IsLittleEndian ) {
							Array.Reverse(
								numericBytes
							);
						}
						await output.WriteAsync(
							numericBytes,
							context.CancellationToken
						).ConfigureAwait( false );
					}
					continue;
				}

				var line = FormatResult(
					result,
					name,
					explicitOperands,
					options
				);
				await WriteRecordAsync(
					output,
					line,
					options.Zero,
					context.CancellationToken
				).ConfigureAwait( false );
			} catch ( Exception ex ) when (
				ex is not OperationCanceledException
			) {
				await context.StandardError.WriteLineAsync(
					$"cksum: {operand.DisplayName}: {ex.Message}"
				).ConfigureAwait( false );
				exitCode = CommandExitCodes.Failure;
			}
		}

		await output.CompleteAsync(
			context.CancellationToken
		).ConfigureAwait( false );
		return exitCode;
	}

	private static string FormatResult(
		ChecksumComputation result,
		string name,
		bool explicitOperands,
		CksumOptions options
	) {
		if (
			ChecksumAlgorithmKind.Crc == result.Algorithm
			&& !options.Base64
		) {
			return explicitOperands
				? $"{result.NumericValue} {result.Length} {name}"
				: $"{result.NumericValue} {result.Length}"
			;
		}
		if (
			ChecksumAlgorithmKind.Bsd == result.Algorithm
			|| ChecksumAlgorithmKind.SysV == result.Algorithm
		) {
			return explicitOperands
				? $"{result.NumericValue} {result.BlockCount} {name}"
				: $"{result.NumericValue} {result.BlockCount}"
			;
		}

		var digest = result.Digest ?? throw new InvalidOperationException(
			"The selected algorithm did not return a digest."
		);
		var value = options.Base64
			? Convert.ToBase64String(
				digest
			)
			: ChecksumText.ToHex(
				digest
			)
		;
		if ( options.Tagged ) {
			return $"{ChecksumProcessor.GetDisplayName( result.Algorithm )} ({name}) = {value}";
		}
		return $"{value}  {name}";
	}

	private static async Task<int> VerifyAsync(
		IReadOnlyCollection<string> operands,
		CommandContext context,
		CksumOptions options,
		ChecksumAlgorithmKind selectedAlgorithm,
		int? selectedLength
	) {
		var manifests = PathnameExpander.Expand(
			0 == operands.Count
				? new string[] { "-" }
				: operands
		);
		var failed = false;
		var validCount = 0;
		var verifiedCount = 0;
		var malformedCount = 0;

		foreach ( var manifestName in manifests ) {
			var manifestOperand = InputOperand.Create(
				manifestName
			);
			try {
				await using var source = InputSource.OpenBinary(
					manifestOperand,
					context
				);
				using var reader = new DelimitedByteRecordReader(
					source.BinaryStream!
				);
				var lineNumber = 0;
				while ( true ) {
					var bytes = await reader.ReadAsync(
						context.CancellationToken
					).ConfigureAwait( false );
					if ( null == bytes ) {
						break;
					}
					lineNumber++;
					var count = bytes.Length;
					if (
						0 < count
						&& (byte)'\n' == bytes[ count - 1 ]
					) {
						count--;
					}
					if (
						0 < count
						&& (byte)'\r' == bytes[ count - 1 ]
					) {
						count--;
					}
					var line = Encoding.UTF8.GetString(
						bytes,
						0,
						count
					);
					ChecksumManifestRecord? record = null;
					NumericChecksumManifestRecord? numericRecord = null;
					if (
						!ChecksumText.TryParseTaggedRecord(
							line,
							out record
						)
						&& !ChecksumText.TryParseStandaloneRecord(
							line,
							selectedAlgorithm,
							selectedLength ?? ChecksumProcessor.GetDefaultLengthBits( selectedAlgorithm ),
							out record
						)
						&& !TryParseNumericRecord(
							line,
							selectedAlgorithm,
							out numericRecord
						)
					) {
						malformedCount++;
						if (
							options.Warn
							&& !options.Status
						) {
							await context.StandardError.WriteLineAsync(
								$"cksum: {manifestName}: {lineNumber}: improperly formatted checksum line"
							).ConfigureAwait( false );
						}
						continue;
					}
					validCount++;
					var verified = null != numericRecord
						? await VerifyNumericRecordAsync(
							numericRecord,
							context,
							options
						).ConfigureAwait( false )
						: await VerifyDigestRecordAsync(
							record!,
							context,
							options
						).ConfigureAwait( false )
					;
					if ( verified.HasValue ) {
						verifiedCount++;
						failed |= !verified.Value;
					}
				}
			} catch ( Exception ex ) when (
				ex is not OperationCanceledException
			) {
				if ( !options.Status ) {
					await context.StandardError.WriteLineAsync(
						$"cksum: {manifestOperand.DisplayName}: {ex.Message}"
					).ConfigureAwait( false );
				}
				failed = true;
			}
		}

		if ( 0 == validCount ) {
			failed = true;
			if ( !options.Status ) {
				await context.StandardError.WriteLineAsync(
					"cksum: no properly formatted checksum lines found"
				).ConfigureAwait( false );
			}
		}
		if (
			options.Strict
			&& 0 < malformedCount
		) {
			failed = true;
		}
		if (
			0 == verifiedCount
			&& options.IgnoreMissing
			&& 0 < validCount
		) {
			failed = true;
		}
		return failed
			? CommandExitCodes.Failure
			: CommandExitCodes.Success
		;
	}

	private static bool TryParseNumericRecord(
		string line,
		ChecksumAlgorithmKind algorithm,
		out NumericChecksumManifestRecord? record
	) {
		record = null;
		if (
			ChecksumAlgorithmKind.Crc != algorithm
			&& ChecksumAlgorithmKind.Bsd != algorithm
			&& ChecksumAlgorithmKind.SysV != algorithm
		) {
			return false;
		}

		var firstSeparator = line.IndexOf(
			' '
		);
		if ( firstSeparator <= 0 ) {
			return false;
		}
		var secondStart = firstSeparator;
		while (
			secondStart < line.Length
			&& ' ' == line[ secondStart ]
		) {
			secondStart++;
		}
		var secondSeparator = line.IndexOf(
			' ',
			secondStart
		);
		if (
			secondStart >= line.Length
			|| secondSeparator <= secondStart
		) {
			return false;
		}
		var fileStart = secondSeparator;
		while (
			fileStart < line.Length
			&& ' ' == line[ fileStart ]
		) {
			fileStart++;
		}
		if ( fileStart >= line.Length ) {
			return false;
		}

		if (
			!ulong.TryParse(
				line.AsSpan(
					0,
					firstSeparator
				),
				NumberStyles.None,
				CultureInfo.InvariantCulture,
				out var checksum
			)
			|| !long.TryParse(
				line.AsSpan(
					secondStart,
					secondSeparator - secondStart
				),
				NumberStyles.None,
				CultureInfo.InvariantCulture,
				out var lengthOrBlocks
			)
		) {
			return false;
		}
		record = new NumericChecksumManifestRecord(
			algorithm,
			checksum,
			lengthOrBlocks,
			line.Substring(
				fileStart
			)
		);
		return true;
	}

	private static async Task<bool?> VerifyNumericRecordAsync(
		NumericChecksumManifestRecord record,
		CommandContext context,
		CksumOptions options
	) {
		try {
			await using var source = InputSource.OpenBinary(
				InputOperand.Create(
					record.FileName
				),
				context
			);
			var result = await ChecksumProcessor.ComputeAsync(
				source.BinaryStream!,
				record.Algorithm,
				cancellationToken: context.CancellationToken
			).ConfigureAwait( false );
			var actualLengthOrBlocks = ChecksumAlgorithmKind.Crc == record.Algorithm
				? result.Length
				: result.BlockCount
			;
			var matches = result.NumericValue == record.ExpectedChecksum
				&& actualLengthOrBlocks == record.ExpectedLengthOrBlocks
			;
			if ( !options.Status ) {
				if (
					matches
					&& !options.Quiet
				) {
					await context.StandardOutput.WriteLineAsync(
						$"{record.FileName}: OK"
					).ConfigureAwait( false );
				} else if ( !matches ) {
					await context.StandardOutput.WriteLineAsync(
						$"{record.FileName}: FAILED"
					).ConfigureAwait( false );
				}
			}
			return matches;
		} catch (
			Exception ex
		) when (
			ex is FileNotFoundException
				or DirectoryNotFoundException
		) {
			if ( options.IgnoreMissing ) {
				return null;
			}
			if ( !options.Status ) {
				await context.StandardOutput.WriteLineAsync(
					$"{record.FileName}: FAILED open or read"
				).ConfigureAwait( false );
				await context.StandardError.WriteLineAsync(
					$"cksum: {record.FileName}: {ex.Message}"
				).ConfigureAwait( false );
			}
			return false;
		} catch ( Exception ex ) when (
			ex is not OperationCanceledException
		) {
			if ( !options.Status ) {
				await context.StandardOutput.WriteLineAsync(
					$"{record.FileName}: FAILED open or read"
				).ConfigureAwait( false );
				await context.StandardError.WriteLineAsync(
					$"cksum: {record.FileName}: {ex.Message}"
				).ConfigureAwait( false );
			}
			return false;
		}
	}

	private static async Task<bool?> VerifyDigestRecordAsync(
		ChecksumManifestRecord record,
		CommandContext context,
		CksumOptions options
	) {
		var algorithm = record.Algorithm ?? throw new ChecksumException(
			"checksum algorithm was not identified"
		);
		try {
			await using var source = InputSource.OpenBinary(
				InputOperand.Create(
					record.FileName
				),
				context
			);
			var result = await ChecksumProcessor.ComputeAsync(
				source.BinaryStream!,
				algorithm,
				record.LengthBits,
				context.CancellationToken
			).ConfigureAwait( false );
			var matches = CryptographicOperations.FixedTimeEquals(
				result.Digest!,
				record.ExpectedDigest
			);
			if ( !options.Status ) {
				if (
					matches
					&& !options.Quiet
				) {
					await context.StandardOutput.WriteLineAsync(
						$"{record.FileName}: OK"
					).ConfigureAwait( false );
				} else if ( !matches ) {
					await context.StandardOutput.WriteLineAsync(
						$"{record.FileName}: FAILED"
					).ConfigureAwait( false );
				}
			}
			return matches;
		} catch (
			Exception ex
		) when (
			ex is FileNotFoundException
				or DirectoryNotFoundException
		) {
			if ( options.IgnoreMissing ) {
				return null;
			}
			if ( !options.Status ) {
				await context.StandardOutput.WriteLineAsync(
					$"{record.FileName}: FAILED open or read"
				).ConfigureAwait( false );
				await context.StandardError.WriteLineAsync(
					$"cksum: {record.FileName}: {ex.Message}"
				).ConfigureAwait( false );
			}
			return false;
		}
	}

	private static async Task WriteRecordAsync(
		Stream output,
		string value,
		bool zero,
		CancellationToken cancellationToken
	) {
		await output.WriteAsync(
			Encoding.UTF8.GetBytes(
				value
			),
			cancellationToken
		).ConfigureAwait( false );
		await output.WriteAsync(
			new byte[] {
				zero
					? (byte)0
					: (byte)'\n'
			},
			cancellationToken
		).ConfigureAwait( false );
	}

}
