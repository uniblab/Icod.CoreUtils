namespace Icod.CoreUtils.Shared.Tests;

using System.Text;
using Icod.CoreUtils.Shared.Processes;
using Xunit;

public sealed class ProcessRunnerTests {

	[Fact]
	public async Task PreservesExactArguments() {
		var values = new string[ 4 ] {
			string.Empty,
			"plain",
			"contains spaces",
			"quote\"and\\slash"
		};
		var options = CreateHostOptions(
			"args"
		);
		foreach ( var value in values ) {
			options.Arguments.Add(
				value
			);
		}
		options.CaptureStandardOutput = true;

		var result = await ProcessRunner.RunAsync(
			options
		);

		Assert.False( result.WasCanceled );
		Assert.Equal( 0, result.ExitCode );
		var actual = result.StandardOutput!
			.Split(
				new string[ 2 ] { "\r\n", "\n" },
				StringSplitOptions.RemoveEmptyEntries
			)
			.Select(
				line => Encoding.UTF8.GetString(
					Convert.FromBase64String(
						line.Substring( 2 )
					)
				)
			)
			.ToArray();
		Assert.Equal( values, actual );
	}

	[Fact]
	public async Task ForwardsStandardInputAndCapturesOutput() {
		var payload = Encoding.UTF8.GetBytes(
			new string( 'x', 200000 )
		);
		await using var input = new MemoryStream(
			payload
		);
		var options = CreateHostOptions(
			"copy"
		);
		options.StandardInput = input;
		options.CaptureStandardOutput = true;

		var result = await ProcessRunner.RunAsync(
			options
		);

		Assert.Equal( 0, result.ExitCode );
		Assert.Equal(
			payload,
			Encoding.UTF8.GetBytes(
				result.StandardOutput!
			)
		);
	}

	[Fact]
	public async Task CapturesStandardErrorAndExitCode() {
		var errorOptions = CreateHostOptions(
			"stderr",
			"problem"
		);
		errorOptions.CaptureStandardError = true;
		var errorResult = await ProcessRunner.RunAsync(
			errorOptions
		);

		Assert.Equal( "problem", errorResult.StandardError );

		var exitOptions = CreateHostOptions(
			"exit",
			"17"
		);
		var exitResult = await ProcessRunner.RunAsync(
			exitOptions
		);
		Assert.Equal( 17, exitResult.ExitCode );
	}

	[Fact]
	public async Task DrainsLargeStandardOutputAndErrorConcurrently() {
		var options = CreateHostOptions(
			"dual",
			"5000"
		);
		options.CaptureStandardOutput = true;
		options.CaptureStandardError = true;

		var result = await ProcessRunner.RunAsync(
			options
		);

		Assert.Equal( 0, result.ExitCode );
		Assert.NotNull( result.StandardOutput );
		Assert.NotNull( result.StandardError );
		Assert.Contains( "out-4999", result.StandardOutput! );
		Assert.Contains( "err-4999", result.StandardError! );
	}

	[Fact]
	public async Task PreCanceledTokenDoesNotStartChild() {
		using var cancellation = new CancellationTokenSource();
		cancellation.Cancel();
		var options = new ProcessRunOptions(
			"an-executable-that-must-not-be-started"
		);

		var result = await ProcessRunner.RunAsync(
			options,
			cancellation.Token
		);

		Assert.True( result.WasCanceled );
		Assert.Null( result.ExitCode );
	}

	[Fact]
	public async Task CancellationTerminatesChild() {
		using var cancellation = new CancellationTokenSource(
			TimeSpan.FromMilliseconds( 250 )
		);
		var options = CreateHostOptions(
			"sleep",
			"30000"
		);

		var result = await ProcessRunner.RunAsync(
			options,
			cancellation.Token
		);

		Assert.True( result.WasCanceled );
		Assert.NotNull( result.ExitCode );
	}

	private static ProcessRunOptions CreateHostOptions(
		params string[] arguments
	) {
		var output = new ProcessRunOptions(
			"dotnet"
		);
		output.Arguments.Add(
			FindProcessTestHost()
		);
		foreach ( var argument in arguments ) {
			output.Arguments.Add(
				argument
			);
		}
		return output;
	}

	private static string FindProcessTestHost() {
		var testOutputDirectory = new DirectoryInfo(
			AppContext.BaseDirectory
		);
		var targetFramework = testOutputDirectory.Name;
		var configuration = testOutputDirectory.Parent?.Name;

		DirectoryInfo? directory = testOutputDirectory;
		while ( null != directory ) {
			var hostProject = Path.Combine(
				directory.FullName,
				"tests",
				"ProcessTestHost",
				"Icod.CoreUtils.ProcessTestHost.csproj"
			);
			if ( File.Exists( hostProject ) ) {
				var hostDirectory = Path.Combine(
					directory.FullName,
					"tests",
					"ProcessTestHost",
					"bin"
				);

				if (
					!string.IsNullOrEmpty( configuration )
					&& !string.IsNullOrEmpty( targetFramework )
				) {
					var configuredHost = Path.Combine(
						hostDirectory,
						configuration,
						targetFramework,
						"Icod.CoreUtils.ProcessTestHost.dll"
					);
					if ( File.Exists( configuredHost ) ) {
						return configuredHost;
					}
				}

				if ( Directory.Exists( hostDirectory ) ) {
					var match = Directory
						.EnumerateFiles(
							hostDirectory,
							"Icod.CoreUtils.ProcessTestHost.dll",
							SearchOption.AllDirectories
						)
						.FirstOrDefault(
							path => !path.Contains(
								string.Concat(
									Path.DirectorySeparatorChar,
									"ref",
									Path.DirectorySeparatorChar
								),
								StringComparison.Ordinal
							)
						);
					if ( null != match ) {
						return match;
					}
				}

				throw new FileNotFoundException(
					"The ProcessTestHost output was not found. Build the ProcessTestHost project before running Shared.Tests.",
					hostDirectory
				);
			}

			directory = directory.Parent;
		}

		throw new FileNotFoundException(
			"The ProcessTestHost project directory was not found from the test output directory."
		);
	}

}
