using System.ComponentModel;
using Icod.CommandFramework.CommandLine;
using Xunit;

namespace Icod.CoreUtils.HostName.Tests;

using Tool = Icod.CoreUtils.HostName.Command;

/// <summary>
/// Verifies the GNU Coreutils 9.11 <c>hostname</c> command surface.
/// </summary>
public sealed class HostNameCommandTests {
	/// <summary>
	/// Verifies that the zero-operand form prints the platform host name.
	/// </summary>
	[Fact]
	public async Task DefaultPrintsPlatformHostName() {
		var platform = new RecordingPlatform {
			HostName = "test-host"
		};
		var output = new StringWriter();
		var status = await RunAsync(
			Array.Empty<string>(),
			platform,
			stdout: output
		);

		Assert.Equal( 0, status );
		Assert.Equal(
			string.Concat( "test-host", Environment.NewLine ),
			output.ToString()
		);
		Assert.Equal( 1, platform.GetCount );
		Assert.Null( platform.SetValue );
	}

	/// <summary>
	/// Verifies that one operand requests active-hostname mutation.
	/// </summary>
	[Fact]
	public async Task OneOperandSetsPlatformHostName() {
		var platform = new RecordingPlatform();
		var status = await RunAsync(
			new[] { "newname" },
			platform
		);

		Assert.Equal( 0, status );
		Assert.Equal( "newname", platform.SetValue );
		Assert.Equal( 0, platform.GetCount );
	}

	/// <summary>
	/// Verifies that <c>--</c> permits a host name beginning with a hyphen.
	/// </summary>
	[Fact]
	public async Task EndOfOptionsAllowsHyphenLeadingHostName() {
		var platform = new RecordingPlatform();
		var status = await RunAsync(
			new[] { "--", "-newname" },
			platform
		);

		Assert.Equal( 0, status );
		Assert.Equal( "-newname", platform.SetValue );
	}

	/// <summary>
	/// Verifies that more than one operand is rejected before mutation.
	/// </summary>
	[Fact]
	public async Task ExtraOperandIsRejectedBeforeMutation() {
		var platform = new RecordingPlatform();
		var error = new StringWriter();
		var status = await RunAsync(
			new[] { "one", "two" },
			platform,
			stderr: error
		);

		Assert.Equal( 1, status );
		Assert.Null( platform.SetValue );
		Assert.Contains(
			"extra operand",
			error.ToString(),
			StringComparison.Ordinal
		);
	}

	/// <summary>
	/// Verifies that the former Inetutils-style query options are not accepted by the Coreutils profile.
	/// </summary>
	/// <param name="option">The Inetutils-style option spelling to reject.</param>
	[Theory]
	[InlineData( "-s" )]
	[InlineData( "--short" )]
	[InlineData( "-f" )]
	[InlineData( "--fqdn" )]
	[InlineData( "-F" )]
	[InlineData( "--file" )]
	[InlineData( "-y" )]
	[InlineData( "--nis" )]
	public async Task InetutilsOptionsAreRejected( string option ) {
		ArgumentException.ThrowIfNullOrEmpty( option );
		var platform = new RecordingPlatform();
		var error = new StringWriter();
		var status = await RunAsync(
			new[] { option },
			platform,
			stderr: error
		);

		Assert.Equal( 1, status );
		Assert.NotEmpty( error.ToString() );
		Assert.Equal( 0, platform.GetCount );
		Assert.Null( platform.SetValue );
	}

	/// <summary>
	/// Verifies that a native mutation failure becomes a controlled diagnostic.
	/// </summary>
	[Fact]
	public async Task PlatformSetFailureIsControlled() {
		var platform = new RecordingPlatform {
			SetException = new Win32Exception(
				1,
				"operation not permitted"
			)
		};
		var error = new StringWriter();
		var status = await RunAsync(
			new[] { "newname" },
			platform,
			stderr: error
		);

		Assert.Equal( 1, status );
		Assert.Contains(
			"cannot set name to 'newname'",
			error.ToString(),
			StringComparison.Ordinal
		);
		Assert.Contains(
			"operation not permitted",
			error.ToString(),
			StringComparison.OrdinalIgnoreCase
		);
	}

