using App.Variables;

namespace App.modules.list;

[ModuleDescription("Inspect and manipulate in-memory lists: add, remove, sort, search, split, join, and group items")]
[System.ComponentModel.Description("Append or insert a value into a list variable at an optional index")]
[Action("add", Cacheable = false)]
public partial class Add : IContext
{
    [VariableName]
    public partial string ListName { get; init; }
    public partial Data.@this Value { get; init; }
    [Default(-1)]
    public partial Data.@this<int> AtIndex { get; init; }

    public Task<Data.@this> Run()
    {
        var data = Context.Variables.Get(ListName);
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
            Context.Variables.Set(ListName, list);
        }

        // Snapshot the Data so each list entry is independent — without this,
        // `add %x% to %list%` stores an alias and later `set %x% = ...` mutates
        // every list entry. Data.SnapshotClone breaks cyclic runtime types
        // (Goal↔Step↔Action) via [JsonIgnore] — see Data.SnapshotClone.
        Data.@this snapshot;
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
                var cloned = global::App.Data.@this.SnapshotClone(rawValue);
                snapshot = new Data.@this(Value.Name, cloned, Value.Type) { Context = Context };
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

        return Task.FromResult(Data(new types.list { count = list.Count, value = list }, App.Data.Type.FromName("list")));
    }
}
