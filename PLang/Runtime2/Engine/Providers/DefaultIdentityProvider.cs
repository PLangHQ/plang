using PLang.Runtime2.Engine.Errors;
using PLang.Runtime2.Engine.Memory;
using PLang.Runtime2.modules;
using PLang.Runtime2.modules.identity;

namespace PLang.Runtime2.Engine.Providers;

/// <summary>
/// Default identity provider backed by System DataSource (SQLite).
/// All methods take the action and navigate to engine/context/datasource.
/// </summary>
public class DefaultIdentityProvider : IIdentityProvider
{
    private const string Table = "identity";

    public string Name => "default";
    public bool IsDefault { get; set; }

    public async Task<Data> GetAsync(Get action)
    {
        var result = await ResolveIdentityAsync(action, action.Name);
        if (!result.Success) return result;

        // Refresh cached %MyIdentity% when resolving the default
        if (action.Name == null)
            action.Context.Engine.System.Identity.Update(result.Value);

        return Data.Ok(result.Value);
    }

    public async Task<Data> CreateAsync(Create action)
    {
        var engine = action.Context.Engine;

        if (string.IsNullOrWhiteSpace(action.Name))
            return Data.FromError(new ActionError("Identity name cannot be empty", "ValidationError", 400));

        var all = await LoadAllAsync(action);
        if (all.Exists(i => string.Equals(i.Name, action.Name, StringComparison.OrdinalIgnoreCase)))
            return Data.FromError(new ActionError($"Identity '{action.Name}' already exists", "DuplicateName", 409));

        var identityResult = GenerateIdentity(action, action.Name, action.SetAsDefault, action.Provider);
        if (!identityResult.Success) return identityResult;
        var identity = (IdentityVariable)identityResult.Value!;

        if (action.SetAsDefault)
        {
            foreach (var existing in all.Where(i => i.IsDefault))
            {
                existing.IsDefault = false;
                var saveResult = await SaveAsync(action, existing);
                if (!saveResult.Success) return saveResult;
            }
        }

        var result = await SaveAsync(action, identity);
        if (!result.Success) return result;

        if (action.SetAsDefault)
            engine.System.Identity.Update(identity);

        return Data.Ok(identity);
    }

    public async Task<Data> ArchiveAsync(Archive action)
    {
        var identity = await LoadAsync(action, action.Name);
        if (identity == null)
            return Data.FromError(new ActionError($"Identity '{action.Name}' not found", "NotFound", 404));

        if (identity.IsDefault)
            return Data.FromError(new ActionError(
                "Cannot archive the default identity. Set a different default first.",
                "CannotArchiveDefault", 400));

        if (identity.IsArchived)
            return Data.Ok();

        identity.IsArchived = true;
        var result = await SaveAsync(action, identity);
        if (!result.Success) return result;

        return Data.Ok();
    }

    public async Task<Data> UnarchiveAsync(Unarchive action)
    {
        var identity = await LoadAsync(action, action.Name);
        if (identity == null)
            return Data.FromError(new ActionError($"Identity '{action.Name}' not found", "NotFound", 404));

        if (!identity.IsArchived)
            return Data.Ok(identity);

        identity.IsArchived = false;
        var result = await SaveAsync(action, identity);
        if (!result.Success) return result;

        return Data.Ok(identity);
    }

    public async Task<Data> SetDefaultAsync(SetDefault action)
    {
        var engine = action.Context.Engine;
        var all = await LoadAllAsync(action);

        var target = all.Find(i => string.Equals(i.Name, action.Name, StringComparison.OrdinalIgnoreCase));
        if (target == null)
            return Data.FromError(new ActionError($"Identity '{action.Name}' not found", "NotFound", 404));

        if (target.IsArchived)
            return Data.FromError(new ActionError($"Cannot set archived identity '{action.Name}' as default", "ArchivedIdentity", 400));

        if (target.IsDefault)
            return Data.Ok(target);

        foreach (var identity in all.Where(i => i.IsDefault))
        {
            identity.IsDefault = false;
            var result = await SaveAsync(action, identity);
            if (!result.Success) return result;
        }

        target.IsDefault = true;
        var saveResult = await SaveAsync(action, target);
        if (!saveResult.Success) return saveResult;

        engine.System.Identity.Update(target);
        return Data.Ok(target);
    }

    public async Task<Data> RenameAsync(Rename action)
    {
        var engine = action.Context.Engine;

        if (string.IsNullOrWhiteSpace(action.NewName))
            return Data.FromError(new ActionError("New name cannot be empty", "ValidationError", 400));

        var identity = await LoadAsync(action, action.Name);
        if (identity == null)
            return Data.FromError(new ActionError($"Identity '{action.Name}' not found", "NotFound", 404));

        var all = await LoadAllAsync(action);
        if (all.Exists(i => string.Equals(i.Name, action.NewName, StringComparison.OrdinalIgnoreCase)))
            return Data.FromError(new ActionError($"Identity '{action.NewName}' already exists", "DuplicateName", 409));

        // Save with new name first, then remove old — no data loss on failure
        var oldName = identity.Name;
        identity.Name = action.NewName;
        var saveResult = await SaveAsync(action, identity);
        if (!saveResult.Success) return saveResult;

        identity.Name = oldName;
        var removeResult = await RemoveAsync(action, identity);
        identity.Name = action.NewName;
        if (!removeResult.Success) return removeResult;

        if (identity.IsDefault)
            engine.System.Identity.Update(identity);

        return Data.Ok(identity);
    }

