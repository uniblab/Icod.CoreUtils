// Original behavior/reference: GNU coreutils 9.11
// Ported to .NET by Timothy J. Bruce <uniblab@hotmail.com>

namespace Icod.CoreUtils.ChRoot;

using System.Globalization;
using System.Runtime.InteropServices;

/// <summary>Describes the host operations required to perform <c>chroot</c>.</summary>
public interface IChrootPlatform {
	/// <summary>Gets whether the host supports the required root-changing operations.</summary>
	bool IsSupported { get; }
	/// <summary>Gets the diagnostic used when the platform is unsupported.</summary>
	string UnsupportedReason { get; }
	/// <summary>Returns whether <paramref name="path"/> resolves to the process's current root.</summary>
	bool IsCurrentRoot( string path );
	/// <summary>Changes root, applies requested credentials, and replaces the current process with the command.</summary>
	ValueTask<ChrootExecutionResult> ExecuteAsync( ChrootExecutionRequest request, CancellationToken cancellationToken = default );
}

/// <summary>Represents one root-changing execution request.</summary>
public sealed class ChrootExecutionRequest {
	/// <summary>Initializes a request.</summary>
	public ChrootExecutionRequest(
		string rootDirectory,
		IReadOnlyList<string> command,
		string? userSpec = null,
		string? groupsSpec = null,
		bool skipChdir = false
	) {
		ArgumentException.ThrowIfNullOrEmpty( rootDirectory );
		ArgumentNullException.ThrowIfNull( command );
		if ( 0 == command.Count ) {
			throw new ArgumentException( "A command is required.", nameof( command ) );
		}
		for ( var index = 0; index < command.Count; index++ ) {
			if ( null == command[ index ] ) {
				throw new ArgumentException( "Command arguments cannot contain null values.", nameof( command ) );
			}
		}
		RootDirectory = rootDirectory;
		Command = command.ToArray();
		UserSpec = userSpec;
		GroupsSpec = groupsSpec;
		SkipChdir = skipChdir;
	}
	/// <summary>Gets the new root directory.</summary>
	public string RootDirectory { get; }
	/// <summary>Gets the exact command and argument vector.</summary>
	public IReadOnlyList<string> Command { get; }
	/// <summary>Gets the optional user/group specification.</summary>
	public string? UserSpec { get; }
	/// <summary>Gets the optional supplementary-group list.</summary>
	public string? GroupsSpec { get; }
	/// <summary>Gets whether changing the working directory to <c>/</c> is suppressed.</summary>
	public bool SkipChdir { get; }
}

/// <summary>Represents a platform execution result. Successful <c>execvp</c> never returns this value.</summary>
public sealed class ChrootExecutionResult {
	/// <summary>Initializes a result.</summary>
	public ChrootExecutionResult( int exitCode, string? diagnostic = null ) {
		ExitCode = exitCode;
		Diagnostic = diagnostic;
	}
	/// <summary>Gets the exit status.</summary>
	public int ExitCode { get; }
	/// <summary>Gets an optional diagnostic.</summary>
	public string? Diagnostic { get; }
}

/// <summary>Provides the native Unix implementation of <c>chroot</c>.</summary>
public sealed class SystemChrootPlatform : IChrootPlatform {
	private const int InternalFailure = 125;
	private const int CannotInvoke = 126;
	private const int NotFound = 127;
	private const int NoEntry = 2;
	/// <summary>Gets the singleton system implementation.</summary>
	public static SystemChrootPlatform Instance { get; } = new();
	/// <inheritdoc />
	public bool IsSupported => OperatingSystem.IsLinux() || OperatingSystem.IsMacOS() || OperatingSystem.IsFreeBSD();
	/// <inheritdoc />
	public string UnsupportedReason => "changing the process root is unsupported on this host";

	private SystemChrootPlatform() {
	}

	/// <inheritdoc />
	public bool IsCurrentRoot( string path ) {
		ArgumentException.ThrowIfNullOrEmpty( path );
		if ( !IsSupported ) {
			return false;
		}
		var pathPointer = IntPtr.Zero;
		var resolvedPointer = IntPtr.Zero;
		try {
			pathPointer = Marshal.StringToCoTaskMemUTF8( path );
			resolvedPointer = NativeMethods.RealPath( pathPointer, IntPtr.Zero );
			if ( IntPtr.Zero == resolvedPointer ) {
				return false;
			}
			var resolved = Marshal.PtrToStringUTF8( resolvedPointer );
			return string.Equals( "/", resolved, StringComparison.Ordinal );
		} finally {
			if ( IntPtr.Zero != resolvedPointer ) {
				NativeMethods.Free( resolvedPointer );
			}
			if ( IntPtr.Zero != pathPointer ) {
				Marshal.FreeCoTaskMem( pathPointer );
			}
		}
	}

