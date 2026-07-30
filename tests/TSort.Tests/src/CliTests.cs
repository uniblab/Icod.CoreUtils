namespace Icod.CoreUtils.TSort.Tests;

using System.Diagnostics;
using Xunit;

/// <summary>Verifies the built <c>tsort</c> process boundary.</summary>
public sealed class CliTests {
	/// <summary>Verifies standard-input ordering through the native apphost.</summary>
	/// <returns>A task representing the test.</returns>
	[Fact]
	public async Task AppHostSortsStandardInput() {
		var result = await RunAsync( "a b b c\n" );
		Assert.Equal( 0, result.ExitCode );
		Assert.Equal(
			string.Concat( "a", Environment.NewLine, "b", Environment.NewLine, "c", Environment.NewLine ),
			result.StandardOutput
		);
		Assert.Empty( result.StandardError );
	}

	/// <summary>Verifies loop diagnostics and failure status through the native apphost.</summary>
	/// <returns>A task representing the test.</returns>
	[Fact]
	public async Task AppHostReportsAndRecoversFromLoop() {
		var result = await RunAsync( "a b b a\n" );
		Assert.Equal( 1, result.ExitCode );
		Assert.Equal(
			string.Concat( "a", Environment.NewLine, "b", Environment.NewLine ),
			result.StandardOutput
		);
		Assert.Equal(
			string.Concat(
				"tsort: -: input contains a loop:", Environment.NewLine,
				"tsort: a", Environment.NewLine,
				"tsort: b", Environment.NewLine
			),
			result.StandardError
		);
	}

	private static async Task<CliResult> RunAsync( string standardInput ) {
		var executableName = OperatingSystem.IsWindows() ? "tsort.exe" : "tsort";
		var executablePath = Path.Combine( AppContext.BaseDirectory, executableName );
		Assert.True( File.Exists( executablePath ), string.Concat( "Missing command apphost: ", executablePath ) );
		using var process = new Process {
			StartInfo = new ProcessStartInfo {
				FileName = executablePath,
				UseShellExecute = false,
				CreateNoWindow = true,
				RedirectStandardInput = true,
				RedirectStandardOutput = true,
				RedirectStandardError = true
			}
		};
		Assert.True( process.Start() );
		var standardOutput = process.StandardOutput.ReadToEndAsync();
		var standardError = process.StandardError.ReadToEndAsync();
		await process.StandardInput.WriteAsync( standardInput );
		process.StandardInput.Close();
		await process.WaitForExitAsync();
		return new CliResult(
			process.ExitCode,
			await standardOutput,
			await standardError
		);
	}

	private sealed record CliResult(
		int ExitCode,
		string StandardOutput,
		string StandardError
	);
}