    public async Task<Data> ListAsync(list action)
    {
        var all = await LoadAllAsync(action);
        var active = all.Where(i => !i.IsArchived).ToList();
        return Data.Ok(active);
    }

    public async Task<Data> ExportAsync(Export action)
    {
        var result = await ResolveIdentityAsync(action, action.Name);
        if (!result.Success) return result;

        return Data.Ok(result.Value!.PrivateKey);
    }

    // --- Internal helpers ---

    /// <summary>
    /// Resolves an identity by name, or gets/creates the default if name is null.
    /// </summary>
    private async Task<Data<IdentityVariable>> ResolveIdentityAsync(IContext action, string? name)
    {
        if (name != null)
        {
            var identity = await LoadAsync(action, name);
            if (identity == null)
                return Data<IdentityVariable>.FromError(new ActionError($"Identity '{name}' not found", "NotFound", 404));
            return Data<IdentityVariable>.Ok(identity);
        }

        return await GetOrCreateDefaultAsync(action);
    }

    // --- Persistence helpers ---

    internal async Task<IdentityVariable?> LoadAsync(IContext action, string name)
    {
        var dataSource = action.Context.Engine.System.DataSource;
        var result = await dataSource.Get(Table, name);

        if (!result.Success || result.Value == null)
            return null;

        return Deserialize(result.Value);
    }

    internal async Task<List<IdentityVariable>> LoadAllAsync(IContext action)
    {
        var dataSource = action.Context.Engine.System.DataSource;
        var result = await dataSource.GetAll(Table);

        if (!result.Success || result.Value is not List<Data> items)
            return new List<IdentityVariable>();

        var identities = new List<IdentityVariable>();
        foreach (var item in items)
        {
            var identity = Deserialize(item.Value);
            if (identity != null)
                identities.Add(identity);
        }
        return identities;
    }

    /// <summary>
    /// Gets the default non-archived identity, or auto-creates one if none exist.
    /// </summary>
    public async Task<Data<IdentityVariable>> GetOrCreateDefaultAsync(IContext action)
    {
        var engine = action.Context.Engine;
        var all = await LoadAllAsync(action);
        var def = all.Find(i => i.IsDefault && !i.IsArchived);
        if (def != null) return Data<IdentityVariable>.Ok(def);

        // Promote an existing non-archived identity
        var candidate = all.Find(i => !i.IsArchived);
        if (candidate != null)
        {
            candidate.IsDefault = true;
            var promoteResult = await SaveAsync(action, candidate);
            if (!promoteResult.Success)
                return Data<IdentityVariable>.FromError(promoteResult.Error!);
            return Data<IdentityVariable>.Ok(candidate);
        }

        // No identities at all — auto-create
        var identityResult = GenerateIdentity(action, "default", true);
        if (!identityResult.Success)
            return Data<IdentityVariable>.FromError(identityResult.Error!);
        def = (IdentityVariable)identityResult.Value!;
        var saveResult = await SaveAsync(action, def);
        if (!saveResult.Success)
            return Data<IdentityVariable>.FromError(saveResult.Error!);
        return Data<IdentityVariable>.Ok(def);
    }

    private async Task<Data> SaveAsync(IContext action, IdentityVariable identity)
    {
        var dataSource = action.Context.Engine.System.DataSource;
        return await dataSource.Set(Table, identity.Name, identity);
    }

    private async Task<Data> RemoveAsync(IContext action, IdentityVariable identity)
    {
        var dataSource = action.Context.Engine.System.DataSource;
        return await dataSource.Remove(Table, identity.Name);
    }

    /// <summary>
    /// Generates a new identity with keys from the configured key provider.
    /// Owns the full sequence: resolve provider → generate keys → build IdentityVariable.
    /// </summary>
    private Data GenerateIdentity(IContext action, string name, bool isDefault, string? providerName = null)
    {
        var engine = action.Context.Engine;
        var keyResult = engine.Providers.Get<IKeyProvider>(providerName);
        if (!keyResult.Success) return keyResult;

        var keysResult = keyResult.Value!.GenerateKeyPair();
        if (!keysResult.Success) return keysResult;

        var now = (DateTimeOffset)action.Context.MemoryStack.GetValue("NowUtc")!;

        return Data.Ok(new IdentityVariable
        {
            Name = name,
            PublicKey = keysResult.Value!.PublicKey,
            PrivateKey = keysResult.Value.PrivateKey,
            IsDefault = isDefault,
            IsArchived = false,
            Created = now
        });
    }

    private static IdentityVariable? Deserialize(object? value)
    {
        if (value is IdentityVariable iv)
            return iv;

        if (value is Dictionary<string, object?> or System.Text.Json.JsonElement)
        {
            try
            {
                var json = System.Text.Json.JsonSerializer.Serialize(value);
                return System.Text.Json.JsonSerializer.Deserialize<IdentityVariable>(json);
            }
            catch (System.Text.Json.JsonException)
            {
                return null;
            }
        }

        return null;
    }
}