	/// <inheritdoc />
	public ValueTask<ChrootExecutionResult> ExecuteAsync( ChrootExecutionRequest request, CancellationToken cancellationToken = default ) {
		ArgumentNullException.ThrowIfNull( request );
		cancellationToken.ThrowIfCancellationRequested();
		if ( !IsSupported ) {
			return ValueTask.FromResult( new ChrootExecutionResult( InternalFailure, $"chroot: {UnsupportedReason}" ) );
		}

		var oldRoot = IsCurrentRoot( request.RootDirectory );
		CredentialResolution? outerCredentials = null;
		if ( !oldRoot && ( null != request.UserSpec || null != request.GroupsSpec ) ) {
			outerCredentials = ResolveCredentials( request.UserSpec, request.GroupsSpec );
		}

		if ( 0 != NativeMethods.ChRoot( request.RootDirectory ) ) {
			return ValueTask.FromResult( Failure( $"chroot: cannot change root directory to '{request.RootDirectory}'" ) );
		}
		if ( !request.SkipChdir && 0 != NativeMethods.ChDir( "/" ) ) {
			return ValueTask.FromResult( Failure( "chroot: cannot chdir to root directory" ) );
		}

		if ( null != request.UserSpec || null != request.GroupsSpec ) {
			var innerCredentials = ResolveCredentials( request.UserSpec, request.GroupsSpec );
			var credentials = ( innerCredentials.Succeeded )
				? innerCredentials
				: outerCredentials
			;
			if ( null == credentials || !credentials.Succeeded ) {
				var diagnostic = innerCredentials.Error ?? outerCredentials?.Error ?? "invalid user or group specification";
				return ValueTask.FromResult( new ChrootExecutionResult( InternalFailure, $"chroot: {diagnostic}" ) );
			}
			var credentialFailure = ApplyCredentials( credentials );
			if ( null != credentialFailure ) {
				return ValueTask.FromResult( credentialFailure );
			}
		}

		cancellationToken.ThrowIfCancellationRequested();
		return ValueTask.FromResult( ExecuteCommand( request.Command ) );
	}

	private static ChrootExecutionResult? ApplyCredentials( CredentialResolution credentials ) {
		ArgumentNullException.ThrowIfNull( credentials );
		if ( credentials.SetSupplementaryGroups ) {
			var setGroupsError = SetSupplementaryGroups( credentials.SupplementaryGroups );
			if ( null != setGroupsError ) {
				return setGroupsError;
			}
		}
		if ( credentials.GroupId.HasValue && 0 != NativeMethods.SetGid( credentials.GroupId.Value ) ) {
			return Failure( "chroot: failed to set group-ID" );
		}
		if ( credentials.UserId.HasValue && 0 != NativeMethods.SetUid( credentials.UserId.Value ) ) {
			return Failure( "chroot: failed to set user-ID" );
		}
		return null;
	}

	private static ChrootExecutionResult? SetSupplementaryGroups( IReadOnlyList<uint> groups ) {
		ArgumentNullException.ThrowIfNull( groups );
		var pointer = IntPtr.Zero;
		try {
			if ( 0 < groups.Count ) {
				pointer = Marshal.AllocHGlobal( checked( groups.Count * sizeof( uint ) ) );
				for ( var index = 0; index < groups.Count; index++ ) {
					Marshal.WriteInt32( pointer, index * sizeof( uint ), unchecked( (int)groups[ index ] ) );
				}
			}
			if ( 0 != NativeMethods.SetGroups( (nuint)groups.Count, pointer ) ) {
				return Failure( "chroot: failed to set supplemental groups" );
			}
			return null;
		} finally {
			if ( IntPtr.Zero != pointer ) {
				Marshal.FreeHGlobal( pointer );
			}
		}
	}

