/*
	Icod.CoreUtils.Shared
	Shared support library for the Icod.CoreUtils command suite.
	Copyright (C) 2026  Timothy J. Bruce <uniblab@hotmail.com>
*/

/*
	This program is free software: you can redistribute it and/or modify
	it under the terms of the GNU Lesser General Public License as published by
	the Free Software Foundation, either version 3 of the License, or
	(at your option) any later version.

	This program is distributed in the hope that it will be useful,
	but WITHOUT ANY WARRANTY; without even the implied warranty of
	MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
	GNU Lesser General Public License for more details.

	You should have received a copy of the GNU Lesser General Public License
	along with this program.  If not, see <https://www.gnu.org/licenses/>.
*/

namespace Icod.CoreUtils.Shared.Ordering;

/// <summary>Configures bounded-memory run generation and bounded-fan-in external merging.</summary>
/// <typeparam name="T">The ordered value type.</typeparam>
public sealed class ExternalOrderingOptions<T> {
	/// <summary>Defines the default per-item managed bookkeeping estimate.</summary>
	public const long DefaultPerItemOverheadBytes = 64;

	/// <summary>Defines the default maximum merge fan-in.</summary>
	public const int DefaultMergeFanIn = 64;

	/// <summary>Defines the default run-stream buffer size.</summary>
	public const int DefaultFileBufferSize = 65_536;

	/// <summary>Defines the default secure run-file template.</summary>
	public const string DefaultRunFileTemplate = "run-XXXXXXXX.bin";

	/// <summary>Initializes external-ordering options.</summary>
	/// <param name="memoryLimitBytes">The approximate maximum payload and bookkeeping bytes retained in one in-memory run.</param>
	/// <param name="sizeEstimator">A deterministic nonnegative payload-size estimator.</param>
	/// <param name="perItemOverheadBytes">The additional managed bookkeeping estimate per item.</param>
	/// <param name="mergeFanIn">The maximum number of run streams opened by one merge operation.</param>
	/// <param name="fileBufferSize">The buffer size used for run streams.</param>
	/// <param name="runFileTemplate">The secure leaf-name template used for run files.</param>
	public ExternalOrderingOptions(
		long memoryLimitBytes,
		Func<T, long> sizeEstimator,
		long perItemOverheadBytes = DefaultPerItemOverheadBytes,
		int mergeFanIn = DefaultMergeFanIn,
		int fileBufferSize = DefaultFileBufferSize,
		string runFileTemplate = DefaultRunFileTemplate
	) {
		ArgumentOutOfRangeException.ThrowIfNegativeOrZero( memoryLimitBytes );
		ArgumentNullException.ThrowIfNull( sizeEstimator );
		ArgumentOutOfRangeException.ThrowIfNegative( perItemOverheadBytes );
		if ( 2 > mergeFanIn ) {
			throw new ArgumentOutOfRangeException(
				nameof( mergeFanIn ),
				"External merge fan-in must be at least two."
			);
		}
		ArgumentOutOfRangeException.ThrowIfNegativeOrZero( fileBufferSize );
		ArgumentException.ThrowIfNullOrWhiteSpace( runFileTemplate );
		this.MemoryLimitBytes = memoryLimitBytes;
		this.SizeEstimator = sizeEstimator;
		this.PerItemOverheadBytes = perItemOverheadBytes;
		this.MergeFanIn = mergeFanIn;
		this.FileBufferSize = fileBufferSize;
		this.RunFileTemplate = runFileTemplate;
	}

	/// <summary>Gets the approximate maximum bytes retained in one in-memory run.</summary>
	public long MemoryLimitBytes { get; }

	/// <summary>Gets the deterministic payload-size estimator.</summary>
	public Func<T, long> SizeEstimator { get; }

	/// <summary>Gets the additional managed bookkeeping estimate per item.</summary>
	public long PerItemOverheadBytes { get; }

	/// <summary>Gets the maximum number of input run streams opened by one merge operation.</summary>
	public int MergeFanIn { get; }

	/// <summary>Gets the run-stream buffer size.</summary>
	public int FileBufferSize { get; }

	/// <summary>Gets the secure run-file leaf-name template.</summary>
	public string RunFileTemplate { get; }
}
