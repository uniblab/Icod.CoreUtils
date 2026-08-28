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

namespace Icod.CoreUtils.Shared.Codecs;

/// <summary>
/// Configures the common base-encoding command runner.
/// </summary>
public sealed class BaseEncodingCommandSettings {

	/// <summary>Gets the fixed encoding, or <see langword="null"/> when an option selects it.</summary>
	public BaseEncodingKind? FixedEncoding {
		get;
		init;
	}

	/// <summary>Gets the encoding-selection options accepted by the command.</summary>
	public IReadOnlyList<BaseEncodingSelection> EncodingSelections {
		get;
		init;
	} = Array.Empty<BaseEncodingSelection>();

	/// <summary>Gets the command name used in diagnostics.</summary>
	public required string ProgramName {
		get;
		init;
	}

	/// <summary>Gets the usage printer.</summary>
	public required Action<TextWriter> PrintUsage {
		get;
		init;
	}

	/// <summary>Gets the version text.</summary>
	public required string VersionText {
		get;
		init;
	}

}