	private static CredentialResolution ResolveCredentials( string? userSpec, string? groupsSpec ) {
		uint? userId = null;
		uint? groupId = null;
		string? userName = null;
		if ( null != userSpec ) {
			var colon = userSpec.IndexOf( ':' );
			var userToken = userSpec;
			string? groupToken = null;
			if ( 0 <= colon ) {
				userToken = userSpec[ ..colon ];
				groupToken = userSpec[ ( colon + 1 ).. ];
			}
			if ( string.IsNullOrEmpty( userToken ) && string.IsNullOrEmpty( groupToken ) ) {
				return CredentialResolution.Failure( $"invalid user specification '{userSpec}'" );
			}
			if ( !string.IsNullOrEmpty( userToken ) ) {
				var user = ResolveUser( userToken );
				if ( !user.Succeeded ) {
					return CredentialResolution.Failure( user.Error! );
				}
				userId = user.UserId;
				groupId = user.GroupId;
				userName = user.UserName;
			}
			if ( !string.IsNullOrEmpty( groupToken ) ) {
				var group = ResolveGroup( groupToken );
				if ( !group.Succeeded ) {
					return CredentialResolution.Failure( group.Error! );
				}
				groupId = group.GroupId;
			}
			if ( userId.HasValue && !groupId.HasValue ) {
				return CredentialResolution.Failure( $"no group specified for unknown uid: {userId.Value.ToString( CultureInfo.InvariantCulture )}" );
			}
		}

		var supplementary = Array.Empty<uint>();
		var setSupplementary = false;
		if ( null != groupsSpec ) {
			setSupplementary = true;
			if ( !string.IsNullOrEmpty( groupsSpec ) ) {
				var resolvedGroups = new List<uint>();
				foreach ( var token in groupsSpec.Split( ',' ) ) {
					var text = token.Trim();
					if ( string.IsNullOrEmpty( text ) ) {
						return CredentialResolution.Failure( $"invalid group list '{groupsSpec}'" );
					}
					var group = ResolveGroup( text );
					if ( !group.Succeeded ) {
						return CredentialResolution.Failure( group.Error! );
					}
					resolvedGroups.Add( group.GroupId!.Value );
				}
				supplementary = resolvedGroups.ToArray();
			}
		} else if ( userId.HasValue ) {
			setSupplementary = true;
			if ( null != userName && groupId.HasValue ) {
				var groupList = ResolveGroupList( userName, groupId.Value );
				if ( !groupList.Succeeded ) {
					return CredentialResolution.Failure( groupList.Error! );
				}
				supplementary = groupList.Groups;
			}
		}
		return CredentialResolution.Success( userId, groupId, userName, supplementary, setSupplementary );
	}

	private static UserResolution ResolveUser( string token ) {
		ArgumentException.ThrowIfNullOrEmpty( token );
		var text = token.Trim();
		var forceNumeric = text.StartsWith( '+' );
		if ( forceNumeric ) {
			text = text[ 1.. ];
		}
		if ( !forceNumeric ) {
			var byName = NativeMethods.GetPwNam( text );
			if ( IntPtr.Zero != byName ) {
				return UserFromPointer( byName );
			}
		}
		if ( !uint.TryParse( text, NumberStyles.None, CultureInfo.InvariantCulture, out var numericId ) ) {
			return UserResolution.Failure( $"invalid user '{token}'" );
		}
		var byId = NativeMethods.GetPwUid( numericId );
		if ( IntPtr.Zero != byId ) {
			var known = UserFromPointer( byId );
			return UserResolution.Success( numericId, known.GroupId, known.UserName );
		}
		return UserResolution.Success( numericId, null, null );
	}

	private static UserResolution UserFromPointer( IntPtr pointer ) {
		if ( IntPtr.Zero == pointer ) {
			return UserResolution.Failure( "invalid user" );
		}
		var native = Marshal.PtrToStructure<PasswdPrefix>( pointer );
		_ = native.Password;
		var name = Marshal.PtrToStringUTF8( native.Name );
		return UserResolution.Success( native.UserId, native.GroupId, name );
	}

