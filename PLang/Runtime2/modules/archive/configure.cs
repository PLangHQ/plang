using System.IO.Compression;
using PLang.Runtime2.Engine.Memory;

namespace PLang.Runtime2.modules.archive;

/// <summary>
/// Configures archive module defaults via the scope chain.
/// Non-null properties are written to the current scope; null properties are left unchanged.
/// Use Default=true to write to the app-wide default scope.
/// </summary>
[Action("configure", Cacheable = false)]
public partial class configure : IContext, IConfigure<Config>
{
    /// <summary>Maximum decompressed size in bytes.</summary>
    public partial long? Max { get; init; }

    /// <summary>Compression level for archive operations.</summary>
    public partial CompressionLevel? Level { get; init; }

    /// <summary>When true, writes config to app-wide default scope instead of current scope.</summary>
    [Default(false)]
    public partial bool Default { get; init; }

    public Task<Data> Run()
    {
        Context.Engine.Config.Apply<Config>(this, Context, Default);
        return Task.FromResult(Data.Ok());
    }
}
