namespace Icod.CoreUtils.Link.Tests;

using Icod.CoreUtils.Shared.Diagnostics;
using LinkCommand = Icod.CoreUtils.Link.Command;
using Xunit;

/// <summary>Exercises GNU-compatible <c>link</c> behavior.</summary>
public sealed class CommandTests {
	/// <summary>Creates a hard link with the strict two-operand form.</summary>
	[Fact]
	public async Task CreatesHardLink() {
		using var temporary = new TemporaryDirectory();
		var source = Path.Combine( temporary.Path, "source" );
		var destination = Path.Combine( temporary.Path, "destination" );
		File.WriteAllText( source, "data" );
		var error = new StringWriter();
		var status = await LinkCommand.RunAsync( new[] { source, destination }, new CommandContext( "link", TextReader.Null, TextWriter.Null, error ) );
		Assert.Equal( CommandExitCodes.Success, status );
		Assert.Equal( "data", File.ReadAllText( destination ) );
		File.WriteAllText( source, "changed" );
		Assert.Equal( "changed", File.ReadAllText( destination ) );
	}

	/// <summary>Rejects missing and extra operands.</summary>
	[Fact]
	public async Task EnforcesExactlyTwoOperands() {
		var missingError = new StringWriter();
		var missing = await LinkCommand.RunAsync( new[] { "one" }, new CommandContext( "link", TextReader.Null, TextWriter.Null, missingError ) );
		var extraError = new StringWriter();
		var extra = await LinkCommand.RunAsync( new[] { "one", "two", "three" }, new CommandContext( "link", TextReader.Null, TextWriter.Null, extraError ) );
		Assert.Equal( CommandExitCodes.Failure, missing );
		Assert.Equal( CommandExitCodes.Failure, extra );
		Assert.Contains( "missing operand", missingError.ToString() );
		Assert.Contains( "extra operand", extraError.ToString() );
	}

	private sealed class TemporaryDirectory : IDisposable {
		public TemporaryDirectory() { Path = System.IO.Path.Combine( System.IO.Path.GetTempPath(), string.Concat( "Icod-Link-", Guid.NewGuid().ToString( "N" ) ) ); Directory.CreateDirectory( Path ); }
		public string Path { get; }
		public void Dispose() { try { Directory.Delete( Path, true ); } catch ( IOException ) { } catch ( UnauthorizedAccessException ) { } }
	}
}
