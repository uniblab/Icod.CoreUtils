/*
	Icod.CoreUtils.Shared
	Shared support library for the Icod.CoreUtils command suite.
	Copyright (C) 2026  Timothy J. Bruce <uniblab@hotmail.com>
*/

/*
	This program is free software: you can redistribute it and/or modify
	it under the terms of the GNU Lesser General Public License as published by
	the Free Software Foundation, either version 3 of the License, or
	(at your option) any later version.

	This program is distributed in the hope that it will be useful,
	but WITHOUT ANY WARRANTY; without even the implied warranty of
	MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
	GNU Lesser General Public License for more details.

	You should have received a copy of the GNU Lesser General Public License
	along with this program.  If not, see <https://www.gnu.org/licenses/>.
*/

namespace Icod.CoreUtils.Shared.Checksums;

using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Icod.CommandFramework.CommandLine;
using Icod.CommandFramework.Diagnostics;
using Icod.CommandFramework.IO;
using Icod.CoreUtils.Shared.FileSystem.Traversal;

/// <summary>
/// Provides the digest command options implementation.
/// </summary>
internal sealed class DigestCommandOptions {

	/// <summary>
	/// Stores the binary value.
	/// </summary>
	public bool Binary {
		get;
		set;
	}

	/// <summary>
	/// Stores the check value.
	/// </summary>
	public bool Check {
		get;
		set;
	}

	/// <summary>
	/// Stores the ignore missing value.
	/// </summary>
	public bool IgnoreMissing {
		get;
		set;
	}

	/// <summary>
	/// Stores the length bits value.
	/// </summary>
	public int? LengthBits {
		get;
		set;
	}

	/// <summary>
	/// Stores the length specified value.
	/// </summary>
	public bool LengthSpecified {
		get;
		set;
	}

	/// <summary>
	/// Stores the quiet value.
	/// </summary>
	public bool Quiet {
		get;
		set;
	}

	/// <summary>
	/// Stores the status value.
	/// </summary>
	public bool Status {
		get;
		set;
	}

	/// <summary>
	/// Stores the strict value.
	/// </summary>
	public bool Strict {
		get;
		set;
	}

	/// <summary>
	/// Stores the tag value.
	/// </summary>
	public bool Tag {
		get;
		set;
	}

	/// <summary>
	/// Stores the warn value.
	/// </summary>
	public bool Warn {
		get;
		set;
	}

	/// <summary>
	/// Stores the zero value.
	/// </summary>
	public bool Zero {
		get;
		set;
	}

}

/// <summary>
/// Implements the common standalone digest command surface.
/// </summary>
public static class DigestCommand {

	/// <summary>
	/// Runs a standalone digest command.
	/// </summary>
	public static async Task<int> RunAsync(
		string[] args,
		CommandContext context,
		DigestCommandSettings settings
	) {
		ArgumentNullException.ThrowIfNull(
			args
		);
		ArgumentNullException.ThrowIfNull(
			context
		);
		ArgumentNullException.ThrowIfNull(
			settings
		);

		try {
			var options = new DigestCommandOptions {
				LengthBits = settings.DefaultLengthBits
			};
			var operands = new List<string>();
			var parseExitCode = await ParseArgumentsAsync(
				args,
				context,
				settings,
				options,
				operands
			).ConfigureAwait( false );
			if ( parseExitCode.HasValue ) {
				return parseExitCode.Value;
			}

			if (
				options.Check
				&& options.Zero
			) {
				await context.StandardError.WriteLineAsync(
					$"{settings.ProgramName}: --zero is not supported with --check"
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
					$"{settings.ProgramName}: verification options require --check"
				).ConfigureAwait( false );
				return CommandExitCodes.Failure;
			}

			return options.Check
				? await VerifyAsync(
					operands,
					context,
					settings,
					options
				).ConfigureAwait( false )
				: await ComputeAsync(
					operands,
					context,
					settings,
					options
				).ConfigureAwait( false )
			;
		} catch ( OperationCanceledException ) {
			return CommandExitCodes.Canceled;
		} catch ( Exception ex ) {
			await context.StandardError.WriteLineAsync(
				$"{settings.ProgramName}: {ex.Message}"
			).ConfigureAwait( false );
			return CommandExitCodes.Failure;
		}
	}

