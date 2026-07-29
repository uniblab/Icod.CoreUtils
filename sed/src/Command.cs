// Original behavior/reference: sed (Lee E. McMahon)
// Ported to .NET by Timothy J. Bruce <uniblab@hotmail.com>

namespace Icod.CoreUtils.Sed;

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
public static class Command {

	#region fields
	private const int DefaultListWidth = 70;
	private const int ErrorExitCode = CommandExitCodes.Failure;
	private const int UsageExitCode = CommandExitCodes.UsageError;
	private const string VersionText = "Icod.CoreUtils.Sed 1.0";
	#endregion fields


	#region nested types
	private sealed class Options {

		public bool Debug {
			get;
			set;
		}

		public bool ExtendedRegularExpressions {
			get;
			set;
		}

		public bool FollowSymlinks {
			get;
			set;
		}

		public bool InPlace {
			get;
			set;
		}

		public bool Posix {
			get;
			set;
		}

		public string? BackupSuffix {
			get;
			set;
		}

		public int ListWidth {
			get;
			set;
		} = DefaultListWidth;

		public bool NullData {
			get;
			set;
		}

		public bool Sandbox {
			get;
			set;
		}

		public bool Separate {
			get;
			set;
		}

		public bool SuppressAutomaticPrint {
			get;
			set;
		}

		public bool Unbuffered {
			get;
			set;
		}

	}

	private readonly struct AddressContext {

		public int LineNumber {
			get;
		}

		public bool IsLastLine {
			get;
		}

		public string PatternSpace {
			get;
		}

		public AddressContext(
			int lineNumber,
			bool isLastLine,
			string patternSpace
		) {
			this.LineNumber = lineNumber;
			this.IsLastLine = isLastLine;
			this.PatternSpace = patternSpace;
		}

	}

	private abstract class Address {

		public virtual bool IsRegularExpression {
			get {
				return false;
			}
		}

		public abstract bool Matches(
			in AddressContext context
		);

		public virtual bool MatchesRangeEnd(
			in AddressContext context
		) {
			return this.Matches(
				context
			);
		}

	}

	private sealed class ZeroAddress : Address {

		public override bool Matches(
			in AddressContext context
		) {
			return false;
		}

	}

	private sealed class LineAddress : Address {

		public int LineNumber {
			get;
		}

		public LineAddress(
			int lineNumber
		) {
			if ( lineNumber <= 0 ) {
				throw new ArgumentOutOfRangeException(
					nameof( lineNumber )
				);
			}
			this.LineNumber = lineNumber;
		}

		public override bool Matches(
			in AddressContext context
		) {
			return context.LineNumber == this.LineNumber;
		}

		public override bool MatchesRangeEnd(
			in AddressContext context
		) {
			return context.LineNumber >= this.LineNumber;
		}

	}

	private sealed class StepAddress : Address {

		public int First {
			get;
		}

		public int Step {
			get;
		}

		public StepAddress(
			int first,
			int step
		) {
			if ( first < 0 ) {
				throw new ArgumentOutOfRangeException(
					nameof( first )
				);
			} else if ( step <= 0 ) {
				throw new ArgumentOutOfRangeException(
					nameof( step )
				);
			}

			this.First = first;
			this.Step = step;
		}

		public override bool Matches(
			in AddressContext context
		) {
			var first = 0 == this.First
				? this.Step
				: this.First
			;
			return (
				first <= context.LineNumber
				&& 0 == ( context.LineNumber - first ) % this.Step
			);
		}

	}

	private sealed class LastLineAddress : Address {

		public override bool Matches(
			in AddressContext context
		) {
			return context.IsLastLine;
		}

	}

	private sealed class RegexAddress : Address {

		private readonly Regex myRegex;

		public override bool IsRegularExpression {
			get {
				return true;
			}
		}

		public RegexAddress(
			string pattern,
			bool extendedRegularExpressions
		) {
			this.myRegex = CreateRegex(
				pattern,
				extendedRegularExpressions,
				RegexOptions.None
			);
		}

		public override bool Matches(
			in AddressContext context
		) {
			return this.myRegex.IsMatch(
				context.PatternSpace
			);
		}

	}

	private abstract class RangeEnd {

		public abstract bool IsEnd(
			in AddressContext context,
			int rangeStartLine,
			bool isStartLine
		);

	}

	private sealed class AddressRangeEnd : RangeEnd {

		private readonly Address myAddress;

		public AddressRangeEnd(
			Address address
		) {
			this.myAddress = address ?? throw new ArgumentNullException(
				nameof( address )
			);
		}

		public override bool IsEnd(
			in AddressContext context,
			int rangeStartLine,
			bool isStartLine
		) {
			if (
				isStartLine
				&& this.myAddress.IsRegularExpression
			) {
				return false;
			}

			return this.myAddress.MatchesRangeEnd(
				context
			);
		}

	}

	private sealed class RelativeRangeEnd : RangeEnd {

		private readonly int myAdditionalLines;

		public RelativeRangeEnd(
			int additionalLines
		) {
			if ( additionalLines < 0 ) {
				throw new ArgumentOutOfRangeException(
					nameof( additionalLines )
				);
			}
			this.myAdditionalLines = additionalLines;
		}

		public override bool IsEnd(
			in AddressContext context,
			int rangeStartLine,
			bool isStartLine
		) {
			return context.LineNumber >= rangeStartLine + this.myAdditionalLines;
		}

	}

	private sealed class MultipleRangeEnd : RangeEnd {

		private readonly int myMultiple;

		public MultipleRangeEnd(
			int multiple
		) {
			if ( multiple <= 0 ) {
				throw new ArgumentOutOfRangeException(
					nameof( multiple )
				);
			}
			this.myMultiple = multiple;
		}

		public override bool IsEnd(
			in AddressContext context,
			int rangeStartLine,
			bool isStartLine
		) {
			return (
				!isStartLine
				&& 0 == context.LineNumber % this.myMultiple
			);
		}

	}

	private readonly struct Selection {

		public bool IsSelected {
			get;
		}

		public bool RangeEnded {
			get;
		}

		public bool RangeStarted {
			get;
		}

		public Selection(
			bool isSelected,
			bool rangeStarted,
			bool rangeEnded
		) {
			this.IsSelected = isSelected;
			this.RangeStarted = rangeStarted;
			this.RangeEnded = rangeEnded;
		}

	}

	private sealed class AddressSelector {

		private bool myRangeActive;
		private int myRangeStartLine;

		public Address? First {
			get;
		}

		public bool Negated {
			get;
		}

		public RangeEnd? Second {
			get;
		}

		public bool HasRange {
			get {
				return null != this.Second;
			}
		}

		public AddressSelector(
			Address? first,
			RangeEnd? second,
			bool negated
		) {
			if (
				null == first
				&& null != second
			) {
				throw new ArgumentException(
					"A range end requires a first address.",
					nameof( second )
				);
			}

			this.First = first;
			this.Second = second;
			this.Negated = negated;
			this.Reset();
		}

		public Selection Evaluate(
			in AddressContext context
		) {
			var rangeStarted = false;
			var rangeEnded = false;
			bool selected;

			if ( null == this.First ) {
				selected = true;
			} else if ( null == this.Second ) {
				selected = this.First.Matches(
					context
				);
			} else if ( this.myRangeActive ) {
				selected = true;
				if (
					this.Second.IsEnd(
						context,
						this.myRangeStartLine,
						isStartLine: false
					)
				) {
					this.myRangeActive = false;
					rangeEnded = true;
				}
			} else if (
				this.First is ZeroAddress
			) {
				selected = false;
			} else if (
				this.First.Matches(
					context
				)
			) {
				selected = true;
				rangeStarted = true;
				this.myRangeStartLine = context.LineNumber;
				this.myRangeActive = !this.Second.IsEnd(
					context,
					this.myRangeStartLine,
					isStartLine: true
				);
				rangeEnded = !this.myRangeActive;
			} else {
				selected = false;
			}

			return new Selection(
				this.Negated
					? !selected
					: selected,
				rangeStarted,
				rangeEnded
			);
		}

		public void Reset() {
			this.myRangeActive = this.First is ZeroAddress;
			this.myRangeStartLine = 0;
		}

	}

