# `expr` implementation

This directory contains the standalone GNU-compatible `expr` language. The command uses the regular-expression foundation established by Completion Gate C1 but keeps expression grammar and evaluation policy inside the `expr` project.

## Components

- `Command.cs` handles help/version processing, dependency selection, diagnostic output, result rendering, cancellation, and GNU exit statuses: 0 for a non-null result, 1 for a null result, 2 for an invalid expression, and 3 for an internal failure.
- `ExpressionEvaluator.cs` is a precedence-aware recursive-descent evaluator. It implements Boolean `|` and `&`, relations, arbitrary-precision arithmetic, match operators, parentheses, quoting with unary `+`, and the `length`, `match`, `index`, and `substr` prefix operations. Short-circuited branches are still parsed for syntax but suppress runtime operations such as division and regular-expression compilation.
- `ExpressionValue.cs` preserves the distinction between integer and string values, performs GNU integer coercion, and implements GNU null-value rules.
- `ExpressionEvaluationException.cs` carries controlled diagnostic lines together with the exit status to return.
- `IExpressionLocaleProvider.cs` defines collation and logical-character operations so locale behavior is injectable.
- `SystemExpressionLocaleProvider.cs` uses .NET culture collation and Unicode scalar values. It treats malformed UTF-16 deterministically by consuming replacement characters rather than stalling or splitting valid surrogate pairs.

## Regular expressions

The evaluator compiles patterns through `IRegularExpressionProvider` with the Gate C1 GNU `expr` compatibility profile. Matching is anchored at the beginning of the source operand. A pattern with a capture returns the first captured string; a pattern without a capture returns the matched logical-character length; failure returns the corresponding GNU null value.

## Arithmetic and evaluation

Integers use `BigInteger`, so arithmetic is not limited by machine word size. Division by zero, non-integer arithmetic operands, malformed grammar, collation failure, and regular-expression diagnostics are converted into `ExpressionEvaluationException` rather than escaping as uncontrolled command failures. All recursive parsing and provider operations observe the supplied cancellation token and enforce a nesting-depth guard.
