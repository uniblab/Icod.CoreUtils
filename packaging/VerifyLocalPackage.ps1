param(
    [string]$ArtifactDirectory = 'artifacts'
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$repositoryRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
Import-Module (Join-Path $PSScriptRoot 'CoreUtilsVersion.psm1') -Force

if (-not [System.IO.Path]::IsPathRooted($ArtifactDirectory)) {
    $ArtifactDirectory = Join-Path $repositoryRoot $ArtifactDirectory
}
$ArtifactDirectory = [System.IO.Path]::GetFullPath($ArtifactDirectory)
if (-not (Test-Path -LiteralPath $ArtifactDirectory -PathType Container)) {
    throw "Artifact directory '$ArtifactDirectory' does not exist."
}

$version = Get-CoreUtilsVersion -RepositoryRoot $repositoryRoot
$packagePath = Join-Path $ArtifactDirectory "Icod.CoreUtils.$version.nupkg"
if (-not (Test-Path -LiteralPath $packagePath -PathType Leaf)) {
    throw "Expected package '$packagePath' was not produced."
}

Add-Type -AssemblyName System.IO.Compression.FileSystem
$archive = [System.IO.Compression.ZipFile]::OpenRead($packagePath)
try {
    $nuspecEntries = @($archive.Entries | Where-Object {
        $_.FullName.EndsWith('.nuspec', [System.StringComparison]::OrdinalIgnoreCase)
    })
    if (1 -ne $nuspecEntries.Count) {
        throw "Package '$packagePath' must contain exactly one .nuspec; found $($nuspecEntries.Count)."
    }

    $reader = [System.IO.StreamReader]::new($nuspecEntries[0].Open())
    try {
        [xml]$nuspec = $reader.ReadToEnd()
    } finally {
        $reader.Dispose()
    }

    $metadata = $nuspec.package.metadata
    if ('Icod.CoreUtils' -ne "$($metadata.id)") {
        throw "Package '$packagePath' declares id '$($metadata.id)'; expected 'Icod.CoreUtils'."
    }
    if ($version -ne "$($metadata.version)") {
        throw "Package '$packagePath' declares version '$($metadata.version)'; expected '$version'."
    }
} finally {
    $archive.Dispose()
}

Write-Host "Validated Icod.CoreUtils package version $version."
