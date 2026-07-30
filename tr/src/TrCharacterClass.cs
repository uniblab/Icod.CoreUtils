namespace Icod.CoreUtils.Tr;

/// <summary>Identifies a POSIX byte character class accepted by <c>tr</c>.</summary>
internal enum TrCharacterClass {
	/// <summary>Letters and decimal digits.</summary>
	Alnum,
	/// <summary>Letters.</summary>
	Alpha,
	/// <summary>Horizontal whitespace.</summary>
	Blank,
	/// <summary>Control bytes.</summary>
	Cntrl,
	/// <summary>ASCII decimal digits.</summary>
	Digit,
	/// <summary>Printable non-space bytes.</summary>
	Graph,
	/// <summary>Lowercase letters.</summary>
	Lower,
	/// <summary>Printable bytes including space.</summary>
	Print,
	/// <summary>Punctuation bytes.</summary>
	Punct,
	/// <summary>Horizontal or vertical whitespace.</summary>
	Space,
	/// <summary>Uppercase letters.</summary>
	Upper,
	/// <summary>ASCII hexadecimal digits.</summary>
	XDigit
}
