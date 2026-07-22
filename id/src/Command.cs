// Ported to .NET by Timothy J. Bruce <uniblab@hotmail.com>

namespace Icod.CoreUtils.Id;

using System;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Text;

/// <summary>
/// id: print user and group information (best-effort).
/// On Windows prints the user name and SID. On Unix-like systems prints numeric uid/gid and username.
/// Supported options:
///   -u    print only the effective user id
///   -g    print only the effective group id
///   -n    with -u or -g print the name instead of the numeric id
///   -?    display help
/// </summary>
public static class Command {
	[DllImport( "libc", SetLastError = true )]
	private static extern uint getuid();

	[DllImport( "libc", SetLastError = true )]
	private static extern uint geteuid();

	[DllImport( "libc", SetLastError = true )]
	private static extern uint getgid();

	[DllImport( "libc", SetLastError = true )]
	private static extern uint getegid();

	public static int Run( string[] args, TextReader? stdin = null, TextWriter? stdout = null, TextWriter? stderr = null ) {
		stdout ??= Console.Out;
		stderr ??= Console.Error;

		var printUid = false;
		var printGid = false;
		var name = false;

		for ( var i = 0; i < args.Length; i++ ) {
			var a = args[ i ];
			switch ( a ) {
				case "-u":
					printUid = true;
					break;
				case "-g":
					printGid = true;
					break;
				case "-n":
					name = true;
					break;
				case "-?":
				case "--help":
					PrintUsage( stdout );
					return 0;
				default:
					// ignore other args
					break;
			}
		}

		try {
			if ( RuntimeInformation.IsOSPlatform( System.Runtime.InteropServices.OSPlatform.Windows ) ) {
				var wi = WindowsIdentity.GetCurrent();
				if ( wi is null ) {
					stderr.WriteLine( "id: unable to determine identity" );
					return 1;
				}

				if ( printUid && !printGid ) {
					if ( name ) {
						stdout.WriteLine( wi.Name );
					} else {
						stdout.WriteLine( wi.User?.Value );
					}
					return 0;
				}

				var sb = new StringBuilder();
				sb.Append( wi.Name );
				if ( wi.User is not null )
					sb.Append( $" ({wi.User.Value})" );
				stdout.WriteLine( sb.ToString() );
				return 0;
			} else {
				// Unix-like platform: use libc to obtain numeric ids
				var euid = geteuid();
				var egid = getegid();

				if ( printUid && !printGid ) {
					if ( name ) {
						// best-effort: map uid to name via environment or /etc/passwd is not implemented
						stdout.WriteLine( Environment.UserName );
					} else {
						stdout.WriteLine( euid );
					}
					return 0;
				}

				if ( printGid && !printUid ) {
					if ( name ) {
						// group name resolution not implemented
						stdout.WriteLine( egid );
					} else {
						stdout.WriteLine( egid );
					}
					return 0;
				}

				// default: print uid, gid and username
				stdout.WriteLine( $"uid={euid}({Environment.UserName}) gid={egid}" );
				return 0;
			}
		} catch ( Exception ex ) {
			stderr.WriteLine( $"id: {ex.Message}" );
			return 1;
		}
	}

	private static void PrintUsage( TextWriter stdout ) {
		stdout.WriteLine( "Usage: id [OPTION]" );
		stdout.WriteLine( "  -u    print only the effective user id" );
		stdout.WriteLine( "  -g    print only the effective group id" );
		stdout.WriteLine( "  -n    with -u or -g print the name instead of the numeric id" );
		stdout.WriteLine( "  -?, --help    display this help and exit" );
	}
}