	private enum InstructionKind {
		AppendText,
		AppendHold,
		AppendNext,
		BeginGroup,
		Branch,
		ChangeText,
		Delete,
		DeleteFirst,
		EndGroup,
		Execute,
		Exchange,
		GetHold,
		Label,
		LineNumber,
		List,
		Next,
		Print,
		PrintFirst,
		Quit,
		QuitSilent,
		ReadFile,
		ReadFileLine,
		SetHold,
		Substitute,
		TestBranch,
		TestNoBranch,
		Transliterate,
		WriteFile,
		WriteFirst
	}

	private sealed class Substitution {

		public bool ExtendedRegularExpressions {
			get;
		}

		public string Flags {
			get;
		}

		public string Pattern {
			get;
		}

		public string Replacement {
			get;
		}

		public Substitution(
			string pattern,
			string replacement,
			string flags,
			bool extendedRegularExpressions
		) {
			this.Pattern = pattern;
			this.Replacement = replacement;
			this.Flags = flags;
			this.ExtendedRegularExpressions = extendedRegularExpressions;
		}

	}

	private sealed class Transliteration {

		public string Destination {
			get;
		}

		public string Source {
			get;
		}

		public Transliteration(
			string source,
			string destination
		) {
			this.Source = source;
			this.Destination = destination;
		}

	}

	private sealed class Instruction {

		public AddressSelector? Address {
			get;
		}

		public object? Argument {
			get;
		}

		public InstructionKind Kind {
			get;
		}

		public int JumpIndex {
			get;
			set;
		} = -1;

		public Instruction(
			InstructionKind kind,
			AddressSelector? address = null,
			object? argument = null
		) {
			this.Kind = kind;
			this.Address = address;
			this.Argument = argument;
		}

	}

	private sealed class SedProgram {

		private readonly Dictionary<string, int> myLabels;

		public IReadOnlyList<Instruction> Instructions {
			get;
		}

		public SedProgram(
			IReadOnlyList<Instruction> instructions
		) {
			this.Instructions = instructions;
			this.myLabels = new Dictionary<string, int>(
				StringComparer.Ordinal
			);

			for (
				var index = 0;
				index < instructions.Count;
				index++
			) {
				var instruction = instructions[ index ];
				if (
					InstructionKind.Label == instruction.Kind
				) {
					var label = instruction.Argument as string
						?? string.Empty
					;
					if (
						this.myLabels.ContainsKey(
							label
						)
					) {
						throw new ScriptParseException(
							$"duplicate label '{label}'"
						);
					}
					this.myLabels.Add(
						label,
						index
					);
				}
			}

			foreach ( var instruction in instructions ) {
				if (
					(
						InstructionKind.Branch == instruction.Kind
						|| InstructionKind.TestBranch == instruction.Kind
						|| InstructionKind.TestNoBranch == instruction.Kind
					)
					&& instruction.Argument is string label
					&& 0 < label.Length
					&& !this.myLabels.ContainsKey(
						label
					)
				) {
					throw new ScriptParseException(
						$"undefined label '{label}'"
					);
				}
			}
		}

		public int ResolveLabel(
			string? label
		) {
			if ( string.IsNullOrEmpty( label ) ) {
				return this.Instructions.Count;
			}

			if (
				!this.myLabels.TryGetValue(
					label,
					out var index
				)
			) {
				throw new ScriptParseException(
					$"undefined label '{label}'"
				);
			}

			return index;
		}

		public void ResetAddresses() {
			foreach ( var instruction in this.Instructions ) {
				instruction.Address?.Reset();
			}
		}

	}

	private sealed class ScriptParseException : Exception {

		public ScriptParseException(
			string message
		) : base(
			message
		) {
		}

	}

	private sealed class ScriptParser {

		private readonly bool myExtendedRegularExpressions;
		private readonly List<Instruction> myInstructions;
		private readonly bool myPosix;
		private string? myLastRegularExpression;
		private readonly bool mySandbox;
		private readonly string myText;
		private int myIndex;

		public ScriptParser(
			string text,
			bool extendedRegularExpressions,
			bool sandbox,
			bool posix
		) {
			this.myText = text ?? throw new ArgumentNullException(
				nameof( text )
			);
			this.myExtendedRegularExpressions = extendedRegularExpressions;
			this.mySandbox = sandbox;
			this.myPosix = posix;
			this.myInstructions = new List<Instruction>();
		}

		public SedProgram Parse() {
			this.ParseSequence(
				stopAtClosingBrace: false
			);
			this.SkipSeparators();
			if ( this.myIndex != this.myText.Length ) {
				throw this.Error(
					"unexpected script text"
				);
			}
			return new SedProgram(
				this.myInstructions
			);
		}

