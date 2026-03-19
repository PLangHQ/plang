using PLang.Runtime2.Engine.Errors;
using PLang.Runtime2.Engine.Memory;

namespace PLang.Runtime2.modules.identity;

/// <summary>
/// Restores an archived identity.
/// Idempotent — unarchiving an active identity succeeds.
/// PLang: unarchive identity 'alice'
/// </summary>
[Action("unarchive", Cacheable = false)]
public partial class Unarchive : IContext
{
    public partial string Name { get; init; }

    public async Task<Data> Run()
    {
        var identity = await IdentityVariable.LoadAsync(Context.Engine, Name);
        if (identity == null)
            return Data.FromError(new ActionError($"Identity '{Name}' not found", "NotFound", 404));

        if (!identity.IsArchived)
            return Data.Ok(identity);

        identity.IsArchived = false;
        var result = await identity.SaveAsync(Context.Engine);
        if (!result.Success) return result;

        return Data.Ok(identity);
    }
}
