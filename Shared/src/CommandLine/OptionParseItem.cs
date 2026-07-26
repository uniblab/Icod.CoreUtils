namespace Icod.CoreUtils.Shared.CommandLine;

/// <summary>
/// Preserves an option or operand in command-line encounter order.
/// </summary>
public sealed class OptionParseItem {

	/// <summary>Gets the zero-based source argument index.</summary>
	public int ArgumentIndex {
		get;
	}

	/// <summary>Gets the item kind.</summary>
	public OptionParseItemKind Kind {
		get;
	}

	/// <summary>Gets the operand when <see cref="Kind"/> is <see cref="OptionParseItemKind.Operand"/>.</summary>
	public string? Operand {
		get;
	}

	/// <summary>Gets the option occurrence when <see cref="Kind"/> is <see cref="OptionParseItemKind.Option"/>.</summary>
	public OptionOccurrence? Option {
		get;
	}

	private OptionParseItem(
		OptionParseItemKind kind,
		int argumentIndex,
		OptionOccurrence? option,
		string? operand
	) {
		this.Kind = kind;
		this.ArgumentIndex = argumentIndex;
		this.Option = option;
		this.Operand = operand;
	}

	internal static OptionParseItem FromOption(
		OptionOccurrence option
	) {
		return new OptionParseItem(
			OptionParseItemKind.Option,
			option.ArgumentIndex,
			option,
			null
		);
	}

	internal static OptionParseItem FromOperand(
		string operand,
		int argumentIndex
	) {
		return new OptionParseItem(
			OptionParseItemKind.Operand,
			argumentIndex,
			null,
			operand
		);
	}

}
