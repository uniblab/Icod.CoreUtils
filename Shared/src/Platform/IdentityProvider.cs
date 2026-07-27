namespace Icod.CoreUtils.Shared.Platform;

using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Security.Principal;

/// <summary>Describes an operating-system group.</summary>
public sealed record GroupIdentity( string Id, string Name );

/// <summary>Describes an operating-system user and their groups.</summary>
public sealed record UserIdentity(
	string Id,
	string Name,
	GroupIdentity PrimaryGroup,
	IReadOnlyList<GroupIdentity> Groups
);

/// <summary>Describes the real and effective identities of the current process.</summary>
public sealed record ProcessIdentity(
	UserIdentity RealUser,
	UserIdentity EffectiveUser,
	GroupIdentity RealGroup,
	GroupIdentity EffectiveGroup,
	IReadOnlyList<GroupIdentity> Groups,
	string? SecurityContext
);

/// <summary>Supplies user, group, and login-session identities.</summary>
public interface IIdentityProvider {
	/// <summary>Gets the identity of the current process.</summary>
	ValueTask<ProcessIdentity> GetCurrentAsync( CancellationToken cancellationToken = default );
	/// <summary>Finds a user by login name.</summary>
	ValueTask<UserIdentity?> FindUserAsync( string userName, CancellationToken cancellationToken = default );
	/// <summary>Finds a user by numeric user ID.</summary>
	ValueTask<UserIdentity?> FindUserByIdAsync( string userId, CancellationToken cancellationToken = default );
	/// <summary>Gets the login-session name, which may differ from the effective user.</summary>
	ValueTask<string?> GetLoginNameAsync( CancellationToken cancellationToken = default );
}

/// <summary>Gets identity information from the current operating system.</summary>
public sealed class SystemIdentityProvider : IIdentityProvider {
	private const int Erange = 34;
	private const int InitialBufferSize = 16 * 1024;
	private const int MaximumBufferSize = 1024 * 1024;

	/// <summary>Gets the process-wide provider instance.</summary>
	public static SystemIdentityProvider Instance { get; } = new();

	private SystemIdentityProvider() { }

	/// <inheritdoc />
	public async ValueTask<ProcessIdentity> GetCurrentAsync( CancellationToken cancellationToken = default ) {
		cancellationToken.ThrowIfCancellationRequested();
		if ( !IsUnix ) return CreatePortableIdentity();

		var realUserId = NativeMethods.GetUid();
		var effectiveUserId = NativeMethods.GetEffectiveUid();
		var realGroupId = NativeMethods.GetGid();
		var effectiveGroupId = NativeMethods.GetEffectiveGid();
		var supplementaryIds = ReadCurrentGroupIds();
		var realUser = ResolveUser( realUserId ) ?? CreateUnknownUser( realUserId, realGroupId );
		var effectiveUser = realUserId == effectiveUserId
			? realUser
			: ResolveUser( effectiveUserId ) ?? CreateUnknownUser( effectiveUserId, effectiveGroupId );
		var realGroup = ResolveGroup( realGroupId );
		var effectiveGroup = realGroupId == effectiveGroupId ? realGroup : ResolveGroup( effectiveGroupId );
		var groups = ResolveGroups( supplementaryIds.Prepend( effectiveGroupId ) );
		var securityContext = await ReadSecurityContextAsync( cancellationToken ).ConfigureAwait( false );
		return new ProcessIdentity(
			realUser,
			effectiveUser,
			realGroup,
			effectiveGroup,
			groups,
			securityContext
		);
	}

	/// <inheritdoc />
	public ValueTask<UserIdentity?> FindUserAsync( string userName, CancellationToken cancellationToken = default ) {
		ArgumentException.ThrowIfNullOrWhiteSpace( userName );
		cancellationToken.ThrowIfCancellationRequested();
		if ( !IsUnix ) {
			var current = CreatePortableIdentity().EffectiveUser;
			return ValueTask.FromResult<UserIdentity?>(
				string.Equals( current.Name, userName, StringComparison.OrdinalIgnoreCase ) ? current : null
			);
		}
		return ValueTask.FromResult( ResolveUser( userName ) );
	}

