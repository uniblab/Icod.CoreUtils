namespace Icod.Tar;

using System;
using System.IO;
using System.Linq;
using System.Formats.Tar;

/// <summary>
/// tar: create (-c) or extract (-x) archives. Minimal wrapper around System.Formats.Tar.
/// Usage:
///   tar -c -f archive.tar file1 file2 ...
///   tar -x -f archive.tar
/// Notes:
/// - Uses concrete UstarTarEntry factory to create entries (avoid instantiating abstract TarEntry).
/// - Uses TarWriter(Stream) overloads available on older/newer runtimes.
/// - Extraction uses the entry.DataStream fallback which is broadly available.
/// </summary>
public static class Command {
	public static int Run( string[] args, TextReader? stdin = null, TextWriter? stdout = null, TextWriter? stderr = null ) {
		stdout ??= Console.Out;
		stderr ??= Console.Error;

		if ( args.Length < 3 ) {
			stderr.WriteLine( "Usage: tar -c|-x -f <archive> [files...]" );
			return 2;
		}

		var mode = args[ 0 ];
		var flag = args[ 1 ];
		var archive = args[ 2 ];

		if ( mode == "-c" ) {
			var files = args.Skip( 3 ).ToArray();
			try {
				using var fs = File.Open( archive, FileMode.Create, FileAccess.Write );
				using var writer = new TarWriter( fs, leaveOpen: false );

				foreach ( var file in files ) {
					if ( Directory.Exists( file ) ) {
						// create directory entry
						var dirEntry = new UstarTarEntry( TarEntryType.Directory, file );
						// option: normalize name to base name if needed:
						// dirEntry.Name = Path.GetFileName(file);
						writer.WriteEntry( dirEntry );
						// write contained files recursively
						foreach ( var path in Directory.EnumerateFiles( file, "*", SearchOption.AllDirectories ) ) {
							var entry = new UstarTarEntry( TarEntryType.Directory, path );
							writer.WriteEntry( entry );
						}
					} else if ( File.Exists( file ) ) {
						var entry = new UstarTarEntry( TarEntryType.Directory, file );
						writer.WriteEntry( entry );
					} else {
						// skip non-existent paths (GNU tar prints warning)
						stderr.WriteLine( $"tar: {file}: Cannot stat: No such file or directory" );
					}
				}
			} catch ( Exception ex ) {
				stderr.WriteLine( $"tar: {ex.Message}" );
				return 1;
			}

			return 0;
		} else if ( mode == "-x" ) {
			try {
				using var fs = File.OpenRead( archive );
				using var reader = new TarReader( fs, leaveOpen: false );
				TarEntry? entry;
				while ( ( entry = reader.GetNextEntry() ) != null ) {
					var entryName = entry.Name ?? string.Empty;

					if ( entry.EntryType == TarEntryType.Directory ) {
						if ( !string.IsNullOrEmpty( entryName ) ) {
							Directory.CreateDirectory( entryName );
						}

						continue;
					}

					if ( entry.EntryType == TarEntryType.RegularFile ) {
						var outPath = entryName;
						var dir = Path.GetDirectoryName( outPath );
						if ( !string.IsNullOrEmpty( dir ) ) {
							Directory.CreateDirectory( dir );
						}

						using var outFs = File.Create( outPath );
						// Use DataStream on the entry which is widely available
						entry.DataStream?.CopyTo( outFs );
					} else {
						// For other entry types (symlink, etc.) best-effort: create directories or skip.
						if ( entry.EntryType == TarEntryType.SymbolicLink && !string.IsNullOrEmpty( entryName ) ) {
							// On Windows creating symlinks may require privileges; skip with a warning.
							stderr.WriteLine( $"tar: warning: skipping symbolic link {entryName}" );
						}
					}
				}
			} catch ( Exception ex ) {
				stderr.WriteLine( $"tar: {ex.Message}" );
				return 1;
			}

			return 0;
		} else {
			stderr.WriteLine( "tar: unknown mode, use -c or -x" );
			return 2;
		}
	}
}
