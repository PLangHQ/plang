using PLang.Runtime2.Engine.Errors;
using PLang.Runtime2.Engine.Memory;

namespace PLang.Runtime2.modules.identity;

/// <summary>
/// Gets an identity by name, or the default identity.
/// Auto-creates a default if none exist.
/// PLang: get identity 'alice', write to %identity%
/// </summary>
[Action("get")]
public partial class Get : IContext
{
    public partial string? Name { get; init; }

    public async Task<Data> Run()
    {
        if (Name != null)
        {
            var identity = await IdentityVariable.LoadAsync(Context.Engine, Name);
            if (identity == null)
                return Data.FromError(new ActionError($"Identity '{Name}' not found", "NotFound", 404));

            return Data.Ok(identity);
        }

        // Get default identity — auto-create if none exist
        var def = await IdentityVariable.GetOrCreateDefaultAsync(Context.Engine);
        Context.Engine.System.Identity.Update(def);
        return Data.Ok(def);
    }
}
