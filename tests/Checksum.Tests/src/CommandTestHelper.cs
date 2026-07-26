namespace Icod.CoreUtils.Checksum.Tests;

using Icod.CoreUtils.Shared.Diagnostics;

internal sealed record CommandResult(
	int ExitCode,
	byte[] OutputBytes,
	string OutputText,
	string Error
);

internal static class CommandTestHelper {

	public static async Task<CommandResult> RunAsync(
		Func<string[], CommandContext, Task<int>> command,
		string[] args,
		byte[] input,
		CancellationToken cancellationToken = default
	) {
		await using var inputStream = new MemoryStream(
			input,
			writable: false
		);
		await using var outputStream = new MemoryStream();
		using var outputText = new StringWriter();
		using var errorText = new StringWriter();
		var exitCode = await command(
			args,
			new CommandContext(
				"test",
				TextReader.Null,
				outputText,
				errorText,
				inputStream,
				outputStream,
				cancellationToken: cancellationToken
			)
		).ConfigureAwait( false );
		return new CommandResult(
			exitCode,
			outputStream.ToArray(),
			outputText.ToString(),
			errorText.ToString()
		);
	}

	public static string DecodeOutput(
		CommandResult result
	) {
		return 0 < result.OutputBytes.Length
			? System.Text.Encoding.UTF8.GetString(
				result.OutputBytes
			)
			: result.OutputText
		;
	}

}
