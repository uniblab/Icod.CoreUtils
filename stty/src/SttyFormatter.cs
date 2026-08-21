namespace Icod.CoreUtils.Stty;

using System.Globalization;
using System.Text;
using Icod.CommandFramework.Terminal;

/// <summary>Formats human-readable and machine-readable terminal state.</summary>
public static class SttyFormatter {
	/// <summary>Formats the ordinary compact state report.</summary>
	/// <param name="mode">The complete mode snapshot.</param>
	/// <returns>The formatted report.</returns>
	public static string FormatDefault( TerminalModeSnapshot mode ) {
		ArgumentNullException.ThrowIfNull( mode );
		if ( TerminalPlatformKind.WindowsConsole == mode.Platform ) {
			return FormatWindows( mode, false );
		}
		var builder = new StringBuilder();
		builder.Append( FormatSpeed( mode ) );
		if ( mode.LineDiscipline.HasValue ) {
			builder.Append( "; line = " );
			builder.Append( mode.LineDiscipline.Value.ToString( CultureInfo.InvariantCulture ) );
		}
		builder.AppendLine( ";" );
		AppendFlagGroup( builder, mode, new[] { "parenb", "parodd", "cs8", "hupcl", "cstopb", "cread", "clocal", "crtscts" } );
		AppendFlagGroup( builder, mode, new[] { "ignbrk", "brkint", "ignpar", "parmrk", "inpck", "istrip", "inlcr", "igncr", "icrnl", "ixon", "ixoff", "iutf8" } );
		AppendFlagGroup( builder, mode, new[] { "opost", "onlcr", "ocrnl", "onocr", "onlret" } );
		AppendFlagGroup( builder, mode, new[] { "isig", "icanon", "iexten", "echo", "echoe", "echok", "echonl", "noflsh", "tostop", "echoctl" } );
		return builder.ToString();
	}

	/// <summary>Formats all reportable terminal state.</summary>
	/// <param name="mode">The complete mode snapshot.</param>
	/// <returns>The formatted report.</returns>
	public static string FormatAll( TerminalModeSnapshot mode ) {
		ArgumentNullException.ThrowIfNull( mode );
		if ( TerminalPlatformKind.WindowsConsole == mode.Platform ) {
			return FormatWindows( mode, true );
		}
		var builder = new StringBuilder();
		builder.Append( FormatSpeed( mode ) );
		if ( mode.LineDiscipline.HasValue ) {
			builder.Append( "; line = " );
			builder.Append( mode.LineDiscipline.Value.ToString( CultureInfo.InvariantCulture ) );
		}
		builder.AppendLine( ";" );
		AppendControls( builder, mode );
		builder.Append( FormatDefaultFlagsOnly( mode ) );
		return builder.ToString();
	}

	/// <summary>Formats the reporting-only <c>speed</c> operand.</summary>
	/// <param name="mode">The complete mode snapshot.</param>
	/// <returns>The speed text without a trailing newline.</returns>
	public static string FormatSpeed( TerminalModeSnapshot mode ) {
		ArgumentNullException.ThrowIfNull( mode );
		if ( TerminalPlatformKind.PosixTermios != mode.Platform ) {
			return "speed is unsupported";
		}
		var input = SpeedText( mode.InputSpeed!.Value );
		var output = SpeedText( mode.OutputSpeed!.Value );
		return input == output
			? string.Concat( "speed ", output, " baud" )
			: string.Concat( "ispeed ", input, " baud; ospeed ", output, " baud" );
	}

