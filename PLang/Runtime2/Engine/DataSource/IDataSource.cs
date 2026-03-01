using PLang.Runtime2.Engine.Memory;

namespace PLang.Runtime2.Engine.DataSource;

/// <summary>
/// Interface for persistent key-value storage.
/// Each actor owns a DataSource. Modules own their tables (encryption → "encryption", settings → "settings").
/// All methods return Data, never throw.
/// </summary>
public interface IDataSource : IDisposable
{
    /// <summary>
    /// Gets a single value by table and key.
    /// Returns Data with the value, or Data with null value if not found.
    /// </summary>
    Task<Data> Get(string table, string key);

    /// <summary>
    /// Gets all key-value pairs in a table.
    /// Returns Data with List&lt;Data&gt; value (each item has Name=key, Value=stored value).
    /// </summary>
    Task<Data> GetAll(string table);

    /// <summary>
    /// Sets a value by table and key. Creates the table if it doesn't exist.
    /// </summary>
    Task<Data> Set(string table, string key, object? value);

    /// <summary>
    /// Removes a value by table and key.
    /// Returns success even if the key didn't exist.
    /// </summary>
    Task<Data> Remove(string table, string key);

    /// <summary>
    /// Checks if a key exists in a table.
    /// Returns Data with bool value.
    /// </summary>
    Task<Data> Exists(string table, string key);

    /// <summary>
    /// Lists all tables in this DataSource.
    /// Returns Data with List&lt;string&gt; value.
    /// </summary>
    Task<Data> Tables();

    /// <summary>
    /// Resolves the table name from a System.Type.
    /// Convention: last namespace segment, lowercased.
    /// e.g., PLang.Runtime2.modules.encryption → "encryption"
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
