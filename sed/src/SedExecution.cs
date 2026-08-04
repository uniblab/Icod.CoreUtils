namespace Icod.LineEditor.Sed;

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Icod.CoreUtils.Shared.CommandLine;
using Icod.CoreUtils.Shared.Diagnostics;
using Icod.CoreUtils.Shared.IO;
using Icod.CoreUtils.Shared.Processes;

// Responsibility: command-cycle execution and deferred output.
public static partial class Command {

	private enum DeferredOutputKind {
		Text,
		File
	}

	private sealed class DeferredOutputItem {

		public string Value {
			get;
		}

		public DeferredOutputKind Kind {
			get;
		}

		public DeferredOutputItem(
			DeferredOutputKind kind,
			string value
		) {
			this.Kind = kind;
			this.Value = value;
		}

	}

	private sealed class ExecutionEnvironment : IDisposable {

		private readonly List<DeferredOutputItem> myDeferredOutput;
		private readonly Dictionary<string, AsyncRecordReader> myReadLineFiles;
		private readonly Dictionary<string, StreamWriter> myWriteFiles;

		public bool Debug {
			get;
		}

		public string HoldSpace {
			get;
			set;
		} = string.Empty;

		public int ListWidth {
			get;
		}

		public bool NullData {
			get;
		}

		public TextWriter Output {
			get;
		}

		public bool SuppressAutomaticPrint {
			get;
		}

		public TextWriter Error {
			get;
		}

		public ExecutionEnvironment(
			TextWriter output,
			TextWriter error,
			bool suppressAutomaticPrint,
			bool nullData,
			int listWidth,
			bool debug
		) {
			this.Output = output;
			this.Error = error;
			this.SuppressAutomaticPrint = suppressAutomaticPrint;
			this.Debug = debug;
			this.NullData = nullData;
			this.ListWidth = listWidth;
			this.myDeferredOutput = new List<DeferredOutputItem>();
			this.myReadLineFiles = new Dictionary<string, AsyncRecordReader>(
				StringComparer.Ordinal
			);
			this.myWriteFiles = new Dictionary<string, StreamWriter>(
				StringComparer.Ordinal
			);
		}

		public void ClearDeferredOutput() {
			this.myDeferredOutput.Clear();
		}

		public void Defer(
			string value
		) {
			this.myDeferredOutput.Add(
				new DeferredOutputItem(
					DeferredOutputKind.Text,
					value
				)
			);
		}

		public void DeferFile(
			string fileName
		) {
			this.myDeferredOutput.Add(
				new DeferredOutputItem(
					DeferredOutputKind.File,
					fileName
				)
			);
		}

		public async Task DeferFileLineAsync(
			string fileName,
			CancellationToken cancellationToken
		) {
			try {
				if (
					!this.myReadLineFiles.TryGetValue(
						fileName,
						out var reader
					)
				) {
					reader = new AsyncRecordReader(
						new StreamReader(
							new FileStream(
								fileName,
								FileMode.Open,
								FileAccess.Read,
								FileShare.Read,
								8192,
								useAsync: true
							),
							Encoding.UTF8,
							detectEncodingFromByteOrderMarks: true,
							bufferSize: 8192,
							leaveOpen: false
						),
						this.NullData,
						ownsReader: true
					);
					this.myReadLineFiles.Add(
						fileName,
						reader
					);
				}

				var line = await reader.ReadAsync(
					cancellationToken
				).ConfigureAwait( false );
				if ( null != line ) {
					this.Defer(
						line
					);
				}
			} catch ( Exception ex ) {
				await this.Error.WriteLineAsync(
					$"sed: {fileName}: {ex.Message}"
				).ConfigureAwait( false );
			}
		}

