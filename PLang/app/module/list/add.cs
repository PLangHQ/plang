using app.variable;

namespace app.module.list;

[Action("add", Cacheable = false)]
public partial class Add : IContext
{
    public partial data.@this<Variable> ListName { get; init; }
    public partial data.@this Value { get; init; }
    [Default(-1)]
    public partial data.@this<int> AtIndex { get; init; }

    public Task<data.@this<type.list>> Run()
    {
        var data = Context.Variables.Get(ListName.Value);
        var existing = data.Value;
        var list = existing as List<object?>;

        if (list == null)
        {
            // Convert non-list value or create new list
            if (existing is System.Collections.IList rawList)
            {
                list = new List<object?>();
                foreach (var item in rawList) list.Add(item);
            }
            else if (existing != null)
            {
                list = new List<object?> { existing };
            }
            else
            {
                list = new List<object?>();
            }
            Context.Variables.Set(ListName.Value, list);
        }

        // Snapshot the Data so each list entry is independent — without this,
        // `add %x% to %list%` stores an alias and later `set %x% = ...` mutates
        // every list entry. Data.SnapshotClone breaks cyclic runtime types
        // (Goal↔Step↔Action) via [JsonIgnore] — see Data.SnapshotClone.
        data.@this snapshot;
        var rawValue = Value.Value;
        if (rawValue is null || rawValue is string || rawValue is bool || rawValue is System.IConvertible)
        {
            // Cheap clone is fine for primitives/strings.
            snapshot = Value.Clone();
        }
        else
        {
            try
            {
                var cloned = global::app.data.@this.SnapshotClone(rawValue);
                snapshot = new data.@this(Value.Name, cloned, Value.Type) { Context = Context };
            }
            catch (System.Exception ex) when (ex is System.Text.Json.JsonException || ex is NotSupportedException)
            {
                // Last resort — alias. Risk of mutation, but better than crashing.
                // Surface the failure so the alias-mode regression stays debuggable.
                _ = Context?.App?.Debug?.Write($"[list.add] snapshot-clone failed for '{Value.Name}': {ex.GetType().Name}: {ex.Message} — proceeding with alias (mutation risk)");
                snapshot = Value;
            }
        }

        if (AtIndex.Value >= 0 && AtIndex.Value <= list.Count)
            list.Insert(AtIndex.Value, snapshot);
        else
            list.Add(snapshot);

        return Task.FromResult(global::app.data.@this<type.list>.Ok(new type.list { count = list.Count, value = list }, app.data.type.FromName("list")));
    }
}
