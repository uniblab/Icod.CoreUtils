# Shared formatting

`GnuEscapeDecoder` implements the reusable escape grammar used by GNU-style command formats and `%b` operands, including octal, hexadecimal, Unicode, and `\c` termination.


The formatting grammar is intentionally distinct from Completion Gate C3's `paste` and `tr` profiles. `GnuEscapeDecoder` retains formatting-specific hexadecimal, Unicode, octal, unknown-escape, and `\c` behavior while delegating only neutral backslash scanning to `Icod.CoreUtils.Shared.Escapes`. A change to another command's grammar must not silently alter formatting output.
