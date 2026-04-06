using System.IO.Compression;
using App.Settings;

namespace App.actions.archive;

/// <summary>
/// Archive module settings. First use case for ISettings.
///
/// The source generator will rewrite these properties to resolve from the
/// settings scope chain. Until then, they return their class defaults.
///
/// Generated read side (what the source generator will produce):
///   public long Max => _context?.Engine.Settings.Resolve&lt;long&gt;("archive.max", _context, 100 * 1024 * 1024);
/// </summary>
public partial class ArchiveSettings : ISettings
{
    /// <summary>
    /// Maximum decompressed size in bytes. Default: 100MB.
    /// PLang: "set max gzip size to 20mb"
    /// </summary>
    public long Max { get; set; } = 100 * 1024 * 1024;

    /// <summary>
    /// Compression level for archive operations.
    /// PLang: "set compression level to fastest"
    /// </summary>
    public CompressionLevel Level { get; set; } = CompressionLevel.Optimal;
}
