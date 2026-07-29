# Platform

The `Icod.CoreUtils.Shared.Platform` namespace centralizes operating-system capabilities and injectable host-information providers.

## Responsibilities

- Detect support for file modes, links, identity, statistics, and related platform features.
- Read users, groups, login records, process information, system identity, and system metrics.
- Provide controlled supported, unsupported, degraded, and failed operation results.
- Isolate the small native interop surface required when the BCL does not expose an operation.

## Portability policy

Windows, Linux, and macOS are required platforms. Implementations make a best effort for FreeBSD and other BSD systems, and keep abstractions suitable for a future TempleOS-compatible provider. BCL APIs are preferred; P/Invoke is confined to narrow provider boundaries. Unsupported operations return controlled results rather than throwing platform exceptions through command code.
