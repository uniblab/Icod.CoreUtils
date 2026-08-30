namespace Icod.CoreUtils.Shared.Tests.Architecture;

using System.Xml.Linq;
using Xunit;

/// <summary>Guards the Completion Gate G3M3 repository-local Shared-library boundary.</summary>
public sealed class RepositoryBoundaryTests {
	/// <summary>Verifies that the Shared project is non-packable and consumes only the published neutral foundations expected by G3M3.</summary>
	[Fact]
	public void SharedProjectIsRepositoryLocalAndNonPackable() {
		var repositoryRoot = FindRepositoryRoot();
		var projectPath = System.IO.Path.Combine(
			repositoryRoot,
			"Shared",
			"Icod.CoreUtils.Shared.csproj"
		);
		var project = XDocument.Load(
			projectPath
		);

		Assert.Equal(
			"false",
			GetProjectProperty( project, "IsPackable" )
		);
		Assert.Equal(
			"2.1.0",
			GetPackageVersion( project, "Icod.CommandFramework" )
		);
		Assert.Equal(
			"1.1.0",
			GetPackageVersion( project, "Icod.Path" )
		);
		Assert.Equal(
			"0.3.0",
			GetPackageVersion( project, "Icod.Terminal" )
		);
		Assert.Null(
			GetPackageVersion( project, "Icod.CoreUtils.Shared" )
		);
	}

	/// <summary>Verifies that no project in the co-resident repository consumes Coreutils Shared as a package.</summary>
	[Fact]
	public void RepositoryDoesNotConsumeCoreUtilsSharedAsPackage() {
		var repositoryRoot = FindRepositoryRoot();
		var offenders = EnumerateProjectFiles( repositoryRoot )
			.Where( HasCoreUtilsSharedPackageReference )
			.Select(
				path => System.IO.Path.GetRelativePath(
					repositoryRoot,
					path
				)
			)
			.OrderBy(
				static path => path,
				StringComparer.Ordinal
			)
			.ToArray();

		Assert.Empty(
			offenders
		);
	}

	private static string FindRepositoryRoot() {
		DirectoryInfo? directory = new(
			AppContext.BaseDirectory
		);
		while ( directory is not null ) {
			if (
				File.Exists(
					System.IO.Path.Combine(
						directory.FullName,
						"Icod.CoreUtils.sln"
					)
				)
			) {
				return directory.FullName;
			}
			directory = directory.Parent;
		}
		throw new InvalidOperationException(
			"The Icod.CoreUtils repository root could not be located from the test output directory."
		);
	}

	private static IEnumerable<string> EnumerateProjectFiles(
		string repositoryRoot
	) => Directory.EnumerateFiles(
		repositoryRoot,
		"*.csproj",
		SearchOption.AllDirectories
	).Where(
		path => !IsGeneratedPath(
			repositoryRoot,
			path
		)
	);

	private static bool IsGeneratedPath(
		string repositoryRoot,
		string path
	) {
		var relativePath = System.IO.Path.GetRelativePath(
			repositoryRoot,
			path
		);
		var segments = relativePath.Split(
			new[] {
				System.IO.Path.DirectorySeparatorChar,
				System.IO.Path.AltDirectorySeparatorChar
			},
			StringSplitOptions.RemoveEmptyEntries
		);
		return segments.Any(
			static segment =>
				segment.Equals( "bin", StringComparison.OrdinalIgnoreCase )
				|| segment.Equals( "obj", StringComparison.OrdinalIgnoreCase )
				|| segment.Equals( ".git", StringComparison.OrdinalIgnoreCase )
		);
	}

	private static bool HasCoreUtilsSharedPackageReference(
		string projectPath
	) {
		var project = XDocument.Load(
			projectPath
		);
		return project.Descendants()
			.Where(
				static element => element.Name.LocalName == "PackageReference"
			)
			.Any(
				static element => string.Equals(
					GetItemInclude( element ),
					"Icod.CoreUtils.Shared",
					StringComparison.OrdinalIgnoreCase
				)
			);
	}

	private static string? GetProjectProperty(
		XDocument project,
		string propertyName
	) => project.Descendants()
		.FirstOrDefault(
			element => element.Name.LocalName == propertyName
		)
		?.Value
		.Trim();

	private static string? GetPackageVersion(
		XDocument project,
		string packageName
	) {
		var reference = project.Descendants()
			.Where(
				static element => element.Name.LocalName == "PackageReference"
			)
			.FirstOrDefault(
				element => string.Equals(
					GetItemInclude( element ),
					packageName,
					StringComparison.OrdinalIgnoreCase
				)
			);
		if ( reference is null ) {
			return null;
		}
		return reference.Attribute( "Version" )?.Value
			?? reference.Elements()
				.FirstOrDefault(
					static element => element.Name.LocalName == "Version"
				)
				?.Value
				.Trim();
	}

	private static string? GetItemInclude(
		XElement element
	) => element.Attribute( "Include" )?.Value
		?? element.Elements()
			.FirstOrDefault(
				static child => child.Name.LocalName == "Include"
			)
			?.Value
			.Trim();
}