	/// <summary>
	/// Verifies that unsupported active-hostname mutation is reported without pretending success.
	/// </summary>
	[Fact]
	public async Task UnsupportedSetIsControlled() {
		var platform = new RecordingPlatform {
			SetException = new PlatformNotSupportedException(
				"setting the active host name is unsupported on this host"
			)
		};
		var error = new StringWriter();
		var status = await RunAsync(
			new[] { "newname" },
			platform,
			stderr: error
		);

		Assert.Equal( 1, status );
		Assert.Contains(
			"unsupported",
			error.ToString(),
			StringComparison.OrdinalIgnoreCase
		);
	}

	/// <summary>
	/// Verifies the common Coreutils help and version options without touching the platform.
	/// </summary>
	[Fact]
	public async Task HelpAndVersionWorkWithoutPlatformAccess() {
		var platform = new RecordingPlatform();
		var help = new StringWriter();
		var version = new StringWriter();

		Assert.Equal(
			0,
			await RunAsync(
				new[] { "--help" },
				platform,
				stdout: help
			)
		);
		Assert.Equal(
			0,
			await RunAsync(
				new[] { "--version" },
				platform,
				stdout: version
			)
		);
		Assert.Contains(
			"Usage: hostname [NAME]",
			help.ToString(),
			StringComparison.Ordinal
		);
		Assert.Contains(
			"hostname (Icod.CoreUtils) 1.0",
			version.ToString(),
			StringComparison.Ordinal
		);
		Assert.Equal( 0, platform.GetCount );
		Assert.Null( platform.SetValue );
	}

	/// <summary>
	/// Verifies that cancellation is observed before platform access.
	/// </summary>
	[Fact]
	public async Task CancellationReturns130BeforePlatformAccess() {
		using var cancellation = new CancellationTokenSource();
		cancellation.Cancel();
		var platform = new RecordingPlatform();
		var status = await RunAsync(
			Array.Empty<string>(),
			platform,
			cancellation.Token
		);

		Assert.Equal( 130, status );
		Assert.Equal( 0, platform.GetCount );
		Assert.Null( platform.SetValue );
	}

	private static Task<int> RunAsync(
		string[] args,
		IHostNamePlatform platform,
		CancellationToken cancellationToken = default,
		TextWriter? stdout = null,
		TextWriter? stderr = null
	) {
		ArgumentNullException.ThrowIfNull( args );
		ArgumentNullException.ThrowIfNull( platform );
		return Tool.RunAsync(
			args,
			new CommandContext(
				"hostname",
				new StringReader( string.Empty ),
				stdout ?? new StringWriter(),
				stderr ?? new StringWriter(),
				cancellationToken: cancellationToken
			),
			platform
		);
	}

	private sealed class RecordingPlatform : IHostNamePlatform {
		/// <summary>Gets the host name returned by the fake platform.</summary>
		public string HostName {
			get;
			init;
		} = "host";

		/// <summary>Gets the number of host-name observations requested.</summary>
		public int GetCount {
			get;
			private set;
		}

		/// <summary>Gets the exception to throw from a mutation request, when any.</summary>
		public Exception? SetException {
			get;
			init;
		}

		/// <summary>Gets the last host name supplied to the fake mutation boundary.</summary>
		public string? SetValue {
			get;
			private set;
		}

		/// <inheritdoc />
		public string GetHostName() {
			GetCount++;
			return HostName;
		}

		/// <inheritdoc />
		public void SetHostName( string hostName ) {
			ArgumentException.ThrowIfNullOrEmpty( hostName );
			if ( null != SetException ) {
				throw SetException;
			}
			SetValue = hostName;
		}
	}
}
