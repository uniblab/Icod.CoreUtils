param(
	[ValidateSet( "Debug", "Release", "Staging" )]
	[string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"
$repositoryRoot = Split-Path -Parent $PSScriptRoot
$temporarySolutionBaseName = ".g3n-coreutils-only"
$temporarySolution = Join-Path $repositoryRoot "$temporarySolutionBaseName.sln"
$temporarySolutionX = Join-Path $repositoryRoot "$temporarySolutionBaseName.slnx"

function Invoke-DotNet {
	param(
		[Parameter( Mandatory = $true )]
		[string[]]$Arguments
	)

	& dotnet @Arguments
	if ( 0 -ne $LASTEXITCODE ) {
		throw "dotnet $($Arguments -join ' ') failed with exit code $LASTEXITCODE."
	}
}

function Test-IsGeneratedPath {
	param(
		[Parameter( Mandatory = $true )]
		[string]$Path
	)

	$relativePath = [System.IO.Path]::GetRelativePath(
		$repositoryRoot,
		$Path
	)
	$segments = $relativePath.Split(
		[System.IO.Path]::DirectorySeparatorChar,
		[System.IO.Path]::AltDirectorySeparatorChar
	)
	return $segments -contains "bin" `
		-or $segments -contains "obj" `
		-or $segments -contains ".git"
}

function Get-ProjectReferencePaths {
	param(
		[Parameter( Mandatory = $true )]
		[System.IO.FileInfo]$Project
	)

	[xml]$document = Get-Content -LiteralPath $Project.FullName -Raw
	foreach ( $reference in $document.Project.ItemGroup.ProjectReference ) {
		if ( $null -eq $reference ) {
			continue
		}
		$include = [string]$reference.Include
		if ( [string]::IsNullOrWhiteSpace( $include ) ) {
			continue
		}
		if ( $include.Contains( '$(' ) ) {
			throw "G3N cannot statically validate the ProjectReference '$include' in '$($Project.FullName)'."
		}
		[System.IO.Path]::GetFullPath(
			[System.IO.Path]::Combine(
				$Project.DirectoryName,
				$include
			)
		)
	}
}

try {
	foreach ( $path in @( $temporarySolution, $temporarySolutionX ) ) {
		if ( Test-Path -LiteralPath $path ) {
			Remove-Item -LiteralPath $path -Force
		}
	}

	$projects = @(
		Get-ChildItem -LiteralPath $repositoryRoot -Recurse -File -Filter "Icod.CoreUtils*.csproj" |
			Where-Object {
				-not ( Test-IsGeneratedPath -Path $_.FullName )
			} |
			Sort-Object FullName
	)

	if ( 0 -eq $projects.Count ) {
		throw "G3N did not discover any retained Icod.CoreUtils projects."
	}

	$projectPaths = [System.Collections.Generic.HashSet[string]]::new(
		[System.StringComparer]::OrdinalIgnoreCase
	)
	foreach ( $project in $projects ) {
		[void]$projectPaths.Add(
			[System.IO.Path]::GetFullPath(
				$project.FullName
			)
		)
	}

	$foreignReferences = [System.Collections.Generic.List[string]]::new()
	foreach ( $project in $projects ) {
		foreach ( $referencePath in Get-ProjectReferencePaths -Project $project ) {
			if ( $projectPaths.Contains( $referencePath ) ) {
				continue
			}
			$foreignReferences.Add(
				"$([System.IO.Path]::GetRelativePath( $repositoryRoot, $project.FullName )) -> " +
				"$([System.IO.Path]::GetRelativePath( $repositoryRoot, $referencePath ))"
			)
		}
	}

	if ( 0 -ne $foreignReferences.Count ) {
		throw (
			"G3N found retained Coreutils projects with ProjectReference dependencies outside the " +
			"retained Coreutils set:`n  " +
			( $foreignReferences -join "`n  " )
		)
	}

	Invoke-DotNet -Arguments @(
		"new",
		"sln",
		"--format",
		"sln",
		"--name",
		$temporarySolutionBaseName,
		"--output",
		$repositoryRoot
	)

	if ( -not ( Test-Path -LiteralPath $temporarySolution ) ) {
		throw "dotnet new sln did not create the expected '$temporarySolution' file."
	}

	foreach ( $project in $projects ) {
		Invoke-DotNet -Arguments @(
			"sln",
			$temporarySolution,
			"add",
			$project.FullName
		)
	}

	Invoke-DotNet -Arguments @(
		"restore",
		$temporarySolution
	)
	Invoke-DotNet -Arguments @(
		"build",
		$temporarySolution,
		"-c",
		$Configuration,
		"--no-restore",
		"-p:ContinuousIntegrationBuild=true"
	)
	Invoke-DotNet -Arguments @(
		"test",
		$temporarySolution,
		"-c",
		$Configuration,
		"--no-build",
		"--logger",
		"trx"
	)
} finally {
	foreach ( $path in @( $temporarySolution, $temporarySolutionX ) ) {
		if ( Test-Path -LiteralPath $path ) {
			Remove-Item -LiteralPath $path -Force
		}
	}
}
