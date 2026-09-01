# FMT(1)

## NAME

**fmt** — reformat paragraph text

## SYNOPSIS

```text
fmt [-WIDTH] [OPTION]... [FILE]...
```

## PATHNAME GLOBBING

Command-line `FILE` operands that contain supported pathname patterns are expanded in-process according to the repository policy. `*` and `?` match within one pathname component, and a component exactly equal to `**` may match zero or more complete components. Matches preserve operand order and repetition; unmatched patterns are preserved as literal operands.

The operand `-` is preserved and retains its standard-input meaning.

## DESCRIPTION

`fmt` reformats paragraphs while preserving the exact bytes of retained words. This implementation targets the GNU Coreutils 9.11 command-line and paragraph-selection behavior.

Supported options are `--crown-margin`, `--prefix`, `--split-only`, `--tagged-paragraph`, `--uniform-spacing`, `--width`, `--goal`, `--help`, and `--version`, together with the obsolete first-argument `-WIDTH` spelling. GNU recognizes `-WIDTH` only as the first argument; later digit options are rejected with a controlled diagnostic.

The command uses asynchronous binary input and output and follows GNU's byte-oriented layout model: word and prefix lengths are source-byte counts in every locale. Input tabs expand at eight-column stops while paragraphs are read and may be reintroduced in generated indentation. Ordinary nonmatching lines retain their original bytes and line ending. GNU-normalized blank and prefix-only lines, and all reformatted lines, use `Environment.NewLine`. Prefix matching and copied-line indentation follow GNU byte columns, including the distinction between the full prefix supplied by the user and the prefix after trailing ASCII spaces are trimmed.

The paragraph optimizer follows GNU's short-line, raggedness, sentence, punctuation, widow, orphan, and opening-parenthesis costs. Crown and tagged modes retain separate paragraph-recognition state rather than sharing an execution engine with `nl`.

## SPLIT-ONLY AND LINE BREAKING

`--split-only` changes only how source paragraphs are recognized: words from two different input lines are never joined into one paragraph. Each eligible input line is still treated as a complete one-line source paragraph and passed through the same GNU cost-based optimizer used in normal mode. Consequently, split-only mode may divide one input line into several output lines, and it is not equivalent to greedy wrapping.

The maximum width is inclusive, matching GNU Coreutils 9.11. A candidate line whose byte length is exactly the requested width remains eligible, but the optimizer may choose a shorter candidate when sentence, punctuation, orphan, widow, or raggedness costs make the complete paragraph cheaper. For example, `fmt --split-only --width=5` may format `aa bb cc` as `aa` followed by `bb cc`, even though `aa bb` exactly fills five bytes. This is intentional GNU behavior rather than an off-by-one error.

Implementation files are under [`src`](src). Tests are in `tests/Fmt.Tests`.

## AUTHORS

GNU `fmt` was written by Ross Paterson.

Migrated to .NET by Timothy J. Bruce <uniblab@hotmail.com>.

## COPYRIGHT

Copyright (c) 2026 Timothy J. Bruce

See the repository `LICENSE` file for licensing terms and notices applicable to this project.

## SEE ALSO

`fmt(1)`, `fold(1)`
