using app.Attributes;
using app.variable;

namespace app.modules.variable;

/// <summary>
/// Compresses the value of an existing variable, returning a Data with
/// <c>type=archived</c> and a gzipped byte[] value. The original variable
/// is untouched; the compressed Data is returned for the caller to store.
///
/// Stage-3 of data-serialize-cleanup: archived.Value is the gzipped wire
/// bytes directly (no inner Data { type=gzip, value=byte[] } wrapper).
/// </summary>
[Action("compress", Cacheable = false)]
public partial class Compress : IContext
{
    /// <summary>The variable to compress.</summary>
    [IsNotNull]
    public partial data.@this<Variable> Variable { get; init; }

    public async Task<data.@this> Run()
    {
        var target = Context.Variables.Get(Variable.Value!.Name);
        if (target == null || !target.IsInitialized)
            return data.@this.FromError(
                new global::app.error.ServiceError($"Variable '{Variable.Value.Name}' is not set",
                    "VariableNotFound", 400));
        return await target.CompressAsync();
    }
}

/// <summary>
/// Decompresses a Data whose <c>type=archived</c>. Returns the original
/// Data (with inner signature intact); returns self unchanged when the
/// target is not an archived outer.
/// </summary>
[Action("decompress", Cacheable = false)]
public partial class Decompress : IContext
{
    /// <summary>The variable holding the archived Data to decompress.</summary>
    [IsNotNull]
    public partial data.@this<Variable> Variable { get; init; }

    public async Task<data.@this> Run()
    {
        var target = Context.Variables.Get(Variable.Value!.Name);
        if (target == null || !target.IsInitialized)
            return data.@this.FromError(
                new global::app.error.ServiceError($"Variable '{Variable.Value.Name}' is not set",
                    "VariableNotFound", 400));
        return await target.DecompressAsync();
    }
}
