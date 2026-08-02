namespace Icod.CoreUtils.Stat;

using System.Globalization;
using System.Text;
using Icod.CoreUtils.Shared.FileSystem.Metadata;
using Icod.CoreUtils.Shared.FileSystem.Traversal;

/// <summary>Expands GNU <c>stat</c> file and filesystem format directives.</summary>
internal static class StatFormatEngine {
	private readonly record struct FieldValue(
		string Text,
		bool Numeric = false,
		int Radix = 10,
		bool Signed = false,
		DateTimeOffset? Timestamp = null
	);

	/// <summary>Formats one file observation.</summary>
	/// <param name="format">The GNU stat format string.</param>
	/// <param name="operand">The operand text.</param>
	/// <param name="metadata">The file metadata.</param>
	/// <param name="fileSystem">Optional containing-filesystem information.</param>
	/// <param name="interpretEscapes">Whether backslash escapes are interpreted.</param>
	/// <returns>The formatted text.</returns>
	public static string FormatFile(
		string format,
		string operand,
		FileSystemMetadata metadata,
		FileSystemInformation? fileSystem,
		bool interpretEscapes
	) => Expand(
		format,
		interpretEscapes,
		directive => GetFileField( directive, operand, metadata, fileSystem )
	);

	/// <summary>Formats one filesystem observation.</summary>
	/// <param name="format">The GNU stat format string.</param>
	/// <param name="operand">The operand text.</param>
	/// <param name="information">The filesystem information.</param>
	/// <param name="interpretEscapes">Whether backslash escapes are interpreted.</param>
	/// <returns>The formatted text.</returns>
	public static string FormatFileSystem(
		string format,
		string operand,
		FileSystemInformation information,
		bool interpretEscapes
	) => Expand(
		format,
		interpretEscapes,
		directive => GetFileSystemField( directive, operand, information )
	);

	/// <summary>Creates the default human-readable file report.</summary>
	/// <param name="operand">The operand text.</param>
	/// <param name="metadata">The file metadata.</param>
	/// <returns>The report without a final generated newline.</returns>
	public static string FormatDefaultFile( string operand, FileSystemMetadata metadata ) {
		var builder = new StringBuilder();
		builder.Append( "  File: " ).Append( QuoteName( operand ) );
		if ( metadata.IsPathIndirection && !metadata.WasDereferenced && metadata.LinkTarget.IsAvailable ) {
			builder.Append( " -> " ).Append( QuoteName( metadata.LinkTarget.GetRequiredValue() ) );
		}
		builder.AppendLine();
		builder.Append( "  Size: " ).Append( ValueOrDash( metadata.Size ) )
			.Append( "\tBlocks: " ).Append( ValueOrDash( metadata.AllocatedBlocks ) )
			.Append( "\tIO Block: " ).Append( ValueOrDash( metadata.PreferredIoBlockSize ) )
			.Append( "   " ).Append( DescribeKind( metadata.Kind, metadata ) ).AppendLine();
		builder.Append( "Device: " ).Append( ValueOrDash( metadata.DeviceIdentifier ) )
			.Append( "\tInode: " ).Append( ValueOrDash( metadata.InodeNumber ) )
			.Append( "\tLinks: " ).Append( ValueOrDash( metadata.LinkCount ) ).AppendLine();
		builder.Append( "Access: (" ).Append( FormatModeOctal( metadata.Mode, 4 ) ).Append( "/" )
			.Append( FormatModeText( metadata ) ).Append( ")  Uid: (" )
			.Append( ValueOrDash( metadata.UserId ) ).Append( "/" )
			.Append( ValueOrDash( metadata.OwnerName ) ).Append( ")   Gid: (" )
			.Append( ValueOrDash( metadata.GroupId ) ).Append( "/" )
			.Append( ValueOrDash( metadata.GroupName ) ).AppendLine( ")" );
		builder.Append( "Access: " ).AppendLine( FormatTimestampOrDash( metadata.AccessTime ) );
		builder.Append( "Modify: " ).AppendLine( FormatTimestampOrDash( metadata.ModificationTime ) );
		builder.Append( "Change: " ).AppendLine( FormatTimestampOrDash( metadata.ChangeTime ) );
		builder.Append( " Birth: " ).Append( FormatTimestampOrDash( metadata.BirthTime ) );
		return builder.ToString();
	}

