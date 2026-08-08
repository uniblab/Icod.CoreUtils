namespace Icod.ProcPs.Shared;

using System.Globalization;
using System.Runtime.InteropServices;
using Icod.CoreUtils.Shared.Host;

/// <summary>Contains aggregate CPU counters using Linux <c>/proc/stat</c> tick semantics.</summary>
public sealed class ProcCpuTimes {
	/// <summary>Gets user-mode ticks.</summary>
	public ulong User { get; }
	/// <summary>Gets nice user-mode ticks.</summary>
	public ulong Nice { get; }
	/// <summary>Gets system-mode ticks.</summary>
	public ulong System { get; }
	/// <summary>Gets idle ticks.</summary>
	public ulong Idle { get; }
	/// <summary>Gets I/O-wait ticks.</summary>
	public ulong IoWait { get; }
	/// <summary>Gets hard-interrupt ticks.</summary>
	public ulong Irq { get; }
	/// <summary>Gets soft-interrupt ticks.</summary>
	public ulong SoftIrq { get; }
	/// <summary>Gets stolen ticks.</summary>
	public ulong Steal { get; }
	/// <summary>Gets guest ticks as reported by the kernel.</summary>
	public ulong Guest { get; }
	/// <summary>Gets nice guest ticks as reported by the kernel.</summary>
	public ulong GuestNice { get; }
	/// <summary>Gets the non-double-counted total used for CPU-delta calculations.</summary>
	public ulong Total => unchecked( this.User + this.Nice + this.System + this.Idle + this.IoWait + this.Irq + this.SoftIrq + this.Steal );
	/// <summary>Initializes CPU counters.</summary>
	public ProcCpuTimes( ulong user, ulong nice, ulong system, ulong idle, ulong ioWait, ulong irq, ulong softIrq, ulong steal, ulong guest, ulong guestNice ) {
		this.User = user;
		this.Nice = nice;
		this.System = system;
		this.Idle = idle;
		this.IoWait = ioWait;
		this.Irq = irq;
		this.SoftIrq = softIrq;
		this.Steal = steal;
		this.Guest = guest;
		this.GuestNice = guestNice;
	}
}

/// <summary>Contains Linux-style load-average information.</summary>
public sealed class ProcLoadAverage {
	/// <summary>Gets the one-minute load average.</summary>
	public double OneMinute { get; }
	/// <summary>Gets the five-minute load average.</summary>
	public double FiveMinutes { get; }
	/// <summary>Gets the fifteen-minute load average.</summary>
	public double FifteenMinutes { get; }
	/// <summary>Gets the number of currently runnable entities.</summary>
	public int Runnable { get; }
	/// <summary>Gets the number of schedulable entities represented by the load source.</summary>
	public int TotalEntities { get; }
	/// <summary>Gets the most recently allocated PID reported by the source.</summary>
	public int LastProcessId { get; }
	/// <summary>Initializes load-average information.</summary>
	public ProcLoadAverage( double oneMinute, double fiveMinutes, double fifteenMinutes, int runnable, int totalEntities, int lastProcessId ) {
		this.OneMinute = oneMinute;
		this.FiveMinutes = fiveMinutes;
		this.FifteenMinutes = fifteenMinutes;
		this.Runnable = runnable;
		this.TotalEntities = totalEntities;
		this.LastProcessId = lastProcessId;
	}
}

/// <summary>Contains system uptime and aggregate idle time.</summary>
public sealed class ProcUptimeInfo {
	/// <summary>Gets elapsed system uptime.</summary>
	public TimeSpan Uptime { get; }
	/// <summary>Gets aggregate processor idle time when supplied by the source.</summary>
	public TimeSpan? IdleTime { get; }
	/// <summary>Initializes uptime information.</summary>
	public ProcUptimeInfo( TimeSpan uptime, TimeSpan? idleTime ) {
		if ( TimeSpan.Zero > uptime ) throw new ArgumentOutOfRangeException( nameof( uptime ) );
		this.Uptime = uptime;
		this.IdleTime = idleTime;
	}
}

