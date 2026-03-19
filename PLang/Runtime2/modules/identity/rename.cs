using PLang.Runtime2.Engine.Errors;
using PLang.Runtime2.Engine.Memory;

namespace PLang.Runtime2.modules.identity;

/// <summary>
/// Renames an identity. Keys are preserved, old name is removed from DataSource.
/// If the renamed identity is the default, updates %MyIdentity%.
/// PLang: rename identity 'alice' to 'alice-prod'
/// </summary>
[Action("rename", Cacheable = false)]
public partial class Rename : IContext
{
    public partial string Name { get; init; }
    public partial string NewName { get; init; }

    public async Task<Data> Run()
    {
        if (string.IsNullOrWhiteSpace(NewName))
            return Data.FromError(new ActionError("New name cannot be empty", "ValidationError", 400));

        var identity = await IdentityVariable.LoadAsync(Context.Engine, Name);
        if (identity == null)
            return Data.FromError(new ActionError($"Identity '{Name}' not found", "NotFound", 404));

        // Check new name uniqueness across all identities (including archived)
        var all = await IdentityVariable.LoadAllAsync(Context.Engine);
        if (all.Exists(i => string.Equals(i.Name, NewName, StringComparison.OrdinalIgnoreCase)))
            return Data.FromError(new ActionError($"Identity '{NewName}' already exists", "DuplicateName", 409));

        // Save with new name first, then remove old entry.
        // If save fails, old entry is untouched — no data loss.
        var oldName = identity.Name;
        identity.Name = NewName;
        var saveResult = await identity.SaveAsync(Context.Engine);
        if (!saveResult.Success) return saveResult;

        identity.Name = oldName;
        var removeResult = await identity.RemoveAsync(Context.Engine);
        identity.Name = NewName;
        if (!removeResult.Success) return removeResult;

        if (identity.IsDefault)
            Context.Engine.System.Identity.Update(identity);

        return Data.Ok(identity);
    }
}