		public async Task FlushDeferredOutputAsync(
			CancellationToken cancellationToken
		) {
			foreach ( var item in this.myDeferredOutput ) {
				cancellationToken.ThrowIfCancellationRequested();

				if ( DeferredOutputKind.Text == item.Kind ) {
					await WriteRecordAsync(
						this.Output,
						item.Value,
						this.NullData
					).ConfigureAwait( false );
					continue;
				}

				try {
					using ( var reader = new AsyncRecordReader(
						new StreamReader(
							new FileStream(
								item.Value,
								FileMode.Open,
								FileAccess.Read,
								FileShare.Read,
								8192,
								useAsync: true
							),
							Encoding.UTF8,
							detectEncodingFromByteOrderMarks: true,
							bufferSize: 8192,
							leaveOpen: false
						),
						this.NullData,
						ownsReader: true
					) ) {
						string? line;
						while (
							null != (
								line = await reader.ReadAsync(
									cancellationToken
								).ConfigureAwait( false )
							)
						) {
							await WriteRecordAsync(
								this.Output,
								line,
								this.NullData
							).ConfigureAwait( false );
						}
					}
				} catch ( Exception ex ) {
					await this.Error.WriteLineAsync(
						$"sed: {item.Value}: {ex.Message}"
					).ConfigureAwait( false );
				}
			}

			this.myDeferredOutput.Clear();
		}

		public async Task WriteFileAsync(
			string fileName,
			string value,
			CancellationToken cancellationToken
		) {
			if (
				!this.myWriteFiles.TryGetValue(
					fileName,
					out var writer
				)
			) {
				writer = new StreamWriter(
					new FileStream(
						fileName,
						FileMode.Create,
						FileAccess.Write,
						FileShare.Read,
						8192,
						useAsync: true
					),
					new UTF8Encoding(
						encoderShouldEmitUTF8Identifier: false
					),
					8192,
					leaveOpen: false
				);
				this.myWriteFiles.Add(
					fileName,
					writer
				);
			}

			cancellationToken.ThrowIfCancellationRequested();
			await WriteRecordAsync(
				writer,
				value,
				this.NullData
			).ConfigureAwait( false );
			await writer.FlushAsync().ConfigureAwait( false );
		}

		public async Task DisposeAsync() {
			foreach ( var writer in this.myWriteFiles.Values ) {
				await writer.FlushAsync().ConfigureAwait( false );
			}
			await this.Output.FlushAsync().ConfigureAwait( false );
			this.Dispose();
		}

		public void Dispose() {
			foreach ( var reader in this.myReadLineFiles.Values ) {
				reader.Dispose();
			}
			this.myReadLineFiles.Clear();

			foreach ( var writer in this.myWriteFiles.Values ) {
				writer.Dispose();
			}
			this.myWriteFiles.Clear();
		}

	}

	private sealed class ExecutionResult {

		public int ExitCode {
			get;
		}

		public bool Quit {
			get;
		}

		public ExecutionResult(
			bool quit,
			int exitCode
		) {
			this.Quit = quit;
			this.ExitCode = exitCode;
		}

	}