	/// <summary>Creates the default human-readable filesystem report.</summary>
	/// <param name="operand">The operand text.</param>
	/// <param name="information">The filesystem information.</param>
	/// <returns>The report without a final generated newline.</returns>
	public static string FormatDefaultFileSystem( string operand, FileSystemInformation information ) {
		var fundamentalBlockSize = FundamentalBlockSize( information );
		var transferBlockSize = TransferBlockSize( information );
		var builder = new StringBuilder();
		builder.Append( "  File: " ).AppendLine( QuoteName( operand ) );
		builder.Append( "    ID: " ).Append( FormatFileSystemIdentifier( information.Identity ) )
			.Append( " Namelen: " ).Append( ValueOrDash( information.MaximumNameLength ) )
			.Append( " Type: " ).AppendLine( ValueOrDash( information.FileSystemType ) );
		builder.Append( "Block size: " ).Append( transferBlockSize.ToString( CultureInfo.InvariantCulture ) )
			.Append( " Fundamental block size: " )
			.AppendLine( fundamentalBlockSize.ToString( CultureInfo.InvariantCulture ) );
		builder.Append( "Blocks: Total: " ).Append( Blocks( information.TotalBytes, fundamentalBlockSize ) )
			.Append( " Free: " ).Append( Blocks( information.FreeBytes, fundamentalBlockSize ) )
			.Append( " Available: " ).Append( Blocks( information.AvailableBytes, fundamentalBlockSize ) )
			.AppendLine();
		builder.Append( "Mount point: " ).Append( ValueOrDash( information.MountPoint ) )
			.Append( " Read-only: " ).Append( ValueOrDash( information.IsReadOnly ) );
		return builder.ToString();
	}

	private static string Expand(
		string format,
		bool interpretEscapes,
		Func<string, FieldValue> getField
	) {
		ArgumentNullException.ThrowIfNull( format );
		var output = new StringBuilder();
		for ( var index = 0; index < format.Length; index++ ) {
			var character = format[index];
			if ( interpretEscapes && '\\' == character ) {
				if ( !AppendEscape( output, format, ref index ) ) {
					break;
				}
				continue;
			}
			if ( '%' != character ) {
				output.Append( character );
				continue;
			}
			if ( index + 1 < format.Length && '%' == format[index + 1] ) {
				output.Append( '%' );
				index++;
				continue;
			}

			var left = false;
			var zero = false;
			var alternate = false;
			var plus = false;
			var spaceSign = false;
			for ( index++; index < format.Length; index++ ) {
				switch ( format[index] ) {
					case '-': left = true; continue;
					case '0': zero = true; continue;
					case '#': alternate = true; continue;
					case '+': plus = true; continue;
					case ' ': spaceSign = true; continue;
				}
				break;
			}
			var width = ReadNumber( format, ref index );
			var precisionSpecified = index < format.Length && '.' == format[index];
			int? precision = null;
			if ( precisionSpecified ) {
				index++;
				precision = ReadNumber( format, ref index );
			}
			if ( index >= format.Length ) {
				throw new FormatException( "invalid format: trailing '%'" );
			}

			string directive;
			if ( format[index] is 'H' or 'L'
				&& index + 1 < format.Length
				&& format[index + 1] is 'd' or 'r' ) {
				directive = format.Substring( index, 2 );
				index++;
			} else {
				directive = format[index].ToString();
			}
			var value = getField( directive );
			var text = FormatFieldValue( value, precisionSpecified, precision );
			if ( alternate && value.Numeric ) {
				text = AddAlternatePrefix( text, value.Radix );
			}
			if ( value.Numeric && value.Signed && 0 < text.Length && '-' != text[0] && '+' != text[0] ) {
				if ( plus ) {
					text = string.Concat( "+", text );
				} else if ( spaceSign ) {
					text = string.Concat( " ", text );
				}
			}
			if ( width.HasValue && text.Length < width.Value ) {
				text = ApplyWidth(
					text,
					width.Value,
					left,
					zero && value.Numeric && !precisionSpecified
				);
			}
			output.Append( text );
		}
		return output.ToString();
	}

