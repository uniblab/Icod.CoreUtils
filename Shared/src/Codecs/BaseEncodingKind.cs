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
/// Identifies an encoding supported by the base-encoding command family.
/// </summary>
public enum BaseEncodingKind {

	/// <summary>RFC 4648 Base16.</summary>
	Base16,

	/// <summary>RFC 4648 Base32.</summary>
	Base32,

	/// <summary>RFC 4648 extended-hex Base32.</summary>
	Base32Hex,

	/// <summary>Visually unambiguous Base58.</summary>
	Base58,

	/// <summary>RFC 4648 Base64.</summary>
	Base64,

	/// <summary>RFC 4648 URL-safe Base64.</summary>
	Base64Url,

	/// <summary>Binary text with the least-significant bit first.</summary>
	Base2Lsbf,

	/// <summary>Binary text with the most-significant bit first.</summary>
	Base2Msbf,

	/// <summary>ZeroMQ Z85.</summary>
	Z85

}
