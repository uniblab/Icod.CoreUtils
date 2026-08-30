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

namespace Icod.CoreUtils.Shared.Codecs;

using System.Globalization;
using Icod.CommandFramework.CommandLine;
using Icod.CommandFramework.Diagnostics;
using Icod.CommandFramework.IO;

/// <summary>
/// Implements the common command-line and stream behavior used by the
/// base-encoding command family.
/// </summary>
public static class BaseEncodingCommand {

	/// <summary>
	/// Runs a configured base-encoding command.
	/// </summary>
	public static async Task<int> RunAsync(
		string[] args,
		CommandContext context,
		BaseEncodingCommandSettings settings
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
			var definitions = new List<OptionDefinition> {
				new OptionDefinition(
					"decode",
					'd',
					new string[] {
						"decode"
					}
				),
				new OptionDefinition(
					"ignore-garbage",
					'i',
					new string[] {
						"ignore-garbage"
					}
				),
				new OptionDefinition(
					"wrap",
					'w',
					new string[] {
						"wrap"
					},
					OptionValueArity.Required
				),
				new OptionDefinition(
					"help",
					longNames: new string[] {
						"help"
					}
				),
				new OptionDefinition(
					"version",
					longNames: new string[] {
						"version"
					}
				)
			};
			foreach ( var selection in settings.EncodingSelections ) {
				definitions.Add(
					new OptionDefinition(
						selection.Key,
						longNames: new string[] {
							selection.LongName
						}
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

			var decode = false;
			var ignoreGarbage = false;
			long wrapColumns = 76;
			var selectedEncoding = settings.FixedEncoding;

			foreach ( var occurrence in result.Options ) {
				switch ( occurrence.Definition.Key ) {
					case "decode":
						decode = true;
						break;
					case "ignore-garbage":
						ignoreGarbage = true;
						break;
					case "wrap":
						if (
							!long.TryParse(
								occurrence.Value,
								NumberStyles.AllowLeadingSign,
								CultureInfo.InvariantCulture,
								out wrapColumns
							)
							|| wrapColumns < 0
						) {
							await context.StandardError.WriteLineAsync(
								$"{settings.ProgramName}: invalid wrap size: '{occurrence.Value}'"
							).ConfigureAwait( false );
							return CommandExitCodes.Failure;
						}
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
					default:
						var selection = settings.EncodingSelections.FirstOrDefault(
							item => string.Equals(
								item.Key,
								occurrence.Definition.Key,
								StringComparison.Ordinal
							)
						);
						if ( null != selection ) {
							selectedEncoding = selection.Encoding;
						}
						break;
				}
			}

			if ( !selectedEncoding.HasValue ) {
				await context.StandardError.WriteLineAsync(
					$"{settings.ProgramName}: missing encoding type"
				).ConfigureAwait( false );
				await WriteHelpHintAsync(
					settings.ProgramName,
					context.StandardError
				).ConfigureAwait( false );
				return CommandExitCodes.Failure;
			}
			if ( 1 < result.Operands.Count ) {
				await context.StandardError.WriteLineAsync(
					$"{settings.ProgramName}: extra operand '{result.Operands[ 1 ]}'"
				).ConfigureAwait( false );
				await WriteHelpHintAsync(
					settings.ProgramName,
					context.StandardError
				).ConfigureAwait( false );
				return CommandExitCodes.Failure;
			}

			var inputPath = ( 0 == result.Operands.Count )
				? "-"
				: await Icod.CoreUtils.Shared.FileSystem.Traversal.PathnameOperandExpander.ExpandSingularAsync(
					result.Operands[ 0 ],
					cancellationToken: context.CancellationToken
				).ConfigureAwait( false )
			;
			var operand = InputOperand.Create(
				inputPath
			);

			try {
				await using var source = InputSource.OpenBinary(
					operand,
					context
				);
				using var output = new ByteOutputStream(
					context.StandardOutput,
					context.StandardOutputStream
				);

				if ( decode ) {
					await BaseEncodingProcessor.DecodeAsync(
						source.BinaryStream!,
						output,
						selectedEncoding.Value,
						ignoreGarbage,
						context.CancellationToken
					).ConfigureAwait( false );
				} else {
					await BaseEncodingProcessor.EncodeAsync(
						source.BinaryStream!,
						output,
						selectedEncoding.Value,
						wrapColumns,
						context.CancellationToken
					).ConfigureAwait( false );
				}
				await output.CompleteAsync(
					context.CancellationToken
				).ConfigureAwait( false );
				return CommandExitCodes.Success;
			} catch ( BaseEncodingException ex ) {
				await context.StandardError.WriteLineAsync(
					$"{settings.ProgramName}: {ex.Message}"
				).ConfigureAwait( false );
				return CommandExitCodes.Failure;
			} catch ( Exception ex ) when (
				ex is not OperationCanceledException
			) {
				await context.StandardError.WriteLineAsync(
					$"{settings.ProgramName}: {operand.DisplayName}: {ex.Message}"
				).ConfigureAwait( false );
				return CommandExitCodes.Failure;
			}
		} catch ( OperationCanceledException ) {
			return CommandExitCodes.Canceled;
		} catch ( Exception ex ) {
			await context.StandardError.WriteLineAsync(
				$"{settings.ProgramName}: {ex.Message}"
			).ConfigureAwait( false );
			return CommandExitCodes.Failure;
		}
	}

	private static async Task WriteHelpHintAsync(
		string programName,
		TextWriter error
	) {
		await error.WriteLineAsync(
			$"Try '{programName} --help' for more information."
		).ConfigureAwait( false );
	}

}
