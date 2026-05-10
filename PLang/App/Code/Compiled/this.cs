using App.Errors;

namespace App.Code.Compiled;

/// <summary>
/// A compiled assembly's bytes, ready to be loaded into a fresh collectible
/// ALC. Owns the load step — Compiler produces this, callers <c>Load()</c> it
/// to get a <see cref="Runtime.@this"/>.
/// </summary>
public sealed class @this
{
    private readonly byte[] _bytes;
    private readonly string _name;
    private readonly string _hash;

    public @this(byte[] bytes, string name, string hash)
    {
        _bytes = bytes;
        _name = name;
        _hash = hash;
    }

    public string Name => _name;
    public int Size => _bytes.Length;
    public string Hash => _hash;

    public Data.@this<Runtime.@this> Load()
    {
        var alc = new PluginLoadContext($"PlangCode_{_name}");
        try
        {
            using var ms = new MemoryStream(_bytes);
            var assembly = alc.LoadFromStream(ms);
            return new Data.@this<Runtime.@this>(value: new Runtime.@this(alc, assembly));
        }
        catch (Exception ex)
        {
            alc.Unload();
            return Data.@this<Runtime.@this>.FromError(
                ActionError.FromException(ex, "LoadError", 500));
        }
    }
}