/// <summary>Contains physical-memory and swap values expressed in bytes.</summary>
public sealed class ProcMemoryInfo {
	/// <summary>Gets all recognized and unrecognized meminfo values keyed by Linux field name.</summary>
	public IReadOnlyDictionary<string, ulong> Fields { get; }
	/// <summary>Gets total physical memory when reported.</summary>
	public ulong? TotalBytes => Get( "MemTotal" );
	/// <summary>Gets free physical memory when reported.</summary>
	public ulong? FreeBytes => Get( "MemFree" );
	/// <summary>Gets available physical memory when reported.</summary>
	public ulong? AvailableBytes => Get( "MemAvailable" );
	/// <summary>Gets total swap when reported.</summary>
	public ulong? SwapTotalBytes => Get( "SwapTotal" );
	/// <summary>Gets free swap when reported.</summary>
	public ulong? SwapFreeBytes => Get( "SwapFree" );
	/// <summary>Initializes memory information.</summary>
	public ProcMemoryInfo( IReadOnlyDictionary<string, ulong> fields ) {
		ArgumentNullException.ThrowIfNull( fields );
		this.Fields = fields;
	}
	private ulong? Get( string key ) => this.Fields.TryGetValue( key, out var value ) ? value : null;
}

/// <summary>Contains one Linux slab allocator row.</summary>
public sealed class ProcSlabEntry {
	/// <summary>Gets the slab cache name.</summary>
	public string Name { get; }
	/// <summary>Gets active object count.</summary>
	public ulong ActiveObjects { get; }
	/// <summary>Gets total object count.</summary>
	public ulong TotalObjects { get; }
	/// <summary>Gets object size in bytes.</summary>
	public ulong ObjectSizeBytes { get; }
	/// <summary>Gets objects per slab.</summary>
	public ulong ObjectsPerSlab { get; }
	/// <summary>Gets pages per slab.</summary>
	public ulong PagesPerSlab { get; }
	/// <summary>Initializes slab information.</summary>
	public ProcSlabEntry( string name, ulong activeObjects, ulong totalObjects, ulong objectSizeBytes, ulong objectsPerSlab, ulong pagesPerSlab ) {
		ArgumentException.ThrowIfNullOrWhiteSpace( name );
		this.Name = name;
		this.ActiveObjects = activeObjects;
		this.TotalObjects = totalObjects;
		this.ObjectSizeBytes = objectSizeBytes;
		this.ObjectsPerSlab = objectsPerSlab;
		this.PagesPerSlab = pagesPerSlab;
	}
}

/// <summary>Contains the huge-page subset of Linux meminfo.</summary>
public sealed class ProcHugePageInfo {
	/// <summary>Gets configured huge pages.</summary>
	public ulong TotalPages { get; }
	/// <summary>Gets free huge pages.</summary>
	public ulong FreePages { get; }
	/// <summary>Gets reserved huge pages.</summary>
	public ulong ReservedPages { get; }
	/// <summary>Gets surplus huge pages.</summary>
	public ulong SurplusPages { get; }
	/// <summary>Gets huge-page size in bytes.</summary>
	public ulong PageSizeBytes { get; }
	/// <summary>Initializes huge-page information.</summary>
	public ProcHugePageInfo( ulong totalPages, ulong freePages, ulong reservedPages, ulong surplusPages, ulong pageSizeBytes ) {
		this.TotalPages = totalPages;
		this.FreePages = freePages;
		this.ReservedPages = reservedPages;
		this.SurplusPages = surplusPages;
		this.PageSizeBytes = pageSizeBytes;
	}
}

/// <summary>Contains user-session metrics when a platform provider can expose them.</summary>
public sealed class ProcUserSessionInfo {
	/// <summary>Gets the number of active user sessions.</summary>
	public int Count { get; }
	/// <summary>Initializes user-session information.</summary>
	public ProcUserSessionInfo( int count ) {
		ArgumentOutOfRangeException.ThrowIfNegative( count );
		this.Count = count;
	}
}

