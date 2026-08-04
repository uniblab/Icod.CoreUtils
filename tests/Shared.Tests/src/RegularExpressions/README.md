# Shared regular-expression tests

This directory verifies the command-neutral GNU regular-expression foundation in `Icod.CoreUtils.Shared.RegularExpressions`.

- `GnuBasicRegularExpressionTests.cs` protects the GNU/POSIX Basic profile and established Coreutils consumers.
- `GnuEmacsRegularExpressionTests.cs` protects the Gnulib Emacs profile used by `ptx`.
- `LineEditorRegularExpressionContractTests.cs` is the Phase LE2 acceptance suite. It validates the Shared BRE/ERE boundary required by GNU Sed 4.10 and GNU Ed 1.22.5 without moving Sed- or Ed-specific state into Shared.

The LE2 suite intentionally stops at compilation, matching, captures, coordinates, locale policy, diagnostics, cancellation, and resource limits. Empty-pattern reuse, address-versus-substitution context, repeated-match progression, replacement parsing, and output encoding remain consumer responsibilities.