	private static string FormatFieldValue(
		FieldValue value,
		bool precisionSpecified,
		int? precision
	) {
		if ( value.Timestamp.HasValue ) {
			return precisionSpecified
				? FormatEpochTimestamp( value.Timestamp.Value, precision ?? 0 )
				: value.Text;
		}
		if ( !precisionSpecified ) {
			return value.Text;
		}
		var requested = precision ?? 0;
		if ( value.Numeric ) {
			return PadNumericPrecision( value.Text, requested );
		}
		return requested < value.Text.Length ? value.Text[..requested] : value.Text;
	}

	private static string PadNumericPrecision( string text, int precision ) {
		var signLength = 0 < text.Length && text[0] is '+' or '-' ? 1 : 0;
		if ( 0 == precision && IsZeroDigits( text, signLength ) ) {
			return string.Empty;
		}
		var prefixLength = HasHexPrefix( text, signLength ) ? 2 : 0;
		var digitLength = text.Length - signLength - prefixLength;
		if ( digitLength >= precision ) {
			return text;
		}
		var insertAt = signLength + prefixLength;
		return text.Insert( insertAt, new string( '0', precision - digitLength ) );
	}

	private static string AddAlternatePrefix( string text, int radix ) {
		var signLength = 0 < text.Length && text[0] is '+' or '-' ? 1 : 0;
		var prefix = radix switch {
			8 => "0",
			16 => "0x",
			_ => string.Empty,
		};
		if ( 0 == text.Length ) {
			return 8 == radix ? "0" : text;
		}
		if ( 0 == prefix.Length
			|| IsZeroDigits( text, signLength )
			|| HasPrefix( text, signLength, prefix ) ) {
			return text;
		}
		return text.Insert( signLength, prefix );
	}

	private static bool IsZeroDigits( string text, int start ) {
		if ( start >= text.Length ) {
			return false;
		}
		for ( var index = start; index < text.Length; index++ ) {
			if ( '0' != text[index] ) {
				return false;
			}
		}
		return true;
	}

	private static string ApplyWidth( string text, int width, bool left, bool zero ) {
		var padding = new string( zero && !left ? '0' : ' ', width - text.Length );
		if ( left ) {
			return string.Concat( text, padding );
		}
		if ( !zero ) {
			return string.Concat( padding, text );
		}
		var signLength = 0 < text.Length && text[0] is '+' or '-' ? 1 : 0;
		var prefixLength = HasHexPrefix( text, signLength ) ? 2 : 0;
		var insertAt = signLength + prefixLength;
		return text.Insert( insertAt, padding );
	}

	private static bool HasHexPrefix( string text, int index ) =>
		index + 1 < text.Length
		&& '0' == text[index]
		&& text[index + 1] is 'x' or 'X';

	private static bool HasPrefix( string text, int index, string prefix ) =>
		index <= text.Length - prefix.Length
		&& text.AsSpan( index, prefix.Length ).Equals(
			prefix.AsSpan(), StringComparison.OrdinalIgnoreCase
		);