/// <summary>Contains one coherent ProcPs system-metric snapshot.</summary>
public sealed class ProcSystemSnapshot {
	/// <summary>Gets aggregate CPU counters.</summary>
	public ProcObservedValue<ProcCpuTimes> Cpu { get; init; } = ProcObservedValue<ProcCpuTimes>.Missing( ProcObservationAvailability.Unavailable );
	/// <summary>Gets physical-memory and swap metrics.</summary>
	public ProcObservedValue<ProcMemoryInfo> Memory { get; init; } = ProcObservedValue<ProcMemoryInfo>.Missing( ProcObservationAvailability.Unavailable );
	/// <summary>Gets load averages.</summary>
	public ProcObservedValue<ProcLoadAverage> LoadAverage { get; init; } = ProcObservedValue<ProcLoadAverage>.Missing( ProcObservationAvailability.Unavailable );
	/// <summary>Gets uptime.</summary>
	public ProcObservedValue<ProcUptimeInfo> Uptime { get; init; } = ProcObservedValue<ProcUptimeInfo>.Missing( ProcObservationAvailability.Unavailable );
	/// <summary>Gets virtual-memory counters by Linux vmstat field name.</summary>
	public ProcObservedValue<IReadOnlyDictionary<string, ulong>> VirtualMemory { get; init; } = ProcObservedValue<IReadOnlyDictionary<string, ulong>>.Missing( ProcObservationAvailability.Unavailable );
	/// <summary>Gets slab allocator rows.</summary>
	public ProcObservedValue<IReadOnlyList<ProcSlabEntry>> Slab { get; init; } = ProcObservedValue<IReadOnlyList<ProcSlabEntry>>.Missing( ProcObservationAvailability.Unavailable );
	/// <summary>Gets huge-page metrics.</summary>
	public ProcObservedValue<ProcHugePageInfo> HugePages { get; init; } = ProcObservedValue<ProcHugePageInfo>.Missing( ProcObservationAvailability.Unavailable );
	/// <summary>Gets user-session metrics.</summary>
	public ProcObservedValue<ProcUserSessionInfo> UserSessions { get; init; } = ProcObservedValue<ProcUserSessionInfo>.Missing( ProcObservationAvailability.Unavailable );
}

/// <summary>Observes reusable system metrics with procps-ng semantics.</summary>
public interface IProcSystemMetricsProvider {
	/// <summary>Gets the metric capabilities exposed by the provider.</summary>
	ProcSystemCapabilities Capabilities { get; }
	/// <summary>Captures a coherent best-effort system snapshot.</summary>
	Task<ProcSystemSnapshot> GetSnapshotAsync( CancellationToken cancellationToken = default );
	/// <summary>Gets physical-memory and swap information without requiring unrelated system observations.</summary>
	async Task<ProcObservedValue<ProcMemoryInfo>> GetMemoryAsync( CancellationToken cancellationToken = default ) => ( await this.GetSnapshotAsync( cancellationToken ).ConfigureAwait( false ) ).Memory;
	/// <summary>Gets system or container uptime using the strongest semantics exposed by the provider.</summary>
	async Task<ProcObservedValue<ProcUptimeInfo>> GetUptimeAsync( bool containerMode, CancellationToken cancellationToken = default ) {
		if ( containerMode ) return ProcObservedValue<ProcUptimeInfo>.Missing( ProcObservationAvailability.Unsupported, "Container uptime is not exposed by this provider." );
		return ( await this.GetSnapshotAsync( cancellationToken ).ConfigureAwait( false ) ).Uptime;
	}
}

/// <summary>Selects Linux procfs metrics or a capability-driven portable provider.</summary>
public sealed class SystemProcSystemMetricsProvider : IProcSystemMetricsProvider {
	private readonly IProcSystemMetricsProvider _inner;
	/// <summary>Gets the shared system metrics provider.</summary>
	public static SystemProcSystemMetricsProvider Instance { get; } = new();
	/// <inheritdoc />
	public ProcSystemCapabilities Capabilities => this._inner.Capabilities;
	/// <summary>Initializes the system metric provider.</summary>
	public SystemProcSystemMetricsProvider() {
		this._inner = OperatingSystem.IsLinux() ? new LinuxProcSystemMetricsProvider() : new PortableProcSystemMetricsProvider();
	}
	/// <inheritdoc />
	public Task<ProcSystemSnapshot> GetSnapshotAsync( CancellationToken cancellationToken = default ) => this._inner.GetSnapshotAsync( cancellationToken );
	/// <inheritdoc />
	public Task<ProcObservedValue<ProcMemoryInfo>> GetMemoryAsync( CancellationToken cancellationToken = default ) => this._inner.GetMemoryAsync( cancellationToken );
	/// <inheritdoc />
	public Task<ProcObservedValue<ProcUptimeInfo>> GetUptimeAsync( bool containerMode, CancellationToken cancellationToken = default ) => this._inner.GetUptimeAsync( containerMode, cancellationToken );
}

