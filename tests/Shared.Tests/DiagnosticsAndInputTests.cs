namespace Icod.CoreUtils.Shared.Tests;

using System.Text;
using Icod.CoreUtils.Shared.Diagnostics;
using Icod.CoreUtils.Shared.IO;
using Xunit;

public sealed class DiagnosticsAndInputTests {

	[Fact]
	public async Task DiagnosticWriterPrefixesMessages() {
		var error = new StringWriter();
		var writer = new DiagnosticWriter(
			"tool",
			error
		);

		await writer.ErrorAsync( "failure" );
		await writer.WarningAsync( "careful" );

		var text = error.ToString();
		Assert.Contains( "tool: failure", text );
		Assert.Contains( "tool: warning: careful", text );
	}

	[Fact]
	public void InputOperandRecognizesStandardInput() {
		var operand = InputOperand.Create(
			null
		);

		Assert.True( operand.IsStandardInput );
		Assert.Equal( "standard input", operand.DisplayName );
	}

	[Fact]
	public async Task InputSourceReadsInjectedStandardInputWithoutOwningIt() {
		var text = new StringReader(
			"payload"
		);
		var context = new CommandContext(
			"tool",
			text,
			new StringWriter(),
			new StringWriter()
		);
		await using var source = InputSource.OpenText(
			InputOperand.Create( "-" ),
			context
		);

		Assert.Same( text, source.TextReader );
		Assert.Equal( "payload", await source.TextReader!.ReadToEndAsync() );
	}

	[Fact]
	public async Task InputSourceReadsFileAsynchronously() {
		var path = System.IO.Path.GetTempFileName();
		try {
			await File.WriteAllTextAsync(
				path,
				"payload",
				Encoding.UTF8
			);
			var context = new CommandContext(
				"tool",
				new StringReader( string.Empty ),
				new StringWriter(),
				new StringWriter()
			);
			await using var source = InputSource.OpenText(
				InputOperand.Create( path ),
				context
			);

			Assert.Equal( "payload", await source.TextReader!.ReadToEndAsync() );
		} finally {
			File.Delete( path );
		}
	}

}