	private static int? ReadNumber( string format, ref int index ) {
		var start = index;
		while ( index < format.Length && char.IsAsciiDigit( format[index] ) ) {
			index++;
		}
		if ( start == index ) {
			return null;
		}
		if ( !int.TryParse(
			format.AsSpan( start, index - start ),
			NumberStyles.None,
			CultureInfo.InvariantCulture,
			out var value
		) ) {
			throw new FormatException( "format width or precision is too large" );
		}
		return value;
	}

	private static bool AppendEscape( StringBuilder output, string format, ref int index ) {
		if ( ++index >= format.Length ) {
			output.Append( '\\' );
			return true;
		}
		switch ( format[index] ) {
			case 'a': output.Append( '\a' ); return true;
			case 'b': output.Append( '\b' ); return true;
			case 'e': output.Append( '\u001b' ); return true;
			case 'f': output.Append( '\f' ); return true;
			case 'n': output.Append( Environment.NewLine ); return true;
			case 'r': output.Append( '\r' ); return true;
			case 't': output.Append( '\t' ); return true;
			case 'v': output.Append( '\v' ); return true;
			case '\\': output.Append( '\\' ); return true;
			case 'x': return AppendRadixEscape( output, format, ref index, 16, 2 );
			default:
				if ( format[index] is >= '0' and <= '7' ) {
					index--;
					return AppendRadixEscape( output, format, ref index, 8, 3 );
				}
				output.Append( format[index] );
				return true;
		}
	}

	private static bool AppendRadixEscape(
		StringBuilder output,
		string format,
		ref int index,
		int radix,
		int maximumDigits
	) {
		var value = 0;
		var digits = 0;
		while ( index + 1 < format.Length && digits < maximumDigits ) {
			var digit = HexDigit( format[index + 1] );
			if ( digit < 0 || digit >= radix ) {
				break;
			}
			index++;
			value = checked( value * radix + digit );
			digits++;
		}
		if ( 0 == digits ) {
			output.Append( radix == 16 ? 'x' : '\\' );
			return true;
		}
		output.Append( (char)value );
		return true;
	}

	private static int HexDigit( char value ) => value switch {
		>= '0' and <= '9' => value - '0',
		>= 'a' and <= 'f' => value - 'a' + 10,
		>= 'A' and <= 'F' => value - 'A' + 10,
		_ => -1,
	};