/// <summary>Reads authoritative procps-ng-style system metrics from Linux procfs.</summary>
public sealed class LinuxProcSystemMetricsProvider : IProcSystemMetricsProvider {
	private const short UserProcessRecord = 7;
	private static readonly object UserSessionSync = new();
	private readonly string _procRoot;
	/// <inheritdoc />
	public ProcSystemCapabilities Capabilities => ProcSystemCapabilities.Memory
		| ProcSystemCapabilities.Swap
		| ProcSystemCapabilities.CpuActivity
		| ProcSystemCapabilities.LoadAverage
		| ProcSystemCapabilities.Uptime
		| ProcSystemCapabilities.VirtualMemory
		| ProcSystemCapabilities.Slab
		| ProcSystemCapabilities.HugePages
		| ProcSystemCapabilities.UserSessions;
	/// <summary>Initializes a Linux system-metric provider.</summary>
	public LinuxProcSystemMetricsProvider( string procRoot = "/proc" ) {
		ArgumentException.ThrowIfNullOrWhiteSpace( procRoot );
		this._procRoot = procRoot;
	}
	/// <inheritdoc />
	public Task<ProcObservedValue<ProcMemoryInfo>> GetMemoryAsync( CancellationToken cancellationToken = default ) => ObserveFileAsync( "meminfo", text => new ProcMemoryInfo( LinuxProcParsers.ParseMemInfo( text ) ), cancellationToken );
	/// <inheritdoc />
	public async Task<ProcObservedValue<ProcUptimeInfo>> GetUptimeAsync( bool containerMode, CancellationToken cancellationToken = default ) {
		if ( !containerMode ) return await ObserveFileAsync( "uptime", ParseUptime, cancellationToken ).ConfigureAwait( false );
		if ( !OperatingSystem.IsLinux() ) return ProcObservedValue<ProcUptimeInfo>.Missing( ProcObservationAvailability.Unsupported, "Container uptime with procps-ng semantics is available only on Linux." );
		var system = await ObserveFileAsync( "uptime", ParseUptime, cancellationToken ).ConfigureAwait( false );
		if ( !system.HasValue ) return system;
		var init = await ObserveFileAsync( Path.Combine( "1", "stat" ), LinuxProcParsers.ParseProcessStat, cancellationToken ).ConfigureAwait( false );
		if ( !init.HasValue ) return ProcObservedValue<ProcUptimeInfo>.Missing( init.Availability, init.Diagnostic );
		try {
			var ticksPerSecond = LinuxSystemNative.SysConf( LinuxSystemNative.ClockTicksPerSecond );
			if ( 0 >= ticksPerSecond ) return ProcObservedValue<ProcUptimeInfo>.Missing( ProcObservationAvailability.Unavailable, "sysconf(_SC_CLK_TCK) did not return a positive clock frequency." );
			var startSeconds = init.Value.StartTimeTicks / (double)ticksPerSecond;
			var containerSeconds = Math.Max( 0d, system.Value.Uptime.TotalSeconds - startSeconds );
			return ProcObservedValue<ProcUptimeInfo>.Available(
				new ProcUptimeInfo( TimeSpan.FromSeconds( containerSeconds ), null ),
				ProcObservationSource.Derived,
				ObservationFidelity.Exact
			);
		} catch ( DllNotFoundException exception ) {
			return ProcObservedValue<ProcUptimeInfo>.Missing( ProcObservationAvailability.Unsupported, exception.Message );
		} catch ( EntryPointNotFoundException exception ) {
			return ProcObservedValue<ProcUptimeInfo>.Missing( ProcObservationAvailability.Unsupported, exception.Message );
		}
	}
	/// <inheritdoc />
	public async Task<ProcSystemSnapshot> GetSnapshotAsync( CancellationToken cancellationToken = default ) {
		var stat = await ObserveFileAsync( "stat", ParseCpu, cancellationToken ).ConfigureAwait( false );
		var memory = await ObserveFileAsync( "meminfo", text => new ProcMemoryInfo( LinuxProcParsers.ParseMemInfo( text ) ), cancellationToken ).ConfigureAwait( false );
		var load = await ObserveFileAsync( "loadavg", ParseLoadAverage, cancellationToken ).ConfigureAwait( false );
		var uptime = await ObserveFileAsync( "uptime", ParseUptime, cancellationToken ).ConfigureAwait( false );
		var vm = await ObserveFileAsync<IReadOnlyDictionary<string, ulong>>( "vmstat", LinuxProcParsers.ParseCounterFile, cancellationToken ).ConfigureAwait( false );
		var slab = await ObserveFileAsync<IReadOnlyList<ProcSlabEntry>>( "slabinfo", ParseSlabInfo, cancellationToken ).ConfigureAwait( false );
		var hugePages = memory.HasValue ? ParseHugePages( memory.Value ) : ProcObservedValue<ProcHugePageInfo>.Missing( memory.Availability, memory.Diagnostic );
		var userSessions = ObserveUserSessions();
		return new ProcSystemSnapshot {
			Cpu = stat,
			Memory = memory,
			LoadAverage = load,
			Uptime = uptime,
			VirtualMemory = vm,
			Slab = slab,
			HugePages = hugePages,
			UserSessions = userSessions
		};
	}
	private static ProcObservedValue<ProcUserSessionInfo> ObserveUserSessions() {
		if ( !OperatingSystem.IsLinux() ) {
			return ProcObservedValue<ProcUserSessionInfo>.Missing(
				ProcObservationAvailability.Unsupported,
				"The Linux user-session provider is available only on Linux."
			);
		}
		lock ( UserSessionSync ) {
			var opened = false;
			try {
				LinuxSessionNative.SetUtmpxEnt();
				opened = true;
				var count = 0;
				while ( true ) {
					var entry = LinuxSessionNative.GetUtmpxEnt();
					if ( IntPtr.Zero == entry ) break;
					if ( UserProcessRecord == Marshal.ReadInt16( entry ) ) count++;
				}
				return ProcObservedValue<ProcUserSessionInfo>.Available(
					new ProcUserSessionInfo( count ),
					ProcObservationSource.PlatformApi,
					ObservationFidelity.Equivalent
				);
			} catch ( DllNotFoundException exception ) {
				return ProcObservedValue<ProcUserSessionInfo>.Missing( ProcObservationAvailability.Unsupported, exception.Message );
			} catch ( EntryPointNotFoundException exception ) {
				return ProcObservedValue<ProcUserSessionInfo>.Missing( ProcObservationAvailability.Unsupported, exception.Message );
			} finally {
				if ( opened ) {
					try { LinuxSessionNative.EndUtmpxEnt(); }
					catch ( DllNotFoundException ) { }
					catch ( EntryPointNotFoundException ) { }
				}
			}
		}
	}
	private static class LinuxSystemNative {
		/// <summary>Gets the POSIX <c>_SC_CLK_TCK</c> selector used by Linux libc.</summary>
		public const int ClockTicksPerSecond = 2;
		/// <summary>Reads a POSIX system-configuration value.</summary>
		[DllImport( "libc", EntryPoint = "sysconf", ExactSpelling = true, SetLastError = true )]
		public static extern long SysConf( int name );
	}
	private static class LinuxSessionNative {
		/// <summary>Rewinds the libc user-accounting iterator.</summary>
		[DllImport( "libc", EntryPoint = "setutxent", ExactSpelling = true )]
		internal static extern void SetUtmpxEnt();
		/// <summary>Reads the next libc user-accounting record.</summary>
		[DllImport( "libc", EntryPoint = "getutxent", ExactSpelling = true )]
		internal static extern IntPtr GetUtmpxEnt();
		/// <summary>Closes the libc user-accounting iterator.</summary>
		[DllImport( "libc", EntryPoint = "endutxent", ExactSpelling = true )]
		internal static extern void EndUtmpxEnt();
	}
	private async Task<ProcObservedValue<T>> ObserveFileAsync<T>( string fileName, Func<string, T> parser, CancellationToken cancellationToken ) {
		try {
			var text = await File.ReadAllTextAsync( Path.Combine( this._procRoot, fileName ), cancellationToken ).ConfigureAwait( false );
			return ProcObservedValue<T>.Available( parser( text ), ProcObservationSource.LinuxProcfs, ObservationFidelity.Exact );
		} catch ( UnauthorizedAccessException exception ) {
			return ProcObservedValue<T>.Missing( ProcObservationAvailability.AccessDenied, exception.Message );
		} catch ( FileNotFoundException exception ) {
			return ProcObservedValue<T>.Missing( ProcObservationAvailability.Unavailable, exception.Message );
		} catch ( DirectoryNotFoundException exception ) {
			return ProcObservedValue<T>.Missing( ProcObservationAvailability.Unavailable, exception.Message );
		} catch ( IOException exception ) {
			return ProcObservedValue<T>.Missing( ProcObservationAvailability.Unavailable, exception.Message );
		} catch ( FormatException exception ) {
			return ProcObservedValue<T>.Missing( ProcObservationAvailability.Malformed, exception.Message );
		} catch ( OverflowException exception ) {
			return ProcObservedValue<T>.Missing( ProcObservationAvailability.Malformed, exception.Message );
		}
	}
	/// <summary>Parses the aggregate <c>cpu</c> line from Linux <c>/proc/stat</c>.</summary>
	public static ProcCpuTimes ParseCpu( string text ) {
		var line = text.Split( '\n' ).FirstOrDefault( value => value.StartsWith( "cpu ", StringComparison.Ordinal ) ) ?? throw new FormatException( "The aggregate cpu line is missing." );
		var fields = line.Split( (char[]?)null, StringSplitOptions.RemoveEmptyEntries );
		if ( 5 > fields.Length ) throw new FormatException( "The aggregate cpu line is incomplete." );
		ulong Read( int index ) => index < fields.Length ? ulong.Parse( fields[ index ], NumberStyles.None, CultureInfo.InvariantCulture ) : 0UL;
		return new ProcCpuTimes( Read( 1 ), Read( 2 ), Read( 3 ), Read( 4 ), Read( 5 ), Read( 6 ), Read( 7 ), Read( 8 ), Read( 9 ), Read( 10 ) );
	}
	/// <summary>Parses Linux <c>/proc/loadavg</c>.</summary>
	public static ProcLoadAverage ParseLoadAverage( string text ) {
		var fields = text.Split( (char[]?)null, StringSplitOptions.RemoveEmptyEntries );
		if ( 5 > fields.Length ) throw new FormatException( "Malformed /proc/loadavg." );
		var entities = fields[ 3 ].Split( '/', 2 );
		if ( 2 != entities.Length ) throw new FormatException( "Malformed loadavg runnable/total field." );
		return new ProcLoadAverage(
			double.Parse( fields[ 0 ], NumberStyles.Float, CultureInfo.InvariantCulture ),
			double.Parse( fields[ 1 ], NumberStyles.Float, CultureInfo.InvariantCulture ),
			double.Parse( fields[ 2 ], NumberStyles.Float, CultureInfo.InvariantCulture ),
			int.Parse( entities[ 0 ], NumberStyles.None, CultureInfo.InvariantCulture ),
			int.Parse( entities[ 1 ], NumberStyles.None, CultureInfo.InvariantCulture ),
			int.Parse( fields[ 4 ], NumberStyles.None, CultureInfo.InvariantCulture )
		);
	}
	/// <summary>Parses Linux <c>/proc/uptime</c>.</summary>
	public static ProcUptimeInfo ParseUptime( string text ) {
		var fields = text.Split( (char[]?)null, StringSplitOptions.RemoveEmptyEntries );
		if ( 1 > fields.Length ) throw new FormatException( "Malformed /proc/uptime." );
		var uptime = TimeSpan.FromSeconds( double.Parse( fields[ 0 ], NumberStyles.Float, CultureInfo.InvariantCulture ) );
		TimeSpan? idle = 1 < fields.Length ? TimeSpan.FromSeconds( double.Parse( fields[ 1 ], NumberStyles.Float, CultureInfo.InvariantCulture ) ) : null;
		return new ProcUptimeInfo( uptime, idle );
	}
	/// <summary>Parses Linux <c>/proc/slabinfo</c> rows required by <c>slabtop</c> and <c>vmstat -m</c>.</summary>
	public static IReadOnlyList<ProcSlabEntry> ParseSlabInfo( string text ) {
		var entries = new List<ProcSlabEntry>();
		foreach ( var line in text.Split( '\n', StringSplitOptions.RemoveEmptyEntries ) ) {
			if ( line.StartsWith( "slabinfo", StringComparison.Ordinal ) || line.StartsWith( "#", StringComparison.Ordinal ) ) continue;
			var fields = line.Split( (char[]?)null, StringSplitOptions.RemoveEmptyEntries );
			if ( 6 > fields.Length ) continue;
			if ( !ulong.TryParse( fields[ 1 ], NumberStyles.None, CultureInfo.InvariantCulture, out var active ) ) continue;
			if ( !ulong.TryParse( fields[ 2 ], NumberStyles.None, CultureInfo.InvariantCulture, out var total ) ) continue;
			if ( !ulong.TryParse( fields[ 3 ], NumberStyles.None, CultureInfo.InvariantCulture, out var size ) ) continue;
			if ( !ulong.TryParse( fields[ 4 ], NumberStyles.None, CultureInfo.InvariantCulture, out var perSlab ) ) continue;
			if ( !ulong.TryParse( fields[ 5 ], NumberStyles.None, CultureInfo.InvariantCulture, out var pages ) ) continue;
			entries.Add( new ProcSlabEntry( fields[ 0 ], active, total, size, perSlab, pages ) );
		}
		return entries;
	}
	private static ProcObservedValue<ProcHugePageInfo> ParseHugePages( ProcMemoryInfo memory ) {
		ulong Read( string key ) => memory.Fields.TryGetValue( key, out var value ) ? value : 0UL;
		return ProcObservedValue<ProcHugePageInfo>.Available(
			new ProcHugePageInfo( Read( "HugePages_Total" ), Read( "HugePages_Free" ), Read( "HugePages_Rsvd" ), Read( "HugePages_Surp" ), Read( "Hugepagesize" ) ),
			ProcObservationSource.LinuxProcfs,
			ObservationFidelity.Exact
		);
	}
}

