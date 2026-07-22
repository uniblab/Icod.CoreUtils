param(
    [string]$SolutionPath = (Get-ChildItem -Path . -Filter *.sln | Select-Object -First 1).FullName
)

if (-not $SolutionPath) {
    Write-Error "No solution file found in the current directory. Place this script in repo root and re-run."
    exit 1
}

$repoRoot = Split-Path -Path $SolutionPath -Parent
Set-Location $repoRoot

# Backup existing Directory.Build.props if present
$dirPropsPath = Join-Path $repoRoot "Directory.Build.props"
if (Test-Path $dirPropsPath) {
    Copy-Item -Path $dirPropsPath -Destination ($dirPropsPath + ".bak") -Force
    Write-Output "Backed up existing Directory.Build.props to Directory.Build.props.bak"
}

# Create Directory.Build.props (overwrite)
$dirPropsContent = @'
<Project>
    <PropertyGroup>
        <!-- Ensure Staging is defined for projects that respect this property -->
        <Configurations>Debug;Staging;Release</Configurations>
    </PropertyGroup>

    <PropertyGroup Condition=" '$(Configuration)' == 'Debug' ">
        <ErrorReport>prompt</ErrorReport>
        <WarningLevel>2</WarningLevel>
        <DebugSymbols>true</DebugSymbols>
        <DebugType>full</DebugType>
        <Optimize>false</Optimize>
        <DefineConstants>DEBUG;TRACE</DefineConstants>
        <!-- Suppress missing XML comment warning only in Debug -->
        <NoWarn>1591</NoWarn>
    </PropertyGroup>

    <PropertyGroup Condition=" '$(Configuration)' == 'Staging' ">
        <ErrorReport>prompt</ErrorReport>
        <WarningLevel>3</WarningLevel>
        <DebugSymbols>true</DebugSymbols>
        <DebugType>full</DebugType>
        <Optimize>false</Optimize>
        <DefineConstants>TRACE</DefineConstants>
    </PropertyGroup>

    <PropertyGroup Condition=" '$(Configuration)' == 'Release' ">
        <AssemblyKeyContainerName>Icod</AssemblyKeyContainerName>
        <DelaySign>false</DelaySign>
        <DebugType>pdbonly</DebugType>
        <Optimize>true</Optimize>
        <ErrorReport>prompt</ErrorReport>
        <WarningLevel>4</WarningLevel>
    </PropertyGroup>
</Project>
'@

Set-Content -Path $dirPropsPath -Value $dirPropsContent -Encoding UTF8
Write-Output "Wrote Directory.Build.props"

# Read solution
$slnRaw = Get-Content -Path $SolutionPath -Raw -Encoding UTF8
# Normalize to LF for easier regex handling
$sln = $slnRaw -replace "`r`n", "`n"

#
# 1) Ensure SolutionConfigurationPlatforms contains Staging|Any CPU
#
if ($sln -notmatch 'Staging\|Any CPU') {
    $sln = [regex]::Replace($sln,
        'GlobalSection\(SolutionConfigurationPlatforms\) = preSolution\s*\n',
        { param($m) $m.Value + "        Staging|Any CPU = Staging|Any CPU`n" },
        [System.Text.RegularExpressions.RegexOptions]::Singleline)
    Write-Output "Inserted 'Staging|Any CPU' into SolutionConfigurationPlatforms"
} else {
    Write-Output "Solution already contains 'Staging|Any CPU'"
}

#
# 2) Ensure ProjectConfigurationPlatforms includes per-project Staging mappings
#
# Collect project GUIDs from Project(...) entries
$projectGuidPattern = 'Project\("[^"]+"\)\s*=\s*"[^"]+",\s*"[^"]+",\s*"\{([0-9A-Fa-f-]+)\}"'
$projectGuids = @()
foreach ($m in [regex]::Matches($sln, $projectGuidPattern)) {
    $projectGuids += $m.Groups[1].Value.ToUpperInvariant()
}

