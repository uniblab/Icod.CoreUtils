namespace Icod.CoreUtils.Install;

using System.ComponentModel;
using System.Diagnostics;

/// <summary>Runs the explicitly selected binary stripping program without invoking a command shell.</summary>
internal static class InstallStripper {
	/// <summary>Strips one staged file.</summary>
	/// <param name="program">The stripping executable.</param>
	/// <param name="path">The staged pathname.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>A task representing the operation.</returns>
	public static async ValueTask StripAsync(
		string program,
		string path,
		CancellationToken cancellationToken = default
	) {
		ArgumentException.ThrowIfNullOrWhiteSpace( program );
		ArgumentException.ThrowIfNullOrWhiteSpace( path );
		var startInfo = new ProcessStartInfo {
			FileName = program,
			UseShellExecute = false,
			RedirectStandardOutput = true,
			RedirectStandardError = true,
			CreateNoWindow = true
		};
		startInfo.ArgumentList.Add( path );
		using var process = new Process { StartInfo = startInfo };
		try {
			if ( !process.Start() ) throw new IOException( string.Concat( "could not start strip program '", program, "'" ) );
		} catch ( Exception exception ) when ( exception is InvalidOperationException or Win32Exception ) {
			throw new IOException( string.Concat( "could not run strip program '", program, "': ", exception.Message ), exception );
		}
		var standardOutput = process.StandardOutput.ReadToEndAsync( cancellationToken );
		var standardError = process.StandardError.ReadToEndAsync( cancellationToken );
		try {
			await process.WaitForExitAsync( cancellationToken ).ConfigureAwait( false );
		} catch ( OperationCanceledException ) when ( cancellationToken.IsCancellationRequested ) {
			try {
				if ( !process.HasExited ) process.Kill( entireProcessTree: true );
			} catch ( InvalidOperationException ) {
			} catch ( Win32Exception ) {
			}
			throw;
		}
		var output = await standardOutput.ConfigureAwait( false );
		var error = await standardError.ConfigureAwait( false );
		if ( process.ExitCode != 0 ) {
			var detail = string.IsNullOrWhiteSpace( error ) ? output : error;
			throw new IOException(
				string.IsNullOrWhiteSpace( detail )
					? string.Concat( "strip program exited with status ", process.ExitCode, "." )
					: detail.Trim()
			);
		}
	}
}
