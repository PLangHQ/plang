using System.IO.Compression;
using PLang.Runtime2.Engine.Memory;

namespace PLang.Runtime2.actions.archive;

/// <summary>
/// Settings action handler for the archive module.
/// Writes setting values to the goal-scoped or engine-default settings scope.
///
/// In the future, the source generator will produce this automatically from ArchiveSettings.
/// This is the manually-written skeleton showing the target shape.
///
/// PLang: "set max gzip size to 20mb"
///   → module: archive, action: settings, parameters: [{ name: "max", value: 20971520 }]
/// </summary>
[Action("settings")]
public partial class Settings : IContext
{
    /// <summary>
    /// Maximum decompressed size in bytes. Null = not being set.
    /// </summary>
    public partial long? Max { get; init; }

    /// <summary>
    /// Compression level. Null = not being set.
    /// </summary>
    public partial CompressionLevel? Level { get; init; }

    /// <summary>
    /// When true, writes to engine-level defaults (persistent).
    /// When false (default), writes to the current goal scope.
    /// </summary>
    [Default(false)]
    public partial bool IsDefault { get; init; }

    public Task<Data> Run()
    {
        throw new NotImplementedException();
    }
}
