using app.variables;

namespace app.modules.settings;

/// <summary>
/// Interface for persistent key-value storage of PLang settings.
/// Each actor owns a store. Modules own their tables (encryption → "encryption", settings → "settings").
/// All methods accept and return Data, never throw.
/// </summary>
public interface IStore : IDisposable
{
    /// <summary>
    /// Gets a single value by table and key.
    /// Returns Data with the value, or Data with null value if not found.
    /// </summary>
    Task<data.@this> Get(string table, string key);

    /// <summary>
    /// Gets a single value by table and key, deserializing the value to T.
    /// Returns Data with a typed Value, or Data with null value if not found.
    /// </summary>
    Task<data.@this> Get<T>(string table, string key) where T : data.@this;

    /// <summary>
    /// Gets all key-value pairs in a table.
    /// Returns Data with List&lt;Data&gt; value (each item has Name=key, Value=stored value).
    /// </summary>
    Task<data.@this> GetAll(string table);

    /// <summary>
    /// Gets all key-value pairs in a table, deserializing each value to T.
    /// Returns Data with List&lt;T&gt; value, or Data with error on failure.
    /// </summary>
    Task<data.@this<List<T>>> GetAll<T>(string table) where T : data.@this;

    /// <summary>
    /// Sets a Data value by table and key. Creates the table if it doesn't exist.
    /// The full Data envelope (value, type, signature) is persisted.
    /// </summary>
    Task<data.@this> Set(string table, string key, data.@this data);

    /// <summary>
    /// Removes a value by table and key.
    /// Returns success even if the key didn't exist.
    /// </summary>
    Task<data.@this> Remove(string table, string key);

    /// <summary>
    /// Checks if a key exists in a table.
    /// Returns Data with bool value.
    /// </summary>
    Task<data.@this<bool>> Exists(string table, string key);

    /// <summary>
    /// Lists all tables in this store.
    /// Returns Data with List&lt;string&gt; value.
    /// </summary>
    Task<data.@this<List<string>>> Tables();

    /// <summary>
    /// Resolves the table name from a System.Type.
    /// Convention: last namespace segment, lowercased.
    /// e.g., app.modules.encryption → "encryption"
    /// </summary>
    static string ResolveTableName(System.Type type)
    {
        var ns = type.Namespace;
        if (string.IsNullOrEmpty(ns))
            return type.Name.ToLowerInvariant();

        var lastDot = ns.LastIndexOf('.');
        return lastDot >= 0 ? ns[(lastDot + 1)..].ToLowerInvariant() : ns.ToLowerInvariant();
    }
}
