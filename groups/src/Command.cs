// Original behavior/reference: GNU coreutils
// Credit: David MacKenzie, James Youngman, Arnold Robbins
// Ported to .NET by Timothy J. Bruce <uniblab@hotmail.com>

namespace Icod.CoreUtils.Groups;

using System;
using System.Collections.Generic;
using System.ComponentModel.Design;
using System.DirectoryServices.AccountManagement;
using System.Globalization;
using System.IO;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Security.Principal;

/// <summary>
/// groups: print group membership for a user (best-effort).
/// On Windows, attempts to list WindowsIdentity groups. On Unix-like platforms
/// best-effort fallback prints the current user as the sole group.
/// Options:
///   -l	print each group on its own line
///   -?	display this help/usage text
/// </summary>
public static class Command {

	private const System.Char SPACE = ' ';
	private const System.Char TAB = '\t';
	private const System.Char LF = '\n';
	private static readonly System.Char[] SPACE_TAB_LF;

	static Command() => SPACE_TAB_LF = new[] { SPACE, TAB, LF };

	/// <summary>
	/// Run the groups command.
	/// Arguments:
	///   -l	print one group per line
	///   -?	display usage
	///   [user] optional username (defaults to current user)
	/// </summary>
	public static int Run( string[] args, TextReader? stdin = null, TextWriter? stdout = null, TextWriter? stderr = null ) {
		stdout ??= Console.Out;
		stderr ??= Console.Error;

		var onePerLine = false;
		var showUsage = false;
		string? user = Environment.UserName;

		// parse options; first non-option argument is taken as username
		for ( var i = 0; i < args.Length; i++ ) {
			var a = args[ i ];
			if ( a == "-l" ) {
				onePerLine = true;
				continue;
			}

			if ( a == "-?" ) {
				showUsage = true;
				continue;
			}

			if ( a.StartsWith( '-' ) ) {
				// unknown option: ignore to preserve original behavior
				continue;
			}

			// first non-option argument is username
			user = a;
			break;
		}

		if ( showUsage ) {
			PrintUsage( stdout );
			return 0;
		}

		try {
			var groups = GetGroupsForUser( user );
			if ( onePerLine ) {
				foreach ( var g in groups ) {
					stdout.WriteLine( g );
				}
			} else {
				stdout.WriteLine( string.Join( " ", groups ) );
			}

			return 0;
		} catch ( NotImplementedException ) {
			throw;
		} catch ( Exception ex ) {
			stderr.WriteLine( $"groups: {ex.Message}" );
			return 1;
		}
	}

	private static void PrintUsage( TextWriter stdout ) {
		stdout.WriteLine( "Usage: groups [-l] [user]" );
		stdout.WriteLine( "  -l	print one group per line" );
		stdout.WriteLine( "  -?	display this help and exit" );
	}

	private static IEnumerable<string> GetGroupsForUser( string user ) {
		if ( RuntimeInformation.IsOSPlatform( OSPlatform.Windows ) ) {
			try {
				using ( var context = new PrincipalContext( ContextType.Machine ) ) {
					var userPrincipal = UserPrincipal.FindByIdentity( context, user );
					if ( userPrincipal is null ) {
						throw new Exception( $"user '{user}' not found" );
					}
					var groups = userPrincipal.GetAuthorizationGroups();
					var names = new List<string>();
					foreach ( var group in groups ) {
						try {
							var name = group.DisplayName ?? group.Name;
							var val = Csv( name );
							names.Add( val );
						} catch {
							// ignore individual failures
						}
					}
					if ( names.Count == 0 ) {
						names.Add( user );
					}
					return names;
				}
			} catch ( Exception ex ) {
				throw new Exception( $"unable to enumerate groups: {ex.Message}" );
			}
		} else {
			// On Unix-like systems, retrieving groups reliably requires interop or reading /etc/group.
			// For a BCL-only portable implementation, provide a minimal fallback.
			// If /etc/group exists, attempt to parse groups that contain the username.
			try {
				var result = new List<string>();
				var etc = "/etc/group";
				if ( File.Exists( etc ) ) {
					foreach ( var line in File.ReadLines( etc ) ) {
						if ( string.IsNullOrWhiteSpace( line ) ) {
							continue;
						}

						var parts = line.Split( ':' );
						if ( parts.Length < 4 ) {
							continue;
						}

						var members = parts[ 3 ].Split( ',', StringSplitOptions.RemoveEmptyEntries );
						foreach ( var m in members ) {
							if ( m == user ) {
								result.Add( parts[ 0 ] );
								break;
							}
						}
					}

					if ( result.Count > 0 ) {
						return result;
					}
				}

				// fallback: return the username as single group
				return new[] { user };
			} catch {
				return new[] { user };
			}
		}
	}
	private static string Csv( object? value ) {
		string text = value switch {
			null => "",
			bool boolean => boolean ? "true" : "false",
			IFormattable formattable =>
				formattable.ToString(
					null,
					CultureInfo.InvariantCulture
				) ?? "",
			_ => value.ToString() ?? ""
		};
		if ( text.Contains( '"' ) ) {
			text = text.Replace( "\"", "\"\"" );
		}

		return 0 <= text.IndexOfAny( SPACE_TAB_LF )
			? $"\"{text}\""
			: text
		;
	}

}
