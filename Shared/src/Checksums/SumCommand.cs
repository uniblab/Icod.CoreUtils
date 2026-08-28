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
using Icod.CommandFramework.CommandLine;
using Icod.CommandFramework.Diagnostics;
using Icod.CommandFramework.IO;

/// <summary>
/// Implements BSD and System V checksum output for the <c>sum</c> command.
/// </summary>
public static class SumCommand {

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
		ArgumentNullException.ThrowIfNull(
			printUsage
		);

		try {
			var algorithm = ChecksumAlgorithmKind.Bsd;
			var operands = new List<string>();
			var parser = new OptionParser(
				new OptionDefinition[] {
					new OptionDefinition( "bsd", 'r' ),
					new OptionDefinition( "sysv", 's', new string[] { "sysv" } ),
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
							"sum",
							error
						)
					).ConfigureAwait( false );
				}
				return CommandExitCodes.Failure;
			}

			foreach ( var occurrence in result.Options ) {
				switch ( occurrence.Definition.Key ) {
					case "bsd":
						algorithm = ChecksumAlgorithmKind.Bsd;
						break;
					case "sysv":
						algorithm = ChecksumAlgorithmKind.SysV;
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

			var explicitOperands = 0 < operands.Count;
			var names = PathnameExpander.Expand(
				explicitOperands
					? operands
					: new string[] { "-" }
			);
			var exitCode = CommandExitCodes.Success;
			foreach ( var name in names ) {
				context.CancellationToken.ThrowIfCancellationRequested();
				var operand = InputOperand.Create(
					name
				);
				try {
					await using var source = InputSource.OpenBinary(
						operand,
						context
					);
					var computation = await ChecksumProcessor.ComputeAsync(
						source.BinaryStream!,
						algorithm,
						cancellationToken: context.CancellationToken
					).ConfigureAwait( false );
					if ( ChecksumAlgorithmKind.Bsd == algorithm ) {
						await context.StandardOutput.WriteAsync(
							computation.NumericValue!.Value.ToString(
								"00000",
								CultureInfo.InvariantCulture
							)
						).ConfigureAwait( false );
						await context.StandardOutput.WriteAsync(
							computation.BlockCount.ToString(
								"     0",
								CultureInfo.InvariantCulture
							)
						).ConfigureAwait( false );
					} else {
						await context.StandardOutput.WriteAsync(
							computation.NumericValue!.Value.ToString(
								CultureInfo.InvariantCulture
							)
						).ConfigureAwait( false );
						await context.StandardOutput.WriteAsync(
							" "
						).ConfigureAwait( false );
						await context.StandardOutput.WriteAsync(
							computation.BlockCount.ToString(
								CultureInfo.InvariantCulture
							)
						).ConfigureAwait( false );
					}
					if ( explicitOperands ) {
						await context.StandardOutput.WriteAsync(
							" "
						).ConfigureAwait( false );
						await context.StandardOutput.WriteAsync(
							name
						).ConfigureAwait( false );
					}
					await context.StandardOutput.WriteLineAsync().ConfigureAwait( false );
				} catch ( Exception ex ) when (
					ex is not OperationCanceledException
				) {
					await context.StandardError.WriteLineAsync(
						$"sum: {operand.DisplayName}: {ex.Message}"
					).ConfigureAwait( false );
					exitCode = CommandExitCodes.Failure;
				}
			}
			await context.StandardOutput.FlushAsync(
				context.CancellationToken
			).ConfigureAwait( false );
			return exitCode;
		} catch ( OperationCanceledException ) {
			return CommandExitCodes.Canceled;
		} catch ( Exception ex ) {
			await context.StandardError.WriteLineAsync(
				$"sum: {ex.Message}"
			).ConfigureAwait( false );
			return CommandExitCodes.Failure;
		}
	}

}
