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
        // every list entry. Force.DeepCloner deep-walks the whole graph and
        // hangs on cyclic runtime types (Goal↔Step↔Action). JSON roundtrip
        // honors [JsonIgnore] (which breaks the cycles) and produces a clean
        // serializable copy — exactly what we want for trace-style snapshots.
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
                // CamelCase naming policy so cloned dict keys match the rest
                // of the serialized data (.pr files, traces, viewer all expect
                // camelCase). Default options would produce PascalCase keys
                // for typed POCO sources (Goal, Step, Action) and the resulting
                // dicts would mismatch downstream readers.
                var jsonOpts = new System.Text.Json.JsonSerializerOptions {
                    PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase,
                    PropertyNameCaseInsensitive = true,
                };
                var json = System.Text.Json.JsonSerializer.Serialize(rawValue, jsonOpts);
                var cloned = System.Text.Json.JsonSerializer.Deserialize<object?>(json, jsonOpts);
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
