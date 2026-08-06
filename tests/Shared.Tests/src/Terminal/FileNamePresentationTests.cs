namespace Icod.CoreUtils.Shared.Tests.Terminal;

using Icod.CoreUtils.Shared.Terminal;
using Xunit;

/// <summary>
/// Verifies GNU-style filename quoting and control-character presentation.
/// </summary>
public sealed class FileNamePresentationTests {
	/// <summary>
	/// Verifies terminal and redirected default quoting styles.
	/// </summary>
	[Theory]
	[InlineData( true, FileNameQuotingStyle.ShellEscape )]
	[InlineData( false, FileNameQuotingStyle.Literal )]
	public void ResolvesTerminalSensitiveDefaultStyle(
		bool terminal,
		FileNameQuotingStyle expected
	) {
		var policy = FileNamePresentationPolicy.ResolveDefault(
			CreateSnapshot( terminal, null )
		);

		Assert.Equal( expected, policy.QuotingStyle );
	}

	/// <summary>
	/// Verifies that the environment style overrides the terminal-sensitive
	/// default.
	/// </summary>
	[Fact]
	public void EnvironmentQuotingStyleOverridesDefault() {
		var policy = FileNamePresentationPolicy.ResolveDefault(
			CreateSnapshot( true, "c" )
		);

		Assert.Equal( FileNameQuotingStyle.C, policy.QuotingStyle );
		Assert.Equal(
			ControlCharacterPresentation.Escape,
			policy.ControlCharacters
		);
	}

	/// <summary>
	/// Verifies that an unrecognized environment style remains available to the
	/// command for diagnostics while the convenience resolver uses its normal
	/// terminal-sensitive fallback.
	/// </summary>
	[Fact]
	public void InvalidEnvironmentStyleUsesFallbackWithoutLosingRawValue() {
		var snapshot = CreateSnapshot( true, "not-a-style" );

		var policy = FileNamePresentationPolicy.ResolveDefault( snapshot );

		Assert.Equal( "not-a-style", snapshot.Environment.QuotingStyle );
		Assert.Equal( FileNameQuotingStyle.ShellEscape, policy.QuotingStyle );
	}

	/// <summary>
	/// Verifies shell quoting of blanks and embedded single quotes.
	/// </summary>
	[Fact]
	public void ShellStyleProducesPasteableSingleQuoteFragments() {
		var policy = new FileNamePresentationPolicy(
			FileNameQuotingStyle.Shell,
			ControlCharacterPresentation.Preserve
		);

		var result = FileNamePresenter.Present(
			"a b'c",
			policy
		);

		Assert.Equal( "'a b'\\''c'", result );
	}

	/// <summary>
	/// Verifies shell-escape presentation of line feeds and tabs.
	/// </summary>
	[Fact]
	public void ShellEscapeUsesDollarQuotedControlSequences() {
		var policy = new FileNamePresentationPolicy(
			FileNameQuotingStyle.ShellEscape,
			ControlCharacterPresentation.Escape
		);

		var result = FileNamePresenter.Present(
			"a\nb\t",
			policy
		);

		Assert.Equal( "$'a\\nb\\t'", result );
	}

	/// <summary>
	/// Verifies C and escape styles.
	/// </summary>
	[Theory]
	[InlineData( FileNameQuotingStyle.C, "\"a\\nb\"" )]
	[InlineData( FileNameQuotingStyle.Escape, "a\\nb" )]
	public void CStylesEscapeControls(
		FileNameQuotingStyle style,
		string expected
	) {
		var policy = new FileNamePresentationPolicy(
			style,
			ControlCharacterPresentation.Escape
		);

		Assert.Equal(
			expected,
			FileNamePresenter.Present( "a\nb", policy )
		);
	}

	/// <summary>
	/// Verifies question-mark replacement for terminal literal output.
	/// </summary>
	[Fact]
	public void LiteralPolicyCanReplaceControls() {
		var policy = new FileNamePresentationPolicy(
			FileNameQuotingStyle.Literal,
			ControlCharacterPresentation.ReplaceWithQuestionMark
		);

		Assert.Equal(
			"a?b",
			FileNamePresenter.Present( "a\u0001b", policy )
		);
	}