	private static FieldValue GetFileField(
		string directive,
		string operand,
		FileSystemMetadata metadata,
		FileSystemInformation? fileSystem
	) => directive switch {
		"a" => Numeric( FormatModeOctal( metadata.Mode ), 8 ),
		"A" => Text( FormatModeText( metadata ) ),
		"b" => Numeric( NumericOrZero( metadata.AllocatedBlocks ) ),
		"B" => Numeric( NumericOrZero( metadata.AllocationBlockSize ) ),
		"C" => Text( "?" ),
		"d" => Numeric( DeviceNumber(
			metadata.DeviceIdentifier, metadata.FileSystemIdentity.Provider
		).ToString( CultureInfo.InvariantCulture ) ),
		"D" => Numeric( DeviceNumber(
			metadata.DeviceIdentifier, metadata.FileSystemIdentity.Provider
		).ToString( "x", CultureInfo.InvariantCulture ), 16 ),
		"Hd" => Numeric( DevicePart(
			metadata.DeviceIdentifier, true, metadata.FileSystemIdentity.Provider
		).ToString( CultureInfo.InvariantCulture ) ),
		"Ld" => Numeric( DevicePart(
			metadata.DeviceIdentifier, false, metadata.FileSystemIdentity.Provider
		).ToString( CultureInfo.InvariantCulture ) ),
		"f" => Numeric(
			metadata.Mode.IsAvailable
				? metadata.Mode.GetRequiredValue().ToString( "x", CultureInfo.InvariantCulture )
				: "0",
			16
		),
		"F" => Text( DescribeKind( metadata.Kind, metadata ) ),
		"g" => Numeric( NumericOrZero( metadata.GroupId ) ),
		"G" => Text( TextOrQuestion( metadata.GroupName ) ),
		"h" => Numeric( NumericOrZero( metadata.LinkCount ) ),
		"i" => Numeric( NumericOrZero( metadata.InodeNumber ) ),
		"m" => Text( fileSystem is null ? "?" : TextOrQuestion( fileSystem.MountPoint ) ),
		"n" => Text( operand ),
		"N" => Text( FormatQuotedName( operand, metadata ) ),
		"o" => Numeric( NumericOrZero( metadata.PreferredIoBlockSize ) ),
		"s" => Numeric( NumericOrZero( metadata.Size ) ),
		"r" => Numeric( SpecialDeviceNumber( metadata.SpecialDeviceIdentifier ).ToString( CultureInfo.InvariantCulture ) ),
		"R" => Numeric( SpecialDeviceNumber( metadata.SpecialDeviceIdentifier ).ToString( "x", CultureInfo.InvariantCulture ), 16 ),
		"Hr" => Numeric( DevicePart( metadata.SpecialDeviceIdentifier, true ).ToString( CultureInfo.InvariantCulture ) ),
		"Lr" => Numeric( DevicePart( metadata.SpecialDeviceIdentifier, false ).ToString( CultureInfo.InvariantCulture ) ),
		"t" => Numeric( DevicePart( metadata.SpecialDeviceIdentifier, true ).ToString( "x", CultureInfo.InvariantCulture ), 16 ),
		"T" => Numeric( DevicePart( metadata.SpecialDeviceIdentifier, false ).ToString( "x", CultureInfo.InvariantCulture ), 16 ),
		"u" => Numeric( NumericOrZero( metadata.UserId ) ),
		"U" => Text( TextOrQuestion( metadata.OwnerName ) ),
		"w" => Text( FormatTimestampOrDash( metadata.BirthTime ) ),
		"W" => Epoch( metadata.BirthTime ),
		"x" => Text( FormatTimestampOrDash( metadata.AccessTime ) ),
		"X" => Epoch( metadata.AccessTime ),
		"y" => Text( FormatTimestampOrDash( metadata.ModificationTime ) ),
		"Y" => Epoch( metadata.ModificationTime ),
		"z" => Text( FormatTimestampOrDash( metadata.ChangeTime ) ),
		"Z" => Epoch( metadata.ChangeTime ),
		_ => throw new FormatException( $"invalid format directive '%{directive}'" ),
	};

	private static FieldValue GetFileSystemField(
		string directive,
		string operand,
		FileSystemInformation information
	) {
		var fundamentalBlockSize = FundamentalBlockSize( information );
		return directive switch {
			"a" => Numeric( Blocks( information.AvailableBytes, fundamentalBlockSize ) ),
			"b" => Numeric( Blocks( information.TotalBytes, fundamentalBlockSize ) ),
			"c" => Numeric( "0" ),
			"d" => Numeric( "0" ),
			"f" => Numeric( Blocks( information.FreeBytes, fundamentalBlockSize ) ),
			"i" => Numeric( FormatFileSystemIdentifier( information.Identity ), 16 ),
			"l" => Numeric( NumericOrZero( information.MaximumNameLength ) ),
			"n" => Text( operand ),
			"s" => Numeric( TransferBlockSize( information ).ToString( CultureInfo.InvariantCulture ) ),
			"S" => Numeric( fundamentalBlockSize.ToString( CultureInfo.InvariantCulture ) ),
			"t" => Numeric( "0", 16 ),
			"T" => Text( TextOrQuestion( information.FileSystemType ) ),
			_ => throw new FormatException( $"invalid file system format directive '%{directive}'" ),
		};
	}

