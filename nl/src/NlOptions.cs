namespace Icod.CoreUtils.NL;

/// <summary>Contains validated options for one <c>nl</c> invocation.</summary>
internal sealed class NlOptions {
	/// <summary>Initializes a validated option set.</summary>
	/// <param name="headerStyle">The header-numbering style.</param>
	/// <param name="bodyStyle">The body-numbering style.</param>
	/// <param name="footerStyle">The footer-numbering style.</param>
	/// <param name="delimiter">The logical-page delimiter model.</param>
	/// <param name="increment">The signed line-number increment.</param>
	/// <param name="blankJoin">The number of adjacent blank lines represented by one number.</param>
	/// <param name="numberFormat">The generated number format.</param>
	/// <param name="renumberSections">Whether each section resets the current number.</param>
	/// <param name="separator">The number-to-line separator.</param>
	/// <param name="startingNumber">The initial line number.</param>
	/// <param name="numberWidth">The minimum number field width.</param>
	/// <param name="operands">The input operands.</param>
	internal NlOptions(
		NlNumberingStyle headerStyle,
		NlNumberingStyle bodyStyle,
		NlNumberingStyle footerStyle,
		NlSectionDelimiter delimiter,
		long increment,
		long blankJoin,
		NlNumberFormat numberFormat,
		bool renumberSections,
		string separator,
		long startingNumber,
		int numberWidth,
		IReadOnlyList<string> operands
	) {
		this.HeaderStyle = headerStyle ?? throw new ArgumentNullException( nameof( headerStyle ) );
		this.BodyStyle = bodyStyle ?? throw new ArgumentNullException( nameof( bodyStyle ) );
		this.FooterStyle = footerStyle ?? throw new ArgumentNullException( nameof( footerStyle ) );
		this.Delimiter = delimiter ?? throw new ArgumentNullException( nameof( delimiter ) );
		this.Increment = increment;
		this.BlankJoin = blankJoin;
		this.NumberFormat = numberFormat;
		this.RenumberSections = renumberSections;
		this.Separator = separator ?? throw new ArgumentNullException( nameof( separator ) );
		this.StartingNumber = startingNumber;
		this.NumberWidth = numberWidth;
		this.Operands = operands ?? throw new ArgumentNullException( nameof( operands ) );
	}

	/// <summary>Gets the number of adjacent blank lines represented by one number.</summary>
	internal long BlankJoin { get; }

	/// <summary>Gets the body-numbering style.</summary>
	internal NlNumberingStyle BodyStyle { get; }

	/// <summary>Gets the logical-page delimiter model.</summary>
	internal NlSectionDelimiter Delimiter { get; }

	/// <summary>Gets the footer-numbering style.</summary>
	internal NlNumberingStyle FooterStyle { get; }

	/// <summary>Gets the header-numbering style.</summary>
	internal NlNumberingStyle HeaderStyle { get; }

	/// <summary>Gets the signed line-number increment.</summary>
	internal long Increment { get; }

	/// <summary>Gets the generated number format.</summary>
	internal NlNumberFormat NumberFormat { get; }

	/// <summary>Gets the minimum number field width.</summary>
	internal int NumberWidth { get; }

	/// <summary>Gets the input operands in encounter order.</summary>
	internal IReadOnlyList<string> Operands { get; }

	/// <summary>Gets whether each section resets the current number.</summary>
	internal bool RenumberSections { get; }

	/// <summary>Gets the number-to-line separator.</summary>
	internal string Separator { get; }

	/// <summary>Gets the initial line number.</summary>
	internal long StartingNumber { get; }

	/// <summary>Gets the numbering style for a logical-page section.</summary>
	/// <param name="section">The section.</param>
	/// <returns>The configured style.</returns>
	internal NlNumberingStyle GetStyle( NlSection section ) {
		return section switch {
			NlSection.Header => this.HeaderStyle,
			NlSection.Body => this.BodyStyle,
			NlSection.Footer => this.FooterStyle,
			_ => throw new ArgumentOutOfRangeException( nameof( section ) )
		};
	}
}
