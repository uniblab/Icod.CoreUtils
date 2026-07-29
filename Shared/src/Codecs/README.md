# Codecs

The `Icod.CoreUtils.Shared.Codecs` namespace supplies the common base-encoding engine used by encoding and decoding commands.

## Responsibilities

- Select and validate supported base encodings.
- Encode and decode streams incrementally.
- Apply wrapping, padding, ignored-character, and diagnostic policies consistently.
- Expose command settings and controlled codec failures without depending on native utilities.

## Design notes

The implementation favors BCL primitives and bounded buffers. Command-facing APIs are asynchronous, cancellation-aware, and operate on injected streams without disposing them. Encoding-specific mechanics stay in this namespace so individual command projects only define their option surfaces and help text.
