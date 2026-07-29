namespace Icod.CoreUtils.Expr;

using System.Numerics;
using Icod.CoreUtils.Shared.RegularExpressions;

/// <summary>
/// Parses and immediately evaluates a tokenized GNU <c>expr</c> expression.
/// </summary>
/// <remarks>
/// The recursive-descent evaluator implements GNU precedence, left associativity, Boolean short-circuit parsing, arbitrary-precision arithmetic, locale-sensitive text operations, and Gate C1 basic regular expressions.
/// </remarks>
internal sealed class ExpressionEvaluator {
	private const int InvalidExpressionStatus = 2;
	private const int FailureStatus = 3;
	private const int MaximumNestingDepth = 256;

	private readonly IReadOnlyList<string> arguments;
	private readonly CancellationToken cancellationToken;
	private readonly IExpressionLocaleProvider localeProvider;
	private readonly IRegularExpressionProvider regularExpressionProvider;
	private int argumentIndex;
	private int nestingDepth;

	/// <summary>
	/// Initializes an evaluator over the supplied tokens and injectable regular-expression and locale services.
	/// </summary>
	/// <param name="arguments">The expression tokens in command-line order.</param>
	/// <param name="regularExpressionProvider">The Gate C1 provider used to compile GNU basic regular expressions.</param>
	/// <param name="localeProvider">The provider used for collation and logical-character operations.</param>
	/// <param name="cancellationToken">The token observed throughout parsing and evaluation.</param>
	/// <exception cref="ArgumentNullException">A required token collection or provider is <see langword="null"/>.</exception>
	public ExpressionEvaluator(
		IReadOnlyList<string> arguments,
		IRegularExpressionProvider regularExpressionProvider,
		IExpressionLocaleProvider localeProvider,
		CancellationToken cancellationToken
	) {
		ArgumentNullException.ThrowIfNull( arguments );
		ArgumentNullException.ThrowIfNull( regularExpressionProvider );
		ArgumentNullException.ThrowIfNull( localeProvider );
		this.arguments = arguments;
		this.regularExpressionProvider = regularExpressionProvider;
		this.localeProvider = localeProvider;
		this.cancellationToken = cancellationToken;
	}

	/// <summary>
	/// Parses and evaluates the complete token sequence and rejects trailing or missing operands.
	/// </summary>
	/// <returns>The evaluated integer or string value.</returns>
	/// <exception cref="ExpressionEvaluationException">The token sequence is syntactically invalid or an evaluated operation fails.</exception>
	public ExpressionValue Evaluate() {
		this.cancellationToken.ThrowIfCancellationRequested();
		var value = this.EvaluateOr( true );
		if ( this.HasMoreArguments ) {
			throw Invalid(
				string.Concat(
					"syntax error: unexpected argument ",
					Quote( this.CurrentArgument )
				)
			);
		}
		return value;
	}

	private bool HasMoreArguments => this.arguments.Count > this.argumentIndex;

	private string CurrentArgument => this.arguments[ this.argumentIndex ];

	private ExpressionValue EvaluateOr( bool evaluate ) {
		this.cancellationToken.ThrowIfCancellationRequested();
		var left = this.EvaluateAnd( evaluate );
		while ( this.Consume( "|" ) ) {
			var right = this.EvaluateAnd( evaluate && left.IsNull );
			if ( left.IsNull ) {
				left = right.IsNull ? ExpressionValue.Zero : right;
			}
		}
		return left;
	}

	private ExpressionValue EvaluateAnd( bool evaluate ) {
		this.cancellationToken.ThrowIfCancellationRequested();
		var left = this.EvaluateRelation( evaluate );
		while ( this.Consume( "&" ) ) {
			var right = this.EvaluateRelation( evaluate && !left.IsNull );
			if ( left.IsNull || right.IsNull ) {
				left = ExpressionValue.Zero;
			}
		}
		return left;
	}