		private void ParseSequence(
			bool stopAtClosingBrace
		) {
			while ( this.myIndex < this.myText.Length ) {
				this.SkipSeparators();
				if ( this.myIndex >= this.myText.Length ) {
					if ( stopAtClosingBrace ) {
						throw this.Error(
							"unterminated command group"
						);
					}
					return;
				}

				if ( '}' == this.myText[ this.myIndex ] ) {
					if ( !stopAtClosingBrace ) {
						throw this.Error(
							"unexpected closing brace"
						);
					}
					this.myIndex++;
					return;
				}

				if ( '#' == this.myText[ this.myIndex ] ) {
					this.SkipComment();
					continue;
				}

				var selector = this.ParseSelector();
				this.SkipHorizontalWhitespace();

				if ( this.myIndex >= this.myText.Length ) {
					throw this.Error(
						"missing command"
					);
				}

				var command = this.myText[ this.myIndex ];
				switch ( command ) {
					case '#':
						if ( null != selector ) {
							throw this.Error(
								"comments cannot have addresses"
							);
						}
						this.SkipComment();
						break;

					case ':':
						if ( null != selector ) {
							throw this.Error(
								"labels cannot have addresses"
							);
						}
						this.myIndex++;
						this.myInstructions.Add(
							new Instruction(
								InstructionKind.Label,
								argument: this.ReadSimpleArgument()
							)
						);
						break;

					case '{': {
							this.myIndex++;
							var begin = new Instruction(
								InstructionKind.BeginGroup,
								selector
							);
							this.myInstructions.Add(
								begin
							);
							this.ParseSequence(
								stopAtClosingBrace: true
							);
							this.myInstructions.Add(
								new Instruction(
									InstructionKind.EndGroup
								)
							);
							begin.JumpIndex = this.myInstructions.Count;
							break;
						}

					case '=':
						this.RequireAtMostOneAddress(
							selector,
							command
						);
						this.myIndex++;
						this.myInstructions.Add(
							new Instruction(
								InstructionKind.LineNumber,
								selector
							)
						);
						this.RequireBoundary();
						break;

					case 'a':
						this.RequireAtMostOneAddress(
							selector,
							command
						);
						this.myIndex++;
						this.myInstructions.Add(
							new Instruction(
								InstructionKind.AppendText,
								selector,
								this.ReadTextArgument()
							)
						);
						break;

					case 'b':
						this.myIndex++;
						this.myInstructions.Add(
							new Instruction(
								InstructionKind.Branch,
								selector,
								this.ReadSimpleArgument()
							)
						);
						break;

					case 'c':
						this.myIndex++;
						this.myInstructions.Add(
							new Instruction(
								InstructionKind.ChangeText,
								selector,
								this.ReadTextArgument()
							)
						);
						break;

					case 'd':
						this.myIndex++;
						this.myInstructions.Add(
							new Instruction(
								InstructionKind.Delete,
								selector
							)
						);
						this.RequireBoundary();
						break;

					case 'D':
						this.myIndex++;
						this.myInstructions.Add(
							new Instruction(
								InstructionKind.DeleteFirst,
								selector
							)
						);
						this.RequireBoundary();
						break;

					case 'e':
						this.RequireGnuExtension(
							command
						);
						this.RequireFileAccess();
						this.myIndex++;
						this.myInstructions.Add(
							new Instruction(
								InstructionKind.Execute,
								selector,
								this.ReadSimpleArgument()
							)
						);
						break;

					case 'g':
						this.myIndex++;
						this.myInstructions.Add(
							new Instruction(
								InstructionKind.GetHold,
								selector
							)
						);
						this.RequireBoundary();
						break;

					case 'G':
						this.myIndex++;
						this.myInstructions.Add(
							new Instruction(
								InstructionKind.AppendHold,
								selector
							)
						);
						this.RequireBoundary();
						break;

					case 'h':
						this.myIndex++;
						this.myInstructions.Add(
							new Instruction(
								InstructionKind.SetHold,
								selector
							)
						);
						this.RequireBoundary();
						break;

					case 'H':
						this.myIndex++;
						this.myInstructions.Add(
							new Instruction(
								InstructionKind.AppendHold,
								selector,
								argument: true
							)
						);
						this.RequireBoundary();
						break;

					case 'i':
						this.RequireAtMostOneAddress(
							selector,
							command
						);
						this.myIndex++;
						this.myInstructions.Add(
							new Instruction(
								InstructionKind.Print,
								selector,
								new InsertArgument(
									this.ReadTextArgument()
								)
							)
						);
						break;

					case 'l':
						this.myIndex++;
						this.SkipHorizontalWhitespace();
						var listWidth = this.ReadOptionalInteger();
						if (
							this.myPosix
							&& listWidth.HasValue
						) {
							throw this.Error(
								"the l command width is not available in POSIX mode"
							);
						}
						this.myInstructions.Add(
							new Instruction(
								InstructionKind.List,
								selector,
								listWidth
							)
						);
						this.RequireBoundary();
						break;

					case 'n':
						this.myIndex++;
						this.myInstructions.Add(
							new Instruction(
								InstructionKind.Next,
								selector
							)
						);
						this.RequireBoundary();
						break;

					case 'N':
						this.myIndex++;
						this.myInstructions.Add(
							new Instruction(
								InstructionKind.AppendNext,
								selector
							)
						);
						this.RequireBoundary();
						break;

					case 'p':
						this.myIndex++;
						this.myInstructions.Add(
							new Instruction(
								InstructionKind.Print,
								selector
							)
						);
						this.RequireBoundary();
						break;

					case 'P':
						this.myIndex++;
						this.myInstructions.Add(
							new Instruction(
								InstructionKind.PrintFirst,
								selector
							)
						);
						this.RequireBoundary();
						break;

					case 'q':
						this.RequireAtMostOneAddress(
							selector,
							command
						);
						this.myIndex++;
						this.SkipHorizontalWhitespace();
						var quitExitCode = this.ReadOptionalInteger();
						if (
							this.myPosix
							&& quitExitCode.HasValue
						) {
							throw this.Error(
								"the q command exit code is not available in POSIX mode"
							);
						}
						this.myInstructions.Add(
							new Instruction(
								InstructionKind.Quit,
								selector,
								quitExitCode
							)
						);
						this.RequireBoundary();
						break;

					case 'Q':
						this.RequireGnuExtension(
							command
						);
						this.RequireAtMostOneAddress(
							selector,
							command
						);
						this.myIndex++;
						this.SkipHorizontalWhitespace();
						this.myInstructions.Add(
							new Instruction(
								InstructionKind.QuitSilent,
								selector,
								this.ReadOptionalInteger()
							)
						);
						this.RequireBoundary();
						break;

					case 'r':
						this.RequireFileAccess();
						this.RequireAtMostOneAddress(
							selector,
							command
						);
						this.myIndex++;
						this.myInstructions.Add(
							new Instruction(
								InstructionKind.ReadFile,
								selector,
								this.ReadFileArgument()
							)
						);
						break;

					case 'R':
						this.RequireGnuExtension(
							command
						);
						this.RequireFileAccess();
						this.RequireAtMostOneAddress(
							selector,
							command
						);
						this.myIndex++;
						this.myInstructions.Add(
							new Instruction(
								InstructionKind.ReadFileLine,
								selector,
								this.ReadFileArgument()
							)
						);
						break;

					case 's':
						this.myInstructions.Add(
							new Instruction(
								InstructionKind.Substitute,
								selector,
								this.ParseSubstitution()
							)
						);
						break;

					case 't':
						this.myIndex++;
						this.myInstructions.Add(
							new Instruction(
								InstructionKind.TestBranch,
								selector,
								this.ReadSimpleArgument()
							)
						);
						break;

					case 'T':
						this.RequireGnuExtension(
							command
						);
						this.myIndex++;
						this.myInstructions.Add(
							new Instruction(
								InstructionKind.TestNoBranch,
								selector,
								this.ReadSimpleArgument()
							)
						);
						break;

					case 'w':
						this.RequireFileAccess();
						this.myIndex++;
						this.myInstructions.Add(
							new Instruction(
								InstructionKind.WriteFile,
								selector,
								this.ReadFileArgument()
							)
						);
						break;

					case 'W':
						this.RequireGnuExtension(
							command
						);
						this.RequireFileAccess();
						this.myIndex++;
						this.myInstructions.Add(
							new Instruction(
								InstructionKind.WriteFirst,
								selector,
								this.ReadFileArgument()
							)
						);
						break;

					case 'x':
						this.myIndex++;
						this.myInstructions.Add(
							new Instruction(
								InstructionKind.Exchange,
								selector
							)
						);
						this.RequireBoundary();
						break;

					case 'y':
						this.myInstructions.Add(
							new Instruction(
								InstructionKind.Transliterate,
								selector,
								this.ParseTransliteration()
							)
						);
						break;

					default:
						throw this.Error(
							$"unsupported command '{command}'"
						);
				}
			}

			if ( stopAtClosingBrace ) {
				throw this.Error(
					"unterminated command group"
				);
			}
		}

		private AddressSelector? ParseSelector() {
			var save = this.myIndex;
			var first = this.TryParseAddress(
				allowRangeEndSpecialForms: false
			);
			if ( null == first ) {
				this.myIndex = save;
				return null;
			}

			this.SkipHorizontalWhitespace();
			RangeEnd? second = null;
			if (
				this.myIndex < this.myText.Length
				&& ',' == this.myText[ this.myIndex ]
			) {
				this.myIndex++;
				this.SkipHorizontalWhitespace();
				second = this.ParseRangeEnd();
			}

			this.SkipHorizontalWhitespace();
			var negated = false;
			if (
				this.myIndex < this.myText.Length
				&& '!' == this.myText[ this.myIndex ]
			) {
				negated = true;
				this.myIndex++;
			}

			return new AddressSelector(
				first,
				second,
				negated
			);
		}

		private Address? TryParseAddress(
			bool allowRangeEndSpecialForms
		) {
			if ( this.myIndex >= this.myText.Length ) {
				return null;
			}

			var character = this.myText[ this.myIndex ];
			if ( '$' == character ) {
				this.myIndex++;
				return new LastLineAddress();
			}

			if ( char.IsDigit( character ) ) {
				var number = this.ReadInteger(
					allowZero: true
				);
				if (
					this.myIndex < this.myText.Length
					&& '~' == this.myText[ this.myIndex ]
				) {
					if ( this.myPosix ) {
						throw this.Error(
							"step addresses are not available in POSIX mode"
						);
					}
					this.myIndex++;
					var step = this.ReadInteger(
						allowZero: false
					);
					return new StepAddress(
						number,
						step
					);
				}

				if ( 0 == number ) {
					if ( this.myPosix ) {
						throw this.Error(
							"address 0 is not available in POSIX mode"
						);
					}
					return new ZeroAddress();
				}
				return new LineAddress(
					number
				);
			}

			if ( '/' == character ) {
				this.myIndex++;
				var pattern = this.ReadDelimited(
					'/'
				);
				pattern = this.ResolveRegularExpression(
					pattern
				);
				return this.CreateRegexAddress(
					pattern
				);
			}

			if (
				'\\' == character
				&& this.myIndex + 1 < this.myText.Length
			) {
				this.myIndex++;
				var delimiter = this.myText[ this.myIndex ];
				this.myIndex++;
				var pattern = this.ReadDelimited(
					delimiter
				);
				pattern = this.ResolveRegularExpression(
					pattern
				);
				return this.CreateRegexAddress(
					pattern
				);
			}

			return null;
		}

		private RangeEnd ParseRangeEnd() {
			if ( this.myIndex >= this.myText.Length ) {
				throw this.Error(
					"missing range end"
				);
			}

			if ( '+' == this.myText[ this.myIndex ] ) {
				if ( this.myPosix ) {
					throw this.Error(
						"relative range addresses are not available in POSIX mode"
					);
				}
				this.myIndex++;
				return new RelativeRangeEnd(
					this.ReadInteger(
						allowZero: true
					)
				);
			}

			if ( '~' == this.myText[ this.myIndex ] ) {
				if ( this.myPosix ) {
					throw this.Error(
						"multiple range addresses are not available in POSIX mode"
					);
				}
				this.myIndex++;
				return new MultipleRangeEnd(
					this.ReadInteger(
						allowZero: false
					)
				);
			}

			var address = this.TryParseAddress(
				allowRangeEndSpecialForms: true
			) ?? throw this.Error(
				"missing range end"
			);
			return new AddressRangeEnd(
				address
			);
		}

