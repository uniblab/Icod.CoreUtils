namespace Icod.CoreUtils.Shared.Escapes;

/// <summary>Describes one backslash and its following managed source position.</summary>
internal readonly struct EscapeSequence {

	/// <summary>Initializes an escape-sequence scan result.</summary>
	/// <param name="backslashOffset">The source offset of the backslash.</param>
	/// <param name="designatorOffset">The source offset after the backslash, or the input length for a trailing backslash.</param>
	/// <param name="isTrailing">Whether no source character follows the backslash.</param>
	internal EscapeSequence(
		int backslashOffset,
		int designatorOffset,
		bool isTrailing
	) {
		this.BackslashOffset = backslashOffset;
		this.DesignatorOffset = designatorOffset;
		this.IsTrailing = isTrailing;
	}

	/// <summary>Gets the source offset of the backslash.</summary>
	internal int BackslashOffset { get; }

	/// <summary>Gets the source offset of the escape designator, or the source length for a trailing backslash.</summary>
	internal int DesignatorOffset { get; }

	/// <summary>Gets whether no source character follows the backslash.</summary>
	internal bool IsTrailing { get; }

}