	private ExpressionValue EvaluateRelation( bool evaluate ) {
		this.cancellationToken.ThrowIfCancellationRequested();
		var left = this.EvaluateAddition( evaluate );
		while ( this.TryConsumeRelation( out var operation ) ) {
			var right = this.EvaluateAddition( evaluate );
			var comparison = 0;
			if ( evaluate ) {
				comparison = this.Compare( left, right );
			}
			left = ExpressionValue.FromInteger(
				operation switch {
					RelationOperation.LessThan => comparison < 0 ? BigInteger.One : BigInteger.Zero,
					RelationOperation.LessThanOrEqual => comparison <= 0 ? BigInteger.One : BigInteger.Zero,
					RelationOperation.Equal => 0 == comparison ? BigInteger.One : BigInteger.Zero,
					RelationOperation.NotEqual => 0 != comparison ? BigInteger.One : BigInteger.Zero,
					RelationOperation.GreaterThanOrEqual => comparison >= 0 ? BigInteger.One : BigInteger.Zero,
					RelationOperation.GreaterThan => comparison > 0 ? BigInteger.One : BigInteger.Zero,
					_ => BigInteger.Zero
				}
			);
		}
		return left;
	}

	private ExpressionValue EvaluateAddition( bool evaluate ) {
		this.cancellationToken.ThrowIfCancellationRequested();
		var left = this.EvaluateMultiplication( evaluate );
		while ( true ) {
			ArithmeticOperation operation;
			if ( this.Consume( "+" ) ) {
				operation = ArithmeticOperation.Add;
			} else if ( this.Consume( "-" ) ) {
				operation = ArithmeticOperation.Subtract;
			} else {
				return left;
			}
			var right = this.EvaluateMultiplication( evaluate );
			if ( evaluate ) {
				left = ApplyArithmetic( left, right, operation );
			}
		}
	}

	private ExpressionValue EvaluateMultiplication( bool evaluate ) {
		this.cancellationToken.ThrowIfCancellationRequested();
		var left = this.EvaluateMatch( evaluate );
		while ( true ) {
			ArithmeticOperation operation;
			if ( this.Consume( "*" ) ) {
				operation = ArithmeticOperation.Multiply;
			} else if ( this.Consume( "/" ) ) {
				operation = ArithmeticOperation.Divide;
			} else if ( this.Consume( "%" ) ) {
				operation = ArithmeticOperation.Remainder;
			} else {
				return left;
			}
			var right = this.EvaluateMatch( evaluate );
			if ( evaluate ) {
				left = ApplyArithmetic( left, right, operation );
			}
		}
	}

	private ExpressionValue EvaluateMatch( bool evaluate ) {
		this.cancellationToken.ThrowIfCancellationRequested();
		var left = this.EvaluatePrefix( evaluate );
		while ( this.Consume( ":" ) ) {
			var right = this.EvaluatePrefix( evaluate );
			if ( evaluate ) {
				left = this.Match( left, right );
			}
		}
		return left;
	}

	private ExpressionValue EvaluatePrefix( bool evaluate ) {
		this.EnterNesting();
		try {
			return this.EvaluatePrefixCore( evaluate );
		} finally {
			this.nestingDepth--;
		}
	}