	private static async Task<ExecutionResult> ExecuteAsync(
		SedProgram program,
		InputSequence input,
		ExecutionEnvironment environment,
		CancellationToken cancellationToken
	) {
		program.ResetAddresses();

		while (
			await input.MoveNextAsync(
				cancellationToken
			).ConfigureAwait( false )
		) {
			var patternSpace = input.Current;
			if ( environment.Debug ) {
				await environment.Error.WriteLineAsync(
					$"INPUT:   {input.LineNumber}"
				).ConfigureAwait( false );
				await environment.Error.WriteLineAsync(
					$"PATTERN: {EscapeDebugText( patternSpace )}"
				).ConfigureAwait( false );
			}
			var substitutionSucceeded = false;
			var automaticPrint = true;
			var programCounter = 0;
			environment.ClearDeferredOutput();

			while ( programCounter < program.Instructions.Count ) {
				cancellationToken.ThrowIfCancellationRequested();

				var instruction = program.Instructions[ programCounter ];
				if ( InstructionKind.Label == instruction.Kind ) {
					programCounter++;
					continue;
				} else if ( InstructionKind.EndGroup == instruction.Kind ) {
					programCounter++;
					continue;
				}

				var context = new AddressContext(
					input.LineNumber,
					input.IsLast,
					patternSpace,
					cancellationToken
				);
				var selection = instruction.Address?.Evaluate(
					context
				) ?? new Selection(
					isSelected: true,
					rangeStarted: false,
					rangeEnded: false
				);

				if ( InstructionKind.BeginGroup == instruction.Kind ) {
					programCounter = selection.IsSelected
						? programCounter + 1
						: instruction.JumpIndex
					;
					continue;
				}

				if ( !selection.IsSelected ) {
					programCounter++;
					continue;
				}

				switch ( instruction.Kind ) {
					case InstructionKind.AppendText: {
							environment.Defer(
								instruction.Argument as string
									?? string.Empty
							);
							programCounter++;
							break;
						}

					case InstructionKind.AppendHold: {
							if ( instruction.Argument is bool ) {
								environment.HoldSpace = string.Concat(
									environment.HoldSpace,
									"\n",
									patternSpace
								);
							} else {
								patternSpace = string.Concat(
									patternSpace,
									"\n",
									environment.HoldSpace
								);
							}
							programCounter++;
							break;
						}

					case InstructionKind.AppendNext: {
							if (
								!await input.MoveNextAsync(
									cancellationToken
								).ConfigureAwait( false )
							) {
								return new ExecutionResult(
									quit: true,
									exitCode: 0
								);
							}
							patternSpace = string.Concat(
								patternSpace,
								"\n",
								input.Current
							);
							programCounter++;
							break;
						}

					case InstructionKind.Branch: {
							programCounter = program.ResolveLabel(
								instruction.Argument as string
							);
							break;
						}

					case InstructionKind.ChangeText: {
							if (
								null == instruction.Address
								|| !instruction.Address.HasRange
								|| instruction.Address.Negated
								|| selection.RangeStarted
							) {
								await WriteRecordAsync(
									environment.Output,
									instruction.Argument as string
										?? string.Empty,
									environment.NullData
								).ConfigureAwait( false );
							}
							automaticPrint = false;
							programCounter = program.Instructions.Count;
							break;
						}

					case InstructionKind.Delete: {
							automaticPrint = false;
							programCounter = program.Instructions.Count;
							break;
						}

					case InstructionKind.DeleteFirst: {
							var newline = patternSpace.IndexOf(
								'\n'
							);
							if ( newline < 0 ) {
								automaticPrint = false;
								programCounter = program.Instructions.Count;
							} else {
								patternSpace = patternSpace.Substring(
									newline + 1
								);
								substitutionSucceeded = false;
								programCounter = 0;
							}
							break;
						}

					case InstructionKind.Execute: {
							var commandText = instruction.Argument as string;
							if ( string.IsNullOrWhiteSpace( commandText ) ) {
								commandText = patternSpace;
							}
							var shellResult = await ExecuteShellAsync(
								commandText,
								environment,
								captureStandardOutput: false,
								cancellationToken
							).ConfigureAwait( false );
							if ( shellResult.ExitCode != 0 ) {
								await environment.Error.WriteLineAsync(
									$"sed: command exited with status {shellResult.ExitCode}"
								).ConfigureAwait( false );
							}
							if ( 0 < shellResult.StandardOutput.Length ) {
								await environment.Output.WriteAsync(
									shellResult.StandardOutput
								).ConfigureAwait( false );
							}
							programCounter++;
							break;
						}

					case InstructionKind.Exchange: {
							var value = patternSpace;
							patternSpace = environment.HoldSpace;
							environment.HoldSpace = value;
							programCounter++;
							break;
						}

					case InstructionKind.GetHold: {
							patternSpace = environment.HoldSpace;
							programCounter++;
							break;
						}

					case InstructionKind.LineNumber: {
							await WriteRecordAsync(
								environment.Output,
								input.LineNumber.ToString(
									CultureInfo.InvariantCulture
								),
								environment.NullData
							).ConfigureAwait( false );
							programCounter++;
							break;
						}

					case InstructionKind.List: {
							var width = instruction.Argument is int configuredWidth
								? configuredWidth
								: environment.ListWidth
							;
							await WriteRecordAsync(
								environment.Output,
								FormatList(
									patternSpace,
									width
								),
								environment.NullData
							).ConfigureAwait( false );
							programCounter++;
							break;
						}

					case InstructionKind.Next: {
							if ( !environment.SuppressAutomaticPrint ) {
								await WriteRecordAsync(
									environment.Output,
									patternSpace,
									environment.NullData
								).ConfigureAwait( false );
							}
							await environment.FlushDeferredOutputAsync(
								cancellationToken
							).ConfigureAwait( false );
							if (
								!await input.MoveNextAsync(
									cancellationToken
								).ConfigureAwait( false )
							) {
								return new ExecutionResult(
									quit: true,
									exitCode: 0
								);
							}
							patternSpace = input.Current;
							substitutionSucceeded = false;
							programCounter++;
							break;
						}

					case InstructionKind.Print: {
							if ( instruction.Argument is InsertArgument insert ) {
								await WriteRecordAsync(
									environment.Output,
									insert.Text,
									environment.NullData
								).ConfigureAwait( false );
							} else {
								await WriteRecordAsync(
									environment.Output,
									patternSpace,
									environment.NullData
								).ConfigureAwait( false );
							}
							programCounter++;
							break;
						}

					case InstructionKind.PrintFirst: {
							await WriteRecordAsync(
								environment.Output,
								FirstPatternLine(
									patternSpace
								),
								environment.NullData
							).ConfigureAwait( false );
							programCounter++;
							break;
						}

					case InstructionKind.Quit: {
							if ( !environment.SuppressAutomaticPrint ) {
								await WriteRecordAsync(
									environment.Output,
									patternSpace,
									environment.NullData
								).ConfigureAwait( false );
							}
							await environment.FlushDeferredOutputAsync(
								cancellationToken
							).ConfigureAwait( false );
							return new ExecutionResult(
								quit: true,
								exitCode: instruction.Argument is int configuredExitCode
									? configuredExitCode
									: 0
							);
						}

					case InstructionKind.QuitSilent: {
							return new ExecutionResult(
								quit: true,
								exitCode: instruction.Argument is int configuredExitCode
									? configuredExitCode
									: 0
							);
						}

					case InstructionKind.ReadFile: {
							environment.DeferFile(
								instruction.Argument as string
									?? string.Empty
							);
							programCounter++;
							break;
						}

					case InstructionKind.ReadFileLine: {
							await environment.DeferFileLineAsync(
								instruction.Argument as string
									?? string.Empty,
								cancellationToken
							).ConfigureAwait( false );
							programCounter++;
							break;
						}

					case InstructionKind.SetHold: {
							environment.HoldSpace = patternSpace;
							programCounter++;
							break;
						}

					case InstructionKind.Substitute: {
							var substitution = instruction.Argument as Substitution
								?? throw new InvalidOperationException()
							;
							var result = ApplySubstitution(
								patternSpace,
								substitution,
								out var replaced,
								cancellationToken
							);
							if ( replaced ) {
								patternSpace = result;
								substitutionSucceeded = true;
								var flags = ParseSubstitutionFlags(
									substitution.Flags
								);
								if ( flags.Execute ) {
									var shellResult = await ExecuteShellAsync(
										patternSpace,
										environment,
										captureStandardOutput: true,
										cancellationToken
									).ConfigureAwait( false );
									patternSpace = shellResult.StandardOutput.TrimEnd(
										'\r',
										'\n'
									);
									if ( shellResult.ExitCode != 0 ) {
										await environment.Error.WriteLineAsync(
											$"sed: command exited with status {shellResult.ExitCode}"
										).ConfigureAwait( false );
									}
								}
								if ( flags.Print ) {
									await WriteRecordAsync(
										environment.Output,
										patternSpace,
										environment.NullData
									).ConfigureAwait( false );
								}
								if ( !string.IsNullOrEmpty( flags.WriteFile ) ) {
									await environment.WriteFileAsync(
										flags.WriteFile,
										patternSpace,
										cancellationToken
									).ConfigureAwait( false );
								}
							}
							programCounter++;
							break;
						}

					case InstructionKind.TestBranch: {
							var branch = substitutionSucceeded;
							substitutionSucceeded = false;
							programCounter = branch
								? program.ResolveLabel(
									instruction.Argument as string
								)
								: programCounter + 1
							;
							break;
						}

					case InstructionKind.TestNoBranch: {
							var branch = !substitutionSucceeded;
							substitutionSucceeded = false;
							programCounter = branch
								? program.ResolveLabel(
									instruction.Argument as string
								)
								: programCounter + 1
							;
							break;
						}

					case InstructionKind.Transliterate: {
							patternSpace = Transliterate(
								patternSpace,
								instruction.Argument as Transliteration
									?? throw new InvalidOperationException()
							);
							programCounter++;
							break;
						}

					case InstructionKind.WriteFile: {
							await environment.WriteFileAsync(
								instruction.Argument as string
									?? string.Empty,
								patternSpace,
								cancellationToken
							).ConfigureAwait( false );
							programCounter++;
							break;
						}

					case InstructionKind.WriteFirst: {
							await environment.WriteFileAsync(
								instruction.Argument as string
									?? string.Empty,
								FirstPatternLine(
									patternSpace
								),
								cancellationToken
							).ConfigureAwait( false );
							programCounter++;
							break;
						}

					default:
						throw new InvalidOperationException(
							$"Unhandled instruction {instruction.Kind}."
						);
				}
			}

			if (
				automaticPrint
				&& !environment.SuppressAutomaticPrint
			) {
				await WriteRecordAsync(
					environment.Output,
					patternSpace,
					environment.NullData
				).ConfigureAwait( false );
			}
			await environment.FlushDeferredOutputAsync(
				cancellationToken
			).ConfigureAwait( false );
		}

		return new ExecutionResult(
			quit: false,
			exitCode: 0
		);
	}

