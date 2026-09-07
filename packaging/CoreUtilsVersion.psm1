Set-StrictMode -Version Latest

function Get-CoreUtilsVersion {
    param(
        [string]$RepositoryRoot = ''
    )

    if ([string]::IsNullOrWhiteSpace($RepositoryRoot)) {
        $RepositoryRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
    } else {
        $RepositoryRoot = [System.IO.Path]::GetFullPath($RepositoryRoot)
    }

    $propsPath = Join-Path $RepositoryRoot 'Directory.Build.props'
    if (-not (Test-Path -LiteralPath $propsPath -PathType Leaf)) {
        throw "Repository version file '$propsPath' does not exist."
    }

    [xml]$props = Get-Content -LiteralPath $propsPath -Raw
    $versionNodes = @($props.SelectNodes('/Project/PropertyGroup/Version'))
    $packageVersionNodes = @($props.SelectNodes('/Project/PropertyGroup/PackageVersion'))

    if (1 -ne $versionNodes.Count) {
        throw "Directory.Build.props must declare Version exactly once; found $($versionNodes.Count)."
    }
    if (1 -ne $packageVersionNodes.Count) {
        throw "Directory.Build.props must declare PackageVersion exactly once; found $($packageVersionNodes.Count)."
    }

    $version = $versionNodes[0].InnerText.Trim()
    $packageVersion = $packageVersionNodes[0].InnerText.Trim()
    if ([string]::IsNullOrWhiteSpace($version) -or [string]::IsNullOrWhiteSpace($packageVersion)) {
        throw 'Directory.Build.props Version and PackageVersion must both be non-empty.'
    }
    if ($version -ne $packageVersion) {
        throw "Version '$version' does not match PackageVersion '$packageVersion'."
    }

    return $version
}

Export-ModuleMember -Function Get-CoreUtilsVersion
