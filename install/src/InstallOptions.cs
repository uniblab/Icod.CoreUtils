namespace Icod.CoreUtils.Install;

using Icod.CoreUtils.Shared.FileSystem.TransactionalReplacement;

/// <summary>Describes one parsed GNU <c>install</c> invocation.</summary>
internal sealed class InstallOptions {
	/// <summary>Gets or sets whether directory operands are created.</summary>
	public bool DirectoryMode { get; set; }
	/// <summary>Gets or sets whether leading destination directories are created.</summary>
	public bool CreateLeadingDirectories { get; set; }
	/// <summary>Gets or sets whether the final operand is always treated as a file.</summary>
	public bool TreatDestinationAsFile { get; set; }
	/// <summary>Gets or sets the explicit target directory.</summary>
	public string? TargetDirectory { get; set; }
	/// <summary>Gets or sets the mode expression.</summary>
	public string ModeText { get; set; } = "u=rwx,go=rx,a-s";
	/// <summary>Gets or sets whether the mode was explicitly supplied.</summary>
	public bool ModeWasExplicit { get; set; }
	/// <summary>Gets or sets the requested owner.</summary>
	public string? Owner { get; set; }
	/// <summary>Gets or sets the requested group.</summary>
	public string? Group { get; set; }
	/// <summary>Gets or sets whether installed files are stripped.</summary>
	public bool Strip { get; set; }
	/// <summary>Gets or sets the stripping program.</summary>
	public string StripProgram { get; set; } = "strip";
	/// <summary>Gets or sets whether a strip program was explicitly selected.</summary>
	public bool StripProgramWasExplicit { get; set; }
	/// <summary>Gets or sets whether unchanged destinations are retained.</summary>
	public bool Compare { get; set; }
	/// <summary>Gets or sets whether source access and modification times are preserved.</summary>
	public bool PreserveTimestamps { get; set; }
	/// <summary>Gets or sets whether a retained backup is requested.</summary>
	public bool Backup { get; set; }
	/// <summary>Gets or sets the backup naming mode.</summary>
	public TransactionalReplacementBackupMode BackupMode { get; set; } = TransactionalReplacementBackupMode.Existing;
	/// <summary>Gets or sets the simple backup suffix.</summary>
	public string BackupSuffix { get; set; } = Environment.GetEnvironmentVariable( "SIMPLE_BACKUP_SUFFIX" ) ?? "~";
	/// <summary>Gets or sets whether actions are reported.</summary>
	public bool Verbose { get; set; }
	/// <summary>Gets or sets whether implementation decisions are explained.</summary>
	public bool Debug { get; set; }
	/// <summary>Gets or sets whether the source SELinux context is preserved.</summary>
	public bool PreserveContext { get; set; }
	/// <summary>Gets or sets whether destination-default SELinux context policy was requested.</summary>
	public bool ContextRequested { get; set; }
	/// <summary>Gets or sets an explicit SELinux context.</summary>
	public string? ExplicitContext { get; set; }
	/// <summary>Gets whether help was requested.</summary>
	public bool ShowHelp { get; set; }
	/// <summary>Gets whether version output was requested.</summary>
	public bool ShowVersion { get; set; }
	/// <summary>Gets positional operands.</summary>
	public List<string> Operands { get; } = new();
}
