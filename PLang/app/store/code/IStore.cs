using app.module.code;

namespace app.store.code;

/// <summary>
/// What gives a store: the app's own (<c>/.data/data.sqlite</c>) and the settings' (<c>/.data/setting/data.sqlite</c>)
/// each ask the default once, at first use, so a store provider loaded with <c>code.load</c> before then is the one
/// they keep. Sqlite is the built-in default.
/// </summary>
public interface IStore : ICode
{
    /// <summary>The store at <paramref name="file"/> — in memory when <paramref name="memory"/> answers yes as it opens
    /// (a test app's), a database of the store's own — born with <paramref name="context"/>.</summary>
    global::app.store.@this Open(global::app.type.item.path.@this file, System.Func<bool> memory, global::app.actor.context.@this context);
}