	/// <summary>
	/// Verifies that valid supplementary Unicode remains intact while an
	/// unpaired surrogate receives a deterministic escape.
	/// </summary>
	[Fact]
	public void UnicodePairsRemainIntactAndInvalidSurrogatesEscape() {
		var policy = new FileNamePresentationPolicy(
			FileNameQuotingStyle.C,
			ControlCharacterPresentation.Escape
		);

		Assert.Equal(
			"\"face-😀\"",
			FileNamePresenter.Present( "face-😀", policy )
		);
		Assert.Equal(
			"\"bad-\\uD800\"",
			FileNamePresenter.Present( "bad-\uD800", policy )
		);
	}

	/// <summary>
	/// Verifies early validation of undefined policy values.
	/// </summary>
	[Fact]
	public void RejectsUndefinedPolicyValues() {
		Assert.Throws<ArgumentOutOfRangeException>(
			() => new FileNamePresentationPolicy(
				(FileNameQuotingStyle)int.MaxValue,
				ControlCharacterPresentation.Preserve
			)
		);
		Assert.Throws<ArgumentOutOfRangeException>(
			() => new FileNamePresentationPolicy(
				FileNameQuotingStyle.Literal,
				(ControlCharacterPresentation)int.MaxValue
			)
		);
		Assert.Throws<ArgumentException>(
			() => new FileNamePresentationPolicy(
				FileNameQuotingStyle.Literal,
				ControlCharacterPresentation.Escape
			)
		);
	}

	/// <summary>
	/// Verifies parsing of every documented quoting-style name.
	/// </summary>
	[Theory]
	[InlineData( "literal", FileNameQuotingStyle.Literal )]
	[InlineData( "shell", FileNameQuotingStyle.Shell )]
	[InlineData( "shell-always", FileNameQuotingStyle.ShellAlways )]
	[InlineData( "shell-escape", FileNameQuotingStyle.ShellEscape )]
	[InlineData( "shell-escape-always", FileNameQuotingStyle.ShellEscapeAlways )]
	[InlineData( "c", FileNameQuotingStyle.C )]
	[InlineData( "c-maybe", FileNameQuotingStyle.CMaybe )]
	[InlineData( "escape", FileNameQuotingStyle.Escape )]
	[InlineData( "clocale", FileNameQuotingStyle.CLocale )]
	[InlineData( "locale", FileNameQuotingStyle.Locale )]
	public void ParsesDocumentedQuotingStyles(
		string value,
		FileNameQuotingStyle expected
	) {
		var parsed = FileNamePresentationPolicy.TryParseQuotingStyle(
			value,
			out var actual
		);

		Assert.True( parsed );
		Assert.Equal( expected, actual );
	}

	private static TerminalPresentationSnapshot CreateSnapshot(
		bool terminal,
		string? quotingStyle
	) {
		var provider = new TerminalPresentationProvider(
			new FakeTerminalDeviceProvider(
				terminal
					? TerminalDeviceObservation.Attached(
						new TerminalDimensions( 80, 24 )
					)
					: TerminalDeviceObservation.Redirected()
			),
			new FakeEnvironmentVariableProvider( quotingStyle )
		);
		return provider.Observe( TerminalStreamKind.StandardOutput );
	}

	private sealed class FakeTerminalDeviceProvider : ITerminalDeviceProvider {
		private readonly TerminalDeviceObservation observation;

		/// <summary>Initializes the fixed terminal-device provider.</summary>
		/// <param name="observation">The observation returned by the provider.</param>
		public FakeTerminalDeviceProvider(
			TerminalDeviceObservation observation
		) {
			this.observation = observation;
		}

		/// <inheritdoc/>
		public TerminalDeviceObservation Observe(
			TerminalStreamKind stream
		) {
			return this.observation;
		}
	}

	private sealed class FakeEnvironmentVariableProvider : IEnvironmentVariableProvider {
		private readonly string? quotingStyle;

		/// <summary>Initializes the environment provider.</summary>
		/// <param name="quotingStyle">The quoting-style value.</param>
		public FakeEnvironmentVariableProvider(
			string? quotingStyle
		) {
			this.quotingStyle = quotingStyle;
		}

		/// <inheritdoc/>
		public string? GetValue(
			string name
		) {
			return "QUOTING_STYLE" == name
				? this.quotingStyle
				: null;
		}
	}
}