if ($projectGuids.Count -eq 0) {
    Write-Output "No projects found in solution (no Project(...) entries)."
} else {
    $pcpPattern = 'GlobalSection\(ProjectConfigurationPlatforms\) = postSolution\s*\n([\s\S]*?)\n\tEndGlobalSection'
    $pcpMatch = [regex]::Match($sln, $pcpPattern)
    if ($pcpMatch.Success) {
        $section = $pcpMatch.Groups[1].Value
        $added = $false
        foreach ($guid in $projectGuids) {
            $entryActive = ("{$guid}.Staging|Any CPU.ActiveCfg")
            if ($section -notmatch [regex]::Escape($entryActive)) {
                $addLines = "        {$guid}.Staging|Any CPU.ActiveCfg = Staging|Any CPU`n" +
                            "        {$guid}.Staging|Any CPU.Build.0 = Staging|Any CPU`n"
                $section += $addLines
                $added = $true
            }
        }
        if ($added) {
            $sln = [regex]::Replace($sln, $pcpPattern, "GlobalSection(ProjectConfigurationPlatforms) = postSolution`n" + $section + "`tEndGlobalSection", [System.Text.RegularExpressions.RegexOptions]::Singleline)
            Write-Output "Added per-project Staging mappings in ProjectConfigurationPlatforms"
        } else {
            Write-Output "Per-project Staging mappings already present"
        }
    } else {
        Write-Output "ProjectConfigurationPlatforms section not found. Solution may be missing that global section. No per-project mappings were added."
    }
}

# Write back solution with CRLF line endings (Visual Studio expects CRLF)
Set-Content -Path $SolutionPath -Value ($sln -replace "`n", "`r`n") -Encoding UTF8
Write-Output "Patched solution file: $SolutionPath"

#
# 3) Patch every .csproj under repository (excluding bin/obj)
#
$csprojFiles = Get-ChildItem -Path $repoRoot -Recurse -Filter *.csproj -File |
    Where-Object { $_.FullName -notmatch '\\bin\\' -and $_.FullName -notmatch '\\obj\\' }

if ($csprojFiles.Count -eq 0) {
    Write-Output "No .csproj files found to patch."
} else {
    $insertBlock = @'
  <PropertyGroup Condition=" '$(Configuration)' == 'Debug' ">
    <ErrorReport>prompt</ErrorReport>
    <WarningLevel>2</WarningLevel>
    <DebugSymbols>true</DebugSymbols>
    <DebugType>full</DebugType>
    <Optimize>false</Optimize>
    <DefineConstants>DEBUG;TRACE</DefineConstants>
    <NoWarn>1591</NoWarn>
  </PropertyGroup>

  <PropertyGroup Condition=" '$(Configuration)' == 'Staging' ">
    <ErrorReport>prompt</ErrorReport>
    <WarningLevel>3</WarningLevel>
    <DebugSymbols>true</DebugSymbols>
    <DebugType>full</DebugType>
    <Optimize>false</Optimize>
    <DefineConstants>TRACE</DefineConstants>
  </PropertyGroup>

  <PropertyGroup Condition=" '$(Configuration)' == 'Release' ">
    <AssemblyKeyContainerName>Icod</AssemblyKeyContainerName>
    <DelaySign>false</DelaySign>
    <DebugType>pdbonly</DebugType>
    <Optimize>true</Optimize>
    <ErrorReport>prompt</ErrorReport>
    <WarningLevel>4</WarningLevel>
  </PropertyGroup>
'@

    foreach ($proj in $csprojFiles) {
        $text = Get-Content -Path $proj.FullName -Raw -Encoding UTF8

        # If stanzas already present, skip
        if ($text -match "Condition=\s*'`\$\((Configuration)\)`'") {
            # crude generic check, but we'll specifically look for Staging condition
        }

        if ($text -match "Condition\s*=\s*'`\$\(Configuration\)'\s*==\s*'Staging'") {
            Write-Output "Project already contains Staging property group: $($proj.FullName)"
            continue
        }

        # backup original
        Copy-Item -Path $proj.FullName -Destination ($proj.FullName + ".bak") -Force

        # inject before closing </Project>
        if ($text -match '</Project>\s*$') {
            $newText = [regex]::Replace($text, '</Project>\s*$', $insertBlock + "`r`n</Project>")
            Set-Content -Path $proj.FullName -Value $newText -Encoding UTF8
            Write-Output "Patched: $($proj.FullName) (backup created: .bak)"
        } else {
            Write-Warning "Could not find closing </Project> in $($proj.FullName); skipping."
        }
    }
}

Write-Output "All done. Recommended actions:"
Write-Output " - Open the solution in Visual Studio to confirm 'Staging' configuration appears."
Write-Output " - Run: dotnet build `"$SolutionPath`" -c Staging"
