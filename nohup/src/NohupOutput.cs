namespace Icod.CoreUtils.Nohup;

/// <summary>
/// Represents one opened append-only <c>nohup</c> output destination.
/// </summary>
public sealed class NohupOutputDestination : IDisposable, IAsyncDisposable {
	/// <summary>Gets the opened output stream.</summary>
	public Stream Stream { get; }

	/// <summary>Gets the display path used for diagnostics.</summary>
	public string Path { get; }

	/// <summary>Gets the open POSIX descriptor when the destination is a native file stream.</summary>
	internal int? PosixFileDescriptor { get; }

	/// <summary>Initializes a destination.</summary>
	public NohupOutputDestination( string path, Stream stream ) {
		ArgumentException.ThrowIfNullOrWhiteSpace( path );
		ArgumentNullException.ThrowIfNull( stream );
		this.Path = path;
		this.Stream = stream;
		this.PosixFileDescriptor = !OperatingSystem.IsWindows()
			&& stream is FileStream fileStream
			&& !fileStream.SafeFileHandle.IsInvalid
				? fileStream.SafeFileHandle.DangerousGetHandle().ToInt32()
				: null
		;
	}

	/// <inheritdoc />
	public void Dispose() => this.Stream.Dispose();

	/// <inheritdoc />
	public ValueTask DisposeAsync() => this.Stream.DisposeAsync();
}

/// <summary>
/// Opens append destinations used by GNU <c>nohup</c>.
/// </summary>
public interface INohupOutputFileProvider {
	/// <summary>Attempts to open or create a file for append.</summary>
	NohupOutputDestination OpenAppend( string path );
}

/// <summary>
/// Reports whether inherited standard output is closed and, on descriptor-based hosts, reserves its slot while an alternate file is opened.
/// </summary>
public interface INohupStandardStreamStateProvider {
	/// <summary>Gets whether the inherited standard-output endpoint is closed rather than redirected.</summary>
	bool IsStandardOutputClosed();

	/// <summary>Temporarily reserves a closed standard-output descriptor so another opened file cannot occupy it.</summary>
	IDisposable ReserveClosedStandardOutput();
}

/// <summary>
/// Observes the system standard-output endpoint for GNU <c>nohup</c> closed-descriptor handling.
/// </summary>
public sealed class SystemNohupStandardStreamStateProvider : INohupStandardStreamStateProvider {
	/// <summary>Gets the reusable system provider.</summary>
	public static SystemNohupStandardStreamStateProvider Instance { get; } = new();

	private SystemNohupStandardStreamStateProvider() { }

	/// <inheritdoc />
	public bool IsStandardOutputClosed() => NohupNative.IsStandardOutputClosed();

	/// <inheritdoc />
	public IDisposable ReserveClosedStandardOutput() => NohupNative.ReserveClosedStandardOutput();
}

/// <summary>
/// Opens <c>nohup.out</c> files through the host file system.
/// </summary>
public sealed class SystemNohupOutputFileProvider : INohupOutputFileProvider {
	/// <summary>Gets the reusable system provider.</summary>
	public static SystemNohupOutputFileProvider Instance { get; } = new();

	private SystemNohupOutputFileProvider() { }

	/// <inheritdoc />
	public NohupOutputDestination OpenAppend( string path ) {
		ArgumentException.ThrowIfNullOrWhiteSpace( path );
		var stream = OperatingSystem.IsWindows()
			? new FileStream( path, CreateAppendOptions() )
			: OpenPosixAppend( path )
		;
		return new NohupOutputDestination( path, stream );
	}

	[System.Runtime.Versioning.UnsupportedOSPlatform( "windows" )]
	private static FileStream OpenPosixAppend(
		string path
	) {
		FileStream stream;
		try {
			stream = new FileStream(
				path,
				new FileStreamOptions {
					Mode = FileMode.CreateNew,
					Access = FileAccess.Write,
					Share = FileShare.Read | FileShare.Write | FileShare.Delete,
					Options = FileOptions.Asynchronous,
					UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite
				}
			);
		} catch ( IOException ) {
			return new FileStream( path, CreateAppendOptions() );
		}
		try {
			NohupNative.ConfigureCreatedOutput( stream );
			return stream;
		} catch {
			stream.Dispose();
			throw;
		}
	}

	private static FileStreamOptions CreateAppendOptions() => new() {
		Mode = FileMode.Append,
		Access = FileAccess.Write,
		Share = FileShare.Read | FileShare.Write | FileShare.Delete,
		Options = FileOptions.Asynchronous
	};
}

/// <summary>
/// Applies the POSIX permission and append flags that GNU <c>nohup</c> requires for a newly created output file.
/// </summary>
internal static class NohupNative {
	private const int BadFileDescriptor = 9;
	private const int FileGetDescriptorFlags = 1;
	private const int FileGetFlags = 3;
	private const int FileSetFlags = 4;
	private const int UserReadWriteMode = 384;
	private const int OpenWriteOnly = 1;
	private const int StandardOutputHandle = -11;
	private static readonly IntPtr InvalidHandleValue = new( -1 );

