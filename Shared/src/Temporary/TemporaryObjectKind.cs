namespace Icod.CoreUtils.Shared.Temporary;

/// <summary>Identifies the temporary object operation to perform.</summary>
public enum TemporaryObjectKind {
	/// <summary>Create a regular file exclusively.</summary>
	File,

	/// <summary>Create a directory exclusively.</summary>
	Directory,

	/// <summary>Generate an unused pathname without creating an object.</summary>
	NameOnly
}