	private static async Task<int?> ParseArgumentsAsync(
		string[] args,
		CommandContext context,
		DigestCommandSettings settings,
		DigestCommandOptions options,
		ICollection<string> operands
	) {
		var definitions = new List<OptionDefinition> {
			new OptionDefinition( "binary", 'b', new string[] { "binary" } ),
			new OptionDefinition( "check", 'c', new string[] { "check" } ),
			new OptionDefinition( "tag", longNames: new string[] { "tag" } ),
			new OptionDefinition( "text", 't', new string[] { "text" } ),
			new OptionDefinition( "zero", 'z', new string[] { "zero" } ),
			new OptionDefinition( "ignore-missing", longNames: new string[] { "ignore-missing" } ),
			new OptionDefinition( "quiet", longNames: new string[] { "quiet" } ),
			new OptionDefinition( "status", longNames: new string[] { "status" } ),
			new OptionDefinition( "strict", longNames: new string[] { "strict" } ),
			new OptionDefinition( "warn", 'w', new string[] { "warn" } ),
			new OptionDefinition( "help", longNames: new string[] { "help" } ),
			new OptionDefinition( "version", longNames: new string[] { "version" } )
		};
		if ( settings.SupportsLength ) {
			definitions.Add(
				new OptionDefinition(
					"length",
					'l',
					new string[] { "length" },
					OptionValueArity.Required
				)
			);
		}

		var parser = new OptionParser(
			definitions,
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
						settings.ProgramName,
						error
					)
				).ConfigureAwait( false );
			}
			return CommandExitCodes.Failure;
		}

		foreach ( var occurrence in result.Options ) {
			switch ( occurrence.Definition.Key ) {
				case "binary":
					options.Binary = true;
					break;
				case "check":
					options.Check = true;
					break;
				case "tag":
					options.Tag = true;
					break;
				case "text":
					options.Binary = false;
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
				case "length":
					if (
						!int.TryParse(
							occurrence.Value,
							NumberStyles.None,
							CultureInfo.InvariantCulture,
							out var lengthBits
						)
					) {
						await context.StandardError.WriteLineAsync(
							$"{settings.ProgramName}: invalid length: '{occurrence.Value}'"
						).ConfigureAwait( false );
						return CommandExitCodes.Failure;
					}
					options.LengthBits = lengthBits;
					options.LengthSpecified = true;
					break;
				case "help":
					settings.PrintUsage(
						context.StandardOutput
					);
					return CommandExitCodes.Success;
				case "version":
					await context.StandardOutput.WriteLineAsync(
						settings.VersionText
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

	private static async Task<int> ComputeAsync(
		IReadOnlyCollection<string> operands,
		CommandContext context,
		DigestCommandSettings settings,
		DigestCommandOptions options
	) {
		var expansion = await PathnameOperandExpander.ExpandAsync(
			0 == operands.Count
				? new string[] { "-" }
				: operands,
			cancellationToken: context.CancellationToken
		).ConfigureAwait( false );
		var expanded = expansion.Operands;
		var exitCode = CommandExitCodes.Success;
		using var output = new ByteOutputStream(
			context.StandardOutput,
			context.StandardOutputStream
		);
		foreach ( var name in expanded ) {
			context.CancellationToken.ThrowIfCancellationRequested();
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
					settings.Algorithm,
					options.LengthBits,
					context.CancellationToken
				).ConfigureAwait( false );
				var line = FormatStandaloneOutput(
					result.Digest!,
					name,
					settings.DisplayName,
					options
				);
				await WriteOutputRecordAsync(
					output,
					line,
					options.Zero,
					context.CancellationToken
				).ConfigureAwait( false );
			} catch ( Exception ex ) when (
				ex is not OperationCanceledException
			) {
				await context.StandardError.WriteLineAsync(
					$"{settings.ProgramName}: {operand.DisplayName}: {ex.Message}"
				).ConfigureAwait( false );
				exitCode = CommandExitCodes.Failure;
			}
		}
		await output.CompleteAsync(
			context.CancellationToken
		).ConfigureAwait( false );
		return exitCode;
	}

	private static string FormatStandaloneOutput(
		byte[] digest,
		string fileName,
		string displayName,
		DigestCommandOptions options
	) {
		var digestText = ChecksumText.ToHex(
			digest
		);
		if ( options.Zero ) {
			return options.Tag
				? $"{displayName} ({fileName}) = {digestText}"
				: $"{digestText} {( options.Binary ? '*' : ' ' )}{fileName}"
			;
		}

		var escaped = ChecksumText.NeedsEscaping(
			fileName
		);
		var name = escaped
			? ChecksumText.EscapeFileName(
				fileName
			)
			: fileName
		;
		var line = options.Tag
			? $"{displayName} ({name}) = {digestText}"
			: $"{digestText} {( options.Binary ? '*' : ' ' )}{name}"
		;
		return escaped
			? string.Concat(
				"\\",
				line
			)
			: line
		;
	}

	private static async Task<int> VerifyAsync(
		IReadOnlyCollection<string> operands,
		CommandContext context,
		DigestCommandSettings settings,
		DigestCommandOptions options
	) {
		var expansion = await PathnameOperandExpander.ExpandAsync(
			0 == operands.Count
				? new string[] { "-" }
				: operands,
			cancellationToken: context.CancellationToken
		).ConfigureAwait( false );
		var manifests = expansion.Operands;
		var failed = false;
		var verifiedCount = 0;
		var formattedCount = 0;
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
					var data = await reader.ReadAsync(
						context.CancellationToken
					).ConfigureAwait( false );
					if ( null == data ) {
						break;
					}
					lineNumber++;
					var length = (
						0 < data.Length
						&& (byte)'\n' == data[ ^1 ]
					)
						? data.Length - 1
						: data.Length
					;
					if (
						0 < length
						&& (byte)'\r' == data[ length - 1 ]
					) {
						length--;
					}
					var line = Encoding.UTF8.GetString(
						data,
						0,
						length
					);
					var requiredLength = settings.SupportsLength
						&& !options.LengthSpecified
							? null
							: options.LengthBits
					;
					if (
						!ChecksumText.TryParseStandaloneRecord(
							line,
							settings.Algorithm,
							requiredLength,
							out var record
						)
						|| null == record
					) {
						malformedCount++;
						if (
							options.Warn
							&& !options.Status
						) {
							await context.StandardError.WriteLineAsync(
								$"{settings.ProgramName}: {manifestName}: {lineNumber}: improperly formatted checksum line"
							).ConfigureAwait( false );
						}
						continue;
					}
					formattedCount++;
					var verificationResult = await VerifyRecordAsync(
						record,
						context,
						settings,
						options
					).ConfigureAwait( false );
					if ( verificationResult.HasValue ) {
						verifiedCount++;
						failed |= !verificationResult.Value;
					}
				}
			} catch ( Exception ex ) when (
				ex is not OperationCanceledException
			) {
				if ( !options.Status ) {
					await context.StandardError.WriteLineAsync(
						$"{settings.ProgramName}: {manifestOperand.DisplayName}: {ex.Message}"
					).ConfigureAwait( false );
				}
				failed = true;
			}
		}

		if (
			0 == formattedCount
			&& !options.Status
		) {
			await context.StandardError.WriteLineAsync(
				$"{settings.ProgramName}: no properly formatted checksum lines found"
			).ConfigureAwait( false );
		}
		if (
			0 < malformedCount
			&& !options.Status
		) {
			await context.StandardError.WriteLineAsync(
				$"{settings.ProgramName}: WARNING: {malformedCount} line(s) are improperly formatted"
			).ConfigureAwait( false );
		}
		if (
			0 == verifiedCount
			&& 0 < formattedCount
			&& options.IgnoreMissing
		) {
			failed = true;
		}
		if (
			0 == formattedCount
			|| (
				options.Strict
				&& 0 < malformedCount
			)
		) {
			failed = true;
		}
		return failed
			? CommandExitCodes.Failure
			: CommandExitCodes.Success
		;
	}

	private static async Task<bool?> VerifyRecordAsync(
		ChecksumManifestRecord record,
		CommandContext context,
		DigestCommandSettings settings,
		DigestCommandOptions options
	) {
		var operand = InputOperand.Create(
			record.FileName
		);
		try {
			await using var source = InputSource.OpenBinary(
				operand,
				context
			);
			var result = await ChecksumProcessor.ComputeAsync(
				source.BinaryStream!,
				settings.Algorithm,
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
					$"{settings.ProgramName}: {record.FileName}: {ex.Message}"
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
					$"{settings.ProgramName}: {record.FileName}: {ex.Message}"
				).ConfigureAwait( false );
			}
			return false;
		}
	}

	private static async Task WriteOutputRecordAsync(
		Stream output,
		string value,
		bool zero,
		CancellationToken cancellationToken
	) {
		var bytes = Encoding.UTF8.GetBytes(
			value
		);
		await output.WriteAsync(
			bytes,
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