	private static FieldValue Numeric( string value, int radix = 10 ) => new( value, true, radix );
	private static FieldValue Text( string value ) => new( value );
	private static FieldValue Epoch( FileSystemMetadataValue<DateTimeOffset> value ) => value.IsAvailable
		? new FieldValue(
			value.GetRequiredValue().ToUnixTimeSeconds().ToString( CultureInfo.InvariantCulture ),
			true,
			10,
			true,
			value.GetRequiredValue()
		)
		: new FieldValue( "0", true, 10, true );

	private static string FormatQuotedName( string operand, FileSystemMetadata metadata ) {
		var text = QuoteName( operand );
		return metadata.IsPathIndirection && !metadata.WasDereferenced && metadata.LinkTarget.IsAvailable
			? string.Concat( text, " -> ", QuoteName( metadata.LinkTarget.GetRequiredValue() ) )
			: text;
	}

	private static string QuoteName( string value ) => string.Concat(
		"'", value.Replace( "'", "'\\''", StringComparison.Ordinal ), "'"
	);

	private static string DescribeKind( FileSystemEntryKind kind, FileSystemMetadata metadata ) {
		if ( metadata.IsVolumeMountPoint ) {
			return "mounted volume";
		}
		if ( metadata.IsJunction ) {
			return "directory junction";
		}
		if ( metadata.IsCloudPlaceholder ) {
			return kind == FileSystemEntryKind.Directory
				? "cloud directory placeholder"
				: "cloud file placeholder";
		}
		return kind switch {
			FileSystemEntryKind.File => "regular file",
			FileSystemEntryKind.Directory => "directory",
			FileSystemEntryKind.SymbolicLink => "symbolic link",
			FileSystemEntryKind.BlockDevice => "block special file",
			FileSystemEntryKind.CharacterDevice => "character special file",
			FileSystemEntryKind.Fifo => "fifo",
			FileSystemEntryKind.Socket => "socket",
			FileSystemEntryKind.NameSurrogate => "name-surrogate reparse point",
			FileSystemEntryKind.ReparsePoint => "reparse point",
			FileSystemEntryKind.Other => "other",
			_ => "unknown",
		};
	}

	private static string FormatModeOctal(
		FileSystemMetadataValue<uint> mode,
		int minimumDigits = 0
	) {
		if ( !mode.IsAvailable ) {
			return minimumDigits > 0 ? new string( '-', minimumDigits ) : "0";
		}
		var text = Convert.ToString( checked( (int)(mode.GetRequiredValue() & 0x0FFFU) ), 8 );
		return minimumDigits > text.Length ? text.PadLeft( minimumDigits, '0' ) : text;
	}

	private static string FormatModeText( FileSystemMetadata metadata ) {
		var type = metadata.Kind switch {
			FileSystemEntryKind.Directory => 'd',
			FileSystemEntryKind.SymbolicLink => 'l',
			FileSystemEntryKind.NameSurrogate or FileSystemEntryKind.ReparsePoint => '?',
			FileSystemEntryKind.BlockDevice => 'b',
			FileSystemEntryKind.CharacterDevice => 'c',
			FileSystemEntryKind.Fifo => 'p',
			FileSystemEntryKind.Socket => 's',
			_ => '-',
		};
		if ( !metadata.Mode.IsAvailable ) {
			return string.Concat( type, "?????????" );
		}
		var mode = metadata.Mode.GetRequiredValue();
		Span<char> result = stackalloc char[10];
		result[0] = type;
		const string permissions = "rwxrwxrwx";
		for ( var index = 0; index < 9; index++ ) {
			var bit = 1U << (8 - index);
			result[index + 1] = 0 != (mode & bit) ? permissions[index] : '-';
		}
		if ( 0 != (mode & 0x800U) ) {
			result[3] = 'x' == result[3] ? 's' : 'S';
		}
		if ( 0 != (mode & 0x400U) ) {
			result[6] = 'x' == result[6] ? 's' : 'S';
		}
		if ( 0 != (mode & 0x200U) ) {
			result[9] = 'x' == result[9] ? 't' : 'T';
		}
		return new string( result );
	}

