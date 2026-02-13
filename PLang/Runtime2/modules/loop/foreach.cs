using PLang.Runtime2.Core;
using PLang.Runtime2.Memory;

namespace PLang.Runtime2.modules.loop;

[Action("foreach")]
public partial class Foreach : IContext
{
    public partial object Collection { get; init; }
    public partial GoalCall GoalName { get; init; }
    [VariableName]
    public partial string? ItemName { get; init; }
    [VariableName]
    public partial string? KeyName { get; init; }

    public async Task<Data> Run()
    {
        var engine = Context.Engine!;
        var items = ResolveCollection();

        if (items == null || items.Count == 0)
            return Data.Ok(new types.loop { itemCount = 0, completed = true });

        var variableName = ItemName ?? "item";

        for (int i = 0; i < items.Count; i++)
        {
            if (Context.CancellationToken.IsCancellationRequested)
                return Data.Ok(new types.loop { itemCount = i, completed = false });

            var (key, value) = items[i];

            Context.MemoryStack.Set(variableName, value);

            if (KeyName != null)
                Context.MemoryStack.Set(KeyName, key);

            var result = await engine.RunGoalAsync(GoalName, Context, Context.CancellationToken);
            if (!result.Success) return result;
        }

        return Data.Ok(new types.loop { itemCount = items.Count, completed = true });
    }

    private List<(object? key, object? value)>? ResolveCollection()
    {
        var collection = Collection;

        if (collection is IList<object?> objList)
            return objList.Select((item, i) => ((object?)i, item)).ToList();

        if (collection is System.Collections.IList list)
            return list.Cast<object?>().Select((item, i) => ((object?)i, item)).ToList();

        if (collection is IDictionary<string, object?> dict)
            return dict.Select(kvp => ((object?)kvp.Key, kvp.Value)).ToList();

        if (collection is System.Collections.IDictionary rawDict)
        {
            var result = new List<(object?, object?)>();
            foreach (System.Collections.DictionaryEntry entry in rawDict)
                result.Add((entry.Key, entry.Value));
            return result;
        }

        if (collection is System.Collections.IEnumerable enumerable and not string)
        {
            var result = new List<(object?, object?)>();
            int idx = 0;
            foreach (var item in enumerable)
                result.Add((idx++, item));
            return result;
        }

        return null;
    }
}
