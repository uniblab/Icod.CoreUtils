namespace Icod.CoreUtils.DD;

/// <summary>
/// Identifies byte, record, file-creation, and synchronization conversions accepted by the <c>conv=</c> operand.
/// </summary>
internal enum DdConversion {
	/// <summary>
	/// Translates EBCDIC input bytes to ASCII before other record conversions.
	/// </summary>
	Ascii,
	/// <summary>
	/// Translates ASCII output bytes to the standard EBCDIC table.
	/// </summary>
	Ebcdic,
	/// <summary>
	/// Translates ASCII output bytes to the alternate IBM EBCDIC table.
	/// </summary>
	Ibm,
	/// <summary>
	/// Pads newline-terminated input records with spaces to the conversion block size.
	/// </summary>
	Block,
	/// <summary>
	/// Converts fixed-width records to newline-terminated records after trimming trailing spaces.
	/// </summary>
	Unblock,
	/// <summary>
	/// Converts ASCII uppercase letters to lowercase.
	/// </summary>
	LowerCase,
	/// <summary>
	/// Converts ASCII lowercase letters to uppercase.
	/// </summary>
	UpperCase,
	/// <summary>
	/// Seeks over all-zero output blocks when the output supports sparse positioning.
	/// </summary>
	Sparse,
	/// <summary>
	/// Swaps each adjacent pair of input bytes, preserving an odd byte across reads.
	/// </summary>
	Swab,
	/// <summary>
	/// Pads short input blocks to the configured input block size.
	/// </summary>
	Sync,
	/// <summary>
	/// Requires creation of a new output file and fails if it already exists.
	/// </summary>
	Exclusive,
	/// <summary>
	/// Prevents creation of a missing output file.
	/// </summary>
	NoCreate,
	/// <summary>
	/// Preserves existing output data beyond the bytes written.
	/// </summary>
	NoTruncate,
	/// <summary>
	/// Continues after recoverable input errors.
	/// </summary>
	NoError,
	/// <summary>
	/// Flushes output file data to stable storage before completion.
	/// </summary>
	FDataSync,
	/// <summary>
	/// Flushes output data and metadata to stable storage before completion.
	/// </summary>
	FileSystemSync,
}

/// <summary>
/// Identifies low-level input or output behavior requested through <c>iflag=</c> or <c>oflag=</c>.
/// </summary>
internal enum DdFlag {
	/// <summary>
	/// Writes output at the end of the destination.
	/// </summary>
	Append,
	/// <summary>
	/// Requests direct I/O that bypasses normal buffering when the platform can provide it.
	/// </summary>
	Direct,
	/// <summary>
	/// Requires the selected input or output path to name a directory.
	/// </summary>
	Directory,
	/// <summary>
	/// Requests synchronized data writes.
	/// </summary>
	DataSync,
	/// <summary>
	/// Requests synchronized data and metadata writes.
	/// </summary>
	Sync,
	/// <summary>
	/// Accumulates reads until a complete input block is available or end of input is reached.
	/// </summary>
	FullBlock,
	/// <summary>
	/// Requests non-blocking I/O.
	/// </summary>
	NonBlock,
	/// <summary>
	/// Requests that reads not update file access time.
	/// </summary>
	NoAccessTime,
	/// <summary>
	/// Requests that transferred data not remain in the filesystem cache.
	/// </summary>
	NoCache,
	/// <summary>
	/// Prevents a terminal input from becoming the process controlling terminal.
	/// </summary>
	NoControllingTerminal,
	/// <summary>
	/// Rejects symbolic-link path targets.
	/// </summary>
	NoFollow,
}

/// <summary>
/// Controls which transfer statistics <c>dd</c> writes to standard error.
/// </summary>
internal enum DdStatusMode {
	/// <summary>
	/// Writes final record counts and transfer-rate information.
	/// </summary>
	Default,
	/// <summary>
	/// Suppresses transfer statistics.
	/// </summary>
	None,
	/// <summary>
	/// Writes record counts but omits the transfer-rate line.
	/// </summary>
	NoTransfer,
	/// <summary>
	/// Writes periodic transfer progress in addition to the final report.
	/// </summary>
	Progress,
}

/// <summary>
/// Represents a parsed <c>dd</c> count, skip, or seek magnitude together with its unit interpretation.
/// </summary>
/// <remarks>
/// The command converts block-based quantities to byte offsets only after the relevant input or output block size is known.
/// </remarks>
/// <param name="Value">The non-negative parsed magnitude after suffix multiplication.</param>
/// <param name="IsBytes"><see langword="true"/> when the operand explicitly requests byte units instead of block units.</param>
internal readonly record struct DdQuantity(
	long Value,
	bool IsBytes
);

/// <summary>
/// Stores the validated operand state used by the <c>dd</c> copy engine.
/// </summary>
/// <remarks>
/// The object retains both scalar settings and the selected conversion and flag sets. <see cref="ApplyBlockSizeOverride"/> normalizes <c>bs=</c> after parsing.
/// </remarks>
internal sealed class DdOptions {
	/// <summary>
	/// Defines the GNU default input and output block size of 512 bytes.
	/// </summary>
	public const int DefaultBlockSize = 512;

	/// <summary>
	/// Gets or sets the optional <c>bs=</c> value that overrides both input and output block sizes.
	/// </summary>
	/// <value>The optional <c>bs=</c> value that overrides both input and output block sizes.</value>
	public int? BlockSizeOverride {
		get;
		set;
	}

	/// <summary>
	/// Gets or sets the number of bytes requested for each input block.
	/// </summary>
	/// <value>The number of bytes requested for each input block.</value>
	public int InputBlockSize {
		get;
		set;
	} = DefaultBlockSize;

