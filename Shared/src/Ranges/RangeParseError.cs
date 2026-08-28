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

namespace Icod.CoreUtils.Shared.Ranges;

/// <summary>Describes one deterministic range-list parsing failure.</summary>
public sealed class RangeParseError {

	/// <summary>Initializes a range parsing error.</summary>
	/// <param name="code">The stable error category.</param>
	/// <param name="characterIndex">The zero-based source character index.</param>
	/// <param name="token">The offending source token.</param>
	/// <param name="message">A command-neutral explanatory message.</param>
	public RangeParseError(
		RangeParseErrorCode code,
		int characterIndex,
		string token,
		string message
	) {
		if ( !Enum.IsDefined( code ) ) {
			throw new ArgumentOutOfRangeException( nameof( code ) );
		}
		ArgumentNullException.ThrowIfNull( token );
		ArgumentNullException.ThrowIfNull( message );
		if ( characterIndex < 0 ) {
			throw new ArgumentOutOfRangeException( nameof( characterIndex ) );
		}
		this.Code = code;
		this.CharacterIndex = characterIndex;
		this.Token = token;
		this.Message = message;
	}

	/// <summary>Gets the stable error category.</summary>
	public RangeParseErrorCode Code { get; }

	/// <summary>Gets the zero-based source character index.</summary>
	public int CharacterIndex { get; }

	/// <summary>Gets the offending source token.</summary>
	public string Token { get; }

	/// <summary>Gets the command-neutral explanatory message.</summary>
	public string Message { get; }

}