	private static string FormatTimestampOrDash( FileSystemMetadataValue<DateTimeOffset> value ) =>
		value.IsAvailable ? FormatHumanTimestamp( value.GetRequiredValue() ) : "-";

	private static string FormatHumanTimestamp( DateTimeOffset value ) {
		var local = TimeZoneInfo.ConvertTime( value, TimeZoneInfo.Local );
		var offset = local.Offset;
		var sign = offset < TimeSpan.Zero ? '-' : '+';
		var absoluteOffset = offset.Duration();
		return string.Concat(
			local.ToString( "yyyy-MM-dd HH:mm:ss.fffffff00 ", CultureInfo.InvariantCulture ),
			sign,
			absoluteOffset.Hours.ToString( "D2", CultureInfo.InvariantCulture ),
			absoluteOffset.Minutes.ToString( "D2", CultureInfo.InvariantCulture )
		);
	}

	private static string FormatEpochTimestamp( DateTimeOffset value, int precision ) {
		if ( precision < 0 ) {
			throw new FormatException( "timestamp precision cannot be negative" );
		}
		var seconds = value.ToUnixTimeSeconds();
		if ( 0 == precision ) {
			return seconds.ToString( CultureInfo.InvariantCulture );
		}
		var epochTicks = value.UtcDateTime.Ticks - DateTimeOffset.UnixEpoch.UtcDateTime.Ticks;
		var negative = epochTicks < 0;
		var absoluteTicks = checked( (ulong)(negative ? -epochTicks : epochTicks) );
		var wholeSeconds = absoluteTicks / (ulong)TimeSpan.TicksPerSecond;
		var fractionalTicks = absoluteTicks % (ulong)TimeSpan.TicksPerSecond;
		var sevenDigits = fractionalTicks.ToString( "D7", CultureInfo.InvariantCulture );
		var fraction = precision <= 7
			? sevenDigits[..precision]
			: string.Concat( sevenDigits, new string( '0', precision - 7 ) );
		return string.Concat(
			negative ? "-" : string.Empty,
			wholeSeconds.ToString( CultureInfo.InvariantCulture ),
			".",
			fraction
		);
	}

	private static string NumericOrZero( FileSystemMetadataValue<ulong> value ) => value.IsAvailable
		? value.GetRequiredValue().ToString( CultureInfo.InvariantCulture )
		: "0";

	private static string NumericOrZero( FileSystemMetadataValue<uint> value ) => value.IsAvailable
		? value.GetRequiredValue().ToString( CultureInfo.InvariantCulture )
		: "0";

	private static string TextOrQuestion( FileSystemMetadataValue<string> value ) => value.IsAvailable
		? value.GetRequiredValue()
		: "?";

	private static string ValueOrDash<T>( FileSystemMetadataValue<T> value ) => value.IsAvailable
		? Convert.ToString( value.GetRequiredValue(), CultureInfo.InvariantCulture ) ?? "-"
		: "-";

	private static ulong FundamentalBlockSize( FileSystemInformation information ) {
		if ( information.FragmentSize.IsAvailable && 0 < information.FragmentSize.GetRequiredValue() ) {
			return information.FragmentSize.GetRequiredValue();
		}
		if ( information.BlockSize.IsAvailable && 0 < information.BlockSize.GetRequiredValue() ) {
			return information.BlockSize.GetRequiredValue();
		}
		return 1;
	}

	private static ulong TransferBlockSize( FileSystemInformation information ) {
		if ( information.BlockSize.IsAvailable && 0 < information.BlockSize.GetRequiredValue() ) {
			return information.BlockSize.GetRequiredValue();
		}
		return FundamentalBlockSize( information );
	}

	private static string Blocks( FileSystemMetadataValue<ulong> bytes, ulong blockSize ) => bytes.IsAvailable
		? (bytes.GetRequiredValue() / Math.Max( 1UL, blockSize )).ToString( CultureInfo.InvariantCulture )
		: "0";

