/*
	Icod.CoreUtils.Shared
	Shared support library for the Icod.CoreUtils command suite.
	Copyright (C) 2026  Timothy J. Bruce <uniblab@hotmail.com>
*/

/*
	This program is free software: you can redistribute it and/or modify
	it under the terms of the GNU Lesser General Public License as published by
	the Free Software Foundation, either version 3 of the License, or
	(at your option) any later version.

	This program is distributed in the hope that it will be useful,
	but WITHOUT ANY WARRANTY; without even the implied warranty of
	MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
	GNU Lesser General Public License for more details.

	You should have received a copy of the GNU Lesser General Public License
	along with this program.  If not, see <https://www.gnu.org/licenses/>.
*/

using FrameworkModes = Icod.CommandFramework.FileSystem.Modes;

namespace Icod.CoreUtils.Shared.FileSystem.Modes;

/// <summary>
/// Identifies the classes affected by one symbolic mode clause.
/// </summary>
[Flags]
public enum FileModeSubject {
	/// <summary>No subject.</summary>
	None = 0,
	/// <summary>The file owner.</summary>
	User = 1,
	/// <summary>Members of the owning group.</summary>
	Group = 2,
	/// <summary>Other users.</summary>
	Other = 4,
	/// <summary>All users.</summary>
	All = User | Group | Other
}

/// <summary>
/// Identifies a mode-change operator.
/// </summary>
public enum FileModeOperator {
	/// <summary>Add the selected bits.</summary>
	Add = 0,
	/// <summary>Remove the selected bits.</summary>
	Remove = 1,
	/// <summary>Replace the affected classes with the selected bits.</summary>
	Assign = 2
}

/// <summary>
/// Describes one operation within a parsed mode clause.
/// </summary>
public sealed class FileModeOperation {
	/// <summary>
	/// Initializes one parsed operation.
	/// </summary>
	/// <param name="operation">The mode-change operator.</param>
	/// <param name="permissions">The symbolic permission text.</param>
	/// <param name="numericValue">The optional operator-numeric operand.</param>
	internal FileModeOperation( FileModeOperator operation, string permissions, int? numericValue ) {
		Operation = operation;
		Permissions = permissions;
		NumericValue = numericValue;
	}

	/// <summary>Gets the operation.</summary>
	public FileModeOperator Operation { get; }

	/// <summary>Gets the symbolic permission text, or an empty string for a numeric operation.</summary>
	public string Permissions { get; }

	/// <summary>Gets the numeric operand when this is an operator-numeric operation.</summary>
	public int? NumericValue { get; }

	/// <summary>Gets whether this operation uses a GNU operator-numeric operand.</summary>
	public bool IsNumeric => NumericValue.HasValue;
}

/// <summary>
/// Describes one comma-delimited mode clause.
/// </summary>
public sealed class FileModeClause {
	private readonly IReadOnlyList<FileModeOperation> operations;

	/// <summary>
	/// Initializes one parsed symbolic clause.
	/// </summary>
	/// <param name="subjects">The affected user classes.</param>
	/// <param name="subjectsWereOmitted">Whether the source omitted the subject portion.</param>
	/// <param name="operations">The operations in source order.</param>
	internal FileModeClause(
		FileModeSubject subjects,
		bool subjectsWereOmitted,
		IReadOnlyList<FileModeOperation> operations
	) {
		Subjects = subjects;
		SubjectsWereOmitted = subjectsWereOmitted;
		ArgumentNullException.ThrowIfNull( operations );
		this.operations = Array.AsReadOnly( operations.ToArray() );
	}

	/// <summary>Gets the explicitly named subjects.</summary>
	public FileModeSubject Subjects { get; }

	/// <summary>Gets whether the subject portion was omitted and is therefore filtered by the umask.</summary>
	public bool SubjectsWereOmitted { get; }

	/// <summary>Gets the operations in source order.</summary>
	public IReadOnlyList<FileModeOperation> Operations => operations;
}

/// <summary>
/// Represents a parsed GNU numeric, operator-numeric, or symbolic mode expression.
/// </summary>
public sealed class FileModeExpression {
	private const int UserPermissionMask = 0x01c0;
	private const int GroupPermissionMask = 0x0038;
	private const int OtherPermissionMask = 0x0007;
	private const int UserClassMask = 0x09c0;
	private const int GroupClassMask = 0x0438;
	private const int OtherClassMask = 0x0207;
	private readonly IReadOnlyList<FileModeClause> clauses;

	/// <summary>
	/// Initializes an absolute numeric mode expression.
	/// </summary>
	/// <param name="absoluteMode">The parsed twelve-bit mode.</param>
	/// <param name="numericDigitCount">The number of octal digits supplied.</param>
	internal FileModeExpression( int absoluteMode, int numericDigitCount ) {
		AbsoluteMode = new FrameworkModes.PosixFileMode( absoluteMode );
		NumericDigitCount = numericDigitCount;
		clauses = Array.Empty<FileModeClause>();
	}

