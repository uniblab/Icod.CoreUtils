namespace Icod.ProcPs.Shared;

using System.Globalization;
using Icod.CoreUtils.Shared.Host;
/// <summary>Describes one configured huge-page pool for a Linux NUMA node.</summary>
public sealed class ProcHugePagePool {
	/// <summary>Gets the huge-page size in bytes.</summary>
	public ulong PageSizeBytes { get; }
	/// <summary>Gets the configured huge-page count.</summary>
	public ulong TotalPages { get; }
	/// <summary>Gets the currently free huge-page count.</summary>
	public ulong FreePages { get; }
	/// <summary>Initializes one huge-page pool observation.</summary>
	/// <param name="pageSizeBytes">Huge-page size in bytes.</param>
	/// <param name="totalPages">Configured page count.</param>
	/// <param name="freePages">Free page count.</param>
	public ProcHugePagePool( ulong pageSizeBytes, ulong totalPages, ulong freePages ) {
		if ( 0UL == pageSizeBytes ) {
			throw new ArgumentOutOfRangeException( nameof( pageSizeBytes ) );
		}
		if ( freePages > totalPages ) {
			throw new ArgumentOutOfRangeException( nameof( freePages ) );
		}
		this.PageSizeBytes = pageSizeBytes;
		this.TotalPages = totalPages;
		this.FreePages = freePages;
	}
}
/// <summary>Describes the huge-page pools observed for one Linux NUMA node.</summary>
public sealed class ProcHugePageNode {
	/// <summary>Gets the zero-based NUMA node identifier.</summary>
	public int NodeId { get; }
	/// <summary>Gets the node's huge-page pools ordered by page size.</summary>
	public IReadOnlyList<ProcHugePagePool> Pools { get; }
	/// <summary>Initializes a NUMA-node huge-page observation.</summary>
	/// <param name="nodeId">Zero-based NUMA node identifier.</param>
	/// <param name="pools">Observed huge-page pools.</param>
	public ProcHugePageNode( int nodeId, IEnumerable<ProcHugePagePool> pools ) {
		ArgumentOutOfRangeException.ThrowIfNegative( nodeId );
		ArgumentNullException.ThrowIfNull( pools );
		this.NodeId = nodeId;
		this.Pools = pools.OrderBy( static pool => pool.PageSizeBytes ).ToArray();
	}
}
/// <summary>Describes huge-page memory attributed to one process.</summary>
public sealed class ProcHugePageProcess {
	/// <summary>Gets the process identifier.</summary>
	public int ProcessId { get; }
	/// <summary>Gets the observed short command name.</summary>
	public string CommandName { get; }
	/// <summary>Gets bytes mapped through shared hugetlb mappings.</summary>
	public ulong SharedBytes { get; }
	/// <summary>Gets bytes mapped through private hugetlb mappings.</summary>
	public ulong PrivateBytes { get; }
	/// <summary>Initializes one process huge-page observation.</summary>
	/// <param name="processId">Positive process identifier.</param>
	/// <param name="commandName">Observed short command name.</param>
	/// <param name="sharedBytes">Shared hugetlb bytes.</param>
	/// <param name="privateBytes">Private hugetlb bytes.</param>
	public ProcHugePageProcess( int processId, string commandName, ulong sharedBytes, ulong privateBytes ) {
		if ( 0 >= processId ) {
			throw new ArgumentOutOfRangeException( nameof( processId ) );
		}
		ArgumentException.ThrowIfNullOrWhiteSpace( commandName );
		this.ProcessId = processId;
		this.CommandName = commandName;
		this.SharedBytes = sharedBytes;
		this.PrivateBytes = privateBytes;
	}
}
/// <summary>Contains one coherent huge-page system and process observation.</summary>
public sealed class ProcHugePageSnapshot {
	/// <summary>Gets NUMA-node huge-page pool observations.</summary>
	public IReadOnlyList<ProcHugePageNode> Nodes { get; }
	/// <summary>Gets processes with nonzero hugetlb usage.</summary>
	public IReadOnlyList<ProcHugePageProcess> Processes { get; }
	/// <summary>Initializes a huge-page snapshot.</summary>
	/// <param name="nodes">NUMA-node huge-page observations.</param>
	/// <param name="processes">Process huge-page observations.</param>
	public ProcHugePageSnapshot( IEnumerable<ProcHugePageNode> nodes, IEnumerable<ProcHugePageProcess> processes ) {
		ArgumentNullException.ThrowIfNull( nodes );
		ArgumentNullException.ThrowIfNull( processes );
		this.Nodes = nodes.OrderBy( static node => node.NodeId ).ToArray();
		this.Processes = processes
			.OrderByDescending( static process => SaturatingAdd( process.SharedBytes, process.PrivateBytes ) )
			.ThenBy( static process => process.ProcessId )
			.ToArray();
	}
	private static ulong SaturatingAdd( ulong left, ulong right ) {
		if ( ulong.MaxValue - left < right ) {
			return ulong.MaxValue;
		}
		return left + right;
	}
}
/// <summary>Observes procps-ng compatible huge-page system and process information.</summary>
public interface IProcHugePageProvider {
	/// <summary>Captures huge-page pools and process hugetlb usage.</summary>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>The observed snapshot or an explicit availability result.</returns>
	Task<ProcObservedValue<ProcHugePageSnapshot>> GetSnapshotAsync( CancellationToken cancellationToken = default );
}
/// <summary>Selects the exact Linux huge-page provider or a controlled unsupported provider.</summary>
public sealed class SystemProcHugePageProvider : IProcHugePageProvider {
	private readonly IProcHugePageProvider _inner;
	/// <summary>Gets the shared system huge-page provider.</summary>
	public static SystemProcHugePageProvider Instance { get; } = new();
	/// <summary>Initializes the system huge-page provider.</summary>
	public SystemProcHugePageProvider() {
		if ( OperatingSystem.IsLinux() ) {
			this._inner = new LinuxProcHugePageProvider(
				SystemProcProcessProvider.Instance,
				SystemProcMemoryMapProvider.Instance
			);
		} else {
			this._inner = UnsupportedProcHugePageProvider.Instance;
		}
	}
	/// <inheritdoc />
	public Task<ProcObservedValue<ProcHugePageSnapshot>> GetSnapshotAsync( CancellationToken cancellationToken = default ) {
		return this._inner.GetSnapshotAsync( cancellationToken );
	}
	private sealed class UnsupportedProcHugePageProvider : IProcHugePageProvider {
		/// <summary>Gets the shared unsupported huge-page provider.</summary>
		public static UnsupportedProcHugePageProvider Instance { get; } = new();
		/// <inheritdoc />
		public Task<ProcObservedValue<ProcHugePageSnapshot>> GetSnapshotAsync( CancellationToken cancellationToken = default ) {
			cancellationToken.ThrowIfCancellationRequested();
			return Task.FromResult(
				ProcObservedValue<ProcHugePageSnapshot>.Missing(
					ProcObservationAvailability.Unsupported,
					"hugetop requires Linux sysfs huge-page pools and /proc/PID/smaps hugetlb accounting."
				)
			);
		}
	}
}
/// <summary>Reads exact Linux huge-page pools and per-process hugetlb accounting.</summary>
public sealed class LinuxProcHugePageProvider : IProcHugePageProvider {
	private const ulong Kibibyte = 1024UL;
	private readonly IProcProcessProvider _processProvider;
	private readonly IProcMemoryMapProvider _memoryMapProvider;
	private readonly string _sysNodeRoot;
	/// <summary>Initializes a Linux huge-page provider.</summary>
	/// <param name="processProvider">Linux process provider.</param>
	/// <param name="memoryMapProvider">Detailed Linux memory-map provider.</param>
	/// <param name="sysNodeRoot">Linux sysfs node root.</param>
	public LinuxProcHugePageProvider(
		IProcProcessProvider processProvider,
		IProcMemoryMapProvider memoryMapProvider,
		string sysNodeRoot = "/sys/devices/system/node"
	) {
		ArgumentNullException.ThrowIfNull( processProvider );
		ArgumentNullException.ThrowIfNull( memoryMapProvider );
		ArgumentException.ThrowIfNullOrWhiteSpace( sysNodeRoot );
		this._processProvider = processProvider;
		this._memoryMapProvider = memoryMapProvider;
		this._sysNodeRoot = sysNodeRoot;
	}
	/// <inheritdoc />
	public async Task<ProcObservedValue<ProcHugePageSnapshot>> GetSnapshotAsync( CancellationToken cancellationToken = default ) {
		cancellationToken.ThrowIfCancellationRequested();
		try {
			var nodes = await this.ReadNodesAsync( cancellationToken ).ConfigureAwait( false );
			if ( 0 == nodes.Count ) {
				return ProcObservedValue<ProcHugePageSnapshot>.Missing(
					ProcObservationAvailability.Unavailable,
					"No Linux sysfs huge-page pools were found."
				);
			}
			var processes = await this.ReadProcessesAsync( cancellationToken ).ConfigureAwait( false );
			return ProcObservedValue<ProcHugePageSnapshot>.Available(
				new ProcHugePageSnapshot( nodes, processes ),
				ProcObservationSource.LinuxSysfs,
				ObservationFidelity.Exact
			);
		} catch ( UnauthorizedAccessException exception ) {
			return ProcObservedValue<ProcHugePageSnapshot>.Missing( ProcObservationAvailability.AccessDenied, exception.Message );
		} catch ( DirectoryNotFoundException exception ) {
			return ProcObservedValue<ProcHugePageSnapshot>.Missing( ProcObservationAvailability.Unavailable, exception.Message );
		} catch ( FileNotFoundException exception ) {
			return ProcObservedValue<ProcHugePageSnapshot>.Missing( ProcObservationAvailability.Unavailable, exception.Message );
		} catch ( IOException exception ) {
			return ProcObservedValue<ProcHugePageSnapshot>.Missing( ProcObservationAvailability.Unavailable, exception.Message );
		} catch ( FormatException exception ) {
			return ProcObservedValue<ProcHugePageSnapshot>.Missing( ProcObservationAvailability.Malformed, exception.Message );
		} catch ( OverflowException exception ) {
			return ProcObservedValue<ProcHugePageSnapshot>.Missing( ProcObservationAvailability.Malformed, exception.Message );
		}
	}
	private async Task<IReadOnlyList<ProcHugePageNode>> ReadNodesAsync( CancellationToken cancellationToken ) {
		var nodes = new List<ProcHugePageNode>();
		foreach ( var nodeDirectory in Directory.EnumerateDirectories( this._sysNodeRoot, "node*", SearchOption.TopDirectoryOnly ) ) {
			cancellationToken.ThrowIfCancellationRequested();
			var nodeName = System.IO.Path.GetFileName( nodeDirectory );
			if ( !TryParseNodeId( nodeName, out var nodeId ) ) {
				continue;
			}
			var hugePagesDirectory = System.IO.Path.Combine( nodeDirectory, "hugepages" );
			if ( !Directory.Exists( hugePagesDirectory ) ) {
				continue;
			}
			var pools = new List<ProcHugePagePool>();
			foreach ( var poolDirectory in Directory.EnumerateDirectories( hugePagesDirectory, "hugepages-*kB", SearchOption.TopDirectoryOnly ) ) {
				cancellationToken.ThrowIfCancellationRequested();
				if ( !TryParsePageSize( System.IO.Path.GetFileName( poolDirectory ), out var pageSizeBytes ) ) {
					continue;
				}
				var totalPages = await ReadUnsignedAsync( System.IO.Path.Combine( poolDirectory, "nr_hugepages" ), cancellationToken ).ConfigureAwait( false );
				var freePages = await ReadUnsignedAsync( System.IO.Path.Combine( poolDirectory, "free_hugepages" ), cancellationToken ).ConfigureAwait( false );
				if ( freePages > totalPages ) {
					throw new FormatException( $"Huge-page pool '{poolDirectory}' reports more free pages than configured pages." );
				}
				pools.Add( new ProcHugePagePool( pageSizeBytes, totalPages, freePages ) );
			}
			if ( 0 < pools.Count ) {
				nodes.Add( new ProcHugePageNode( nodeId, pools ) );
			}
		}
		return nodes.OrderBy( static node => node.NodeId ).ToArray();
	}
	private async Task<IReadOnlyList<ProcHugePageProcess>> ReadProcessesAsync( CancellationToken cancellationToken ) {
		var collection = await this._processProvider.GetProcessesAsync( cancellationToken ).ConfigureAwait( false );
		var result = new List<ProcHugePageProcess>();
		foreach ( var process in collection.Processes ) {
			cancellationToken.ThrowIfCancellationRequested();
			var maps = await this._memoryMapProvider.ObserveAsync( process, detailed: true, cancellationToken: cancellationToken ).ConfigureAwait( false );
			if ( !maps.HasValue ) {
				continue;
			}
			ulong sharedKilobytes = 0UL;
			ulong privateKilobytes = 0UL;
			foreach ( var region in maps.Value.Regions ) {
				sharedKilobytes = SaturatingAdd( sharedKilobytes, region.GetMetric( "Shared_Hugetlb" ) ?? 0UL );
				privateKilobytes = SaturatingAdd( privateKilobytes, region.GetMetric( "Private_Hugetlb" ) ?? 0UL );
			}
			if ( 0UL == sharedKilobytes && 0UL == privateKilobytes ) {
				continue;
			}
			var commandName = "?";
			if ( process.CommandName.HasValue && !string.IsNullOrWhiteSpace( process.CommandName.Value ) ) {
				commandName = process.CommandName.Value;
			}
			result.Add(
				new ProcHugePageProcess(
					process.ProcessId,
					commandName,
					SaturatingMultiply( sharedKilobytes, Kibibyte ),
					SaturatingMultiply( privateKilobytes, Kibibyte )
				)
			);
		}
		return result
			.OrderByDescending( static process => SaturatingAdd( process.SharedBytes, process.PrivateBytes ) )
			.ThenBy( static process => process.ProcessId )
			.ToArray();
	}
	private static bool TryParseNodeId( string? name, out int nodeId ) {
		nodeId = 0;
		if ( string.IsNullOrWhiteSpace( name ) || !name.StartsWith( "node", StringComparison.Ordinal ) ) {
			return false;
		}
		return int.TryParse( name[ 4.. ], NumberStyles.None, CultureInfo.InvariantCulture, out nodeId ) && 0 <= nodeId;
	}
	private static bool TryParsePageSize( string? name, out ulong pageSizeBytes ) {
		pageSizeBytes = 0UL;
		const string prefix = "hugepages-";
		const string suffix = "kB";
		if ( string.IsNullOrWhiteSpace( name )
			|| !name.StartsWith( prefix, StringComparison.Ordinal )
			|| !name.EndsWith( suffix, StringComparison.Ordinal ) ) {
			return false;
		}
		var sizeText = name[ prefix.Length..^suffix.Length ];
		if ( !ulong.TryParse( sizeText, NumberStyles.None, CultureInfo.InvariantCulture, out var kibibytes ) || 0UL == kibibytes ) {
			return false;
		}
		pageSizeBytes = checked( kibibytes * Kibibyte );
		return true;
	}
	private static async Task<ulong> ReadUnsignedAsync( string path, CancellationToken cancellationToken ) {
		var text = await File.ReadAllTextAsync( path, cancellationToken ).ConfigureAwait( false );
		return ulong.Parse( text.Trim(), NumberStyles.None, CultureInfo.InvariantCulture );
	}
	private static ulong SaturatingAdd( ulong left, ulong right ) {
		if ( ulong.MaxValue - left < right ) {
			return ulong.MaxValue;
		}
		return left + right;
	}
	private static ulong SaturatingMultiply( ulong value, ulong multiplier ) {
		if ( 0UL != multiplier && ulong.MaxValue / multiplier < value ) {
			return ulong.MaxValue;
		}
		return value * multiplier;
	}
}
/// <summary>Describes one Linux slab-cache row with slab-instance counts required by slabtop.</summary>
public sealed class ProcSlabCacheEntry {
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
	/// <summary>Gets active slab count.</summary>
	public ulong ActiveSlabs { get; }
	/// <summary>Gets total slab count.</summary>
	public ulong TotalSlabs { get; }
	/// <summary>Initializes a complete slab-cache observation.</summary>
	public ProcSlabCacheEntry(
		string name,
		ulong activeObjects,
		ulong totalObjects,
		ulong objectSizeBytes,
		ulong objectsPerSlab,
		ulong pagesPerSlab,
		ulong activeSlabs,
		ulong totalSlabs
	) {
		ArgumentException.ThrowIfNullOrWhiteSpace( name );
		if ( activeObjects > totalObjects ) {
			throw new ArgumentOutOfRangeException( nameof( activeObjects ) );
		}
		if ( activeSlabs > totalSlabs ) {
			throw new ArgumentOutOfRangeException( nameof( activeSlabs ) );
		}
		if ( 0UL == objectSizeBytes ) {
			throw new ArgumentOutOfRangeException( nameof( objectSizeBytes ) );
		}
		if ( 0UL == objectsPerSlab ) {
			throw new ArgumentOutOfRangeException( nameof( objectsPerSlab ) );
		}
		if ( 0UL == pagesPerSlab ) {
			throw new ArgumentOutOfRangeException( nameof( pagesPerSlab ) );
		}
		this.Name = name;
		this.ActiveObjects = activeObjects;
		this.TotalObjects = totalObjects;
		this.ObjectSizeBytes = objectSizeBytes;
		this.ObjectsPerSlab = objectsPerSlab;
		this.PagesPerSlab = pagesPerSlab;
		this.ActiveSlabs = activeSlabs;
		this.TotalSlabs = totalSlabs;
	}
}
/// <summary>Observes the exact Linux slab allocator cache table required by slabtop.</summary>
public interface IProcSlabProvider {
	/// <summary>Captures the current slab-cache table.</summary>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>The slab-cache rows or an explicit availability result.</returns>
	Task<ProcObservedValue<IReadOnlyList<ProcSlabCacheEntry>>> GetSlabsAsync( CancellationToken cancellationToken = default );
}
/// <summary>Selects the Linux slabinfo provider or a controlled unsupported provider.</summary>
public sealed class SystemProcSlabProvider : IProcSlabProvider {
	private readonly IProcSlabProvider _inner;
	/// <summary>Gets the shared system slab provider.</summary>
	public static SystemProcSlabProvider Instance { get; } = new();
	/// <summary>Initializes the system slab provider.</summary>
	public SystemProcSlabProvider() {
		if ( OperatingSystem.IsLinux() ) {
			this._inner = new LinuxProcSlabProvider();
		} else {
			this._inner = UnsupportedProcSlabProvider.Instance;
		}
	}
	/// <inheritdoc />
	public Task<ProcObservedValue<IReadOnlyList<ProcSlabCacheEntry>>> GetSlabsAsync( CancellationToken cancellationToken = default ) {
		return this._inner.GetSlabsAsync( cancellationToken );
	}
	private sealed class UnsupportedProcSlabProvider : IProcSlabProvider {
		/// <summary>Gets the shared unsupported slab provider.</summary>
		public static UnsupportedProcSlabProvider Instance { get; } = new();
		/// <inheritdoc />
		public Task<ProcObservedValue<IReadOnlyList<ProcSlabCacheEntry>>> GetSlabsAsync( CancellationToken cancellationToken = default ) {
			cancellationToken.ThrowIfCancellationRequested();
			return Task.FromResult(
				ProcObservedValue<IReadOnlyList<ProcSlabCacheEntry>>.Missing(
					ProcObservationAvailability.Unsupported,
					"slabtop requires the Linux /proc/slabinfo allocator interface."
				)
			);
		}
	}
}
/// <summary>Reads exact slab-cache accounting from Linux procfs.</summary>
public sealed class LinuxProcSlabProvider : IProcSlabProvider {
	private readonly string _procRoot;
	/// <summary>Initializes a Linux slab provider.</summary>
	/// <param name="procRoot">Linux procfs root used by production or fixtures.</param>
	public LinuxProcSlabProvider( string procRoot = "/proc" ) {
		ArgumentException.ThrowIfNullOrWhiteSpace( procRoot );
		this._procRoot = procRoot;
	}
	/// <inheritdoc />
	public async Task<ProcObservedValue<IReadOnlyList<ProcSlabCacheEntry>>> GetSlabsAsync( CancellationToken cancellationToken = default ) {
		cancellationToken.ThrowIfCancellationRequested();
		try {
			var text = await File.ReadAllTextAsync( System.IO.Path.Combine( this._procRoot, "slabinfo" ), cancellationToken ).ConfigureAwait( false );
			var entries = ProcKernelMemoryParsers.ParseSlabInfo( text );
			return ProcObservedValue<IReadOnlyList<ProcSlabCacheEntry>>.Available(
				entries,
				ProcObservationSource.LinuxProcfs,
				ObservationFidelity.Exact
			);
		} catch ( UnauthorizedAccessException exception ) {
			return ProcObservedValue<IReadOnlyList<ProcSlabCacheEntry>>.Missing( ProcObservationAvailability.AccessDenied, exception.Message );
		} catch ( DirectoryNotFoundException exception ) {
			return ProcObservedValue<IReadOnlyList<ProcSlabCacheEntry>>.Missing( ProcObservationAvailability.Unavailable, exception.Message );
		} catch ( FileNotFoundException exception ) {
			return ProcObservedValue<IReadOnlyList<ProcSlabCacheEntry>>.Missing( ProcObservationAvailability.Unavailable, exception.Message );
		} catch ( IOException exception ) {
			return ProcObservedValue<IReadOnlyList<ProcSlabCacheEntry>>.Missing( ProcObservationAvailability.Unavailable, exception.Message );
		} catch ( FormatException exception ) {
			return ProcObservedValue<IReadOnlyList<ProcSlabCacheEntry>>.Missing( ProcObservationAvailability.Malformed, exception.Message );
		}
	}
}
/// <summary>Parses Linux kernel-memory text formats used by ProcPs full-screen tools.</summary>
public static class ProcKernelMemoryParsers {
	/// <summary>Parses Linux <c>/proc/slabinfo</c> including the <c>slabdata</c> active/total slab counts.</summary>
	/// <param name="text">Complete slabinfo text.</param>
	/// <returns>Parsed slab-cache entries in kernel order.</returns>
	public static IReadOnlyList<ProcSlabCacheEntry> ParseSlabInfo( string text ) {
		ArgumentNullException.ThrowIfNull( text );
		var entries = new List<ProcSlabCacheEntry>();
		foreach ( var line in text.Split( '\n', StringSplitOptions.RemoveEmptyEntries ) ) {
			if ( line.StartsWith( "slabinfo", StringComparison.Ordinal ) || line.StartsWith( "#", StringComparison.Ordinal ) ) {
				continue;
			}
			var fields = line.Split( (char[]?)null, StringSplitOptions.RemoveEmptyEntries );
			if ( 6 > fields.Length ) {
				throw new FormatException( $"The slabinfo row '{line}' does not contain the required core fields." );
			}
			if ( !TryReadCoreFields( fields, out var activeObjects, out var totalObjects, out var objectSize, out var objectsPerSlab, out var pagesPerSlab ) ) {
				throw new FormatException( $"The slabinfo row for '{fields[ 0 ]}' contains invalid numeric fields." );
			}
			if ( activeObjects > totalObjects ) {
				throw new FormatException( $"The slabinfo row for '{fields[ 0 ]}' reports more active objects than total objects." );
			}
			if ( 0UL == objectSize || 0UL == objectsPerSlab || 0UL == pagesPerSlab ) {
				throw new FormatException( $"The slabinfo row for '{fields[ 0 ]}' reports a zero size or slab geometry." );
			}
			var slabDataIndex = Array.FindIndex( fields, static field => string.Equals( field, "slabdata", StringComparison.Ordinal ) );
			if ( 0 > slabDataIndex
				|| slabDataIndex + 2 >= fields.Length
				|| !ulong.TryParse( fields[ slabDataIndex + 1 ], NumberStyles.None, CultureInfo.InvariantCulture, out var activeSlabs )
				|| !ulong.TryParse( fields[ slabDataIndex + 2 ], NumberStyles.None, CultureInfo.InvariantCulture, out var totalSlabs ) ) {
				throw new FormatException( $"The slabinfo row for '{fields[ 0 ]}' does not contain valid slabdata counts." );
			}
			if ( activeSlabs > totalSlabs ) {
				throw new FormatException( $"The slabinfo row for '{fields[ 0 ]}' reports more active slabs than total slabs." );
			}
			entries.Add(
				new ProcSlabCacheEntry(
					fields[ 0 ],
					activeObjects,
					totalObjects,
					objectSize,
					objectsPerSlab,
					pagesPerSlab,
					activeSlabs,
					totalSlabs
				)
			);
		}
		return entries;
	}
	private static bool TryReadCoreFields(
		string[] fields,
		out ulong activeObjects,
		out ulong totalObjects,
		out ulong objectSize,
		out ulong objectsPerSlab,
		out ulong pagesPerSlab
	) {
		activeObjects = 0UL;
		totalObjects = 0UL;
		objectSize = 0UL;
		objectsPerSlab = 0UL;
		pagesPerSlab = 0UL;
		return ulong.TryParse( fields[ 1 ], NumberStyles.None, CultureInfo.InvariantCulture, out activeObjects )
			&& ulong.TryParse( fields[ 2 ], NumberStyles.None, CultureInfo.InvariantCulture, out totalObjects )
			&& ulong.TryParse( fields[ 3 ], NumberStyles.None, CultureInfo.InvariantCulture, out objectSize )
			&& ulong.TryParse( fields[ 4 ], NumberStyles.None, CultureInfo.InvariantCulture, out objectsPerSlab )
			&& ulong.TryParse( fields[ 5 ], NumberStyles.None, CultureInfo.InvariantCulture, out pagesPerSlab );
	}
}
