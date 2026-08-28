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

namespace Icod.CoreUtils.Shared.Platform;

using System.Net;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

/// <summary>Identifies a Linux <c>utmp</c> record type.</summary>
public enum LoginRecordType : short {
	/// <summary>An unused record.</summary>
	Empty = 0,
	/// <summary>A run-level transition.</summary>
	RunLevel = 1,
	/// <summary>The system boot time.</summary>
	BootTime = 2,
	/// <summary>The time after a clock change.</summary>
	NewTime = 3,
	/// <summary>The time before a clock change.</summary>
	OldTime = 4,
	/// <summary>An init process.</summary>
	InitProcess = 5,
	/// <summary>A login process.</summary>
	LoginProcess = 6,
	/// <summary>An active user process.</summary>
	UserProcess = 7,
	/// <summary>A terminated process.</summary>
	DeadProcess = 8,
	/// <summary>An accounting record.</summary>
	Accounting = 9
}

/// <summary>Represents a login-accounting record.</summary>
/// <param name="Type">The type value.</param>
/// <param name="ProcessId">The process id value.</param>
/// <param name="Line">The line value.</param>
/// <param name="Id">The id value.</param>
/// <param name="User">The user value.</param>
/// <param name="Host">The host value.</param>
/// <param name="TerminationStatus">The termination status value.</param>
/// <param name="ExitStatus">The exit status value.</param>
/// <param name="SessionId">The session id value.</param>
/// <param name="Timestamp">The timestamp value.</param>
/// <param name="Address">The address value.</param>
public sealed record LoginRecord(
	LoginRecordType Type,
	int ProcessId,
	string Line,
	string Id,
	string User,
	string Host,
	short TerminationStatus,
	short ExitStatus,
	int SessionId,
	DateTimeOffset Timestamp,
	IPAddress? Address
);

/// <summary>Supplies login-accounting records.</summary>
public interface ILoginRecordProvider {
	/// <summary>Gets whether login-accounting records are supported on this platform.</summary>
	bool IsSupported { get; }
	/// <summary>Reads records from the default or specified accounting file.</summary>
	IAsyncEnumerable<LoginRecord> ReadAsync( string? fileName = null, CancellationToken cancellationToken = default );
	/// <summary>Gets the terminal line attached to standard input, without the <c>/dev/</c> prefix.</summary>
	ValueTask<string?> GetStandardInputTerminalLineAsync( CancellationToken cancellationToken = default );
}

/// <summary>Reads Linux <c>utmp</c> login-accounting records.</summary>
public sealed class SystemLoginRecordProvider : ILoginRecordProvider {
	private const int RecordLength = 384;

	/// <summary>Gets the process-wide provider instance.</summary>
	public static SystemLoginRecordProvider Instance { get; } = new();

	private SystemLoginRecordProvider() { }

	/// <inheritdoc />
	public bool IsSupported => OperatingSystem.IsLinux();

	/// <inheritdoc />
	public async IAsyncEnumerable<LoginRecord> ReadAsync(
		string? fileName = null,
		[EnumeratorCancellation] CancellationToken cancellationToken = default
	) {
		if ( !this.IsSupported ) yield break;
		cancellationToken.ThrowIfCancellationRequested();
		var path = fileName ?? ResolveDefaultPath();
		if ( string.IsNullOrEmpty( path ) ) yield break;

		FileStream stream;
		try {
			stream = new FileStream(
				path,
				FileMode.Open,
				FileAccess.Read,
				FileShare.ReadWrite | FileShare.Delete,
				RecordLength,
				FileOptions.Asynchronous | FileOptions.SequentialScan
			);
		} catch ( FileNotFoundException ) {
			yield break;
		} catch ( DirectoryNotFoundException ) {
			yield break;
		}

		await using ( stream ) {
			var buffer = new byte[ RecordLength ];
			while ( true ) {
				var count = await ReadRecordAsync( stream, buffer, cancellationToken ).ConfigureAwait( false );
				if ( 0 == count ) yield break;
				if ( RecordLength != count ) yield break;
				yield return Parse( buffer );
			}
		}
	}

