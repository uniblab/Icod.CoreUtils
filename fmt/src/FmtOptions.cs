namespace Icod.CoreUtils.Fmt;

/// <summary>Contains validated options for one <c>fmt</c> invocation.</summary>
internal sealed class FmtOptions {
	/// <summary>Initializes a validated option set.</summary>
	/// <param name="crownMargin">Whether crown-margin paragraph recognition is enabled.</param>
	/// <param name="taggedParagraph">Whether tagged-paragraph recognition is enabled.</param>
	/// <param name="splitOnly">Whether input lines may only be split, not joined.</param>
	/// <param name="uniformSpacing">Whether inter-word and sentence spacing is normalized uniformly.</param>
	/// <param name="maximumWidth">The maximum output width.</param>
	/// <param name="goalWidth">The preferred output width.</param>
	/// <param name="prefix">The normalized prefix.</param>
	/// <param name="operands">The input operands.</param>
	internal FmtOptions(
		bool crownMargin,
		bool taggedParagraph,
		bool splitOnly,
		bool uniformSpacing,
		int maximumWidth,
		int goalWidth,
		FmtPrefix prefix,
		IReadOnlyList<string> operands
	) {
		this.CrownMargin = crownMargin;
		this.TaggedParagraph = taggedParagraph;
		this.SplitOnly = splitOnly;
		this.UniformSpacing = uniformSpacing;
		this.MaximumWidth = maximumWidth;
		this.GoalWidth = goalWidth;
		this.Prefix = prefix ?? throw new ArgumentNullException( nameof( prefix ) );
		this.Operands = operands ?? throw new ArgumentNullException( nameof( operands ) );
	}

	/// <summary>Gets whether crown-margin paragraph recognition is enabled.</summary>
	internal bool CrownMargin { get; }

	/// <summary>Gets the preferred output width.</summary>
	internal int GoalWidth { get; }

	/// <summary>Gets the maximum output width.</summary>
	internal int MaximumWidth { get; }

	/// <summary>Gets the input operands in encounter order.</summary>
	internal IReadOnlyList<string> Operands { get; }

	/// <summary>Gets the normalized required prefix.</summary>
	internal FmtPrefix Prefix { get; }

	/// <summary>Gets whether input lines may only be split.</summary>
	internal bool SplitOnly { get; }

	/// <summary>Gets whether tagged-paragraph recognition is enabled.</summary>
	internal bool TaggedParagraph { get; }

	/// <summary>Gets whether spaces are normalized uniformly.</summary>
	internal bool UniformSpacing { get; }
}
