# Escape parser tests

- `PasteDelimiterParserTests.cs` covers empty delimiter lists, `\0` empty slots, all named escapes, ordinary and multibyte delimiter characters, injected stateless encodings, unknown escapes, trailing-backslash errors, and invalid managed scalar input.
- `TrByteEscapeParserTests.cs` covers named and one-to-three-digit octal bytes, escaped-state metadata, GNU's overflowing-octal warning and two-digit fallback, trailing-backslash preservation, unknown escapes, multibyte characters, injected stateless encodings, and invalid managed scalar input.
- `FormattingEscapeCompatibilityTests.cs` characterizes the existing `GnuEscapeDecoder` grammar before and after extraction of neutral scanning mechanics, including formatting-specific unknown-escape, trailing-backslash, octal, Unicode, and stop-output behavior.