/// <summary>Provides only defensible cross-platform system metrics when Linux procfs is unavailable.</summary>
public sealed class PortableProcSystemMetricsProvider : IProcSystemMetricsProvider {
	/// <inheritdoc />
	public ProcSystemCapabilities Capabilities => ProcSystemCapabilities.Uptime;
	/// <inheritdoc />
	public async Task<ProcObservedValue<ProcMemoryInfo>> GetMemoryAsync( CancellationToken cancellationToken = default ) => ( await this.GetSnapshotAsync( cancellationToken ).ConfigureAwait( false ) ).Memory;
	/// <inheritdoc />
	public async Task<ProcObservedValue<ProcUptimeInfo>> GetUptimeAsync( bool containerMode, CancellationToken cancellationToken = default ) {
		cancellationToken.ThrowIfCancellationRequested();
		if ( containerMode ) return ProcObservedValue<ProcUptimeInfo>.Missing( ProcObservationAvailability.Unsupported, "Container uptime with procps-ng semantics is not exposed by the portable provider." );
		return ( await this.GetSnapshotAsync( cancellationToken ).ConfigureAwait( false ) ).Uptime;
	}
	/// <inheritdoc />
	public Task<ProcSystemSnapshot> GetSnapshotAsync( CancellationToken cancellationToken = default ) {
		cancellationToken.ThrowIfCancellationRequested();
		var unsupported = "Linux procps-ng system metric semantics are not exposed by the portable provider.";
		return Task.FromResult( new ProcSystemSnapshot {
			Cpu = ProcObservedValue<ProcCpuTimes>.Missing( ProcObservationAvailability.Unsupported, unsupported ),
			Memory = ProcObservedValue<ProcMemoryInfo>.Missing( ProcObservationAvailability.Unsupported, unsupported ),
			LoadAverage = ProcObservedValue<ProcLoadAverage>.Missing( ProcObservationAvailability.Unsupported, unsupported ),
			Uptime = ProcObservedValue<ProcUptimeInfo>.Available( new ProcUptimeInfo( TimeSpan.FromMilliseconds( Environment.TickCount64 ), null ), ProcObservationSource.PlatformApi, ObservationFidelity.Equivalent ),
			VirtualMemory = ProcObservedValue<IReadOnlyDictionary<string, ulong>>.Missing( ProcObservationAvailability.Unsupported, unsupported ),
			Slab = ProcObservedValue<IReadOnlyList<ProcSlabEntry>>.Missing( ProcObservationAvailability.Unsupported, unsupported ),
			HugePages = ProcObservedValue<ProcHugePageInfo>.Missing( ProcObservationAvailability.Unsupported, unsupported ),
			UserSessions = ProcObservedValue<ProcUserSessionInfo>.Missing( ProcObservationAvailability.Unsupported, unsupported )
		} );
	}
}