	/// <summary>Gets whether process standard output is closed rather than redirected.</summary>
	internal static bool IsStandardOutputClosed() {
		if ( OperatingSystem.IsWindows() ) {
			var handle = GetStdHandle( StandardOutputHandle );
			return IntPtr.Zero == handle || InvalidHandleValue == handle;
		}
		var result = Fcntl( 1, FileGetDescriptorFlags, 0 );
		return 0 > result && BadFileDescriptor == System.Runtime.InteropServices.Marshal.GetLastPInvokeError();
	}

	/// <summary>Reserves descriptor 1 while a replacement stderr destination is opened, restoring its closed state on disposal.</summary>
	internal static IDisposable ReserveClosedStandardOutput() {
		if ( OperatingSystem.IsWindows() || !IsStandardOutputClosed() ) return NoopReservation.Instance;
		var descriptor = Open( "/dev/null", OpenWriteOnly );
		if ( 0 > descriptor ) {
			var error = System.Runtime.InteropServices.Marshal.GetLastPInvokeError();
			throw new IOException( $"Unable to reserve closed standard output (errno {error})." );
		}
		if ( 1 != descriptor ) {
			try {
				if ( 0 > Dup2( descriptor, 1 ) ) {
					var error = System.Runtime.InteropServices.Marshal.GetLastPInvokeError();
					throw new IOException( $"Unable to reserve closed standard output (errno {error})." );
				}
			} finally {
				_ = Close( descriptor );
			}
		}
		return new StandardOutputReservation();
	}

	/// <summary>Sets exact user read/write permissions and atomic append behavior on a newly created output stream.</summary>
	internal static void ConfigureCreatedOutput(
		FileStream stream
	) {
		ArgumentNullException.ThrowIfNull( stream );
		var descriptor = stream.SafeFileHandle.DangerousGetHandle().ToInt32();
		if ( 0 != FChMod( descriptor, UserReadWriteMode ) ) {
			var error = System.Runtime.InteropServices.Marshal.GetLastPInvokeError();
			throw new IOException( $"Unable to set nohup output permissions (errno {error})." );
		}
		var flags = Fcntl( descriptor, FileGetFlags, 0 );
		if ( 0 > flags ) {
			var error = System.Runtime.InteropServices.Marshal.GetLastPInvokeError();
			throw new IOException( $"Unable to read nohup output flags (errno {error})." );
		}
		var appendFlag = OperatingSystem.IsLinux() ? 0x0400 : 0x0008;
		if ( 0 > Fcntl( descriptor, FileSetFlags, flags | appendFlag ) ) {
			var error = System.Runtime.InteropServices.Marshal.GetLastPInvokeError();
			throw new IOException( $"Unable to enable append mode for nohup output (errno {error})." );
		}
	}

	/// <summary>Gets one Windows process standard handle.</summary>
	[System.Runtime.InteropServices.DllImport(
		"kernel32.dll",
		SetLastError = true
	)]
	private static extern IntPtr GetStdHandle(
		int standardHandle
	);

	/// <summary>Opens a POSIX path without creation flags.</summary>
	[System.Runtime.InteropServices.DllImport(
		"libc",
		EntryPoint = "open",
		SetLastError = true
	)]
	private static extern int Open(
		string path,
		int flags
	);

	/// <summary>Duplicates one POSIX descriptor onto another descriptor number.</summary>
	[System.Runtime.InteropServices.DllImport(
		"libc",
		EntryPoint = "dup2",
		SetLastError = true
	)]
	private static extern int Dup2(
		int sourceDescriptor,
		int destinationDescriptor
	);

	/// <summary>Closes one POSIX descriptor.</summary>
	[System.Runtime.InteropServices.DllImport(
		"libc",
		EntryPoint = "close",
		SetLastError = true
	)]
	private static extern int Close(
		int descriptor
	);

	/// <summary>Changes the mode of an already-open POSIX file descriptor.</summary>
	[System.Runtime.InteropServices.DllImport(
		"libc",
		EntryPoint = "fchmod",
		SetLastError = true
	)]
	private static extern int FChMod(
		int descriptor,
		int mode
	);

	/// <summary>Reads or changes POSIX file status flags.</summary>
	[System.Runtime.InteropServices.DllImport(
		"libc",
		EntryPoint = "fcntl",
		SetLastError = true
	)]
	private static extern int Fcntl(
		int descriptor,
		int command,
		int argument
	);

	private sealed class StandardOutputReservation : IDisposable {
		/// <inheritdoc />
		public void Dispose() => _ = Close( 1 );
	}

	private sealed class NoopReservation : IDisposable {
		internal static NoopReservation Instance { get; } = new();
		private NoopReservation() { }
		/// <inheritdoc />
		public void Dispose() { }
	}
}
