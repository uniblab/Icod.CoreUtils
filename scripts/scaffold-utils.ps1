# Run from repository root. Creates projects under <name>\src and adds them to Icod.CoreUtils.sln.
$utils = @(
    "arch","b2sum","basenc","chcon","comm","dir","dircolors","factor","id","install","link","ln",
    "logname","nice","pathchk","pinky","seq","sha224sum","sha256sum","sha384sum","sha512sum",
    "stdbuf","sync","tac","tee","tsort","uplink","users","vdir"
)

$root = (Get-Location).Path
$sln = Join-Path $root "Icod.CoreUtils.sln"
if (-not (Test-Path $sln)) {
    Write-Warning "Solution file 'Icod.CoreUtils.sln' not found in $root. The script will still create projects but won't add them to the solution."
}

function ToPascal([string]$s) {
    if ($s.Length -eq 0) { return $s }
    return $s.Substring(0,1).ToUpper() + $s.Substring(1)
}

foreach ($u in $utils) {
    $pascal = ToPascal $u
    $projDir = Join-Path $root $u
    $srcDir = Join-Path $projDir "src"
    if (-not (Test-Path $srcDir)) { New-Item -ItemType Directory -Force -Path $srcDir | Out-Null }

    $csprojPath = Join-Path $srcDir ("Icod.CoreUtils." + $pascal + ".csproj")
    $csprojContent = @"
<Project Sdk="Microsoft.NET.Sdk">
    <PropertyGroup>
        <TargetFramework>net9.0</TargetFramework>
        <Nullable>enable</Nullable>
        <ImplicitUsings>enable</ImplicitUsings>
        <GenerateAssemblyInfo>false</GenerateAssemblyInfo>
    </PropertyGroup>
</Project>
"@
    $csprojContent | Out-File -FilePath $csprojPath -Encoding utf8

    $commandPath = Join-Path $srcDir "Command.cs"
    $commandContent = @"
﻿// Ported to .NET by Timothy J. Bruce <uniblab@hotmail.com>

namespace Icod.CoreUtils.$pascal;

using System;
using System.IO;

/// <summary>
/// `$u: placeholder stub. Prints usage and supports `-?`/`--help`.
/// Replace the implementation with the actual utility behavior.
/// </summary>
public static class Command {
    public static int Run(string[] args, TextReader? stdin = null, TextWriter? stdout = null, TextWriter? stderr = null) {
        stdout ??= Console.Out;
        stderr ??= Console.Error;

        foreach (var a in args) {
            if (a == "-?" || a == "--help") {
                PrintUsage(stdout);
                return 0;
            }
        }

        // TODO: implement $u behavior here.
        PrintUsage(stdout);
        return 0;
    }

    private static void PrintUsage(TextWriter stdout) {
        stdout.WriteLine($"Usage: $u [-?]");
        stdout.WriteLine("  -?    display this help and exit");
    }
}
"@
    $commandContent | Out-File -FilePath $commandPath -Encoding utf8

    if (Test-Path $sln) {
        dotnet sln $sln add $csprojPath | Out-Null
        Write-Host "Added project: $csprojPath"
    } else {
        Write-Host "Created project files for: $u (solution not updated)"
    }
}
Write-Host "Scaffold complete."
