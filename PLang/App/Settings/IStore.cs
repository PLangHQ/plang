using App.Variables;

namespace App.Settings;

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
    Task<Data.@this> Get(string table, string key);

    /// <summary>
    /// Gets a single value by table and key, deserializing the value to T.
    /// Returns Data with a typed Value, or Data with null value if not found.
    /// </summary>
    Task<Data.@this> Get<T>(string table, string key) where T : Data.@this;

    /// <summary>
    /// Gets all key-value pairs in a table.
    /// Returns Data with List&lt;Data&gt; value (each item has Name=key, Value=stored value).
    /// </summary>
    Task<Data.@this> GetAll(string table);

    /// <summary>
    /// Gets all key-value pairs in a table, deserializing each value to T.
    /// Returns Data with List&lt;T&gt; value, or Data with error on failure.
    /// </summary>
    Task<Data.@this<List<T>>> GetAll<T>(string table) where T : Data.@this;

    /// <summary>
    /// Sets a Data value by table and key. Creates the table if it doesn't exist.
    /// The full Data envelope (value, type, signature) is persisted.
    /// </summary>
    Task<Data.@this> Set(string table, string key, Data.@this data);

    /// <summary>
    /// Removes a value by table and key.
    /// Returns success even if the key didn't exist.
    /// </summary>
    Task<Data.@this> Remove(string table, string key);

    /// <summary>
    /// Checks if a key exists in a table.
    /// Returns Data with bool value.
    /// </summary>
    Task<Data.@this> Exists(string table, string key);

    /// <summary>
    /// Lists all tables in this store.
    /// Returns Data with List&lt;string&gt; value.
    /// </summary>
    Task<Data.@this> Tables();

    /// <summary>
    /// Resolves the table name from a System.Type.
    /// Convention: last namespace segment, lowercased.
    /// e.g., App.modules.encryption → "encryption"
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