		private Substitution ParseSubstitution() {
			this.myIndex++;
			if ( this.myIndex >= this.myText.Length ) {
				throw this.Error(
					"substitution is missing its delimiter"
				);
			}

			var delimiter = this.myText[ this.myIndex ];
			this.myIndex++;

			var pattern = this.ReadDelimited(
				delimiter
			);
			pattern = this.ResolveRegularExpression(
				pattern
			);
			this.ValidateRegularExpression(
				pattern
			);
			var replacement = this.ReadDelimited(
				delimiter
			);

			var flagStart = this.myIndex;
			while (
				this.myIndex < this.myText.Length
				&& !this.IsCommandSeparator(
					this.myText[ this.myIndex ]
				)
				&& '}' != this.myText[ this.myIndex ]
			) {
				this.myIndex++;
			}

			var flags = this.myText.Substring(
				flagStart,
				this.myIndex - flagStart
			).Trim();

			this.ValidateSubstitutionFlags(
				flags
			);

			return new Substitution(
				pattern,
				replacement,
				flags,
				this.myExtendedRegularExpressions
			);
		}

		private Transliteration ParseTransliteration() {
			this.myIndex++;
			if ( this.myIndex >= this.myText.Length ) {
				throw this.Error(
					"transliteration is missing its delimiter"
				);
			}

			var delimiter = this.myText[ this.myIndex ];
			this.myIndex++;
			var source = this.ReadDelimited(
				delimiter
			);
			var destination = this.ReadDelimited(
				delimiter
			);
			this.RequireBoundary();
			if (
				ExpandCharacterSet(
					source
				).Length
				!= ExpandCharacterSet(
					destination
				).Length
			) {
				throw this.Error(
					"the y command source and destination must have equal lengths"
				);
			}

			return new Transliteration(
				source,
				destination
			);
		}

		private string ReadDelimited(
			char delimiter
		) {
			var output = new StringBuilder();
			var escaped = false;

			while ( this.myIndex < this.myText.Length ) {
				var character = this.myText[ this.myIndex ];
				this.myIndex++;

				if ( escaped ) {
					if ( delimiter == character ) {
						output.Append(
							character
						);
					} else {
						output.Append(
							'\\'
						);
						output.Append(
							character
						);
					}
					escaped = false;
				} else if ( '\\' == character ) {
					escaped = true;
				} else if ( delimiter == character ) {
					return output.ToString();
				} else {
					output.Append(
						character
					);
				}
			}

			throw this.Error(
				$"unterminated expression using delimiter '{delimiter}'"
			);
		}

		private string ResolveRegularExpression(
			string pattern
		) {
			if ( 0 == pattern.Length ) {
				return this.myLastRegularExpression
					?? throw this.Error(
						"no previous regular expression"
					)
				;
			}

			this.myLastRegularExpression = pattern;
			return pattern;
		}

		private string ReadTextArgument() {
			this.SkipHorizontalWhitespace();
			if (
				this.myIndex < this.myText.Length
				&& '\\' == this.myText[ this.myIndex ]
			) {
				this.myIndex++;
				if (
					this.myIndex < this.myText.Length
					&& '\r' == this.myText[ this.myIndex ]
				) {
					this.myIndex++;
				}
				if (
					this.myIndex < this.myText.Length
					&& '\n' == this.myText[ this.myIndex ]
				) {
					this.myIndex++;
				}
			}

			return UnescapeSedText(
				this.ReadUntilCommandSeparator()
			);
		}

		private string ReadFileArgument() {
			this.SkipHorizontalWhitespace();
			var output = this.ReadUntilCommandSeparator().Trim();
			if ( 0 == output.Length ) {
				throw this.Error(
					"missing file name"
				);
			}
			return output;
		}

		private string ReadSimpleArgument() {
			this.SkipHorizontalWhitespace();
			return this.ReadUntilCommandSeparator().Trim();
		}

		private string ReadUntilCommandSeparator() {
			var output = new StringBuilder();
			var escaped = false;

			while ( this.myIndex < this.myText.Length ) {
				var character = this.myText[ this.myIndex ];
				if (
					!escaped
					&& (
						this.IsCommandSeparator(
							character
						)
						|| '}' == character
					)
				) {
					break;
				}

				this.myIndex++;
				if ( escaped ) {
					output.Append(
						character
					);
					escaped = false;
				} else if ( '\\' == character ) {
					escaped = true;
					output.Append(
						character
					);
				} else {
					output.Append(
						character
					);
				}
			}

			return output.ToString();
		}

		private int? ReadOptionalInteger() {
			if (
				this.myIndex >= this.myText.Length
				|| !char.IsDigit(
					this.myText[ this.myIndex ]
				)
			) {
				return null;
			}
			return this.ReadInteger(
				allowZero: true
			);
		}

		private int ReadInteger(
			bool allowZero
		) {
			var start = this.myIndex;
			while (
				this.myIndex < this.myText.Length
				&& char.IsDigit(
					this.myText[ this.myIndex ]
				)
			) {
				this.myIndex++;
			}

			if (
				start == this.myIndex
				|| !int.TryParse(
					this.myText.Substring(
						start,
						this.myIndex - start
					),
					NumberStyles.None,
					CultureInfo.InvariantCulture,
					out var output
				)
				|| (
					!allowZero
					&& output <= 0
				)
			) {
				throw this.Error(
					"invalid numeric argument"
				);
			}

			return output;
		}

		private RegexAddress CreateRegexAddress(
			string pattern
		) {
			this.ValidateRegularExpression(
				pattern
			);
			return new RegexAddress(
				pattern,
				this.myExtendedRegularExpressions
			);
		}

		private void ValidateRegularExpression(
			string pattern
		) {
			try {
				_ = CreateRegex(
					pattern,
					this.myExtendedRegularExpressions,
					RegexOptions.None
				);
			} catch ( ArgumentException ex ) {
				throw this.Error(
					$"invalid regular expression: {ex.Message}"
				);
			}
		}

		private void RequireGnuExtension(
			char command
		) {
			if ( this.myPosix ) {
				throw this.Error(
					$"command '{command}' is not available in POSIX mode"
				);
			}
		}

		private void ValidateSubstitutionFlags(
			string flags
		) {
			var index = 0;
			var occurrenceSeen = false;
			while ( index < flags.Length ) {
				var character = flags[ index ];
				if ( char.IsWhiteSpace( character ) ) {
					index++;
					continue;
				}
				if ( char.IsDigit( character ) ) {
					if ( occurrenceSeen ) {
						throw this.Error(
							"multiple substitution occurrence numbers"
						);
					}
					occurrenceSeen = true;
					var occurrenceStart = index;
					while (
						index < flags.Length
						&& char.IsDigit( flags[ index ] )
					) {
						index++;
					}
					if (
						!int.TryParse(
							flags.Substring(
								occurrenceStart,
								index - occurrenceStart
							),
							NumberStyles.None,
							CultureInfo.InvariantCulture,
							out var occurrence
						)
						|| occurrence <= 0
					) {
						throw this.Error(
							"substitution occurrence must be a positive integer"
						);
					}
					continue;
				}
				if (
					'g' == character
					|| 'p' == character
				) {
					index++;
					continue;
				}
				if ( 'w' == character ) {
					if ( this.mySandbox ) {
						throw this.Error(
							"the substitution w flag is disabled in sandbox mode"
						);
					}
					index++;
					while (
						index < flags.Length
						&& char.IsWhiteSpace( flags[ index ] )
					) {
						index++;
					}
					if ( index >= flags.Length ) {
						throw this.Error(
							"the substitution w flag requires a file name"
						);
					}
					return;
				}
				if (
					'i' == character
					|| 'I' == character
					|| 'm' == character
					|| 'M' == character
					|| 'e' == character
				) {
					if ( this.myPosix ) {
						throw this.Error(
							$"substitution flag '{character}' is not available in POSIX mode"
						);
					}
					if (
						'e' == character
						&& this.mySandbox
					) {
						throw this.Error(
							"the substitution e flag is disabled in sandbox mode"
						);
					}
					index++;
					continue;
				}
				throw this.Error(
					$"unknown substitution flag '{character}'"
				);
			}
		}