	/// <inheritdoc />
	public ValueTask<UserIdentity?> FindUserByIdAsync( string userId, CancellationToken cancellationToken = default ) {
		ArgumentException.ThrowIfNullOrWhiteSpace( userId );
		cancellationToken.ThrowIfCancellationRequested();
		if ( !TryParseUserId( userId, out var numericId ) ) {
			return ValueTask.FromResult<UserIdentity?>( null );
		}
		if ( !IsUnix ) {
			var current = CreatePortableIdentity().EffectiveUser;
			return ValueTask.FromResult<UserIdentity?>( current.Id == numericId.ToString( System.Globalization.CultureInfo.InvariantCulture ) ? current : null );
		}
		return ValueTask.FromResult( ResolveUser( numericId ) );
	}

	/// <inheritdoc />
	public ValueTask<string?> GetLoginNameAsync( CancellationToken cancellationToken = default ) {
		cancellationToken.ThrowIfCancellationRequested();
		if ( !IsUnix ) return ValueTask.FromResult<string?>( Environment.UserName );
		var buffer = new byte[ 1024 ];
		var result = NativeMethods.GetLoginName( buffer, (nuint)buffer.Length );
		return ValueTask.FromResult<string?>( 0 == result ? DecodeNullTerminated( buffer ) : null );
	}

	private static bool TryParseUserId( string value, out uint userId ) {
		var start = 0;
		if ( 0 < value.Length && '+' == value[0] ) start = 1;
		if ( start == value.Length ) {
			userId = 0;
			return false;
		}
		for ( var index = start; index < value.Length; index++ ) {
			if ( value[index] is < '0' or > '9' ) {
				userId = 0;
				return false;
			}
		}
		while ( start < value.Length && '0' == value[start] ) start++;
		if ( start == value.Length ) {
			userId = 0;
			return true;
		}
		return uint.TryParse( value.AsSpan( start ), System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out userId );
	}

	private static bool IsUnix => OperatingSystem.IsLinux() || OperatingSystem.IsMacOS() || OperatingSystem.IsFreeBSD();

	private static ProcessIdentity CreatePortableIdentity() {
		if ( OperatingSystem.IsWindows() ) return CreateWindowsIdentity();
		var name = Environment.UserName;
		var group = new GroupIdentity( name, name );
		var user = new UserIdentity( name, name, group, new[] { group } );
		return new ProcessIdentity( user, user, group, group, new[] { group }, null );
	}

	[SupportedOSPlatform( "windows" )]
	private static ProcessIdentity CreateWindowsIdentity() {
		using var identity = WindowsIdentity.GetCurrent();
		var fullName = string.IsNullOrEmpty( identity.Name ) ? Environment.UserName : identity.Name;
		var name = fullName.Contains( '\\' ) ? fullName[(fullName.LastIndexOf( '\\' ) + 1)..] : fullName;
		var userId = identity.User?.Value ?? name;
		var groups = new List<GroupIdentity>();
		if ( null != identity.Groups ) {
			foreach ( var sid in identity.Groups ) {
				var groupName = sid.Value;
				try {
					groupName = sid.Translate( typeof( NTAccount ) ).Value;
				} catch ( IdentityNotMappedException ) { }
				groups.Add( new GroupIdentity( sid.Value, groupName ) );
			}
		}
		var primary = groups.FirstOrDefault() ?? new GroupIdentity( userId, name );
		if ( 0 == groups.Count ) groups.Add( primary );
		var user = new UserIdentity( userId, name, primary, groups );
		return new ProcessIdentity( user, user, primary, primary, groups, null );
	}