	private static ulong DeviceNumber(
		FileSystemMetadataValue<string> value,
		string? provider = null
	) {
		if ( !value.IsAvailable ) {
			return 0;
		}
		var text = value.GetRequiredValue();
		var parts = text.Split( ':', StringSplitOptions.RemoveEmptyEntries );
		if ( 2 == parts.Length
			&& uint.TryParse( parts[0], CultureInfo.InvariantCulture, out var major )
			&& uint.TryParse( parts[1], CultureInfo.InvariantCulture, out var minor ) ) {
			return provider?.StartsWith( "linux-", StringComparison.Ordinal ) is true
				? MakeLinuxDeviceNumber( major, minor )
				: ((ulong)major << 32) | minor;
		}
		if ( string.Equals( provider, "windows-volume", StringComparison.Ordinal )
			&& ulong.TryParse(
				text,
				NumberStyles.HexNumber,
				CultureInfo.InvariantCulture,
				out var windowsNumber
			) ) {
			return windowsNumber;
		}
		if ( ulong.TryParse( text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var number ) ) {
			return number;
		}
		if ( ulong.TryParse( text, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out number ) ) {
			return number;
		}
		return StableHash( text );
	}

	private static ulong SpecialDeviceNumber( FileSystemMetadataValue<string> value ) => DeviceNumber(
		value,
		OperatingSystem.IsLinux() ? "linux-device" : null
	);

	private static ulong MakeLinuxDeviceNumber( uint major, uint minor ) =>
		(minor & 0xFFUL)
		| ((major & 0xFFFUL) << 8)
		| ((minor & ~0xFFUL) << 12)
		| ((major & ~0xFFFUL) << 32);

	private static ulong DevicePart(
		FileSystemMetadataValue<string> value,
		bool major,
		string? provider = null
	) {
		if ( !value.IsAvailable ) {
			return 0;
		}
		var text = value.GetRequiredValue();
		var parts = text.Split( ':', StringSplitOptions.RemoveEmptyEntries );
		if ( 2 == parts.Length
			&& ulong.TryParse( parts[major ? 0 : 1], CultureInfo.InvariantCulture, out var part ) ) {
			return part;
		}
		var packed = DeviceNumber( value, provider );
		if ( OperatingSystem.IsMacOS() ) {
			return major ? (packed >> 24) & 0xFFUL : packed & 0xFFFFFFUL;
		}
		return major ? packed >> 32 : packed & 0xFFFFFFFFUL;
	}

	private static string FormatFileSystemIdentifier( FileSystemIdentity identity ) {
		if ( !identity.IsAvailable ) {
			return "0";
		}
		var identityValue = identity.Value!;
		if ( string.Equals( identity.Provider, "windows-volume", StringComparison.Ordinal )
			&& ulong.TryParse(
				identityValue,
				NumberStyles.HexNumber,
				CultureInfo.InvariantCulture,
				out var hexadecimal
			) ) {
			return hexadecimal.ToString( "x", CultureInfo.InvariantCulture );
		}
		if ( ulong.TryParse( identityValue, CultureInfo.InvariantCulture, out var number ) ) {
			return number.ToString( "x", CultureInfo.InvariantCulture );
		}
		if ( ulong.TryParse(
			identityValue,
			NumberStyles.HexNumber,
			CultureInfo.InvariantCulture,
			out hexadecimal
		) ) {
			return hexadecimal.ToString( "x", CultureInfo.InvariantCulture );
		}
		return StableHash( identity.ToString() ).ToString( "x", CultureInfo.InvariantCulture );
	}

	private static ulong StableHash( string text ) {
		ulong hash = 14695981039346656037UL;
		foreach ( var character in text ) {
			hash ^= character;
			hash *= 1099511628211UL;
		}
		return hash;
	}
}
