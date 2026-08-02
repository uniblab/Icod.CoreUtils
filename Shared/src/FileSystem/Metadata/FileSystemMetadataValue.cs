namespace Icod.CoreUtils.Shared.FileSystem.Metadata;

/// <summary>
/// Identifies whether one filesystem metadata value is available and, when it is not, why.
/// </summary>
public enum FileSystemMetadataAvailability {
	/// <summary>The provider could not obtain the value for this observation.</summary>
	Unavailable = 0,
	/// <summary>The value is available.</summary>
	Available = 1,
	/// <summary>The host platform or filesystem does not expose the value.</summary>
	Unsupported = 2,
	/// <summary>The value does not apply to this filesystem object.</summary>
	NotApplicable = 3
}

/// <summary>
/// Carries one filesystem metadata value together with explicit availability information.
/// </summary>
/// <typeparam name="T">The metadata value type.</typeparam>
public readonly record struct FileSystemMetadataValue<T> {
	private FileSystemMetadataValue(
		FileSystemMetadataAvailability availability,
		T? value,
		string? message
	) {
		Availability = availability;
		Value = value;
		Message = message;
	}

	/// <summary>Gets the availability state.</summary>
	public FileSystemMetadataAvailability Availability { get; }

	/// <summary>Gets the value when <see cref="Availability"/> is <see cref="FileSystemMetadataAvailability.Available"/>.</summary>
	public T? Value { get; }

	/// <summary>Gets an optional provider explanation.</summary>
	public string? Message { get; }

	/// <summary>Gets whether the value is available.</summary>
	public bool IsAvailable => Availability == FileSystemMetadataAvailability.Available;

	/// <summary>Gets the available value or throws when the metadata is not available.</summary>
	/// <returns>The available value.</returns>
	/// <exception cref="InvalidOperationException">The metadata is not available.</exception>
	public T GetRequiredValue() {
		if ( !IsAvailable ) {
			throw new InvalidOperationException( Message ?? "The filesystem metadata value is not available." );
		}
		return Value!;
	}

	/// <summary>Creates an available value.</summary>
	/// <param name="value">The available value.</param>
	/// <returns>The metadata result.</returns>
	public static FileSystemMetadataValue<T> Available( T value ) => new(
		FileSystemMetadataAvailability.Available,
		value,
		null
	);

	/// <summary>Creates an unavailable value.</summary>
	/// <param name="message">An optional explanation.</param>
	/// <returns>The metadata result.</returns>
	public static FileSystemMetadataValue<T> Unavailable( string? message = null ) => new(
		FileSystemMetadataAvailability.Unavailable,
		default,
		message
	);

	/// <summary>Creates an unsupported value.</summary>
	/// <param name="message">An optional explanation.</param>
	/// <returns>The metadata result.</returns>
	public static FileSystemMetadataValue<T> Unsupported( string? message = null ) => new(
		FileSystemMetadataAvailability.Unsupported,
		default,
		message
	);

	/// <summary>Creates a value that does not apply to the observed object.</summary>
	/// <param name="message">An optional explanation.</param>
	/// <returns>The metadata result.</returns>
	public static FileSystemMetadataValue<T> NotApplicable( string? message = null ) => new(
		FileSystemMetadataAvailability.NotApplicable,
		default,
		message
	);
}
