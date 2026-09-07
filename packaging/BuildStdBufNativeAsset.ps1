param(
    [Parameter(Mandatory = $true)]
    [ValidateSet('linux-x64', 'linux-arm64', 'osx-x64', 'osx-arm64')]
    [string]$RuntimeIdentifier,

    [ValidateSet('Debug', 'Staging', 'Release')]
    [string]$Configuration = 'Release',

    [string]$OutputDirectory = 'artifacts/native'
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$repositoryRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
Import-Module (Join-Path $PSScriptRoot 'RepositoryTools.psm1') -Force

if (-not [System.IO.Path]::IsPathRooted($OutputDirectory)) {
    $OutputDirectory = Join-Path $repositoryRoot $OutputDirectory
}
$OutputDirectory = [System.IO.Path]::GetFullPath($OutputDirectory)
$publishDirectory = Join-Path $repositoryRoot "artifacts/native-publish/$RuntimeIdentifier"

foreach ($path in @($OutputDirectory, $publishDirectory)) {
    New-Item -ItemType Directory -Path $path -Force | Out-Null
}

$projectPath = Join-Path $repositoryRoot 'stdbuf/Icod.CoreUtils.StdBuf.csproj'
Invoke-DotNet -Arguments @(
    'publish',
    $projectPath,
    '-c', $Configuration,
    '-r', $RuntimeIdentifier,
    '--self-contained', 'false',
    '-p:PublishSelfContained=false',
    '-p:PublishSingleFile=true',
    '-p:PublishTrimmed=false',
    '-p:DebugType=None',
    '-p:DebugSymbols=false',
    '-p:ContinuousIntegrationBuild=true',
    '-o', $publishDirectory
)

$extension = if ($RuntimeIdentifier.StartsWith('linux-', [System.StringComparison]::OrdinalIgnoreCase)) {
    'so'
} else {
    'dylib'
}
$source = Join-Path $publishDirectory "libicodstdbuf.$extension"
if (-not (Test-Path -LiteralPath $source -PathType Leaf)) {
    throw "Publish did not produce '$source'."
}

$destination = Join-Path $OutputDirectory "libicodstdbuf-$RuntimeIdentifier.$extension"
Copy-Item -LiteralPath $source -Destination $destination -Force
Write-Host "Created stdbuf native asset: $destination"
