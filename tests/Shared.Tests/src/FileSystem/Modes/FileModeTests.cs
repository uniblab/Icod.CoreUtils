using Icod.CoreUtils.Shared.FileSystem.Modes;
using Xunit;

namespace Icod.CoreUtils.Shared.Tests.FileSystem.Modes;

/// <summary>
/// Exercises GNU numeric and symbolic mode semantics supplied by Completion Gate E4.
/// </summary>
public sealed class FileModeTests {
	/// <summary>Verifies absolute octal parsing, special bits, and stable octal formatting.</summary>
	[Fact]
	public void ParsesAbsoluteNumericModes() {
		var ordinary = FileModeParser.ParseRequired( "755" );
		var special = FileModeParser.ParseRequired( "4751" );

		Assert.True( ordinary.IsAbsoluteNumeric );
		Assert.Equal( 0x01ed, ordinary.Apply( new PosixFileMode( 0 ), false, FileCreationMask.None ).Value );
		Assert.Equal( 0x09e9, special.Apply( new PosixFileMode( 0 ), false, FileCreationMask.None ).Value );
		Assert.Equal( "4751", special.AbsoluteMode!.Value.ToString() );
	}

	/// <summary>Verifies structured diagnostics for invalid and excessive numeric modes.</summary>
	[Fact]
	public void RejectsInvalidNumericModes() {
		var digit = FileModeParser.Parse( "888" );
		var range = FileModeParser.Parse( "10000" );

		Assert.False( digit.Succeeded );
		Assert.Equal( FileModeParseErrorCode.InvalidNumericDigit, digit.ErrorCode );
		Assert.Equal( 0, digit.ErrorOffset );
		Assert.False( range.Succeeded );
		Assert.Equal( FileModeParseErrorCode.NumericValueOutOfRange, range.ErrorCode );
	}

	/// <summary>Verifies GNU operator-numeric addition, removal, and assignment.</summary>
	[Fact]
	public void AppliesOperatorNumericModes() {
		var current = new PosixFileMode( 0x0180 );

		Assert.Equal(
			0x0190,
			FileModeParser.ParseRequired( "+20" ).Apply( current, false, FileCreationMask.None ).Value
		);
		Assert.Equal(
			0x0100,
			FileModeParser.ParseRequired( "-200" ).Apply( current, false, FileCreationMask.None ).Value
		);
		Assert.Equal(
			0x0048,
			FileModeParser.ParseRequired( "=110" ).Apply( current, false, FileCreationMask.None ).Value
		);
		Assert.Equal(
			0x0100,
			FileModeParser.ParseRequired( "=0,u+r" ).Apply( current, false, FileCreationMask.None ).Value
		);
	}

	/// <summary>Verifies subject assignment and multiple clauses are applied in source order.</summary>
	[Fact]
	public void AppliesSymbolicClausesInOrder() {
		var expression = FileModeParser.ParseRequired( "u=rwx,g=rx,o=,g+w" );
		var result = expression.Apply( new PosixFileMode( 0 ), false, FileCreationMask.None );

		Assert.Equal( 0x01f8, result.Value );
	}

	/// <summary>Verifies symbolic permission copying observes changes made by earlier clauses.</summary>
	[Fact]
	public void CopiesPermissionClassesFromCurrentMode() {
		var expression = FileModeParser.ParseRequired( "g=u,o=g" );
		var result = expression.Apply( new PosixFileMode( 0x01a0 ), false, FileCreationMask.None );

		Assert.Equal( 0x01b6, result.Value );
	}

	/// <summary>Verifies conditional execute applies to directories and already executable files only.</summary>
	[Fact]
	public void AppliesConditionalExecute() {
		var expression = FileModeParser.ParseRequired( "a+X" );
		var nonExecutable = new PosixFileMode( 0x01a4 );
		var executable = new PosixFileMode( 0x01a5 );

		Assert.Equal( 0x01a4, expression.Apply( nonExecutable, false, FileCreationMask.None ).Value );
		Assert.Equal( 0x01ed, expression.Apply( nonExecutable, true, FileCreationMask.None ).Value );
		Assert.Equal( 0x01ed, expression.Apply( executable, false, FileCreationMask.None ).Value );
	}

