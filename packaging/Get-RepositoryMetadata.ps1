param(
    [ValidateSet('Debug', 'Staging', 'Release')]
    [string]$Configuration = 'Release',

    [string]$GitHubOutputPath = ''
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$repositoryRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
Import-Module (Join-Path $PSScriptRoot 'RepositoryTools.psm1') -Force

$solutionPath = Get-RepositorySolution -RepositoryRoot $repositoryRoot -AllowMissing
$hasSolution = $null -ne $solutionPath
$solutionOutputPath = if ($hasSolution) {
    [System.IO.Path]::GetRelativePath($repositoryRoot, $solutionPath).Replace('\', '/')
} else {
    ''
}

$result = [ordered]@{
    RepositoryRoot = $repositoryRoot
    HasSolution = $hasSolution
    SolutionPath = if ($hasSolution) { $solutionPath } else { '' }
}

if (-not [string]::IsNullOrWhiteSpace($GitHubOutputPath)) {
    "has_solution=$($hasSolution.ToString().ToLowerInvariant())" >> $GitHubOutputPath
    "solution_path=$solutionOutputPath" >> $GitHubOutputPath
}

[pscustomobject]$result