	private static GroupResolution ResolveGroup( string token ) {
		ArgumentException.ThrowIfNullOrEmpty( token );
		var text = token.Trim();
		var forceNumeric = text.StartsWith( '+' );
		if ( forceNumeric ) {
			text = text[ 1.. ];
		}
		if ( !forceNumeric ) {
			var byName = NativeMethods.GetGrNam( text );
			if ( IntPtr.Zero != byName ) {
				var native = Marshal.PtrToStructure<GroupPrefix>( byName );
				_ = native.Name;
				_ = native.Password;
				return GroupResolution.Success( native.GroupId );
			}
		}
		if ( !uint.TryParse( text, NumberStyles.None, CultureInfo.InvariantCulture, out var numericId ) ) {
			return GroupResolution.Failure( $"invalid group '{token}'" );
		}
		if ( forceNumeric ) {
			return GroupResolution.Success( numericId );
		}
		var byId = NativeMethods.GetGrGid( numericId );
		if ( IntPtr.Zero != byId ) {
			var native = Marshal.PtrToStructure<GroupPrefix>( byId );
			_ = native.Name;
			_ = native.Password;
			return GroupResolution.Success( native.GroupId );
		}
		return GroupResolution.Success( numericId );
	}

	private static GroupListResolution ResolveGroupList( string userName, uint primaryGroup ) {
		ArgumentException.ThrowIfNullOrEmpty( userName );
		var count = 16;
		for ( var attempt = 0; attempt < 3; attempt++ ) {
			var groups = new uint[ count ];
			var requested = count;
			var result = NativeMethods.GetGroupList( userName, primaryGroup, groups, ref requested );
			if ( 0 <= result ) {
				if ( requested < groups.Length ) {
					Array.Resize( ref groups, requested );
				}
				return GroupListResolution.Success( groups );
			}
			if ( requested <= count ) {
				return GroupListResolution.Failure( $"failed to get supplemental groups for '{userName}'" );
			}
			count = requested;
		}
		return GroupListResolution.Failure( $"failed to get supplemental groups for '{userName}'" );
	}

	private static ChrootExecutionResult ExecuteCommand( IReadOnlyList<string> command ) {
		ArgumentNullException.ThrowIfNull( command );
		var strings = new IntPtr[ command.Count ];
		var argv = IntPtr.Zero;
		try {
			for ( var index = 0; index < command.Count; index++ ) {
				strings[ index ] = Marshal.StringToCoTaskMemUTF8( command[ index ] );
			}
			argv = Marshal.AllocHGlobal( checked( ( command.Count + 1 ) * IntPtr.Size ) );
			for ( var index = 0; index < strings.Length; index++ ) {
				Marshal.WriteIntPtr( argv, index * IntPtr.Size, strings[ index ] );
			}
			Marshal.WriteIntPtr( argv, command.Count * IntPtr.Size, IntPtr.Zero );
			_ = NativeMethods.ExecVp( strings[ 0 ], argv );
			var error = Marshal.GetLastPInvokeError();
			var status = ( NoEntry == error )
				? NotFound
				: CannotInvoke
			;
			return new ChrootExecutionResult( status, $"chroot: failed to run command '{command[ 0 ]}': {GetErrorText( error )}" );
		} finally {
			if ( IntPtr.Zero != argv ) {
				Marshal.FreeHGlobal( argv );
			}
			foreach ( var pointer in strings ) {
				if ( IntPtr.Zero != pointer ) {
					Marshal.FreeCoTaskMem( pointer );
				}
			}
		}
	}

	private static ChrootExecutionResult Failure( string prefix ) {
		ArgumentException.ThrowIfNullOrEmpty( prefix );
		var error = Marshal.GetLastPInvokeError();
		return new ChrootExecutionResult( InternalFailure, $"{prefix}: {GetErrorText( error )}" );
	}

	private static string GetErrorText( int error ) {
		var pointer = NativeMethods.StrError( error );
		var text = Marshal.PtrToStringAnsi( pointer );
		if ( string.IsNullOrEmpty( text ) ) {
			return $"error {error.ToString( CultureInfo.InvariantCulture )}";
		}
		return text;
	}

	[StructLayout( LayoutKind.Sequential )]
	private struct PasswdPrefix {
		public IntPtr Name;
		public IntPtr Password;
		public uint UserId;
		public uint GroupId;
	}

	[StructLayout( LayoutKind.Sequential )]
	private struct GroupPrefix {
		public IntPtr Name;
		public IntPtr Password;
		public uint GroupId;
	}