	/// <summary>Verifies an omitted subject is filtered by the supplied creation mask.</summary>
	[Fact]
	public void AppliesUmaskToOmittedSubjects() {
		var expression = FileModeParser.ParseRequired( "+w" );
		var result = expression.Apply( new PosixFileMode( 0x0124 ), false, new FileCreationMask( 0x0012 ) );

		Assert.Equal( 0x01a4, result.Value );
	}

	/// <summary>Verifies assignment does not alter ordinary bits excluded by an omitted-subject umask.</summary>
	[Fact]
	public void PreservesUmaskExcludedBitsDuringAssignment() {
		var expression = FileModeParser.ParseRequired( "=r" );
		var result = expression.Apply( new PosixFileMode( 0x01b6 ), false, new FileCreationMask( 0x0012 ) );

		Assert.Equal( 0x0136, result.Value );
	}

	/// <summary>Verifies directory set-ID preservation and explicit numeric clearing.</summary>
	[Fact]
	public void PreservesDirectorySetIdsUnlessExplicitlyCleared() {
		var current = new PosixFileMode( 0x0ded );
		var numericPreserved = FileModeParser.ParseRequired( "755" ).Apply(
			current,
			true,
			FileCreationMask.None
		);
		var symbolicPreserved = FileModeParser.ParseRequired( "u=rwx,go=rx" ).Apply(
			current,
			true,
			FileCreationMask.None
		);
		var fiveDigitCleared = FileModeParser.ParseRequired( "00755" ).Apply(
			current,
			true,
			FileCreationMask.None
		);
		var operatorCleared = FileModeParser.ParseRequired( "=755" ).Apply(
			current,
			true,
			FileCreationMask.None
		);
		var symbolicCleared = FileModeParser.ParseRequired( "u=rwx,go=rx,a-s" ).Apply(
			current,
			true,
			FileCreationMask.None
		);

		Assert.Equal( 0x0ded, numericPreserved.Value );
		Assert.Equal( 0x0ded, symbolicPreserved.Value );
		Assert.Equal( 0x01ed, fiveDigitCleared.Value );
		Assert.Equal( 0x01ed, operatorCleared.Value );
		Assert.Equal( 0x01ed, symbolicCleared.Value );
	}

	/// <summary>Verifies creation masks suppress ordinary bits while retaining requested special bits.</summary>
	[Fact]
	public void AppliesCreationMaskWithoutDiscardingSpecialBits() {
		var requested = new PosixFileMode( 0x09b6 );
		var result = new FileCreationMask( 0x0012 ).Apply( requested );

		Assert.Equal( 0x09a4, result.Value );
		Assert.Equal( "022", new FileCreationMask( 0x0012 ).ToString() );
	}

	/// <summary>Verifies empty clauses, invalid subjects, missing operators, and mixed copies are rejected.</summary>
	[Fact]
	public void RejectsMalformedSymbolicModes() {
		var empty = FileModeParser.Parse( "u+r," );
		var subject = FileModeParser.Parse( "q+r" );
		var laterSubject = FileModeParser.Parse( "ugq+r" );
		var missingOperator = FileModeParser.Parse( "r" );
		var copy = FileModeParser.Parse( "g=uw" );

		Assert.Equal( FileModeParseErrorCode.EmptyClause, empty.ErrorCode );
		Assert.Equal( FileModeParseErrorCode.InvalidSubject, subject.ErrorCode );
		Assert.Equal( FileModeParseErrorCode.InvalidSubject, laterSubject.ErrorCode );
		Assert.Equal( 2, laterSubject.ErrorOffset );
		Assert.Equal( FileModeParseErrorCode.MissingOperator, missingOperator.ErrorCode );
		Assert.Equal( FileModeParseErrorCode.InvalidPermissionCopy, copy.ErrorCode );
	}
}