	private ExpressionValue EvaluatePrefixCore( bool evaluate ) {
		this.cancellationToken.ThrowIfCancellationRequested();
		if ( this.Consume( "+" ) ) {
			this.RequireMoreArguments();
			return ExpressionValue.FromString( this.TakeArgument() );
		}
		if ( this.Consume( "length" ) ) {
			var operand = this.EvaluatePrefix( evaluate );
			return ExpressionValue.FromInteger(
				this.localeProvider.GetLength(
					operand.AsString(),
					this.cancellationToken
				)
			);
		}
		if ( this.Consume( "match" ) ) {
			var left = this.EvaluatePrefix( evaluate );
			var right = this.EvaluatePrefix( evaluate );
			return evaluate ? this.Match( left, right ) : left;
		}
		if ( this.Consume( "index" ) ) {
			var left = this.EvaluatePrefix( evaluate );
			var right = this.EvaluatePrefix( evaluate );
			return ExpressionValue.FromInteger(
				this.localeProvider.IndexOfAny(
					left.AsString(),
					right.AsString(),
					this.cancellationToken
				)
			);
		}
		if ( this.Consume( "substr" ) ) {
			var value = this.EvaluatePrefix( evaluate );
			var position = this.EvaluatePrefix( evaluate );
			var length = this.EvaluatePrefix( evaluate );
			if (
				!position.TryGetInteger( out var integerPosition )
				|| !length.TryGetInteger( out var integerLength )
			) {
				return ExpressionValue.FromString( string.Empty );
			}
			return ExpressionValue.FromString(
				this.localeProvider.Substring(
					value.AsString(),
					integerPosition,
					integerLength,
					this.cancellationToken
				)
			);
		}
		return this.EvaluatePrimary( evaluate );
	}

	private ExpressionValue EvaluatePrimary( bool evaluate ) {
		this.cancellationToken.ThrowIfCancellationRequested();
		this.RequireMoreArguments();
		if ( this.Consume( "(" ) ) {
			var value = this.EvaluateOr( evaluate );
			if ( !this.HasMoreArguments ) {
				throw Invalid(
					string.Concat(
						"syntax error: expecting ')' after ",
						Quote( this.arguments[ this.argumentIndex - 1 ] )
					)
				);
			}
			if ( !this.Consume( ")" ) ) {
				throw Invalid(
					string.Concat(
						"syntax error: expecting ')' instead of ",
						Quote( this.CurrentArgument )
					)
				);
			}
			return value;
		}
		if ( ")" == this.CurrentArgument ) {
			throw Invalid( "syntax error: unexpected ')'" );
		}
		return ExpressionValue.FromString( this.TakeArgument() );
	}

	private int Compare( ExpressionValue left, ExpressionValue right ) {
		this.cancellationToken.ThrowIfCancellationRequested();
		if (
			left.TryGetInteger( out var leftInteger )
			&& right.TryGetInteger( out var rightInteger )
		) {
			return leftInteger.CompareTo( rightInteger );
		}
		var leftText = left.AsString();
		var rightText = right.AsString();
		try {
			return this.localeProvider.Compare(
				leftText,
				rightText,
				this.cancellationToken
			);
		} catch ( OperationCanceledException ) when ( this.cancellationToken.IsCancellationRequested ) {
			throw;
		} catch ( Exception exception ) {
			throw new ExpressionEvaluationException(
				[
					string.Concat( "string comparison failed: ", exception.Message ),
					"set LC_ALL='C' to work around the problem",
					string.Concat(
						"the strings compared were ",
						Quote( leftText ),
						" and ",
						Quote( rightText )
					)
				],
				InvalidExpressionStatus
			);
		}
	}

	private ExpressionValue Match( ExpressionValue source, ExpressionValue pattern ) {
		this.cancellationToken.ThrowIfCancellationRequested();
		var compileResult = this.regularExpressionProvider.Compile(
			pattern.AsString(),
			RegularExpressionOptions.GnuExprCompatibility,
			this.cancellationToken
		);
		if ( compileResult.Expression is not ICompiledRegularExpression expression ) {
			throw Invalid(
				compileResult.Diagnostic?.Message
				?? "invalid regular expression"
			);
		}
		var matchResult = expression.Match(
			source.AsString(),
			new RegularExpressionMatchOptions {
				RequireMatchAtStart = true
			},
			this.cancellationToken
		);
		if ( !matchResult.IsSuccess ) {
			var diagnosticSuffix = matchResult.Diagnostic is RegularExpressionDiagnostic diagnostic
				? string.Concat( ": ", diagnostic.Message )
				: string.Empty;
			throw new ExpressionEvaluationException(
				string.Concat(
					"error in regular expression matcher",
					diagnosticSuffix
				),
				FailureStatus
			);
		}
		var match = matchResult.Match;
		if ( 0 < expression.CaptureCount ) {
			if (
				match is null
				|| 0 == match.Captures.Count
				|| !match.Captures[ 0 ].Success
			) {
				return ExpressionValue.FromString( string.Empty );
			}
			return ExpressionValue.FromString(
				match.Captures[ 0 ].Value ?? string.Empty
			);
		}
		if ( match is null ) {
			return ExpressionValue.Zero;
		}
		return ExpressionValue.FromInteger(
			this.localeProvider.GetLength(
				match.Value,
				this.cancellationToken
			)
		);
	}