	private sealed class CredentialResolution {
		public bool Succeeded { get; private init; }
		public uint? UserId { get; private init; }
		public uint? GroupId { get; private init; }
		public string? UserName { get; private init; }
		public uint[] SupplementaryGroups { get; private init; } = [];
		public bool SetSupplementaryGroups { get; private init; }
		public string? Error { get; private init; }
		public static CredentialResolution Success( uint? userId, uint? groupId, string? userName, uint[] groups, bool setGroups ) {
			ArgumentNullException.ThrowIfNull( groups );
			return new CredentialResolution {
				Succeeded = true,
				UserId = userId,
				GroupId = groupId,
				UserName = userName,
				SupplementaryGroups = groups,
				SetSupplementaryGroups = setGroups
			};
		}
		public static CredentialResolution Failure( string error ) {
			ArgumentException.ThrowIfNullOrEmpty( error );
			return new CredentialResolution { Error = error };
		}
	}

	private sealed class UserResolution {
		public bool Succeeded { get; private init; }
		public uint? UserId { get; private init; }
		public uint? GroupId { get; private init; }
		public string? UserName { get; private init; }
		public string? Error { get; private init; }
		public static UserResolution Success( uint userId, uint? groupId, string? userName ) {
			return new UserResolution { Succeeded = true, UserId = userId, GroupId = groupId, UserName = userName };
		}
		public static UserResolution Failure( string error ) {
			ArgumentException.ThrowIfNullOrEmpty( error );
			return new UserResolution { Error = error };
		}
	}

	private sealed class GroupResolution {
		public bool Succeeded { get; private init; }
		public uint? GroupId { get; private init; }
		public string? Error { get; private init; }
		public static GroupResolution Success( uint groupId ) {
			return new GroupResolution { Succeeded = true, GroupId = groupId };
		}
		public static GroupResolution Failure( string error ) {
			ArgumentException.ThrowIfNullOrEmpty( error );
			return new GroupResolution { Error = error };
		}
	}

	private sealed class GroupListResolution {
		public bool Succeeded { get; private init; }
		public uint[] Groups { get; private init; } = [];
		public string? Error { get; private init; }
		public static GroupListResolution Success( uint[] groups ) {
			ArgumentNullException.ThrowIfNull( groups );
			return new GroupListResolution { Succeeded = true, Groups = groups };
		}
		public static GroupListResolution Failure( string error ) {
			ArgumentException.ThrowIfNullOrEmpty( error );
			return new GroupListResolution { Error = error };
		}
	}

	private static class NativeMethods {
		[DllImport( "libc", EntryPoint = "chroot", SetLastError = true )]
		public static extern int ChRoot( [MarshalAs( UnmanagedType.LPUTF8Str )] string path );
		[DllImport( "libc", EntryPoint = "chdir", SetLastError = true )]
		public static extern int ChDir( [MarshalAs( UnmanagedType.LPUTF8Str )] string path );
		[DllImport( "libc", EntryPoint = "setuid", SetLastError = true )]
		public static extern int SetUid( uint userId );
		[DllImport( "libc", EntryPoint = "setgid", SetLastError = true )]
		public static extern int SetGid( uint groupId );
		[DllImport( "libc", EntryPoint = "setgroups", SetLastError = true )]
		public static extern int SetGroups( nuint count, IntPtr groups );
		[DllImport( "libc", EntryPoint = "getpwnam", SetLastError = true )]
		public static extern IntPtr GetPwNam( [MarshalAs( UnmanagedType.LPUTF8Str )] string name );
		[DllImport( "libc", EntryPoint = "getpwuid", SetLastError = true )]
		public static extern IntPtr GetPwUid( uint userId );
		[DllImport( "libc", EntryPoint = "getgrnam", SetLastError = true )]
		public static extern IntPtr GetGrNam( [MarshalAs( UnmanagedType.LPUTF8Str )] string name );
		[DllImport( "libc", EntryPoint = "getgrgid", SetLastError = true )]
		public static extern IntPtr GetGrGid( uint groupId );
		[DllImport( "libc", EntryPoint = "getgrouplist", SetLastError = true )]
		public static extern int GetGroupList(
			[MarshalAs( UnmanagedType.LPUTF8Str )] string user,
			uint primaryGroup,
			[Out] uint[] groups,
			ref int count
		);
		[DllImport( "libc", EntryPoint = "execvp", SetLastError = true )]
		public static extern int ExecVp( IntPtr file, IntPtr argv );
		[DllImport( "libc", EntryPoint = "realpath", SetLastError = true )]
		public static extern IntPtr RealPath( IntPtr path, IntPtr resolvedPath );
		[DllImport( "libc", EntryPoint = "free" )]
		public static extern void Free( IntPtr pointer );
		[DllImport( "libc", EntryPoint = "strerror" )]
		public static extern IntPtr StrError( int error );
	}
}
