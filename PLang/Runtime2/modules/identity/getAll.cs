using PLang.Runtime2.Engine.Memory;

namespace PLang.Runtime2.modules.identity;

/// <summary>
/// Lists all non-archived identities.
/// PLang: get all identities, write to %identities%
/// </summary>
[Action("getAll")]
public partial class GetAll : IContext
{
    public async Task<Data> Run()
    {
        var all = await IdentityVariable.LoadAllAsync(Context.Engine);
        var active = all.Where(i => !i.IsArchived).ToList();
        return Data.Ok(active);
    }
}
