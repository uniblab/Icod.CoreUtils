namespace Icod.CoreUtils.Shared.FileSystem.Modes;

/// <summary>
/// Identifies the portable POSIX permission and special mode bits.
/// </summary>
[Flags]
public enum PosixFileModeBits {
	/// <summary>No mode bits.</summary>
	None = 0,
	/// <summary>Execute or search permission for other users.</summary>
	OtherExecute = 0x0001,
	/// <summary>Write permission for other users.</summary>
	OtherWrite = 0x0002,
	/// <summary>Read permission for other users.</summary>
	OtherRead = 0x0004,
	/// <summary>Execute or search permission for members of the owning group.</summary>
	GroupExecute = 0x0008,
	/// <summary>Write permission for members of the owning group.</summary>
	GroupWrite = 0x0010,
	/// <summary>Read permission for members of the owning group.</summary>
	GroupRead = 0x0020,
	/// <summary>Execute or search permission for the owner.</summary>
	UserExecute = 0x0040,
	/// <summary>Write permission for the owner.</summary>
	UserWrite = 0x0080,
	/// <summary>Read permission for the owner.</summary>
	UserRead = 0x0100,
	/// <summary>The restricted-deletion or sticky bit.</summary>
	Sticky = 0x0200,
	/// <summary>The set-group-ID bit.</summary>
	SetGroupId = 0x0400,
	/// <summary>The set-user-ID bit.</summary>
	SetUserId = 0x0800,
	/// <summary>All ordinary permission bits.</summary>
	Permissions = 0x01ff,
	/// <summary>All special mode bits.</summary>
	Special = 0x0e00,
	/// <summary>All portable permission and special bits.</summary>
	All = 0x0fff
}

/// <summary>
/// Represents the portable twelve-bit POSIX file mode used by GNU mode expressions.
/// </summary>
public readonly record struct PosixFileMode {
	private const int MaximumValue = 0x0fff;

	/// <summary>
	/// Initializes a mode from an octal-compatible integer value.
	/// </summary>
	/// <param name="value">A value from zero through octal 7777.</param>
	public PosixFileMode( int value ) {
		if ( value < 0 || value > MaximumValue ) {
			throw new ArgumentOutOfRangeException( nameof( value ) );
		}
		Value = value;
	}

	/// <summary>
	/// Initializes a mode from portable mode bits.
	/// </summary>
	/// <param name="bits">The portable mode bits.</param>
	public PosixFileMode( PosixFileModeBits bits )
		: this( (int)bits ) {
	}

	/// <summary>Gets the numeric mode value.</summary>
	public int Value { get; }

	/// <summary>Gets the mode as portable flags.</summary>
	public PosixFileModeBits Bits => (PosixFileModeBits)Value;

	/// <summary>Gets whether this mode contains one or more execute or search bits.</summary>
	public bool HasAnyExecuteBit => (Value & 0x0049) != 0;

	/// <summary>
	/// Converts this value to the .NET Unix mode representation.
	/// </summary>
	/// <returns>The equivalent Unix mode value.</returns>
	public UnixFileMode ToUnixFileMode() => (UnixFileMode)Value;

	/// <summary>
	/// Converts a .NET Unix mode to the shared portable representation.
	/// </summary>
	/// <param name="mode">The .NET Unix mode.</param>
	/// <returns>The equivalent shared mode.</returns>
	public static PosixFileMode FromUnixFileMode( UnixFileMode mode ) {
		return new PosixFileMode( (int)mode & MaximumValue );
	}

	/// <summary>
	/// Formats the value as four octal digits.
	/// </summary>
	/// <returns>The octal mode text.</returns>
	public override string ToString() => Convert.ToString( Value, 8 ).PadLeft( 4, '0' );
}

/// <summary>
/// Represents the ordinary permission bits suppressed during object creation.
/// </summary>
public readonly record struct FileCreationMask {
	private const int MaximumValue = 0x01ff;

	/// <summary>
	/// Initializes a creation mask.
	/// </summary>
	/// <param name="value">A value from zero through octal 777.</param>
	public FileCreationMask( int value ) {
		if ( value < 0 || value > MaximumValue ) {
			throw new ArgumentOutOfRangeException( nameof( value ) );
		}
		Value = value;
	}

	/// <summary>Gets an empty creation mask.</summary>
	public static FileCreationMask None { get; } = new( 0 );

	/// <summary>Gets the numeric mask value.</summary>
	public int Value { get; }

	/// <summary>
	/// Applies this creation mask while retaining any requested special bits.
	/// </summary>
	/// <param name="requestedMode">The mode requested by the caller.</param>
	/// <returns>The effective creation mode.</returns>
	public PosixFileMode Apply( PosixFileMode requestedMode ) {
		return new PosixFileMode(
			(requestedMode.Value & 0x0e00)
				| ((requestedMode.Value & 0x01ff) & ~Value)
		);
	}

	/// <summary>
	/// Formats the value as three octal digits.
	/// </summary>
	/// <returns>The octal mask text.</returns>
	public override string ToString() => Convert.ToString( Value, 8 ).PadLeft( 3, '0' );
}