	private static string FormatDefaultFlagsOnly( TerminalModeSnapshot mode ) {
		var builder = new StringBuilder();
		AppendFlagGroup( builder, mode, new[] { "parenb", "parodd", "cs5", "cs6", "cs7", "cs8", "hupcl", "cstopb", "cread", "clocal", "crtscts" } );
		AppendFlagGroup( builder, mode, new[] { "ignbrk", "brkint", "ignpar", "parmrk", "inpck", "istrip", "inlcr", "igncr", "icrnl", "iuclc", "ixon", "ixany", "ixoff", "imaxbel", "iutf8" } );
		AppendFlagGroup( builder, mode, new[] { "opost", "olcuc", "onlcr", "ocrnl", "onocr", "onlret", "ofill", "ofdel" } );
		AppendFlagGroup( builder, mode, new[] { "isig", "icanon", "iexten", "echo", "echoe", "echok", "echonl", "noflsh", "tostop", "echoctl", "echoprt", "echoke", "flusho", "pendin" } );
		return builder.ToString();
	}

	private static void AppendControls( StringBuilder builder, TerminalModeSnapshot mode ) {
		var names = 64 == mode.NativeFlagWidth
			? new[] { "eof", "eol", "eol2", "erase", "werase", "kill", "rprnt", null, "intr", "quit", "susp", "dsusp", "start", "stop", "lnext", "discard", "min", "time", "status" }
			: new[] { "intr", "quit", "erase", "kill", "eof", "time", "min", null, "start", "stop", "susp", "eol", "rprnt", "discard", "werase", "lnext", "eol2" };
		var first = true;
		for ( var index = 0; index < names.Length && index < mode.ControlCharacters.Count; ++index ) {
			if ( names[ index ] is null ) {
				continue;
			}
			if ( !first ) {
				builder.Append( "; " );
			}
			first = false;
			builder.Append( names[ index ] );
			builder.Append( " = " );
			if ( names[ index ] is "min" or "time" ) {
				builder.Append( mode.ControlCharacters[ index ].ToString( CultureInfo.InvariantCulture ) );
			} else {
				builder.Append( TerminalControlCharacterFormatter.Format(
					mode.ControlCharacters[ index ],
					mode.DisabledControlCharacter
				) );
			}
		}
		builder.AppendLine( ";" );
	}

	private static void AppendFlagGroup(
		StringBuilder builder,
		TerminalModeSnapshot mode,
		IEnumerable<string> names
	) {
		var first = true;
		foreach ( var name in names ) {
			if ( !first ) {
				builder.Append( ' ' );
			}
			first = false;
			if ( !IsFlagSet( mode, name ) ) {
				builder.Append( '-' );
			}
			builder.Append( name );
		}
		builder.AppendLine();
	}

	private static bool IsFlagSet( TerminalModeSnapshot mode, string name ) {
		var mac = 64 == mode.NativeFlagWidth;
		if ( name is "cs5" or "cs6" or "cs7" or "cs8" ) {
			var sizeMask = mac ? 0x300UL : 0x30UL;
			var value = mode.ControlFlags & sizeMask;
			var expected = mac
				? name switch { "cs5" => 0UL, "cs6" => 0x100UL, "cs7" => 0x200UL, _ => 0x300UL }
				: name switch { "cs5" => 0UL, "cs6" => 0x10UL, "cs7" => 0x20UL, _ => 0x30UL };
			return value == expected;
		}
		var mask = InputMask( name );
		if ( 0 != mask ) {
			return 0 != ( mode.InputFlags & mask );
		}
		mask = OutputMask( name );
		if ( 0 != mask ) {
			return 0 != ( mode.OutputFlags & mask );
		}
		mask = ControlMask( name, mac );
		if ( 0 != mask ) {
			return 0 != ( mode.ControlFlags & mask );
		}
		mask = LocalMask( name, mac );
		return 0 != mask && 0 != ( mode.LocalFlags & mask );
	}

