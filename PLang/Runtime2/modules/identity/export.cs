using PLang.Runtime2.Engine.Errors;
using PLang.Runtime2.Engine.Memory;

namespace PLang.Runtime2.modules.identity;

/// <summary>
/// Exports the private key of an identity.
/// Returns the raw private key string.
/// PLang: export identity 'alice' private key, write to %privateKey%
/// </summary>
[Action("export")]
public partial class Export : IContext
{
    public partial string? Name { get; init; }

    public async Task<Data> Run()
    {
        IdentityVariable? identity;

        if (Name != null)
        {
            identity = await IdentityVariable.LoadAsync(Context.Engine, Name);
            if (identity == null)
                return Data.FromError(new ActionError($"Identity '{Name}' not found", "NotFound", 404));
        }
        else
        {
            var defResult = await IdentityVariable.GetOrCreateDefaultAsync(Context.Engine);
            if (!defResult.Success) return defResult;
            identity = defResult.Value;
        }

        return Data.Ok(identity!.PrivateKey);
    }
}
