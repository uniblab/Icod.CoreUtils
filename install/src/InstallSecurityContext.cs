namespace Icod.CoreUtils.Install;

using System.ComponentModel;
using System.Runtime.InteropServices;

/// <summary>Provides the SELinux-context operations required by <c>install</c>.</summary>
internal interface IInstallSecurityContextProvider {
	/// <summary>Gets whether SELinux labeling is enabled and available.</summary>
	bool IsEnabled { get; }

	/// <summary>Reads the SELinux context attached to a pathname.</summary>
	/// <param name="path">The pathname to inspect.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>The context, or <see langword="null"/> when SELinux is unavailable or the path is unlabeled.</returns>
	ValueTask<string?> GetContextAsync( string path, CancellationToken cancellationToken = default );

	/// <summary>Resolves the SELinux policy-default context for a destination pathname.</summary>
	/// <param name="destinationPath">The final destination pathname.</param>
	/// <param name="targetIsDirectory">Whether the destination is a directory.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>The default context, or <see langword="null"/> when SELinux is unavailable or policy selects no label.</returns>
	ValueTask<string?> GetDefaultContextAsync(
		string destinationPath,
		bool targetIsDirectory,
		CancellationToken cancellationToken = default
	);

	/// <summary>Applies the requested context policy to a private staged file or directory.</summary>
	/// <param name="sourcePath">The source pathname when source context is preserved.</param>
	/// <param name="destinationPath">The final destination pathname used for policy lookup.</param>
	/// <param name="stagingPath">The private stage or directory to label.</param>
	/// <param name="preserveSourceContext">Whether the source context is preserved.</param>
	/// <param name="destinationDefaultContext">Whether policy-default destination labeling is requested.</param>
	/// <param name="explicitContext">An explicit context, when supplied.</param>
	/// <param name="destinationExisted">Whether the final destination existed before this operation.</param>
	/// <param name="targetIsDirectory">Whether the target object is a directory.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>A task representing the operation.</returns>
	ValueTask ApplyAsync(
		string? sourcePath,
		string destinationPath,
		string stagingPath,
		bool preserveSourceContext,
		bool destinationDefaultContext,
		string? explicitContext,
		bool destinationExisted,
		bool targetIsDirectory,
		CancellationToken cancellationToken = default
	);
}

/// <summary>Implements SELinux labeling through <c>libselinux</c> without invoking host utilities.</summary>
internal sealed class SystemInstallSecurityContextProvider : IInstallSecurityContextProvider {
	private const int NoData = 61;
	private const uint DirectoryMode = 0x4000;
	private const uint RegularFileMode = 0x8000;

	/// <summary>Gets the shared implementation.</summary>
	public static SystemInstallSecurityContextProvider Instance { get; } = new();

	private SystemInstallSecurityContextProvider() {
	}

	/// <inheritdoc/>
	public bool IsEnabled => IsSelinuxEnabled();

	/// <inheritdoc/>
	public ValueTask<string?> GetContextAsync( string path, CancellationToken cancellationToken = default ) {
		ArgumentException.ThrowIfNullOrWhiteSpace( path );
		cancellationToken.ThrowIfCancellationRequested();
		if ( !IsSelinuxEnabled() ) return ValueTask.FromResult<string?>( null );
		return ValueTask.FromResult( ReadContext( path ) );
	}

	/// <inheritdoc/>
	public ValueTask<string?> GetDefaultContextAsync(
		string destinationPath,
		bool targetIsDirectory,
		CancellationToken cancellationToken = default
	) {
		ArgumentException.ThrowIfNullOrWhiteSpace( destinationPath );
		cancellationToken.ThrowIfCancellationRequested();
		if ( !IsSelinuxEnabled() ) return ValueTask.FromResult<string?>( null );
		return ValueTask.FromResult( MatchContext( destinationPath, targetIsDirectory ) );
	}