	/// <summary>
	/// Initializes a symbolic or operator-numeric expression.
	/// </summary>
	/// <param name="clauses">The parsed clauses in source order.</param>
	internal FileModeExpression( IReadOnlyList<FileModeClause> clauses ) {
		ArgumentNullException.ThrowIfNull( clauses );
		this.clauses = Array.AsReadOnly( clauses.ToArray() );
	}

	/// <summary>Gets the absolute numeric mode, when the expression is a plain numeric mode.</summary>
	public FrameworkModes.PosixFileMode? AbsoluteMode { get; }

	/// <summary>Gets the number of octal digits supplied for a plain numeric mode.</summary>
	public int NumericDigitCount { get; }

	/// <summary>Gets the parsed symbolic and operator-numeric clauses.</summary>
	public IReadOnlyList<FileModeClause> Clauses => clauses;

	/// <summary>Gets whether this is a plain absolute numeric mode.</summary>
	public bool IsAbsoluteNumeric => AbsoluteMode.HasValue;

	/// <summary>
	/// Applies the expression to an existing mode.
	/// </summary>
	/// <param name="currentMode">The existing file mode.</param>
	/// <param name="isDirectory">Whether the target is a directory.</param>
	/// <param name="creationMask">The current umask used when a symbolic clause omits its subjects.</param>
	/// <returns>The resulting mode.</returns>
	public FrameworkModes.PosixFileMode Apply(
		FrameworkModes.PosixFileMode currentMode,
		bool isDirectory,
		FrameworkModes.FileCreationMask creationMask
	) {
		if ( AbsoluteMode.HasValue ) {
			var value = AbsoluteMode.Value.Value;
			if ( isDirectory && NumericDigitCount <= 4 ) {
				value |= currentMode.Value & 0x0c00;
			}
			return new FrameworkModes.PosixFileMode( value );
		}

		var mode = currentMode.Value;
		foreach ( var clause in clauses ) {
			var subjectMask = GetSubjectMask( clause.Subjects );
			if ( clause.SubjectsWereOmitted ) {
				subjectMask &= ~(creationMask.Value & 0x01ff);
			}
			foreach ( var operation in clause.Operations ) {
				var operationSubjectMask = subjectMask;
				if (
					isDirectory
						&& !operation.IsNumeric
						&& !operation.Permissions.Contains( 's' )
				) {
					operationSubjectMask &= ~0x0c00;
				}
				var permissionBits = operation.IsNumeric
					? operation.NumericValue!.Value
					: GetSymbolicPermissionBits( operation.Permissions, mode, isDirectory );
				permissionBits &= operationSubjectMask;
				mode = operation.Operation switch {
					FileModeOperator.Add => mode | permissionBits,
					FileModeOperator.Remove => mode & ~permissionBits,
					FileModeOperator.Assign => (mode & ~operationSubjectMask) | permissionBits,
					_ => throw new InvalidOperationException( "The parsed mode contains an unknown operation." )
				};
			}
		}
		return new FrameworkModes.PosixFileMode( mode & 0x0fff );
	}

	private static int GetSubjectMask( FileModeSubject subjects ) {
		var mask = 0;
		if ( (subjects & FileModeSubject.User) != 0 ) {
			mask |= UserClassMask;
		}
		if ( (subjects & FileModeSubject.Group) != 0 ) {
			mask |= GroupClassMask;
		}
		if ( (subjects & FileModeSubject.Other) != 0 ) {
			mask |= OtherClassMask;
		}
		return mask;
	}

	private static int GetSymbolicPermissionBits( string permissions, int currentMode, bool isDirectory ) {
		if ( permissions.Length == 1 && permissions[ 0 ] is 'u' or 'g' or 'o' ) {
			return CopyPermissionClass( permissions[ 0 ], currentMode );
		}

		var bits = 0;
		foreach ( var permission in permissions ) {
			bits |= permission switch {
				'r' => 0x0124,
				'w' => 0x0092,
				'x' => 0x0049,
				'X' when isDirectory || (currentMode & 0x0049) != 0 => 0x0049,
				'X' => 0,
				's' => 0x0c00,
				't' => 0x0200,
				_ => throw new InvalidOperationException( "The parsed mode contains an unknown permission." )
			};
		}
		return bits;
	}

	private static int CopyPermissionClass( char source, int mode ) {
		return source switch {
			'u' => ReplicatePermissionTriplet( (mode & UserPermissionMask) >> 6 ),
			'g' => ReplicatePermissionTriplet( (mode & GroupPermissionMask) >> 3 ),
			'o' => ReplicatePermissionTriplet( mode & OtherPermissionMask ),
			_ => throw new InvalidOperationException( "The parsed mode contains an unknown copy source." )
		};
	}

	private static int ReplicatePermissionTriplet( int triplet ) {
		return triplet | (triplet << 3) | (triplet << 6);
	}
}