	private static ExpressionValue ApplyArithmetic(
		ExpressionValue left,
		ExpressionValue right,
		ArithmeticOperation operation
	) {
		if (
			!left.TryGetInteger( out var leftInteger )
			|| !right.TryGetInteger( out var rightInteger )
		) {
			throw Invalid( "non-integer argument" );
		}
		if (
			operation is ArithmeticOperation.Divide or ArithmeticOperation.Remainder
			&& BigInteger.Zero == rightInteger
		) {
			throw Invalid( "division by zero" );
		}
		return ExpressionValue.FromInteger(
			operation switch {
				ArithmeticOperation.Add => leftInteger + rightInteger,
				ArithmeticOperation.Subtract => leftInteger - rightInteger,
				ArithmeticOperation.Multiply => leftInteger * rightInteger,
				ArithmeticOperation.Divide => leftInteger / rightInteger,
				ArithmeticOperation.Remainder => leftInteger % rightInteger,
				_ => BigInteger.Zero
			}
		);
	}

	private bool Consume( string value ) {
		if ( !this.HasMoreArguments || !String.Equals( this.CurrentArgument, value, StringComparison.Ordinal ) ) {
			return false;
		}
		this.argumentIndex++;
		return true;
	}

	private bool TryConsumeRelation( out RelationOperation operation ) {
		if ( this.Consume( "<" ) ) {
			operation = RelationOperation.LessThan;
			return true;
		}
		if ( this.Consume( "<=" ) ) {
			operation = RelationOperation.LessThanOrEqual;
			return true;
		}
		if ( this.Consume( "=" ) || this.Consume( "==" ) ) {
			operation = RelationOperation.Equal;
			return true;
		}
		if ( this.Consume( "!=" ) ) {
			operation = RelationOperation.NotEqual;
			return true;
		}
		if ( this.Consume( ">=" ) ) {
			operation = RelationOperation.GreaterThanOrEqual;
			return true;
		}
		if ( this.Consume( ">" ) ) {
			operation = RelationOperation.GreaterThan;
			return true;
		}
		operation = default;
		return false;
	}

	private string TakeArgument() {
		this.RequireMoreArguments();
		return this.arguments[ this.argumentIndex++ ];
	}

	private void RequireMoreArguments() {
		if ( this.HasMoreArguments ) {
			return;
		}
		var previous = 0 < this.argumentIndex
			? this.arguments[ this.argumentIndex - 1 ]
			: string.Empty;
		throw Invalid(
			string.Concat(
				"syntax error: missing argument after ",
				Quote( previous )
			)
		);
	}

	private void EnterNesting() {
		this.cancellationToken.ThrowIfCancellationRequested();
		this.nestingDepth++;
		if ( MaximumNestingDepth >= this.nestingDepth ) {
			return;
		}
		this.nestingDepth--;
		throw Invalid( "expression nesting depth exceeded" );
	}

	private static string Quote( string value ) {
		return string.Concat( "'", value, "'" );
	}

	private static ExpressionEvaluationException Invalid( string message ) {
		return new ExpressionEvaluationException( message, InvalidExpressionStatus );
	}

	private enum ArithmeticOperation {
		Add,
		Subtract,
		Multiply,
		Divide,
		Remainder
	}

	private enum RelationOperation {
		LessThan,
		LessThanOrEqual,
		Equal,
		NotEqual,
		GreaterThanOrEqual,
		GreaterThan
	}
}
