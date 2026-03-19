using PLang.Runtime2.Engine.Errors;
using PLang.Runtime2.Engine.Memory;

namespace PLang.Runtime2.modules.identity;

/// <summary>
/// Archives an identity (soft-delete). Cannot archive the default identity.
/// Idempotent — archiving an already-archived identity succeeds.
/// PLang: archive identity 'alice'
/// </summary>
[Action("archive", Cacheable = false)]
public partial class Archive : IContext
{
    public partial string Name { get; init; }

    public async Task<Data> Run()
    {
        var identity = await IdentityVariable.LoadAsync(Context.Engine, Name);
        if (identity == null)
            return Data.FromError(new ActionError($"Identity '{Name}' not found", "NotFound", 404));

        if (identity.IsDefault)
            return Data.FromError(new ActionError(
                "Cannot archive the default identity. Set a different default first.",
                "CannotArchiveDefault", 400));

        if (identity.IsArchived)
            return Data.Ok();

        identity.IsArchived = true;
        var result = await identity.SaveAsync(Context.Engine);
        if (!result.Success) return result;

        return Data.Ok();
    }
}
