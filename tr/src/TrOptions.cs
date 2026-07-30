namespace Icod.CoreUtils.Tr;

/// <summary>Describes the validated operation requested from <c>tr</c>.</summary>
internal sealed class TrOptions {
	/// <summary>Initializes a mutable validated option set.</summary>
	public TrOptions() {
	}

	/// <summary>Gets whether the first set is complemented.</summary>
	public bool Complement { get; init; }

	/// <summary>Gets whether bytes in the first set are deleted.</summary>
	public bool Delete { get; init; }

	/// <summary>Gets whether repeated bytes in the final set are squeezed.</summary>
	public bool SqueezeRepeats { get; init; }

	/// <summary>Gets whether the first translation array is truncated to the second array.</summary>
	public bool TruncateSet1 { get; init; }

	/// <summary>Gets the first set expression.</summary>
	public string String1 { get; init; } = string.Empty;

	/// <summary>Gets the optional second set expression.</summary>
	public string? String2 { get; init; }

	/// <summary>Gets whether the operation performs translation.</summary>
	public bool Translating => !this.Delete && null != this.String2;
}
