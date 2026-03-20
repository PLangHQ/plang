using PLang.Runtime2.Engine;
using PLang.Runtime2.Engine.Memory;
using PLang.Runtime2.Engine.Providers;

namespace PLang.Runtime2.modules.identity;

/// <summary>
/// Represents a PLang identity (Ed25519 key pair with metadata).
/// OBP: persistence methods belong to the owner — LoadAsync/SaveAsync/RemoveAsync navigate to DataSource.
/// </summary>
public sealed class IdentityVariable
{
    private const string Table = "identity";

    public string Name { get; set; } = "";
    public string PublicKey { get; set; } = "";

    [Sensitive]
    public string PrivateKey { get; set; } = "";

    public bool IsDefault { get; set; }
    public bool IsArchived { get; set; }
    public DateTime Created { get; set; }

    /// <summary>
    /// String context returns the public key — %MyIdentity% in a string gives the public key.
    /// </summary>
    public override string ToString() => PublicKey;

    // --- OBP: persistence belongs to the owner ---

    /// <summary>
    /// Loads a single identity by name from System DataSource.
    /// Returns null if not found.
    /// </summary>
    public static async Task<IdentityVariable?> LoadAsync(Engine.@this engine, string name)
    {
        var dataSource = engine.System.DataSource;
        var result = await dataSource.Get(Table, name);

        if (!result.Success || result.Value == null)
            return null;

        return Deserialize(result.Value);
    }

    /// <summary>
    /// Loads all identities (including archived) from System DataSource.
    /// </summary>
    public static async Task<List<IdentityVariable>> LoadAllAsync(Engine.@this engine)
    {
        var dataSource = engine.System.DataSource;
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
    /// Single source of truth for default identity resolution — used by both Get handler and IdentityData.
    /// </summary>
    public static async Task<IdentityVariable> GetOrCreateDefaultAsync(Engine.@this engine)
    {
        var all = await LoadAllAsync(engine);
        var def = all.Find(i => i.IsDefault && !i.IsArchived);
        if (def != null) return def;

        // Promote an existing non-archived identity (e.g. one named "default" without IsDefault=true)
        var candidate = all.Find(i => !i.IsArchived);
        if (candidate != null)
        {
            candidate.IsDefault = true;
            var promoteResult = await candidate.SaveAsync(engine);
            if (!promoteResult.Success)
                throw new InvalidOperationException($"Failed to promote identity '{candidate.Name}' to default: {promoteResult.Error?.Message}");
            return candidate;
        }

        // No identities at all — auto-create
        IKeyProvider keyProvider = engine.Providers.Get<IKeyProvider>()
            ?? (IKeyProvider?)engine.Providers.Get<ISigningProvider>()
            ?? new Ed25519Provider();
        var keys = keyProvider.GenerateKeyPair();
        def = new IdentityVariable
        {
            Name = "default",
            PublicKey = keys.PublicKey,
            PrivateKey = keys.PrivateKey,
            IsDefault = true,
            IsArchived = false,
            Created = DateTime.UtcNow
        };
        var saveResult = await def.SaveAsync(engine);
        if (!saveResult.Success)
            throw new InvalidOperationException($"Failed to save auto-created default identity: {saveResult.Error?.Message}");
        return def;
    }

    /// <summary>
    /// Saves this identity to System DataSource, keyed by Name.
    /// </summary>
    public async Task<Data> SaveAsync(Engine.@this engine)
    {
        var dataSource = engine.System.DataSource;
        return await dataSource.Set(Table, Name, this);
    }

    /// <summary>
    /// Removes this identity from System DataSource by Name.
    /// </summary>
    public async Task<Data> RemoveAsync(Engine.@this engine)
    {
        var dataSource = engine.System.DataSource;
        return await dataSource.Remove(Table, Name);
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
                    ? (c is DateTime dt ? dt : c is string s && DateTime.TryParse(s, out var parsed) ? parsed : DateTime.UtcNow)
                    : DateTime.UtcNow
            };
        }

        return null;
    }
}
