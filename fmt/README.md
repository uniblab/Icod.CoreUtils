# fmt

`fmt` reformats paragraphs while preserving the exact bytes of retained words. This implementation targets the GNU Coreutils 9.11 command-line and paragraph-selection behavior.

## Usage

```text
fmt [-WIDTH] [OPTION]... [FILE]...
```

Supported options are `--crown-margin`, `--prefix`, `--split-only`, `--tagged-paragraph`, `--uniform-spacing`, `--width`, `--goal`, `--help`, and `--version`, together with the obsolete first-argument `-WIDTH` spelling. GNU recognizes `-WIDTH` only as the first argument; later digit options are rejected with a controlled diagnostic.

The command uses asynchronous binary input and output and follows GNU's byte-oriented layout model: word and prefix lengths are source-byte counts in every locale. Input tabs expand at eight-column stops while paragraphs are read and may be reintroduced in generated indentation. Ordinary nonmatching lines retain their original bytes and line ending. GNU-normalized blank and prefix-only lines, and all reformatted lines, use `Environment.NewLine`. Prefix matching and copied-line indentation follow GNU byte columns, including the distinction between the full prefix supplied by the user and the prefix after trailing ASCII spaces are trimmed.

The paragraph optimizer follows GNU's short-line, raggedness, sentence, punctuation, widow, orphan, and opening-parenthesis costs. Crown and tagged modes retain separate paragraph-recognition state rather than sharing an execution engine with `nl`.

Implementation files are under [`src`](src). Tests are in `tests/Fmt.Tests`.
