# Icod.CoreUtils.Shared.Platform

This namespace contains Coreutils-specific host-information providers that remain after the neutral platform mechanism moved to the standalone `Icod.CommandFramework` package.

## Retained Coreutils responsibilities

- Read login-accounting records used by commands such as `who`, `users`, and related reporting tools.
- Provide the Coreutils process-information model and host process observations used by command-family behavior.
- Provide system-information and system-metrics observations used by Coreutils reporting commands.
- Provide Coreutils user-information records and lookup behavior that are not part of the neutral framework identity contract.

The retained production source is:

- `LoginRecordProvider.cs`
- `ProcessInformation.cs`
- `SystemInformationProvider.cs`
- `SystemMetrics.cs`
- `UserInformation.cs`

## Framework ownership

Neutral operating-system mechanism is owned by `Icod.CommandFramework.Platform`, including:

- `GroupIdentity`, `UserIdentity`, and `ProcessIdentity`
- `IIdentityProvider` and `SystemIdentityProvider`
- `PlatformCapabilities`
- `PlatformFeature`
- `PlatformOperationResult`
- `SelinuxContext`
- `SelinuxExecutionResult`
- `ISelinuxPlatform`
- `NativeSelinuxPlatform`

Coreutils code that needs those contracts consumes the published framework package directly.

## Portability policy

Windows, Linux, and macOS remain required platforms, with BSD support best effort where the retained providers can obtain meaningful information. Platform-specific parsing and native calls stay behind narrow provider boundaries. Unsupported observations must remain controlled and explicit rather than leaking unrelated host exceptions through command behavior.

SELinux mechanism is no longer implemented in this namespace. `chcon` and `runcon` consume the neutral SELinux boundary from `Icod.CommandFramework.Platform`.
