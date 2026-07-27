namespace Icod.CoreUtils.DD;

internal enum DdConversion {
	Ascii,
	Ebcdic,
	Ibm,
	Block,
	Unblock,
	LowerCase,
	UpperCase,
	Sparse,
	Swab,
	Sync,
	Exclusive,
	NoCreate,
	NoTruncate,
	NoError,
	FDataSync,
	FileSystemSync,
}

internal enum DdFlag {
	Append,
	Direct,
	Directory,
	DataSync,
	Sync,
	FullBlock,
	NonBlock,
	NoAccessTime,
	NoCache,
	NoControllingTerminal,
	NoFollow,
}

internal enum DdStatusMode {
	Default,
	None,
	NoTransfer,
	Progress,
}

internal readonly record struct DdQuantity(
	long Value,
	bool IsBytes
);

internal sealed class DdOptions {
	public const int DefaultBlockSize = 512;

	public int? BlockSizeOverride {
		get;
		set;
	}

	public int InputBlockSize {
		get;
		set;
	} = DefaultBlockSize;

	public int OutputBlockSize {
		get;
		set;
	} = DefaultBlockSize;

	public int ConversionBlockSize {
		get;
		set;
	}

	public string? InputFile {
		get;
		set;
	}

	public string? OutputFile {
		get;
		set;
	}

	public DdQuantity? Count {
		get;
		set;
	}

	public DdQuantity Skip {
		get;
		set;
	}

	public DdQuantity Seek {
		get;
		set;
	}

	public DdStatusMode Status {
		get;
		set;
	} = DdStatusMode.Default;

	public HashSet<DdConversion> Conversions {
		get;
	} = [];

	public HashSet<DdFlag> InputFlags {
		get;
	} = [];

	public HashSet<DdFlag> OutputFlags {
		get;
	} = [];

	public bool HasConversion(
		DdConversion conversion
	) => this.Conversions.Contains( conversion );

	public bool HasInputFlag(
		DdFlag flag
	) => this.InputFlags.Contains( flag );

	public bool HasOutputFlag(
		DdFlag flag
	) => this.OutputFlags.Contains( flag );

	public bool UsesBlockConversion => 0 < this.ConversionBlockSize
		&& (
			this.HasConversion( DdConversion.Block )
			|| this.HasConversion( DdConversion.Ebcdic )
			|| this.HasConversion( DdConversion.Ibm )
		)
	;

	public bool UsesUnblockConversion => 0 < this.ConversionBlockSize
		&& (
			this.HasConversion( DdConversion.Unblock )
			|| this.HasConversion( DdConversion.Ascii )
		)
	;

	public bool UseDirectBlockCopy => this.BlockSizeOverride.HasValue
		&& this.Conversions.All(
			conversion => conversion is DdConversion.Sync
				or DdConversion.NoError
				or DdConversion.NoTruncate
		)
	;

	public void ApplyBlockSizeOverride() {
		if ( !this.BlockSizeOverride.HasValue ) {
			return;
		}
		this.InputBlockSize = this.BlockSizeOverride.Value;
		this.OutputBlockSize = this.BlockSizeOverride.Value;
	}
}