	private static UserIdentity CreateUnknownUser( uint userId, uint groupId ) {
		var group = ResolveGroup( groupId );
		return new UserIdentity( userId.ToString( System.Globalization.CultureInfo.InvariantCulture ), userId.ToString( System.Globalization.CultureInfo.InvariantCulture ), group, new[] { group } );
	}

	private static UserIdentity? ResolveUser( uint userId ) {
		return ReadPasswd(
			( IntPtr buffer, nuint size, out Passwd passwd, out IntPtr result ) => NativeMethods.GetPasswordByUid( userId, out passwd, buffer, size, out result )
		);
	}

	private static UserIdentity? ResolveUser( string userName ) {
		return ReadPasswd(
			( IntPtr buffer, nuint size, out Passwd passwd, out IntPtr result ) => NativeMethods.GetPasswordByName( userName, out passwd, buffer, size, out result )
		);
	}

	private delegate int PasswdReader( IntPtr buffer, nuint size, out Passwd passwd, out IntPtr result );

	private static UserIdentity? ReadPasswd( PasswdReader reader ) {
		for ( var bufferSize = InitialBufferSize; bufferSize <= MaximumBufferSize; bufferSize *= 2 ) {
			var buffer = Marshal.AllocHGlobal( bufferSize );
			try {
				var error = reader( buffer, (nuint)bufferSize, out var passwd, out var result );
				if ( Erange == error ) continue;
				if ( 0 != error || IntPtr.Zero == result ) return null;
				var name = Marshal.PtrToStringUTF8( passwd.Name ) ?? passwd.UserId.ToString( System.Globalization.CultureInfo.InvariantCulture );
				var primary = ResolveGroup( passwd.GroupId );
				var groupIds = ReadNamedUserGroupIds( name, passwd.GroupId );
				var groups = ResolveGroups( groupIds.Prepend( passwd.GroupId ) );
				return new UserIdentity(
					passwd.UserId.ToString( System.Globalization.CultureInfo.InvariantCulture ),
					name,
					primary,
					groups
				);
			} finally {
				Marshal.FreeHGlobal( buffer );
			}
		}
		return null;
	}

	private static GroupIdentity ResolveGroup( uint groupId ) {
		for ( var bufferSize = InitialBufferSize; bufferSize <= MaximumBufferSize; bufferSize *= 2 ) {
			var buffer = Marshal.AllocHGlobal( bufferSize );
			try {
				var error = NativeMethods.GetGroupByGid( groupId, out var group, buffer, (nuint)bufferSize, out var result );
				if ( Erange == error ) continue;
				var id = groupId.ToString( System.Globalization.CultureInfo.InvariantCulture );
				if ( 0 != error || IntPtr.Zero == result ) return new GroupIdentity( id, id );
				return new GroupIdentity( id, Marshal.PtrToStringUTF8( group.Name ) ?? id );
			} finally {
				Marshal.FreeHGlobal( buffer );
			}
		}
		var fallback = groupId.ToString( System.Globalization.CultureInfo.InvariantCulture );
		return new GroupIdentity( fallback, fallback );
	}

	private static IReadOnlyList<GroupIdentity> ResolveGroups( IEnumerable<uint> groupIds ) => groupIds
		.Distinct()
		.Select( ResolveGroup )
		.ToArray();

	private static IReadOnlyList<uint> ReadCurrentGroupIds() {
		var count = NativeMethods.GetGroupsCount( 0, IntPtr.Zero );
		if ( 0 >= count ) return Array.Empty<uint>();
		var groups = new uint[ count ];
		var actual = NativeMethods.GetGroups( groups.Length, groups );
		if ( 0 > actual ) return Array.Empty<uint>();
		return groups.Take( actual ).ToArray();
	}

