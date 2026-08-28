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

namespace Icod.CoreUtils.Shared.Checksums;

/// <summary>
/// Configures one of the standalone digest commands.
/// </summary>
public sealed class DigestCommandSettings {

	/// <summary>Gets the fixed digest algorithm.</summary>
	public required ChecksumAlgorithmKind Algorithm {
		get;
		init;
	}

	/// <summary>Gets the default digest length in bits.</summary>
	public required int DefaultLengthBits {
		get;
		init;
	}

	/// <summary>Gets the BSD-style algorithm label.</summary>
	public required string DisplayName {
		get;
		init;
	}

	/// <summary>Gets the command name used in diagnostics.</summary>
	public required string ProgramName {
		get;
		init;
	}

	/// <summary>Gets the usage writer.</summary>
	public required Action<TextWriter> PrintUsage {
		get;
		init;
	}

	/// <summary>Gets whether <c>--length</c> is accepted.</summary>
	public bool SupportsLength {
		get;
		init;
	}

	/// <summary>Gets version output.</summary>
	public required string VersionText {
		get;
		init;
	}

}