	/// <inheritdoc />
	public ValueTask<string?> GetStandardInputTerminalLineAsync( CancellationToken cancellationToken = default ) {
		cancellationToken.ThrowIfCancellationRequested();
		if ( !this.IsSupported ) return ValueTask.FromResult<string?>( null );
		var buffer = new byte[ 4096 ];
		if ( 0 != NativeMethods.GetTerminalName( 0, buffer, (nuint)buffer.Length ) ) {
			return ValueTask.FromResult<string?>( null );
		}
		var path = Decode( buffer );
		const string prefix = "/dev/";
		return ValueTask.FromResult<string?>(
			path.StartsWith( prefix, StringComparison.Ordinal ) ? path[prefix.Length..] : path
		);
	}

	private static string? ResolveDefaultPath() {
		if ( File.Exists( "/var/run/utmp" ) ) return "/var/run/utmp";
		if ( File.Exists( "/run/utmp" ) ) return "/run/utmp";
		return null;
	}

	private static async Task<int> ReadRecordAsync( Stream stream, byte[] buffer, CancellationToken cancellationToken ) {
		var total = 0;
		while ( total < buffer.Length ) {
			var count = await stream.ReadAsync( buffer.AsMemory( total ), cancellationToken ).ConfigureAwait( false );
			if ( 0 == count ) break;
			total += count;
		}
		return total;
	}

	private static LoginRecord Parse( byte[] bytes ) {
		var seconds = ReadInt32( bytes, 340 );
		var microseconds = ReadInt32( bytes, 344 );
		DateTimeOffset timestamp;
		try {
			timestamp = DateTimeOffset.FromUnixTimeSeconds( seconds ).AddTicks( microseconds * 10L );
		} catch ( ArgumentOutOfRangeException ) {
			timestamp = DateTimeOffset.UnixEpoch;
		}
		return new LoginRecord(
			(LoginRecordType)ReadInt16( bytes, 0 ),
			ReadInt32( bytes, 4 ),
			Decode( bytes, 8, 32 ),
			Decode( bytes, 40, 4 ),
			Decode( bytes, 44, 32 ),
			Decode( bytes, 76, 256 ),
			ReadInt16( bytes, 332 ),
			ReadInt16( bytes, 334 ),
			ReadInt32( bytes, 336 ),
			timestamp,
			ReadAddress( bytes )
		);
	}

	private static IPAddress? ReadAddress( byte[] bytes ) {
		var address = bytes.AsSpan( 348, 16 ).ToArray();
		if ( address.All( value => 0 == value ) ) return null;
		if ( address.AsSpan( 4 ).ToArray().All( value => 0 == value ) ) {
			return new IPAddress( address.AsSpan( 0, 4 ) );
		}
		return new IPAddress( address );
	}

	private static short ReadInt16( byte[] bytes, int offset ) {
		var value = BitConverter.ToInt16( bytes, offset );
		return value;
	}

	private static int ReadInt32( byte[] bytes, int offset ) {
		var value = BitConverter.ToInt32( bytes, offset );
		return value;
	}

	private static string Decode( byte[] bytes ) => Decode( bytes, 0, bytes.Length );
	private static string Decode( byte[] bytes, int offset, int count ) {
		var span = bytes.AsSpan( offset, count );
		var zero = span.IndexOf( (byte)0 );
		if ( 0 <= zero ) span = span[..zero];
		return Encoding.UTF8.GetString( span ).TrimEnd();
	}

	private static class NativeMethods {
		/// <summary>
		/// Gets terminal name.
		/// </summary>
		[DllImport( "libc", EntryPoint = "ttyname_r" )]
		internal static extern int GetTerminalName( int fileDescriptor, [Out] byte[] buffer, nuint bufferSize );
	}
}
