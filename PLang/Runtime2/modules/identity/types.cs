using PLang.Runtime2.Engine;
using PLang.Runtime2.Engine.Memory;

namespace PLang.Runtime2.modules.identity;

/// <summary>
/// Represents a PLang identity (Ed25519 key pair with metadata).
/// OBP: persistence methods belong to the owner — LoadAsync/SaveAsync/RemoveAsync navigate to DataSource.
/// </summary>
public class IdentityVariable
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
                Created = dict.TryGetValue("Created", out var c) && c is DateTime dt ? dt :
                          dict.TryGetValue("Created", out var cs) && cs is string s && DateTime.TryParse(s, out var parsed) ? parsed :
                          DateTime.UtcNow
            };
        }

        // Try JSON round-trip for other object types
        try
        {
            var json = System.Text.Json.JsonSerializer.Serialize(value);
            return System.Text.Json.JsonSerializer.Deserialize<IdentityVariable>(json);
        }
        catch
        {
            return null;
        }
    }
}