		private void RequireAtMostOneAddress(
			AddressSelector? selector,
			char command
		) {
			if (
				null != selector
				&& selector.HasRange
			) {
				throw this.Error(
					$"command '{command}' accepts at most one address"
				);
			}
		}

		private void RequireFileAccess() {
			if ( this.mySandbox ) {
				throw this.Error(
					"file access commands are disabled in sandbox mode"
				);
			}
		}

		private void RequireBoundary() {
			if (
				this.myIndex < this.myText.Length
				&& !this.IsCommandSeparator(
					this.myText[ this.myIndex ]
				)
				&& '}' != this.myText[ this.myIndex ]
				&& !char.IsWhiteSpace(
					this.myText[ this.myIndex ]
				)
			) {
				throw this.Error(
					"unexpected text after command"
				);
			}
		}

		private void SkipComment() {
			while (
				this.myIndex < this.myText.Length
				&& '\n' != this.myText[ this.myIndex ]
			) {
				this.myIndex++;
			}
		}

		private void SkipHorizontalWhitespace() {
			while (
				this.myIndex < this.myText.Length
				&& (
					' ' == this.myText[ this.myIndex ]
					|| '\t' == this.myText[ this.myIndex ]
				)
			) {
				this.myIndex++;
			}
		}

		private void SkipSeparators() {
			while ( this.myIndex < this.myText.Length ) {
				var character = this.myText[ this.myIndex ];
				if (
					';' == character
					|| '\r' == character
					|| '\n' == character
					|| ' ' == character
					|| '\t' == character
				) {
					this.myIndex++;
				} else {
					break;
				}
			}
		}

		private bool IsCommandSeparator(
			char character
		) {
			return (
				';' == character
				|| '\r' == character
				|| '\n' == character
			);
		}

		private ScriptParseException Error(
			string message
		) {
			return new ScriptParseException(
				$"{message} near script position {this.myIndex + 1}"
			);
		}

	}

	private sealed class InsertArgument {

		public string Text {
			get;
		}

		public InsertArgument(
			string text
		) {
			this.Text = text;
		}

	}

	private sealed class SourceSpec {

		public string Path {
			get;
		}

		public SourceSpec(
			string path
		) {
			this.Path = path;
		}

	}

	private sealed class AsyncRecordReader : IDisposable {

		private readonly DelimitedRecordReader myReader;
		private readonly bool myOwnsReader;
		private readonly TextReader myTextReader;

		public AsyncRecordReader(
			TextReader reader,
			bool nullData,
			bool ownsReader
		) {
			this.myTextReader = reader ?? throw new ArgumentNullException(
				nameof( reader )
			);
			this.myOwnsReader = ownsReader;
			this.myReader = new DelimitedRecordReader(
				reader,
				nullData
					? '\0'
					: '\n',
				bufferSize: 8192,
				trimCarriageReturn: !nullData
			);
		}

		public async Task<string?> ReadAsync(
			CancellationToken cancellationToken
		) {
			return await this.myReader.ReadAsync(
				cancellationToken
			).ConfigureAwait( false );
		}

		public void Dispose() {
			if ( this.myOwnsReader ) {
				this.myTextReader.Dispose();
			}
		}

	}

	private sealed class InputSequence : IDisposable {

		private AsyncRecordReader? myCurrentReader;
		private bool myInitialized;
		private string? myLookahead;
		private bool myLookaheadAvailable;
		private readonly bool myNullData;
		private int mySourceIndex = -1;
		private readonly IReadOnlyList<SourceSpec> mySources;
		private readonly TextReader myStandardInput;

		public string Current {
			get;
			private set;
		} = string.Empty;

		public bool IsLast {
			get;
			private set;
		}

		public int LineNumber {
			get;
			private set;
		}

		public InputSequence(
			IReadOnlyList<SourceSpec> sources,
			TextReader standardInput,
			bool nullData
		) {
			this.mySources = sources;
			this.myStandardInput = standardInput;
			this.myNullData = nullData;
		}

		public async Task<bool> MoveNextAsync(
			CancellationToken cancellationToken
		) {
			if ( !this.myInitialized ) {
				this.myInitialized = true;
				this.myLookahead = await this.ReadRawAsync(
					cancellationToken
				).ConfigureAwait( false );
				this.myLookaheadAvailable = null != this.myLookahead;
			}

			if ( !this.myLookaheadAvailable ) {
				return false;
			}

			this.Current = this.myLookahead ?? string.Empty;
			this.myLookahead = await this.ReadRawAsync(
				cancellationToken
			).ConfigureAwait( false );
			this.myLookaheadAvailable = null != this.myLookahead;
			this.IsLast = !this.myLookaheadAvailable;
			this.LineNumber++;
			return true;
		}

		private async Task<string?> ReadRawAsync(
			CancellationToken cancellationToken
		) {
			while ( true ) {
				if (
					null == this.myCurrentReader
					&& !this.OpenNextSource()
				) {
					return null;
				}

				var value = await this.myCurrentReader!.ReadAsync(
					cancellationToken
				).ConfigureAwait( false );
				if ( null != value ) {
					return value;
				}

				this.CloseCurrentReader();
			}
		}

