# `ptx` implementation notes

The engine follows the GNU Coreutils 9.11 `ptx.c` phases: decode policy, prepare word and context recognition, digest parameter files, discover keyword occurrences, order occurrences by bytewise keyword comparison while preserving source order for ties, plan output fields, and emit the selected representation.

Default GNU mode recognizes ASCII letters as words and uses GNU's punctuation-plus-tab/two-space sentence boundary. Traditional mode defaults to non-space/tab/newline words, newline contexts, and roff output. `-W` and `-S` are compiled through `Icod.CoreUtils.Shared.RegularExpressions` after GNU-style option-string unescaping.

Every selected context is stored once in a secure temporary workspace. Occurrences retain only the keyword, reference, context offset, and field offsets. The Shared external-ordering engine bounds in-memory run growth and merge fan-in. A single unusually large context remains the irreducible unit required to evaluate arbitrary word or sentence regular expressions.
