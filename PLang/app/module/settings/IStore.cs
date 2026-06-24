using app.variable;

namespace app.module.settings;

/// <summary>
/// Interface for persistent key-value storage of PLang settings.
/// Each actor owns a store. Modules own their tables (encryption → "encryption", settings → "settings").
/// All methods accept and return Data, never throw.
/// </summary>
public interface IStore : IDisposable
{
    /// <summary>
    /// Gets a single value by table and key, as its plang item type <typeparamref name="T"/>
    /// (forced to a real value, never a raw string): a defined class (<c>Get&lt;Identity&gt;</c>)
    /// or, for a value from plang code, <c>Get&lt;item&gt;</c>. Returns <c>Data&lt;T&gt;</c>
    /// (null value if not found). There is no untyped get — every stored value has a type.
    /// </summary>
    Task<data.@this<T>> Get<T>(string table, string key) where T : global::app.type.item.@this, global::app.type.item.ICreate<T>;

    /// <summary>
    /// Gets all key-value pairs in a table, each forced to its plang item type
    /// <typeparamref name="T"/>. Returns Data with a list of <c>Data&lt;T&gt;</c>.
    /// </summary>
    Task<data.@this<global::app.type.list.@this>> GetAll<T>(string table) where T : global::app.type.item.@this, global::app.type.item.ICreate<T>;

    /// <summary>
    /// Sets a Data value by table and key. Creates the table if it doesn't exist.
    /// The full Data (value, type, signature) is persisted.
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
    Task<data.@this<global::app.type.@bool.@this>> Exists(string table, string key);

    /// <summary>
    /// Lists all tables in this store.
    /// Returns Data with List&lt;string&gt; value.
    /// </summary>
    Task<data.@this<global::app.type.list.@this>> Tables();

    /// <summary>
    /// Resolves the table name from a System.Type.
    /// Convention: last namespace segment, lowercased.
    /// e.g., app.module.encryption → "encryption"
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