	private static IReadOnlyList<uint> ReadNamedUserGroupIds( string userName, uint primaryGroupId ) {
		var count = 1;
		var groups = new uint[ count ];
		var result = NativeMethods.GetGroupList( userName, primaryGroupId, groups, ref count );
		if ( 0 <= result ) return groups.Take( count ).ToArray();
		if ( 0 >= count ) return Array.Empty<uint>();
		groups = new uint[ count ];
		result = NativeMethods.GetGroupList( userName, primaryGroupId, groups, ref count );
		return 0 <= result ? groups.Take( count ).ToArray() : Array.Empty<uint>();
	}

	private static async Task<string?> ReadSecurityContextAsync( CancellationToken cancellationToken ) {
		if ( !OperatingSystem.IsLinux() || !Directory.Exists( "/sys/fs/selinux" ) ) return null;
		const string path = "/proc/self/attr/current";
		try {
			var value = await File.ReadAllTextAsync( path, cancellationToken ).ConfigureAwait( false );
			value = value.TrimEnd( '\0', '\r', '\n' );
			return string.IsNullOrWhiteSpace( value ) || string.Equals( value, "unconfined", StringComparison.Ordinal ) ? null : value;
		} catch ( FileNotFoundException ) {
			return null;
		} catch ( DirectoryNotFoundException ) {
			return null;
		} catch ( UnauthorizedAccessException ) {
			return null;
		} catch ( IOException ) {
			return null;
		}
	}

	private static string DecodeNullTerminated( byte[] bytes ) {
		var length = Array.IndexOf( bytes, (byte)0 );
		if ( 0 > length ) length = bytes.Length;
		return System.Text.Encoding.UTF8.GetString( bytes, 0, length );
	}

	[StructLayout( LayoutKind.Sequential )]
	private struct Passwd {
		public IntPtr Name;
		public IntPtr Password;
		public uint UserId;
		public uint GroupId;
		public IntPtr Gecos;
		public IntPtr Directory;
		public IntPtr Shell;
	}

	[StructLayout( LayoutKind.Sequential )]
	private struct Group {
		public IntPtr Name;
		public IntPtr Password;
		public uint GroupId;
		public IntPtr Members;
	}

	private static class NativeMethods {
		[DllImport( "libc", EntryPoint = "getuid" )]
		internal static extern uint GetUid();
		[DllImport( "libc", EntryPoint = "geteuid" )]
		internal static extern uint GetEffectiveUid();
		[DllImport( "libc", EntryPoint = "getgid" )]
		internal static extern uint GetGid();
		[DllImport( "libc", EntryPoint = "getegid" )]
		internal static extern uint GetEffectiveGid();
		[DllImport( "libc", EntryPoint = "getgroups" )]
		internal static extern int GetGroupsCount( int size, IntPtr groups );
		[DllImport( "libc", EntryPoint = "getgroups" )]
		internal static extern int GetGroups( int size, [Out] uint[] groups );
		[DllImport( "libc", EntryPoint = "getgrouplist", CharSet = CharSet.Ansi )]
		internal static extern int GetGroupList( string userName, uint primaryGroup, [Out] uint[] groups, ref int groupCount );
		[DllImport( "libc", EntryPoint = "getpwuid_r" )]
		internal static extern int GetPasswordByUid( uint userId, out Passwd password, IntPtr buffer, nuint bufferSize, out IntPtr result );
		[DllImport( "libc", EntryPoint = "getpwnam_r", CharSet = CharSet.Ansi )]
		internal static extern int GetPasswordByName( string userName, out Passwd password, IntPtr buffer, nuint bufferSize, out IntPtr result );
		[DllImport( "libc", EntryPoint = "getgrgid_r" )]
		internal static extern int GetGroupByGid( uint groupId, out Group group, IntPtr buffer, nuint bufferSize, out IntPtr result );
		[DllImport( "libc", EntryPoint = "getlogin_r" )]
		internal static extern int GetLoginName( [Out] byte[] buffer, nuint bufferSize );
	}
}
