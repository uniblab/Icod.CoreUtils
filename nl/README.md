# nl

`nl` numbers logical document lines with independently configurable header, body, and footer styles. This implementation targets GNU Coreutils 9.11.

## Usage

```text
nl [OPTION]... [FILE]...
```

Supported options are `--body-numbering`, `--section-delimiter`, `--footer-numbering`, `--header-numbering`, `--line-increment`, `--join-blank-lines`, `--number-format`, `--no-renumber`, `--number-separator`, `--starting-line-number`, `--number-width`, `--help`, and `--version`.

All operands form one logical document. The current number, section, blank-line group, and deferred overflow state continue across file boundaries and missing operands. Section delimiters select header, body, or footer styles and normally reset the current number. Pattern styles use the shared fully managed GNU basic regular-expression engine.

Original line bytes and retained line feeds are copied exactly. As GNU does, an unterminated final input line receives a generated `Environment.NewLine`. Generated number fields are culture-invariant; generated logical-page separator lines use `Environment.NewLine`. One-character delimiter values receive the GNU-compatible colon second character, while longer values are accepted as a GNU extension and repeated as a whole. Character counting follows the active profile: UTF-8 locales count Unicode scalars, while `C` and `POSIX` count bytes.

Implementation files are under [`src`](src). Tests are in `tests/NL.Tests`.
