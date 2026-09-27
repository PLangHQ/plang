namespace app.store;

/// <summary>
/// The app's store — <c>app.store</c>: tables owned by their owners (setup's executed steps, the LLM
/// cache, the settings rows), each a key → Data map. A kind of store says where the tables live (sqlite,
/// on disk or in memory). Every door answers Data; none throws.
/// </summary>
public abstract class @this : System.IDisposable
{
    /// <summary>
    /// A single value by table and key, as its plang item type <typeparamref name="T"/>
    /// (forced to a real value, never a raw string): a defined class (<c>Get&lt;Identity&gt;</c>)
    /// or, for a value from plang code, <c>Get&lt;item&gt;</c>. Returns <c>Data&lt;T&gt;</c>
    /// (null value if not found). There is no untyped get — every stored value has a type.
    /// </summary>
    public abstract Task<data.@this<T>> Get<T>(string table, string key) where T : global::app.type.item.@this, global::app.type.item.ICreate<T>;

    /// <summary>Every value in a table, each forced to its plang item type <typeparamref name="T"/>:
    /// Data with a list of <c>Data&lt;T&gt;</c>.</summary>
    public abstract Task<data.@this<global::app.type.item.list.@this>> GetAll<T>(string table) where T : global::app.type.item.@this, global::app.type.item.ICreate<T>;

    /// <summary>Stores a Data by table and key, the table made if it isn't there. The full Data
    /// (value, type, signature) is kept.</summary>
    public abstract Task<data.@this> Set(string table, string key, data.@this data);

    /// <summary>Removes a value by table and key; success even when the key wasn't there.</summary>
    public abstract Task<data.@this> Remove(string table, string key);

    /// <summary>Whether a table holds the key.</summary>
    public abstract Task<data.@this<global::app.type.item.@bool.@this>> Exists(string table, string key);

    /// <summary>The store's tables, by name.</summary>
    public abstract Task<data.@this<global::app.type.item.list.@this>> Tables();

    public abstract void Dispose();
}
