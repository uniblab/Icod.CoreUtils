# Packaging and build automation

This directory contains the repository-local build, validation, archive, and release support for `Icod.CoreUtils`.

The orchestration shape follows the shared templates maintained in `uniblab/.github`, with CoreUtils-specific extensions retained where the command suite has stronger distribution requirements.

## Local build entry points

The repository-root wrappers are intentionally thin:

- `build.cmd` invokes `packaging/Invoke-Build.ps1` with `Debug` configuration on Windows.
- `build.sh` invokes the same PowerShell orchestrator with `Debug` configuration on Unix-like hosts.

With no section argument, the orchestrator performs:

```text
clean
restore
build
test
pack
validate
```

Individual sections may be selected with `clean`, `restore`, `build`, `test`, `pack`, or `validate`.

`Invoke-Build.ps1` discovers the single root solution through `RepositoryTools.psm1`; local wrappers do not hard-code project graphs or duplicate orchestration logic.

## Shared template-style helpers

### `RepositoryTools.psm1`

Provides neutral repository/build helpers used by the local and GitHub workflows, including solution discovery, MSBuild property reading, project discovery, executable-project inspection, package metadata reading, and checked `dotnet` invocation.

### `Get-RepositoryMetadata.ps1`

Discovers repository metadata needed by GitHub Actions. For workflow outputs the solution path is emitted repository-relative so a metadata job running on Linux can safely feed Windows and macOS jobs.

CoreUtils does not perform generic executable-project discovery in this metadata step because its executable distribution shape is already an explicit repository invariant; avoiding that scan keeps the workflow gate inexpensive.

### `Invoke-Build.ps1`

Owns the local clean/restore/build/test/pack/validate pipeline. `build.cmd` and `build.sh` are only host-specific launchers.

## CoreUtils-specific helpers

### `CoreUtilsVersion.psm1`

Reads and validates the centralized repository `Version` and `PackageVersion` from root `Directory.Build.props`.

### `CoreUtilsCommands.ps1`

Defines the authoritative standalone-command/project map used by distribution and archive validation.

### `VerifyLocalPackage.ps1`

Requires the expected `Icod.CoreUtils.<version>.nupkg`, then verifies its NuGet package ID and version against the centralized repository version.

### `VerifyDistribution.ps1`

Performs the full distribution gate for the active host: build, test, package, install, and exercise the router and standalone commands according to the repository's platform rules.

### `BuildReleaseArchive.ps1`

Builds and validates one RID-specific executable ZIP. Tagged releases create archives for:

```text
win-x64
win-arm64
linux-x64
linux-arm64
osx-x64
osx-arm64
```

### `BuildStdBufNativeAsset.ps1`

Builds only the native `stdbuf` shim for one Unix RID and stages it under `artifacts/native` using an RID-qualified filename.

This helper exists so the NuGet package does not have to wait for, download, and unpack complete release ZIP archives merely to obtain the four native shims it embeds.

## GitHub workflow model

Workflow names and responsibilities follow the organization templates:

- `pull-request.yml` — `Staging` build/test on Windows, Linux, and macOS; Linux also packs and validates the NuGet package.
- `main.yml` — six-platform `Release` distribution validation after pushes to `main`.
- `distribution-validation.yml` — manually dispatched six-platform validation for `Debug`, `Staging`, or `Release`.
- `release.yml` — tagged release construction and publication.

The release workflow uses parallel artifact production:

```text
metadata
 ├── archives[6] ───────────────────────────────┐
 └── native-assets[4] → package ─┬→ NuGet.org ─┤
                                 └→ GitHub Pkg ─┤
                                                ↓
                                         GitHub Release
```

The final GitHub Release is the convergence gate. NuGet.org and GitHub Packages are independent publication destinations and therefore run in parallel once the package artifact has been validated.

## Template relationship

The organization templates are the baseline for naming, configuration policy, permissions, concurrency, metadata discovery, and job topology. CoreUtils intentionally specializes the generic template in three areas:

1. the repository has a fixed executable suite rather than an optional executable-project shape;
2. release ZIPs contain all standalone commands plus the multicall router;
3. the NuGet package embeds four platform-specific `stdbuf` native libraries.

Repository-local deviations should remain limited to demonstrated CoreUtils requirements. General-purpose improvements belong upstream in `uniblab/.github` so sibling repositories can inherit them.