	private static string FormatWindows( TerminalModeSnapshot mode, bool all ) {
		var direction = TerminalConsoleDirection.Input == mode.ConsoleDirection ? "input" : "output";
		var native = mode.ConsoleMode!.Value;
		var builder = new StringBuilder();
		builder.Append( "console " );
		builder.Append( direction );
		builder.Append( " mode = 0x" );
		builder.Append( native.ToString( "x8", CultureInfo.InvariantCulture ) );
		builder.AppendLine( ";" );
		if ( TerminalConsoleDirection.Input == mode.ConsoleDirection ) {
			AppendWindowsFlag( builder, "isig", native, 0x1 );
			AppendWindowsFlag( builder, "icanon", native, 0x2 );
			AppendWindowsFlag( builder, "echo", native, 0x4 );
			if ( all ) {
				AppendWindowsFlag( builder, "window-input", native, 0x8 );
				AppendWindowsFlag( builder, "mouse-input", native, 0x10 );
				AppendWindowsFlag( builder, "quick-edit", native, 0x40 );
				AppendWindowsFlag( builder, "virtual-terminal-input", native, 0x200 );
			}
		} else {
			AppendWindowsFlag( builder, "opost", native, 0x1 );
			AppendWindowsFlag( builder, "onlcr", native, 0x2 );
			if ( all ) {
				AppendWindowsFlag( builder, "virtual-terminal-output", native, 0x4 );
			}
		}
		builder.AppendLine();
		return builder.ToString();
	}

	private static void AppendWindowsFlag( StringBuilder builder, string name, uint mode, uint mask ) {
		if ( 0 == ( mode & mask ) ) {
			builder.Append( '-' );
		}
		builder.Append( name );
		builder.Append( ' ' );
	}

	private static string SpeedText( TerminalSpeed speed ) {
		return speed.BaudRate?.ToString( CultureInfo.InvariantCulture )
			?? string.Concat( "native-0x", speed.NativeCode.ToString( "x", CultureInfo.InvariantCulture ) );
	}

	private static ulong InputMask( string name ) => name switch {
		"ignbrk" => 0x1, "brkint" => 0x2, "ignpar" => 0x4, "parmrk" => 0x8,
		"inpck" => 0x10, "istrip" => 0x20, "inlcr" => 0x40, "igncr" => 0x80,
		"icrnl" => 0x100, "iuclc" => 0x200, "ixon" => 0x400, "ixany" => 0x800,
		"ixoff" => 0x1000, "imaxbel" => 0x2000, "iutf8" => 0x4000, _ => 0
	};

	private static ulong OutputMask( string name ) => name switch {
		"opost" => 0x1, "olcuc" => 0x2, "onlcr" => 0x4, "ocrnl" => 0x8,
		"onocr" => 0x10, "onlret" => 0x20, "ofill" => 0x40, "ofdel" => 0x80, _ => 0
	};

	private static ulong ControlMask( string name, bool mac ) {
		if ( mac ) {
			return name switch {
				"cstopb" => 0x400, "cread" => 0x800, "parenb" => 0x1000, "parodd" => 0x2000,
				"hupcl" => 0x4000, "clocal" => 0x8000, "crtscts" => 0x30000, _ => 0
			};
		}
		return name switch {
			"cstopb" => 0x40, "cread" => 0x80, "parenb" => 0x100, "parodd" => 0x200,
			"hupcl" => 0x400, "clocal" => 0x800, "crtscts" => 0x80000000, _ => 0
		};
	}

	private static ulong LocalMask( string name, bool mac ) {
		if ( mac ) {
			return name switch {
				"echoke" => 0x1, "echoe" => 0x2, "echok" => 0x4, "echo" => 0x8,
				"echonl" => 0x10, "echoprt" => 0x20, "echoctl" => 0x40, "isig" => 0x80,
				"icanon" => 0x100, "iexten" => 0x400, "tostop" => 0x400000,
				"flusho" => 0x800000, "pendin" => 0x20000000, "noflsh" => 0x80000000, _ => 0
			};
		}
		return name switch {
			"isig" => 0x1, "icanon" => 0x2, "echo" => 0x8, "echoe" => 0x10,
			"echok" => 0x20, "echonl" => 0x40, "noflsh" => 0x80, "tostop" => 0x100,
			"echoctl" => 0x200, "echoprt" => 0x400, "echoke" => 0x800,
			"flusho" => 0x1000, "pendin" => 0x4000, "iexten" => 0x8000, _ => 0
		};
	}
}
