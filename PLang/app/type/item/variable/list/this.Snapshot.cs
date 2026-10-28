namespace app.type.item.variable.list;

public partial class @this
{
    /// <summary>
    /// The variables a program holds, each the Data whole (name, value, type, properties) under its name — what a snapshot
    /// keeps of this memory. Left out:
    ///  - !-prefixed infrastructure variables (!app, !filesystem, !error, …);
    ///  - always-fresh cells (Now, NowUtc, GUID, MyIdentity), which are made again where they are read.
    /// Under the store's key, the truth: one Data held under two names is held under both. Set, each comes back as it was
    /// saved, born again in this memory's context so it answers as this actor: a restore is no program's set, so nothing
    /// bound on a set runs.
    /// </summary>
    [global::app.Snapshot]
    public global::app.type.item.dict.@this row
    {
        get
        {
            var dict = new global::app.type.item.dict.@this(_context.App.type.list[typeof(global::app.type.item.dict.@this)]);
            foreach (var (name, variable) in _variables)
            {
                // the rows a program set: not the system's, not a value made again where it is read — the lazy lives on
                // the computed instance, so check the instance, not the Data subtype (a store re-wrap loses the subtype)
                if (IsSystem(name) || variable is data.DynamicData || variable.Item is global::app.type.item.computed) continue;
                dict.Set(name, variable);
            }
            return dict;
        }
        private set
        {
            foreach (var variable in value.Rows(_context)) Bind(variable.Name, variable.Clone().Copy(_context));
        }
    }
}
