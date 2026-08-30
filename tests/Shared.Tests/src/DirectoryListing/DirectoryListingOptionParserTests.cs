namespace Icod.CoreUtils.Shared.Tests.DirectoryListing;

using Icod.CoreUtils.Shared.DirectoryListing;
using Icod.CoreUtils.Shared.Presentation;
using Icod.Terminal;
using Xunit;

/// <summary>Verifies shared ls, dir, and vdir command-line profiles.</summary>
public sealed class DirectoryListingOptionParserTests {
	/// <summary>Verifies terminal-sensitive ls defaults and fixed dir/vdir profiles.</summary>
	[Theory]
	[InlineData( DirectoryListingProfile.Ls, true, DirectoryListingFormat.Columns )]
	[InlineData( DirectoryListingProfile.Ls, false, DirectoryListingFormat.SingleColumn )]
	[InlineData( DirectoryListingProfile.Dir, false, DirectoryListingFormat.Columns )]
	[InlineData( DirectoryListingProfile.VDir, true, DirectoryListingFormat.Long )]
	public void AppliesProfileDefaults(
		DirectoryListingProfile profile,
		bool terminal,
		DirectoryListingFormat expected
	) {
		var options = DirectoryListingOptionParser.Parse(
			profile,
			Array.Empty<string>(),
			CreateSnapshot( terminal )
		);

		Assert.Equal( expected, options.Format );
		Assert.Equal( new[] { "." }, options.Operands );
		if ( profile is DirectoryListingProfile.Dir or DirectoryListingProfile.VDir ) {
			Assert.Equal( FileNameQuotingStyle.Escape, options.QuotingStyle );
		}
	}

	/// <summary>Verifies separated long-option values and bundled short options.</summary>
	[Fact]
	public void ParsesSharedListingOptions() {
		var options = DirectoryListingOptionParser.Parse(
			DirectoryListingProfile.Ls,
			new[] {
				"-laiRh",
				"--sort", "size",
				"--time", "access",
				"--quoting-style", "c",
				"--width", "120",
				"--block-size", "2K",
				"operand"
			},
			CreateSnapshot( false )
		);

		Assert.Equal( DirectoryListingFormat.Long, options.Format );
		Assert.True( options.ShowAll );
		Assert.True( options.ShowInode );
		Assert.True( options.Recursive );
		Assert.True( options.HumanReadable );
		Assert.Equal( DirectoryListingSort.Size, options.Sort );
		Assert.Equal( DirectoryListingTimeField.Access, options.TimeField );
		Assert.Equal( FileNameQuotingStyle.C, options.QuotingStyle );
		Assert.Equal( 120, options.Width );
		Assert.Equal( 2048UL, options.BlockSize );
		Assert.Equal( new[] { "operand" }, options.Operands );
	}

	/// <summary>Verifies the GNU command-line-directory dereference mode is distinct from <c>-H</c>.</summary>
	[Fact]
	public void ParsesCommandLineDirectoryDereferenceMode() {
		var options = DirectoryListingOptionParser.Parse(
			DirectoryListingProfile.Ls,
			new[] { "--dereference-command-line-symlink-to-dir" },
			CreateSnapshot( false )
		);

		Assert.Equal( DirectoryListingDereferenceMode.CommandLineDirectory, options.DereferenceMode );
	}

	/// <summary>Verifies invalid option values retain the offending argument in the diagnostic.</summary>
	[Fact]
	public void ReportsInvalidSeparatedQuotingStyle() {
		var exception = Assert.Throws<DirectoryListingUsageException>(
			() => DirectoryListingOptionParser.Parse(
				DirectoryListingProfile.Ls,
				new[] { "--quoting-style", "invalid-style" },
				CreateSnapshot( false )
			)
		);

		Assert.Contains( "invalid-style", exception.Message );
	}

	private static OutputPresentationSnapshot CreateSnapshot( bool terminal ) {
		var provider = new OutputPresentationProvider(
			new FakeTerminalControlProvider( terminal ),
			new FakeEnvironmentVariableProvider()
		);
		return provider.Observe( StandardStreamKind.StandardOutput );
	}

	private sealed class FakeTerminalControlProvider : ITerminalControlProvider {
		private readonly bool terminal;

		/// <summary>Initializes a fixed terminal observation.</summary>
		/// <param name="terminal">Whether the endpoint is attached to a terminal.</param>
		public FakeTerminalControlProvider( bool terminal ) {
			this.terminal = terminal;
		}

		/// <inheritdoc/>
		public TerminalControlResult<TerminalEndpointObservation> Observe(
			TerminalEndpoint endpoint
		) {
			ArgumentNullException.ThrowIfNull( endpoint );
			TerminalPlatformKind? platform = this.terminal
				? TerminalPlatformKind.PosixTermios
				: null;
			var capabilities = this.terminal
				? TerminalControlCapabilities.Attachment
				: TerminalControlCapabilities.None;
			return TerminalControlResult<TerminalEndpointObservation>.Available(
				new TerminalEndpointObservation(
					this.terminal,
					null,
					platform,
					capabilities
				)
			);
		}

		/// <inheritdoc/>
		public TerminalControlResult<Icod.TermInfo.TerminalSize> GetSize(
			TerminalEndpoint endpoint
		) {
			ArgumentNullException.ThrowIfNull( endpoint );
			return TerminalControlResult<Icod.TermInfo.TerminalSize>.Unsupported( "not used" );
		}

		/// <inheritdoc/>
		public TerminalControlResult<TerminalModeSnapshot> GetMode(
			TerminalEndpoint endpoint
		) {
			ArgumentNullException.ThrowIfNull( endpoint );
			return TerminalControlResult<TerminalModeSnapshot>.Unsupported( "not used" );
		}

		/// <inheritdoc/>
		public TerminalControlMutationResult SetMode(
			TerminalEndpoint endpoint,
			TerminalModeSnapshot mode,
			TerminalModeApplyTiming timing
		) {
			ArgumentNullException.ThrowIfNull( endpoint );
			ArgumentNullException.ThrowIfNull( mode );
			return TerminalControlMutationResult.Unsupported( "not used" );
		}
	}

	private sealed class FakeEnvironmentVariableProvider : IEnvironmentVariableProvider {
		/// <inheritdoc/>
		public string? GetValue( string name ) {
			return null;
		}
	}
}
