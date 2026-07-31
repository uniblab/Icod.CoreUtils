# `ptx` tests

The Batch 25 test project covers the GNU Coreutils 9.11 command contract: default dumb output, roff and TeX directives, traditional invocation, external parameter files, custom word and sentence regular expressions, case folding, automatic and input references, multiple GNU-mode inputs, the traditional output operand, option errors, informational options, cancellation, stream ownership, and the lowercase apphost identity.

Expected structured and plain-text fixtures were characterized against GNU `ptx` with the C locale. Platform record terminators use `Environment.NewLine` in accordance with repository policy.
