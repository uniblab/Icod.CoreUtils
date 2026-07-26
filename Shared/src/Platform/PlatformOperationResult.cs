namespace Icod.CoreUtils.Shared.Platform;

/// <summary>
/// Describes a supported, unsupported, successful, or failed platform operation.
/// </summary>
public class PlatformOperationResult {

	/// <summary>Gets an exception associated with a failed operation.</summary>
	public Exception? Exception {
		get;
	}

	/// <summary>Gets a user-facing explanation.</summary>
	public string? Message {
		get;
	}

	/// <summary>Gets whether the operation succeeded.</summary>
	public bool Succeeded {
		get;
	}

	/// <summary>Gets whether the operation is supported on this platform.</summary>
	public bool Supported {
		get;
	}

	/// <summary>
	/// Initializes a platform operation result.
	/// </summary>
	protected PlatformOperationResult(
		bool supported,
		bool succeeded,
		string? message,
		Exception? exception
	) {
		this.Supported = supported;
		this.Succeeded = succeeded;
		this.Message = message;
		this.Exception = exception;
	}

	/// <summary>Creates a successful result.</summary>
	public static PlatformOperationResult Success() {
		return new PlatformOperationResult(
			true,
			true,
			null,
			null
		);
	}

	/// <summary>Creates a supported but failed result.</summary>
	public static PlatformOperationResult Failure(
		string message,
		Exception? exception = null
	) {
		return new PlatformOperationResult(
			true,
			false,
			message,
			exception
		);
	}

	/// <summary>Creates an unsupported result.</summary>
	public static PlatformOperationResult Unsupported(
		string message
	) {
		return new PlatformOperationResult(
			false,
			false,
			message,
			null
		);
	}
}

/// <summary>
/// Describes a platform operation that returns a value.
/// </summary>
public sealed class PlatformOperationResult<T> : PlatformOperationResult {

	/// <summary>Gets the returned value when successful.</summary>
	public T? Value {
		get;
	}

	private PlatformOperationResult(
		bool supported,
		bool succeeded,
		T? value,
		string? message,
		Exception? exception
	) : base(
		supported,
		succeeded,
		message,
		exception
	) {
		this.Value = value;
	}

	/// <summary>Creates a successful result.</summary>
	public static PlatformOperationResult<T> Success(
		T value
	) {
		return new PlatformOperationResult<T>(
			true,
			true,
			value,
			null,
			null
		);
	}

	/// <summary>Creates a supported but failed result.</summary>
	public static new PlatformOperationResult<T> Failure(
		string message,
		Exception? exception = null
	) {
		return new PlatformOperationResult<T>(
			true,
			false,
			default,
			message,
			exception
		);
	}

	/// <summary>Creates an unsupported result.</summary>
	public static new PlatformOperationResult<T> Unsupported(
		string message
	) {
		return new PlatformOperationResult<T>(
			false,
			false,
			default,
			message,
			null
		);
	}
}