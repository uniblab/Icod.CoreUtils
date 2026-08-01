namespace Icod.DiffUtils.Shared.Edits;

/// <summary>Identifies one operation in a two-way line edit script.</summary>
public enum EditOperationKind {
	/// <summary>The line is present in both inputs.</summary>
	Equal,
	/// <summary>The line is removed from the first input.</summary>
	Delete,
	/// <summary>The line is inserted from the second input.</summary>
	Insert
}
