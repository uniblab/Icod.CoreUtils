// Original behavior/reference: GNU coreutils
// Ported to .NET by Timothy J. Bruce <uniblab@hotmail.com>

namespace Icod.CoreUtils.Cut;

using System;
using System.IO;
using System.Text;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// cut: remove sections from each line of files.
/// Supports:
///   -b list (byte positions) [not fully accurate on multi-byte encodings]
///   -c list (character positions)
///   -f list (fields) with -d delimiter (default TAB)
/// Only one of -b, -c, -f may be specified.
/// </summary>
public static class Command {
    public static int Run( string[] args, TextReader? stdin = null, TextWriter? stdout = null, TextWriter? stderr = null ) {
        stdout ??= Console.Out;
        stderr ??= Console.Error;

        var modeB = false;
        var modeC = false;
        var modeF = false;
        string? fieldSpec = null;
        var delimiter = "\t";
        var i = 0;
        for ( ; i < args.Length; i++ ) {
            if ( !args[ i ].StartsWith( '-' ) ) {
                break;
            }

            if ( args[ i ] == "-b" ) {
                modeB = true;
                if ( i + 1 < args.Length ) {
                    i++;
                    fieldSpec = args[ i ];
                }
            } else if ( args[ i ] == "-c" ) {
                modeC = true;
                if ( i + 1 < args.Length ) {
                    i++;
                    fieldSpec = args[ i ];
                }
            } else if ( args[ i ] == "-f" ) {
                modeF = true;
                if ( i + 1 < args.Length ) {
                    i++;
                    fieldSpec = args[ i ];
                }
            } else if ( args[ i ] == "-d" ) {
                if ( i + 1 < args.Length ) {
                    i++;
                    delimiter = args[ i ];
                }
            }
        }

        var rem = new List<string>();
        for ( ; i < args.Length; i++ ) {
            rem.Add( args[ i ] );
        }

        if ( !modeB && !modeC && !modeF ) {
            stderr.WriteLine( "cut: you must specify a mode: -b, -c, or -f" );
            return 1;
        }

        if ( string.IsNullOrEmpty( fieldSpec ) ) {
            stderr.WriteLine( "cut: missing list" );
            return 1;
        }

        var ranges = ParseList( fieldSpec );

        if ( rem.Count == 0 ) {
            return ProcessReader( "<stdin>", stdin ?? Console.In, stdout, stderr, modeB, modeC, modeF, delimiter, ranges );
        }

        var exit = 0;
        foreach ( var path in rem ) {
            try {
                using var sr = new StreamReader( path, Encoding.UTF8, detectEncodingFromByteOrderMarks: true );
                var rc = ProcessReader( path, sr, stdout, stderr, modeB, modeC, modeF, delimiter, ranges );
                if ( rc != 0 ) {
                    exit = rc;
                }
            } catch ( Exception ex ) {
                stderr.WriteLine( $"cut: {path}: {ex.Message}" );
                exit = 1;
            }
        }

        return exit;
    }

    private static int ProcessReader( string sourceName, TextReader reader, TextWriter stdout, TextWriter stderr, bool modeB, bool modeC, bool modeF, string delimiter, List<(int start, int end)> ranges ) {
        string? line;
        while ( ( line = reader.ReadLine() ) is not null ) {
            if ( modeF ) {
                var parts = line.Split( new[] { delimiter }, StringSplitOptions.None );
                var sel = new List<string>();
                for ( var i = 0; i < ranges.Count; i++ ) {
                    var r = ranges[ i ];
                    for ( var f = r.start; f <= r.end && f - 1 < parts.Length; f++ ) {
                        if ( f - 1 >= 0 && f - 1 < parts.Length ) {
                            sel.Add( parts[ f - 1 ] );
                        }
                    }
                }

                stdout.WriteLine( string.Join( delimiter, sel ) );
            } else if ( modeC ) {
                var chars = line.ToCharArray();
                var sb = new StringBuilder();
                for ( var i = 0; i < ranges.Count; i++ ) {
                    var r = ranges[ i ];
                    for ( var p = r.start; p <= r.end && p - 1 < chars.Length; p++ ) {
                        if ( p - 1 >= 0 && p - 1 < chars.Length ) {
                            sb.Append( chars[ p - 1 ] );
                        }
                    }
                }

                stdout.WriteLine( sb.ToString() );
            } else if ( modeB ) {
                // Note: This is not accurate for multi-byte encodings; treat bytes on UTF-8 bytes.
                var bytes = Encoding.UTF8.GetBytes( line );
                var outBuf = new MemoryStream();
                for ( var i = 0; i < ranges.Count; i++ ) {
                    var r = ranges[ i ];
                    for ( var p = r.start; p <= r.end && p - 1 < bytes.Length; p++ ) {
                        if ( p - 1 >= 0 && p - 1 < bytes.Length ) {
                            outBuf.WriteByte( bytes[ p - 1 ] );
                        }
                    }
                }

                stdout.WriteLine( Encoding.UTF8.GetString( outBuf.ToArray() ) );
            }
        }

        return 0;
    }

    private static List<(int start, int end)> ParseList( string spec ) {
        var list = new List<(int start, int end)>();
        var parts = spec.Split( ',' );
        foreach ( var part in parts ) {
            if ( part.Contains( '-' ) ) {
                var pe = part.Split( '-', 2 );
                var a = string.IsNullOrEmpty( pe[ 0 ] ) ? 1 : int.Parse( pe[ 0 ], System.Globalization.NumberStyles.Integer );
                var b = string.IsNullOrEmpty( pe[ 1 ] ) ? int.MaxValue : int.Parse( pe[ 1 ], System.Globalization.NumberStyles.Integer );
                list.Add( (a, b) );
            } else {
                var v = int.Parse( part, System.Globalization.NumberStyles.Integer );
                list.Add( (v, v) );
            }
        }

        return list;
    }
}