		private bool OpenNextSource() {
			this.mySourceIndex++;
			if ( this.mySources.Count <= this.mySourceIndex ) {
				return false;
			}

			var source = this.mySources[ this.mySourceIndex ];
			if ( "-" == source.Path ) {
				this.myCurrentReader = new AsyncRecordReader(
					this.myStandardInput,
					this.myNullData,
					ownsReader: false
				);
			} else {
				this.myCurrentReader = new AsyncRecordReader(
					new StreamReader(
						new FileStream(
							source.Path,
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
					this.myNullData,
					ownsReader: true
				);
			}
			return true;
		}

		private void CloseCurrentReader() {
			this.myCurrentReader?.Dispose();
			this.myCurrentReader = null;
		}

		public void Dispose() {
			this.CloseCurrentReader();
		}

	}

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

	#endregion nested types


	#region public methods

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

	#endregion public methods


	#region argument methods

	private static async Task<int?> ParseArgumentsAsync(
		string[] args,
		Options options,
		ICollection<string> scripts,
		ICollection<string> files,
		TextWriter stdout,
		TextWriter stderr,
		CancellationToken cancellationToken
	) {
		var parser = new OptionParser(
			new OptionDefinition[] {
				new OptionDefinition( "quiet", 'n', new string[] { "quiet", "silent" } ),
				new OptionDefinition( "debug", longNames: new string[] { "debug" } ),
				new OptionDefinition( "expression", 'e', new string[] { "expression" }, OptionValueArity.Required ),
				new OptionDefinition( "file", 'f', new string[] { "file" }, OptionValueArity.Required ),
				new OptionDefinition( "follow-symlinks", longNames: new string[] { "follow-symlinks" } ),
				new OptionDefinition( "in-place", 'i', new string[] { "in-place" }, OptionValueArity.Optional ),
				new OptionDefinition( "line-length", 'l', new string[] { "line-length" }, OptionValueArity.Required ),
				new OptionDefinition( "posix", longNames: new string[] { "posix" } ),
				new OptionDefinition( "regexp-extended", 'E', new string[] { "regexp-extended" } ),
				new OptionDefinition( "regexp-extended-r", 'r' ),
				new OptionDefinition( "separate", 's', new string[] { "separate" } ),
				new OptionDefinition( "sandbox", longNames: new string[] { "sandbox" } ),
				new OptionDefinition( "unbuffered", 'u', new string[] { "unbuffered" } ),
				new OptionDefinition( "null-data", 'z', new string[] { "null-data" } ),
				new OptionDefinition( "help", '?', new string[] { "help" } ),
				new OptionDefinition( "version", 'V', new string[] { "version" } )
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
				await stderr.WriteLineAsync(
					OptionDiagnosticFormatter.Format(
						"sed",
						error
					)
				).ConfigureAwait( false );
			}
			return UsageExitCode;
		}

		foreach ( var occurrence in result.Options ) {
			cancellationToken.ThrowIfCancellationRequested();
			switch ( occurrence.Definition.Key ) {
				case "quiet":
					options.SuppressAutomaticPrint = true;
					break;
				case "debug":
					options.Debug = true;
					break;
				case "expression":
					scripts.Add(
						occurrence.Value ?? string.Empty
					);
					break;
				case "file":
					scripts.Add(
						await ReadScriptFileAsync(
							occurrence.Value ?? string.Empty,
							cancellationToken
						).ConfigureAwait( false )
					);
					break;
				case "follow-symlinks":
					options.FollowSymlinks = true;
					break;
				case "in-place":
					options.InPlace = true;
					options.BackupSuffix = occurrence.Value ?? string.Empty;
					break;
				case "line-length":
					if (
						!int.TryParse(
							occurrence.Value,
							NumberStyles.None,
							CultureInfo.InvariantCulture,
							out var listWidth
						)
						|| listWidth <= 0
					) {
						await stderr.WriteLineAsync(
							"sed: option --line-length requires a positive integer"
						).ConfigureAwait( false );
						return UsageExitCode;
					}
					options.ListWidth = listWidth;
					break;
				case "posix":
					options.Posix = true;
					break;
				case "regexp-extended":
				case "regexp-extended-r":
					options.ExtendedRegularExpressions = true;
					break;
				case "separate":
					options.Separate = true;
					break;
				case "sandbox":
					options.Sandbox = true;
					break;
				case "unbuffered":
					options.Unbuffered = true;
					break;
				case "null-data":
					options.NullData = true;
					break;
				case "help":
					await PrintUsageAsync(
						stdout
					).ConfigureAwait( false );
					return CommandExitCodes.Success;
				case "version":
					await stdout.WriteLineAsync(
						VersionText
					).ConfigureAwait( false );
					return CommandExitCodes.Success;
			}
		}

		foreach ( var operand in result.Operands ) {
			files.Add(
				operand
			);
		}
		return null;
	}

	#endregion argument methods


	#region execution methods

	private static async Task<ExecutionResult> ProcessInPlaceAsync(
		string path,
		Options options,
		SedProgram program,
		TextWriter stderr,
		CancellationToken cancellationToken
	) {
		var editPath = ResolveInPlacePath(
			path,
			options.FollowSymlinks
		);
		var directory = Path.GetDirectoryName(
			editPath
		) ?? ".";
		var temporaryPath = Path.Combine(
			directory,
			$".sed.{Path.GetRandomFileName()}.tmp"
		);
		var attributes = File.GetAttributes(
			editPath
		);
		UnixFileMode? unixMode = null;
		if ( !OperatingSystem.IsWindows() ) {
			unixMode = File.GetUnixFileMode(
				editPath
			);
		}

		Encoding encoding;
		using ( var stream = new FileStream(
			editPath,
			FileMode.Open,
			FileAccess.Read,
			FileShare.Read,
			8192,
			useAsync: true
		) )
		using ( var reader = new StreamReader(
			stream,
			Encoding.UTF8,
			detectEncodingFromByteOrderMarks: true,
			bufferSize: 8192,
			leaveOpen: false
		) ) {
			var probe = new char[ 1 ];
			_ = await reader.ReadAsync(
				probe.AsMemory(),
				cancellationToken
			).ConfigureAwait( false );
			encoding = reader.CurrentEncoding;
		}

		try {
			ExecutionResult result;
			using ( var outputStream = new FileStream(
				temporaryPath,
				FileMode.CreateNew,
				FileAccess.Write,
				FileShare.None,
				8192,
				useAsync: true
			) )
			using ( var output = new StreamWriter(
				outputStream,
				encoding,
				8192,
				leaveOpen: false
			) )
			using ( var input = new InputSequence(
				new SourceSpec[ 1 ] {
					new SourceSpec(
						editPath
					)
				},
				TextReader.Null,
				options.NullData
			) ) {
				if ( options.Unbuffered ) {
					output.AutoFlush = true;
				}

				var environment = new ExecutionEnvironment(
					output,
					stderr,
					options.SuppressAutomaticPrint,
					options.NullData,
					options.ListWidth,
					options.Debug
				);
				try {
					result = await ExecuteAsync(
						program,
						input,
						environment,
						cancellationToken
					).ConfigureAwait( false );
					await output.FlushAsync().ConfigureAwait( false );
				} finally {
					await environment.DisposeAsync().ConfigureAwait( false );
				}
			}

			if (
				null != options.BackupSuffix
				&& 0 < options.BackupSuffix.Length
			) {
				var backupPath = BuildBackupPath(
					editPath,
					options.BackupSuffix
				);
				if ( File.Exists( backupPath ) ) {
					File.Delete(
						backupPath
					);
				}
				File.Move(
					editPath,
					backupPath
				);
			} else {
				if (
					0 != (
						attributes & FileAttributes.ReadOnly
					)
				) {
					File.SetAttributes(
						editPath,
						attributes & ~FileAttributes.ReadOnly
					);
				}
				File.Delete(
					editPath
				);
			}

			File.Move(
				temporaryPath,
				editPath
			);
			File.SetAttributes(
				editPath,
				attributes & ~FileAttributes.ReparsePoint
			);
			if (
				!OperatingSystem.IsWindows()
				&& unixMode.HasValue
			) {
				File.SetUnixFileMode(
					editPath,
					unixMode.Value
				);
			}
			return result;
		} catch {
			if ( File.Exists( temporaryPath ) ) {
				File.Delete(
					temporaryPath
				);
			}
			throw;
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
					patternSpace
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
								out var replaced
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

	#endregion execution methods


	#region substitution methods

	private sealed class SubstitutionFlags {

		public bool Execute {
			get;
			set;
		}

		public bool Global {
			get;
			set;
		}

		public bool IgnoreCase {
			get;
			set;
		}

		public bool Multiline {
			get;
			set;
		}

		public int? Occurrence {
			get;
			set;
		}

		public bool Print {
			get;
			set;
		}

		public string? WriteFile {
			get;
			set;
		}

	}

	private static SubstitutionFlags ParseSubstitutionFlags(
		string flags
	) {
		var output = new SubstitutionFlags();
		var index = 0;

		while ( index < flags.Length ) {
			var character = flags[ index ];
			if ( char.IsWhiteSpace( character ) ) {
				index++;
			} else if ( char.IsDigit( character ) ) {
				var start = index;
				while (
					index < flags.Length
					&& char.IsDigit(
						flags[ index ]
					)
				) {
					index++;
				}
				output.Occurrence = int.Parse(
					flags.Substring(
						start,
						index - start
					),
					CultureInfo.InvariantCulture
				);
			} else if ( 'e' == character ) {
				output.Execute = true;
				index++;
			} else if ( 'g' == character ) {
				output.Global = true;
				index++;
			} else if ( 'p' == character ) {
				output.Print = true;
				index++;
			} else if (
				'i' == character
				|| 'I' == character
			) {
				output.IgnoreCase = true;
				index++;
			} else if (
				'm' == character
				|| 'M' == character
			) {
				output.Multiline = true;
				index++;
			} else if ( 'w' == character ) {
				index++;
				while (
					index < flags.Length
					&& char.IsWhiteSpace(
						flags[ index ]
					)
				) {
					index++;
				}
				output.WriteFile = flags.Substring(
					index
				).Trim();
				break;
			} else {
				index++;
			}
		}

		return output;
	}

	private static string ApplySubstitution(
		string input,
		Substitution substitution,
		out bool replaced
	) {
		var flags = ParseSubstitutionFlags(
			substitution.Flags
		);
		var options = RegexOptions.None;
		if ( flags.IgnoreCase ) {
			options |= RegexOptions.IgnoreCase;
		}
		if ( flags.Multiline ) {
			options |= RegexOptions.Multiline;
		}

		var regex = CreateRegex(
			substitution.Pattern,
			substitution.ExtendedRegularExpressions,
			options
		);
		var matches = regex.Matches(
			input
		);
		if ( 0 == matches.Count ) {
			replaced = false;
			return input;
		}

		var first = flags.Occurrence ?? 1;
		if (
			first <= 0
			|| matches.Count < first
		) {
			replaced = false;
			return input;
		}

		var output = new StringBuilder(
			input.Length
		);
		var cursor = 0;
		var replacementCount = 0;

		for (
			var index = 0;
			index < matches.Count;
			index++
		) {
			var matchNumber = index + 1;
			var shouldReplace = flags.Global
				? first <= matchNumber
				: first == matchNumber
			;
			if ( !shouldReplace ) {
				continue;
			}

			var match = matches[ index ];
			output.Append(
				input,
				cursor,
				match.Index - cursor
			);
			output.Append(
				ExpandReplacement(
					substitution.Replacement,
					match
				)
			);
			cursor = match.Index + match.Length;
			replacementCount++;

			if ( !flags.Global ) {
				break;
			}
		}

		if ( 0 == replacementCount ) {
			replaced = false;
			return input;
		}

		output.Append(
			input,
			cursor,
			input.Length - cursor
		);
		replaced = true;
		return output.ToString();
	}

	private static string ExpandReplacement(
		string replacement,
		Match match
	) {
		var output = new StringBuilder();

		for (
			var index = 0;
			index < replacement.Length;
			index++
		) {
			var character = replacement[ index ];
			if ( '&' == character ) {
				output.Append(
					match.Value
				);
			} else if (
				'\\' == character
				&& index + 1 < replacement.Length
			) {
				index++;
				var escaped = replacement[ index ];
				if (
					'0' <= escaped
					&& escaped <= '9'
				) {
					var groupNumber = escaped - '0';
					if ( groupNumber < match.Groups.Count ) {
						output.Append(
							match.Groups[ groupNumber ].Value
						);
					}
				} else {
					switch ( escaped ) {
						case 'n':
							output.Append(
								'\n'
							);
							break;
						case 'r':
							output.Append(
								'\r'
							);
							break;
						case 't':
							output.Append(
								'\t'
							);
							break;
						default:
							output.Append(
								escaped
							);
							break;
					}
				}
			} else {
				output.Append(
					character
				);
			}
		}

		return output.ToString();
	}

	#endregion substitution methods


	#region regex methods

	private static Regex CreateRegex(
		string pattern,
		bool extendedRegularExpressions,
		RegexOptions options
	) {
		var translated = TranslatePosixClasses(
			extendedRegularExpressions
				? pattern
				: TranslateBasicRegularExpression(
					pattern
				)
		);
		return new Regex(
			translated,
			options
		);
	}

	private static string TranslateBasicRegularExpression(
		string pattern
	) {
		var output = new StringBuilder(
			pattern.Length
		);
		var inCharacterClass = false;

		for (
			var index = 0;
			index < pattern.Length;
			index++
		) {
			var character = pattern[ index ];
			if ( '\\' == character ) {
				if ( index + 1 >= pattern.Length ) {
					output.Append(
						'\\'
					);
					break;
				}

				var escaped = pattern[ ++index ];
				switch ( escaped ) {
					case '(':
					case ')':
					case '{':
					case '}':
					case '+':
					case '?':
					case '|':
						output.Append(
							escaped
						);
						break;
					default:
						output.Append(
							'\\'
						);
						output.Append(
							escaped
						);
						break;
				}
			} else if ( '[' == character ) {
				inCharacterClass = true;
				output.Append(
					character
				);
			} else if (
				']' == character
				&& inCharacterClass
			) {
				inCharacterClass = false;
				output.Append(
					character
				);
			} else if (
				!inCharacterClass
				&& (
					'(' == character
					|| ')' == character
					|| '{' == character
					|| '}' == character
					|| '+' == character
					|| '?' == character
					|| '|' == character
				)
			) {
				output.Append(
					'\\'
				);
				output.Append(
					character
				);
			} else {
				output.Append(
					character
				);
			}
		}

		return output.ToString();
	}

	private static string TranslatePosixClasses(
		string pattern
	) {
		return pattern
			.Replace( "[[:alnum:]]", "[A-Za-z0-9]" )
			.Replace( "[[:alpha:]]", "[A-Za-z]" )
			.Replace( "[[:blank:]]", "[ \\t]" )
			.Replace( "[[:cntrl:]]", "[\\x00-\\x1F\\x7F]" )
			.Replace( "[[:digit:]]", "[0-9]" )
			.Replace( "[[:graph:]]", "[\\x21-\\x7E]" )
			.Replace( "[[:lower:]]", "[a-z]" )
			.Replace( "[[:print:]]", "[\\x20-\\x7E]" )
			.Replace( "[[:punct:]]", "[!-/:-@\\[-`{-~]" )
			.Replace( "[[:space:]]", "\\s" )
			.Replace( "[[:upper:]]", "[A-Z]" )
			.Replace( "[[:xdigit:]]", "[A-Fa-f0-9]" )
		;
	}

	#endregion regex methods


	private sealed record ShellResult(
		int ExitCode,
		string StandardOutput
	);

	private sealed class TextWriterStream : Stream {

		private readonly Decoder myDecoder;
		private readonly Encoding myEncoding;
		private readonly TextWriter myWriter;

		public override bool CanRead {
			get {
				return false;
			}
		}

		public override bool CanSeek {
			get {
				return false;
			}
		}

		public override bool CanWrite {
			get {
				return true;
			}
		}

		public override long Length {
			get {
				throw new NotSupportedException();
			}
		}

		public override long Position {
			get {
				throw new NotSupportedException();
			}
			set {
				throw new NotSupportedException();
			}
		}

		public TextWriterStream(
			TextWriter writer,
			Encoding encoding
		) {
			this.myWriter = writer ?? throw new ArgumentNullException(
				nameof( writer )
			);
			this.myEncoding = encoding ?? throw new ArgumentNullException(
				nameof( encoding )
			);
			this.myDecoder = encoding.GetDecoder();
		}

		public override void Flush() {
			this.myWriter.Flush();
		}

		public override async Task FlushAsync(
			CancellationToken cancellationToken
		) {
			var characters = new char[
				this.myEncoding.GetMaxCharCount(
					0
				)
			];
			this.myDecoder.Convert(
				ReadOnlySpan<byte>.Empty,
				characters.AsSpan(),
				flush: true,
				out _,
				out var charactersUsed,
				out _
			);
			if ( 0 < charactersUsed ) {
				await this.myWriter.WriteAsync(
					characters.AsMemory(
						0,
						charactersUsed
					),
					cancellationToken
				).ConfigureAwait( false );
			}
			await this.myWriter.FlushAsync(
				cancellationToken
			).ConfigureAwait( false );
		}

		public override int Read(
			byte[] buffer,
			int offset,
			int count
		) {
			throw new NotSupportedException();
		}

		public override long Seek(
			long offset,
			SeekOrigin origin
		) {
			throw new NotSupportedException();
		}

		public override void SetLength(
			long value
		) {
			throw new NotSupportedException();
		}

		public override void Write(
			byte[] buffer,
			int offset,
			int count
		) {
			var characters = new char[
				this.myEncoding.GetMaxCharCount(
					count
				)
			];
			this.myDecoder.Convert(
				buffer.AsSpan(
					offset,
					count
				),
				characters.AsSpan(),
				flush: false,
				out _,
				out var charactersUsed,
				out _
			);
			this.myWriter.Write(
				characters,
				0,
				charactersUsed
			);
		}

		public override async ValueTask WriteAsync(
			ReadOnlyMemory<byte> buffer,
			CancellationToken cancellationToken = default
		) {
			if ( buffer.IsEmpty ) {
				return;
			}
			var characters = new char[
				this.myEncoding.GetMaxCharCount(
					buffer.Length
				)
			];
			this.myDecoder.Convert(
				buffer.Span,
				characters.AsSpan(),
				flush: false,
				out _,
				out var charactersUsed,
				out _
			);
			await this.myWriter.WriteAsync(
				characters.AsMemory(
					0,
					charactersUsed
				),
				cancellationToken
			).ConfigureAwait( false );
		}

	}

	private static async Task<ShellResult> ExecuteShellAsync(
		string command,
		ExecutionEnvironment environment,
		bool captureStandardOutput,
		CancellationToken cancellationToken
	) {
		await using var outputStream = captureStandardOutput
			? null
			: new TextWriterStream(
				environment.Output,
				Encoding.UTF8
			)
		;
		await using var errorStream = new TextWriterStream(
			environment.Error,
			Encoding.UTF8
		);
		var options = new ProcessRunOptions(
			OperatingSystem.IsWindows()
				? Environment.GetEnvironmentVariable( "COMSPEC" ) ?? "cmd.exe"
				: "/bin/sh"
		) {
			CaptureStandardOutput = captureStandardOutput,
			OutputEncoding = Encoding.UTF8,
			StandardError = errorStream,
			StandardOutput = outputStream
		};
		if ( OperatingSystem.IsWindows() ) {
			options.Arguments.Add( "/d" );
			options.Arguments.Add( "/s" );
			options.Arguments.Add( "/c" );
		} else {
			options.Arguments.Add( "-c" );
		}
		options.Arguments.Add(
			command
		);
		var result = await ProcessRunner.RunAsync(
			options,
			cancellationToken
		).ConfigureAwait( false );
		if ( result.WasCanceled ) {
			throw new OperationCanceledException(
				cancellationToken
			);
		}
		if ( null != outputStream ) {
			await outputStream.FlushAsync(
				cancellationToken
			).ConfigureAwait( false );
		}
		await errorStream.FlushAsync(
			cancellationToken
		).ConfigureAwait( false );
		return new ShellResult(
			result.ExitCode ?? ErrorExitCode,
			result.StandardOutput ?? string.Empty
		);
	}

	private static string BuildBackupPath(
		string path,
		string suffix
	) {
		return suffix.Contains(
			"*",
			StringComparison.Ordinal
		)
			? suffix.Replace(
				"*",
				path,
				StringComparison.Ordinal
			)
			: string.Concat(
				path,
				suffix
			)
		;
	}

	private static string ResolveInPlacePath(
		string path,
		bool followSymlinks
	) {
		if ( !followSymlinks ) {
			return path;
		}
		var info = new FileInfo(
			path
		);
		var target = info.ResolveLinkTarget(
			returnFinalTarget: true
		);
		return target?.FullName ?? path;
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

	#region text methods

	private static string Transliterate(
		string input,
		Transliteration transliteration
	) {
		var source = ExpandCharacterSet(
			transliteration.Source
		);
		var destination = ExpandCharacterSet(
			transliteration.Destination
		);
		if ( source.Length != destination.Length ) {
			throw new ScriptParseException(
				"the y command source and destination must have equal lengths"
			);
		}

		var map = new Dictionary<char, char>();
		for (
			var index = 0;
			index < source.Length;
			index++
		) {
			map[ source[ index ] ] = destination[ index ];
		}

		var output = input.ToCharArray();
		for (
			var index = 0;
			index < output.Length;
			index++
		) {
			if (
				map.TryGetValue(
					output[ index ],
					out var replacement
				)
			) {
				output[ index ] = replacement;
			}
		}
		return new string(
			output
		);
	}

	private static string ExpandCharacterSet(
		string value
	) {
		var output = new StringBuilder();

		for (
			var index = 0;
			index < value.Length;
			index++
		) {
			var character = value[ index ];
			if (
				index + 2 < value.Length
				&& '-' == value[ index + 1 ]
				&& character <= value[ index + 2 ]
			) {
				var end = value[ index + 2 ];
				for (
					var current = character;
					current <= end;
					current++
				) {
					output.Append(
						current
					);
				}
				index += 2;
			} else if (
				'\\' == character
				&& index + 1 < value.Length
			) {
				index++;
				output.Append(
					UnescapeCharacter(
						value[ index ]
					)
				);
			} else {
				output.Append(
					character
				);
			}
		}

		return output.ToString();
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

	private static string UnescapeSedText(
		string value
	) {
		var output = new StringBuilder(
			value.Length
		);
		for (
			var index = 0;
			index < value.Length;
			index++
		) {
			var character = value[ index ];
			if (
				'\\' == character
				&& index + 1 < value.Length
			) {
				index++;
				output.Append(
					UnescapeCharacter(
						value[ index ]
					)
				);
			} else {
				output.Append(
					character
				);
			}
		}
		return output.ToString();
	}

	private static char UnescapeCharacter(
		char character
	) {
		return character switch {
			'a' => '\a',
			'b' => '\b',
			'f' => '\f',
			'n' => '\n',
			'r' => '\r',
			't' => '\t',
			'v' => '\v',
			_ => character
		};
	}

	private static async Task<string> ReadScriptFileAsync(
		string path,
		CancellationToken cancellationToken
	) {
		using ( var reader = new StreamReader(
			new FileStream(
				path,
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
		) ) {
			cancellationToken.ThrowIfCancellationRequested();
			return await reader.ReadToEndAsync(
				cancellationToken
			).ConfigureAwait( false );
		}
	}

	private static async Task WriteRecordAsync(
		TextWriter writer,
		string value,
		bool nullData
	) {
		await writer.WriteAsync(
			value
		).ConfigureAwait( false );

		if ( nullData ) {
			await writer.WriteAsync(
				'\0'
			).ConfigureAwait( false );
		} else {
			await writer.WriteLineAsync().ConfigureAwait( false );
		}
	}

	#endregion text methods


	#region usage methods

	private static async Task PrintUsageAsync(
		TextWriter stdout
	) {
		using ( var buffer = new StringWriter(
			CultureInfo.InvariantCulture
		) ) {
			PrintUsage(
				buffer
			);
			await stdout.WriteAsync(
				buffer.ToString()
			).ConfigureAwait( false );
		}
	}

	private static void PrintUsage(
		TextWriter stdout
	) {
		stdout.WriteLine(
			"Usage: sed [OPTION]... {script-only-if-no-other-script} [input-file]..."
		);
		stdout.WriteLine(
			"  -?, --help                  display this help"
		);
		stdout.WriteLine(
			"  -V, --version               display version information"
		);
		stdout.WriteLine(
			"  -n, --quiet, --silent       suppress automatic printing"
		);
		stdout.WriteLine(
			"      --debug                  annotate program execution"
		);
		stdout.WriteLine(
			"  -e SCRIPT                   add SCRIPT to the program"
		);
		stdout.WriteLine(
			"  -f FILE                     add commands from script FILE"
		);
		stdout.WriteLine(
			"  -i[SUFFIX]                  edit files in place; optionally back up"
		);
		stdout.WriteLine(
			"      --follow-symlinks        follow symlinks when editing in place"
		);
		stdout.WriteLine(
			"      --posix                  disable GNU extensions"
		);
		stdout.WriteLine(
			"  -E, -r                      use extended regular expressions"
		);
		stdout.WriteLine(
			"  -s, --separate              treat input files separately"
		);
		stdout.WriteLine(
			"  -u, --unbuffered            flush output more frequently"
		);
		stdout.WriteLine(
			"  -z, --null-data             separate records with NUL"
		);
		stdout.WriteLine(
			"  -l N, --line-length=N       set the l-command wrap width"
		);
		stdout.WriteLine(
			"      --sandbox                disable e, r, R, w, W, and s///e"
		);
		stdout.WriteLine();
		stdout.WriteLine(
			"Addresses:"
		);
		stdout.WriteLine(
			"  N        line N; $ last line; /expr/ matching pattern space"
		);
		stdout.WriteLine(
			"  M,N      inclusive address range; append ! to negate"
		);
		stdout.WriteLine(
			"  F~S      every Sth line beginning with F"
		);
		stdout.WriteLine(
			"  A,+N     address A and the following N lines"
		);
		stdout.WriteLine(
			"  A,~N     address A through the next line-number multiple of N"
		);
		stdout.WriteLine();
		stdout.WriteLine(
			"Commands:"
		);
		stdout.WriteLine(
			"  =        print input line number"
		);
		stdout.WriteLine(
			"  a TEXT   append TEXT after the current cycle"
		);
		stdout.WriteLine(
			"  b LABEL  branch unconditionally"
		);
		stdout.WriteLine(
			"  c TEXT   replace selected pattern spaces with TEXT"
		);
		stdout.WriteLine(
			"  d, D     delete pattern space / delete through first newline"
		);
		stdout.WriteLine(
			"  e [CMD]  execute CMD, or execute pattern space when omitted"
		);
		stdout.WriteLine(
			"  g,G,h,H,x manipulate pattern and hold spaces"
		);
		stdout.WriteLine(
			"  i TEXT   insert TEXT before the current pattern space"
		);
		stdout.WriteLine(
			"  l [N]    list pattern space unambiguously"
		);
		stdout.WriteLine(
			"  n, N     read next record / append next record"
		);
		stdout.WriteLine(
			"  p, P     print pattern space / first pattern-space line"
		);
		stdout.WriteLine(
			"  q, Q     quit with / without automatic printing"
		);
		stdout.WriteLine(
			"  r,R FILE append FILE / one successive line from FILE"
		);
		stdout.WriteLine(
			"  sXreXreplacementXFLAGS  substitute using delimiter X"
		);
		stdout.WriteLine(
			"           FLAGS: N, e, g, p, i/I, m/M, w FILE"
		);
		stdout.WriteLine(
			"  t,T LABEL branch after successful / unsuccessful substitution"
		);
		stdout.WriteLine(
			"  w,W FILE write pattern space / first pattern-space line"
		);
		stdout.WriteLine(
			"  yXsrcXdstX transliterate characters"
		);
		stdout.WriteLine(
			"  :LABEL   define a label; { ... } group commands; # comment"
		);
	}

	#endregion usage methods

}
