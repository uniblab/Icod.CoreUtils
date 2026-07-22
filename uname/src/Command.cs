// Original behavior/reference: GNU coreutils
// Ported to .NET by Timothy J. Bruce <uniblab@hotmail.com>

namespace Icod.CoreUtils.Uname;

using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

/// <summary>
/// uname: print system information (best-effort).
/// Supported options:
///   -a  all (equivalent to: -s -n -r -v -m)
///   -s  kernel name
///   -n  nodename
///   -r  kernel release
///   -v  kernel version
///   -m  machine
/// </summary>
public static partial class Command {
	public static int Run( string[] args, TextReader? stdin = null, TextWriter? stdout = null, TextWriter? stderr = null ) {
		stdin ??= Console.In;
		stdout ??= Console.Out;
		stderr ??= Console.Error;

		var showAll = false;
		var showS = false;
		var showN = false;
		var showR = false;
		var showV = false;
		var showM = false;

		if ( args.Length == 0 ) {
			showS = true;
		} else {
			foreach ( var a in args ) {
				if ( a == "-a" ) {
					showAll = true;
				} else if ( a == "-s" ) {
					showS = true;
				} else if ( a == "-n" ) {
					showN = true;
				} else if ( a == "-r" ) {
					showR = true;
				} else if ( a == "-v" ) {
					showV = true;
				} else if ( a == "-m" ) {
					showM = true;
				} else {
					// ignore unknown
				}
			}
		}

		if ( showAll ) {
			showS = true;
			showN = true;
			showR = true;
			showV = true;
			showM = true;
		}

		try {
			var parts = new System.Collections.Generic.List<string>();
			if ( showS ) {
				parts.Add( GetKernelName() );
			}

			if ( showN ) {
				parts.Add( Environment.MachineName );
			}

			if ( showR ) {
				parts.Add( GetKernelRelease() );
			}

			if ( showV ) {
				parts.Add( GetKernelVersion() );
			}

			if ( showM ) {
				parts.Add( RuntimeInformation.OSArchitecture.ToString() );
			}

			stdout.WriteLine( string.Join( " ", parts ) );
			return 0;
		} catch ( Exception ex ) {
			stderr.WriteLine( $"uname: {ex.Message}" );
			return 1;
		}
	}

	private static string GetKernelName() {
		if ( RuntimeInformation.IsOSPlatform( OSPlatform.Windows ) ) {
			return "Windows";
		}

		if ( RuntimeInformation.IsOSPlatform( OSPlatform.Linux ) ) {
			return "Linux";
		}

		if ( RuntimeInformation.IsOSPlatform( OSPlatform.OSX ) ) {
			return "Darwin";
		}

		return RuntimeInformation.OSDescription;
	}

	private static string GetKernelRelease() {
		try {
			return RuntimeInformation.OSDescription;
		} catch {
			return "";
		}
	}

	private static string GetKernelVersion() {
		try {
			return Environment.OSVersion.VersionString;
		} catch {
			return "";
		}
	}
}