	private static string EscapeDebugText(
		string value
	) {
		return value
			.Replace( "\\", "\\\\", StringComparison.Ordinal )
			.Replace( "\r", "\\r", StringComparison.Ordinal )
			.Replace( "\n", "\\n", StringComparison.Ordinal )
			.Replace( "\0", "\\0", StringComparison.Ordinal )
		;
	}



	private static string FormatList(
		string value,
		int width
	) {
		var escaped = new StringBuilder();
		foreach ( var character in value ) {
			switch ( character ) {
				case '\\':
					escaped.Append(
						"\\\\"
					);
					break;
				case '\a':
					escaped.Append(
						"\\a"
					);
					break;
				case '\b':
					escaped.Append(
						"\\b"
					);
					break;
				case '\f':
					escaped.Append(
						"\\f"
					);
					break;
				case '\n':
					escaped.Append(
						"\\n"
					);
					break;
				case '\r':
					escaped.Append(
						"\\r"
					);
					break;
				case '\t':
					escaped.Append(
						"\\t"
					);
					break;
				default:
					if (
						char.IsControl(
							character
						)
					) {
						escaped.AppendFormat(
							CultureInfo.InvariantCulture,
							"\\x{0:X2}",
							(int)character
						);
					} else {
						escaped.Append(
							character
						);
					}
					break;
			}
		}
		escaped.Append(
			'$'
		);

		if (
			width <= 0
			|| escaped.Length <= width
		) {
			return escaped.ToString();
		}

		var output = new StringBuilder();
		var index = 0;
		while ( index < escaped.Length ) {
			var count = Math.Min(
				width,
				escaped.Length - index
			);
			output.Append(
				escaped,
				index,
				count
			);
			index += count;
			if ( index < escaped.Length ) {
				output.Append(
					"\\\n"
				);
			}
		}
		return output.ToString();
	}

	private static string FirstPatternLine(
		string patternSpace
	) {
		var index = patternSpace.IndexOf(
			'\n'
		);
		return index < 0
			? patternSpace
			: patternSpace.Substring(
				0,
				index
			)
		;
	}


}
