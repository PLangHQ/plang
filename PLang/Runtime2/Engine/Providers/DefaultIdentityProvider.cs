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
        var engine = action.Context.Engine;

        if (action.Name != null)
        {
            var identity = await LoadAsync(action, action.Name);
            if (identity == null)
                return Data.FromError(new ActionError($"Identity '{action.Name}' not found", "NotFound", 404));

            return Data.Ok(identity);
        }

        var defResult = await GetOrCreateDefaultAsync(action);
        if (!defResult.Success) return defResult;

        engine.System.Identity.Update(defResult.Value);
        return Data.Ok(defResult.Value);
    }

    public async Task<Data> CreateAsync(Create action)
    {
        var engine = action.Context.Engine;

        if (string.IsNullOrWhiteSpace(action.Name))
            return Data.FromError(new ActionError("Identity name cannot be empty", "ValidationError", 400));

        var all = await LoadAllAsync(action);
        if (all.Exists(i => string.Equals(i.Name, action.Name, StringComparison.OrdinalIgnoreCase)))
            return Data.FromError(new ActionError($"Identity '{action.Name}' already exists", "DuplicateName", 409));

        // Resolve key provider — name flows through, null gets default
        var keyResult = engine.Providers.Get<IKeyProvider>(action.Provider);
        IKeyProvider keyProvider;
        if (keyResult.Success)
            keyProvider = keyResult.Value!;
        else
        {
            var sigResult = engine.Providers.Get<ISigningProvider>();
            keyProvider = sigResult.Success ? sigResult.Value! : new Ed25519Provider();
        }

        KeyPair keys;
        try
        {
            keys = keyProvider.GenerateKeyPair();
        }
        catch (Exception ex)
        {
            return Data.FromError(ActionError.FromException(ex, "KeyGenerationError", 500));
        }

        if (action.SetAsDefault)
        {
            foreach (var existing in all.Where(i => i.IsDefault))
            {
                existing.IsDefault = false;
                var saveResult = await SaveAsync(action, existing);
                if (!saveResult.Success) return saveResult;
            }
        }

        var now = (DateTimeOffset)action.Context.MemoryStack.GetValue("NowUtc")!;

        var identity = new IdentityVariable
        {
            Name = action.Name,
            PublicKey = keys.PublicKey,
            PrivateKey = keys.PrivateKey,
            IsDefault = action.SetAsDefault,
            IsArchived = false,
            Created = now
        };

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

    public async Task<Data> GetAllAsync(GetAll action)
    {
        var all = await LoadAllAsync(action);
        var active = all.Where(i => !i.IsArchived).ToList();
        return Data.Ok(active);
    }

    public async Task<Data> ExportAsync(Export action)
    {
        IdentityVariable? identity;

        if (action.Name != null)
        {
            identity = await LoadAsync(action, action.Name);
            if (identity == null)
                return Data.FromError(new ActionError($"Identity '{action.Name}' not found", "NotFound", 404));
        }
        else
        {
            var defResult = await GetOrCreateDefaultAsync(action);
            if (!defResult.Success) return defResult;
            identity = defResult.Value;
        }

        return Data.Ok(identity!.PrivateKey);
    }

    // --- Internal persistence helpers ---

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
    internal async Task<Data<IdentityVariable>> GetOrCreateDefaultAsync(IContext action)
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
        var keyResult = engine.Providers.Get<IKeyProvider>();
        IKeyProvider keyProvider;
        if (keyResult.Success)
            keyProvider = keyResult.Value!;
        else
        {
            var sigResult = engine.Providers.Get<ISigningProvider>();
            keyProvider = sigResult.Success ? sigResult.Value! : new Ed25519Provider();
        }

        KeyPair keys;
        try
        {
            keys = keyProvider.GenerateKeyPair();
        }
        catch (Exception ex)
        {
            return Data<IdentityVariable>.FromError(ActionError.FromException(ex, "KeyGenerationError", 500));
        }

        var now = (DateTimeOffset)action.Context.MemoryStack.GetValue("NowUtc")!;

        def = new IdentityVariable
        {
            Name = "default",
            PublicKey = keys.PublicKey,
            PrivateKey = keys.PrivateKey,
            IsDefault = true,
            IsArchived = false,
            Created = now
        };
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

    private static IdentityVariable? Deserialize(object? value)
    {
        if (value is IdentityVariable iv)
            return iv;

        if (value is Dictionary<string, object?> dict)
        {
            return new IdentityVariable
            {
                Name = dict.TryGetValue("Name", out var n) ? n?.ToString() ?? "" : "",
                PublicKey = dict.TryGetValue("PublicKey", out var pk) ? pk?.ToString() ?? "" : "",
                PrivateKey = dict.TryGetValue("PrivateKey", out var prk) ? prk?.ToString() ?? "" : "",
                IsDefault = dict.TryGetValue("IsDefault", out var d) && d is bool bd && bd,
                IsArchived = dict.TryGetValue("IsArchived", out var a) && a is bool ba && ba,
                Created = dict.TryGetValue("Created", out var c)
                    ? (c is DateTimeOffset dto ? dto
                        : c is DateTime dt ? new DateTimeOffset(dt, TimeSpan.Zero)
                        : c is string s && DateTimeOffset.TryParse(s, out var parsed) ? parsed
                        : DateTimeOffset.UtcNow)
                    : DateTimeOffset.UtcNow
            };
        }

        return null;
    }
}