	/// <summary>
	/// Gets or sets the number of bytes accumulated for each output block.
	/// </summary>
	/// <value>The number of bytes accumulated for each output block.</value>
	public int OutputBlockSize {
		get;
		set;
	} = DefaultBlockSize;

	/// <summary>
	/// Gets or sets the record width used by <c>block</c> and <c>unblock</c> conversions.
	/// </summary>
	/// <value>The record width used by <c>block</c> and <c>unblock</c> conversions.</value>
	public int ConversionBlockSize {
		get;
		set;
	}

	/// <summary>
	/// Gets or sets the input pathname, or <see langword="null"/> for standard input.
	/// </summary>
	/// <value>The input pathname, or <see langword="null"/> for standard input.</value>
	public string? InputFile {
		get;
		set;
	}

	/// <summary>
	/// Gets or sets the output pathname, or <see langword="null"/> for standard output.
	/// </summary>
	/// <value>The output pathname, or <see langword="null"/> for standard output.</value>
	public string? OutputFile {
		get;
		set;
	}

	/// <summary>
	/// Gets or sets the optional maximum number of input blocks or bytes to copy.
	/// </summary>
	/// <value>The optional maximum number of input blocks or bytes to copy.</value>
	public DdQuantity? Count {
		get;
		set;
	}

	/// <summary>
	/// Gets or sets the number of input blocks or bytes skipped before copying.
	/// </summary>
	/// <value>The number of input blocks or bytes skipped before copying.</value>
	public DdQuantity Skip {
		get;
		set;
	}

	/// <summary>
	/// Gets or sets the number of output blocks or bytes skipped before writing.
	/// </summary>
	/// <value>The number of output blocks or bytes skipped before writing.</value>
	public DdQuantity Seek {
		get;
		set;
	}

	/// <summary>
	/// Gets or sets the selected statistics-reporting mode.
	/// </summary>
	/// <value>The selected statistics-reporting mode.</value>
	public DdStatusMode Status {
		get;
		set;
	} = DdStatusMode.Default;

	/// <summary>
	/// Gets the set of requested <c>conv=</c> operations.
	/// </summary>
	/// <value>The set of requested <c>conv=</c> operations.</value>
	public HashSet<DdConversion> Conversions {
		get;
	} = [];

	/// <summary>
	/// Gets the set of requested <c>iflag=</c> behaviors.
	/// </summary>
	/// <value>The set of requested <c>iflag=</c> behaviors.</value>
	public HashSet<DdFlag> InputFlags {
		get;
	} = [];

	/// <summary>
	/// Gets the set of requested <c>oflag=</c> behaviors.
	/// </summary>
	/// <value>The set of requested <c>oflag=</c> behaviors.</value>
	public HashSet<DdFlag> OutputFlags {
		get;
	} = [];

	/// <summary>
	/// Determines whether a conversion was requested.
	/// </summary>
	/// <param name="conversion">The conversion whose presence is tested.</param>
	/// <returns><see langword="true"/> when the conversion is present.</returns>
	public bool HasConversion(
		DdConversion conversion
	) => this.Conversions.Contains( conversion );

	/// <summary>
	/// Determines whether an input flag was requested.
	/// </summary>
	/// <param name="flag">The input or output flag whose presence is tested.</param>
	/// <returns><see langword="true"/> when the input flag is present.</returns>
	public bool HasInputFlag(
		DdFlag flag
	) => this.InputFlags.Contains( flag );

	/// <summary>
	/// Determines whether an output flag was requested.
	/// </summary>
	/// <param name="flag">The input or output flag whose presence is tested.</param>
	/// <returns><see langword="true"/> when the output flag is present.</returns>
	public bool HasOutputFlag(
		DdFlag flag
	) => this.OutputFlags.Contains( flag );

	/// <summary>
	/// Gets whether the selected conversions require newline records to be padded to fixed width.
	/// </summary>
	/// <value>Whether the selected conversions require newline records to be padded to fixed width.</value>
	public bool UsesBlockConversion => 0 < this.ConversionBlockSize
		&& (
			this.HasConversion( DdConversion.Block )
			|| this.HasConversion( DdConversion.Ebcdic )
			|| this.HasConversion( DdConversion.Ibm )
		)
	;

	/// <summary>
	/// Gets whether the selected conversions require fixed-width records to become newline records.
	/// </summary>
	/// <value>Whether the selected conversions require fixed-width records to become newline records.</value>
	public bool UsesUnblockConversion => 0 < this.ConversionBlockSize
		&& (
			this.HasConversion( DdConversion.Unblock )
			|| this.HasConversion( DdConversion.Ascii )
		)
	;

	/// <summary>
	/// Gets whether <c>bs=</c> and the selected conversions permit one input block to map directly to one output block.
	/// </summary>
	/// <value>Whether <c>bs=</c> and the selected conversions permit one input block to map directly to one output block.</value>
	public bool UseDirectBlockCopy => this.BlockSizeOverride.HasValue
		&& this.Conversions.All(
			conversion => conversion is DdConversion.Sync
				or DdConversion.NoError
				or DdConversion.NoTruncate
		)
	;

	/// <summary>
	/// Applies <c>bs=</c> to the input and output block sizes after all operands have been parsed.
	/// </summary>
	public void ApplyBlockSizeOverride() {
		if ( !this.BlockSizeOverride.HasValue ) {
			return;
		}
		this.InputBlockSize = this.BlockSizeOverride.Value;
		this.OutputBlockSize = this.BlockSizeOverride.Value;
	}
}