	/// <inheritdoc/>
	public async ValueTask ApplyAsync(
		string? sourcePath,
		string destinationPath,
		string stagingPath,
		bool preserveSourceContext,
		bool destinationDefaultContext,
		string? explicitContext,
		bool destinationExisted,
		bool targetIsDirectory,
		CancellationToken cancellationToken = default
	) {
		ArgumentException.ThrowIfNullOrWhiteSpace( destinationPath );
		ArgumentException.ThrowIfNullOrWhiteSpace( stagingPath );
		cancellationToken.ThrowIfCancellationRequested();
		if ( !IsSelinuxEnabled() ) return;

		string? requestedContext;
		if ( preserveSourceContext ) {
			ArgumentException.ThrowIfNullOrWhiteSpace( sourcePath );
			requestedContext = await GetContextAsync( sourcePath, cancellationToken ).ConfigureAwait( false );
		} else if ( destinationDefaultContext && explicitContext is null ) {
			requestedContext = await GetDefaultContextAsync(
				destinationPath,
				targetIsDirectory,
				cancellationToken
			).ConfigureAwait( false );
		} else if ( explicitContext is not null && !destinationExisted ) {
			requestedContext = explicitContext;
		} else if ( destinationExisted ) {
			requestedContext = await GetContextAsync( destinationPath, cancellationToken ).ConfigureAwait( false );
		} else {
			return;
		}

		if ( requestedContext is null ) RemoveContext( stagingPath );
		else SetContext( stagingPath, requestedContext );
	}

	private static bool IsSelinuxEnabled() {
		if ( !OperatingSystem.IsLinux() ) return false;
		try {
			return NativeMethods.IsSelinuxEnabled() > 0;
		} catch ( DllNotFoundException ) {
			return false;
		} catch ( EntryPointNotFoundException ) {
			return false;
		}
	}

	private static string? ReadContext( string path ) {
		IntPtr context = IntPtr.Zero;
		try {
			var length = NativeMethods.GetFileContext( path, out context );
			if ( length < 0 ) {
				var error = Marshal.GetLastPInvokeError();
				if ( error == NoData ) return null;
				throw CreateIOException( "getfilecon", path, error );
			}
			return DecodeContext( context );
		} finally {
			if ( context != IntPtr.Zero ) NativeMethods.FreeContext( context );
		}
	}

	private static string? MatchContext( string path, bool isDirectory ) {
		IntPtr context = IntPtr.Zero;
		try {
			var result = NativeMethods.MatchPathContext(
				path,
				isDirectory ? DirectoryMode : RegularFileMode,
				out context
			);
			if ( result != 0 ) throw CreateIOException( "matchpathcon", path, Marshal.GetLastPInvokeError() );
			var value = DecodeContext( context );
			return string.Equals( value, "<<none>>", StringComparison.Ordinal ) ? null : value;
		} finally {
			if ( context != IntPtr.Zero ) NativeMethods.FreeContext( context );
		}
	}

	private static string? DecodeContext( IntPtr context ) {
		if ( context == IntPtr.Zero ) return null;
		var value = Marshal.PtrToStringUTF8( context );
		return string.IsNullOrEmpty( value ) ? null : value;
	}

	private static void SetContext( string path, string context ) {
		if ( NativeMethods.SetFileContext( path, context ) != 0 ) {
			throw CreateIOException( "setfilecon", path, Marshal.GetLastPInvokeError() );
		}
	}

	private static void RemoveContext( string path ) {
		if ( NativeMethods.RemoveExtendedAttribute( path, "security.selinux" ) == 0 ) return;
		var error = Marshal.GetLastPInvokeError();
		if ( error != NoData ) throw CreateIOException( "removexattr", path, error );
	}

	private static IOException CreateIOException( string operation, string path, int error ) {
		return new IOException(
			string.Concat( operation, " failed for '", path, "': ", new Win32Exception( error ).Message )
		);
	}

	private static class NativeMethods {
		private const string SelinuxLibrary = "libselinux.so.1";

		[DllImport( SelinuxLibrary, EntryPoint = "is_selinux_enabled", SetLastError = true )]
		public static extern int IsSelinuxEnabled();

		[DllImport( SelinuxLibrary, EntryPoint = "getfilecon", SetLastError = true, CharSet = CharSet.Ansi )]
		public static extern int GetFileContext( string path, out IntPtr context );

		[DllImport( SelinuxLibrary, EntryPoint = "setfilecon", SetLastError = true, CharSet = CharSet.Ansi )]
		public static extern int SetFileContext( string path, string context );

		[DllImport( SelinuxLibrary, EntryPoint = "matchpathcon", SetLastError = true, CharSet = CharSet.Ansi )]
		public static extern int MatchPathContext( string path, uint mode, out IntPtr context );

		[DllImport( SelinuxLibrary, EntryPoint = "freecon", SetLastError = false )]
		public static extern void FreeContext( IntPtr context );

		[DllImport( "libc", EntryPoint = "removexattr", SetLastError = true, CharSet = CharSet.Ansi )]
		public static extern int RemoveExtendedAttribute( string path, string name );
	}
}
