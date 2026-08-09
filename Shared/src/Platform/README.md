# Platform

The `Icod.CoreUtils.Shared.Platform` namespace centralizes operating-system capabilities and injectable host-information providers.

## Responsibilities

- Detect support for file modes, links, identity, statistics, security contexts, and related platform features.
- Read users, groups, login records, process information, system identity, system metrics, and supported native security contexts.
- Provide controlled supported, unsupported, degraded, and failed operation results.
- Isolate the small native interop surface required when the BCL does not expose an operation.
- Provide the injectable SELinux boundary used by `chcon` and `runcon`, including file/link contexts, context validation, transition computation, and execution-context setup.

## Portability policy

Windows, Linux, and macOS are required platforms. Implementations make a best effort for FreeBSD and other BSD systems, and keep abstractions suitable for a future TempleOS-compatible provider. BCL APIs are preferred; P/Invoke is confined to narrow provider boundaries. Unsupported operations return controlled results rather than throwing platform exceptions through command code. SELinux operations are Linux-specific and require a usable `libselinux.so.1` plus an SELinux-enabled kernel; other required runners retain portable command metadata and controlled unsupported behavior.
