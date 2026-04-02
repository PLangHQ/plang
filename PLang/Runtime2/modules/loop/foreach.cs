using PLang.Runtime2.Engine;
using PLang.Runtime2.Engine.Memory;

namespace PLang.Runtime2.modules.loop;

[Action("foreach")]
public partial class Foreach : IContext
{
    public partial object? Collection { get; init; }
    public partial GoalCall GoalName { get; init; }
    [VariableName]
    public partial string? ItemName { get; init; }
    [VariableName]
    public partial string? KeyName { get; init; }

    public async Task<Data> Run()
    {
        if (Collection == null)
            return Data.Ok(new types.loop { itemCount = 0, completed = true });

        var engine = Context.Engine!;
        var variableName = ItemName ?? "item";
        int count = 0;

        // Iterate lazily — respects custom enumerators (e.g. GoalSteps skipping disabled steps)
        foreach (var (key, value) in EnumerateCollection())
        {
            if (Context.CancellationToken.IsCancellationRequested)
                return Data.Ok(new types.loop { itemCount = count, completed = false });

            Context.MemoryStack.Set(variableName, value);

            if (KeyName != null)
                Context.MemoryStack.Set(KeyName, key);

            var result = await engine.RunGoalAsync(GoalName, Context, Context.CancellationToken);
            if (!result.Success && !result.Handled) return result;
            count++;
        }

        return Data.Ok(new types.loop { itemCount = count, completed = true });
    }

    private IEnumerable<(object? key, object? value)> EnumerateCollection()
    {
        var collection = Collection;

        if (collection is IDictionary<string, object?> dict)
        {
            foreach (var kvp in dict)
                yield return (kvp.Key, kvp.Value);
            yield break;
        }

        if (collection is System.Collections.IDictionary rawDict)
        {
            foreach (System.Collections.DictionaryEntry entry in rawDict)
                yield return (entry.Key, entry.Value);
            yield break;
        }

        // For all other enumerables (including IList, GoalSteps, etc.)
        // iterate lazily through the collection's own enumerator
        if (collection is System.Collections.IEnumerable enumerable and not string)
        {
            int idx = 0;
            foreach (var item in enumerable)
                yield return (idx++, item);
        }
    }
}
