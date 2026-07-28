# Shared binary formatting

This subsystem provides the platform-neutral portions of binary display commands:

- parsing GNU `od` type strings;
- explicit native, little-endian, and big-endian interpretation;
- primitive integral, character, half, bfloat16, single, and double formatting;
- least-common-multiple line-width validation and GNU-compatible fallback; and
- reusable field-padding calculations.

The `C`, `S`, and `I` integral aliases are fixed at 1, 2, and 4 bytes. `L` follows the host C ABI assumption used by this project: 4 bytes on Windows and pointer width on Unix-like systems. BSD behavior is **best effort** because the supported CI matrix is Windows, Ubuntu, and macOS.

The floating `L` alias is accepted on hosts where C `long double` is represented by an 8-byte IEEE value (Windows and Apple Silicon macOS). Native 80-bit and 128-bit long-double encodings are reported as unsupported instead of being silently misinterpreted.
