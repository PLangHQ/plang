using PLang.Runtime2.Engine.Errors;
using PLang.Runtime2.Engine.Memory;

namespace PLang.Runtime2.modules.identity;

/// <summary>
/// Switches which identity is the default. Only one default at a time.
/// PLang: set default identity to 'alice'
/// </summary>
[Action("setDefault", Cacheable = false)]
public partial class SetDefault : IContext
{
    public partial string Name { get; init; }

    public async Task<Data> Run()
    {
        var all = await IdentityVariable.LoadAllAsync(Context.Engine);

        var target = all.Find(i => string.Equals(i.Name, Name, StringComparison.OrdinalIgnoreCase));
        if (target == null)
            return Data.FromError(new ActionError($"Identity '{Name}' not found", "NotFound", 404));

        if (target.IsArchived)
            return Data.FromError(new ActionError($"Cannot set archived identity '{Name}' as default", "ArchivedIdentity", 400));

        if (target.IsDefault)
            return Data.Ok(target);

        // Clear all existing defaults, set the target
        foreach (var identity in all.Where(i => i.IsDefault))
        {
            identity.IsDefault = false;
            var result = await identity.SaveAsync(Context.Engine);
            if (!result.Success) return result;
        }

        target.IsDefault = true;
        var saveResult = await target.SaveAsync(Context.Engine);
        if (!saveResult.Success) return saveResult;

        Context.Engine.System.Identity.Update(target);
        return Data.Ok(target);
    }
}
