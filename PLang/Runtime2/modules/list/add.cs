using PLang.Runtime2.Engine.Memory;

namespace PLang.Runtime2.modules.list;

[Action("add", Cacheable = false)]
public partial class Add : IContext
{
    [VariableName]
    public partial string ListName { get; init; }
    public partial object? Value { get; init; }
    [Default(-1)]
    public partial int AtIndex { get; init; }

    public Task<Data> Run()
    {
        var existing = Context.MemoryStack.GetValue(ListName);
        var list = existing as List<object?> ?? new List<object?>();

        if (existing != null && existing is not List<object?>)
        {
            // Wrap non-list existing value into a list
            if (existing is System.Collections.IList rawList)
            {
                list = new List<object?>();
                foreach (var item in rawList) list.Add(item);
            }
            else
            {
                list = new List<object?> { existing };
            }
        }

        if (AtIndex >= 0 && AtIndex <= list.Count)
            list.Insert(AtIndex, Value);
        else
            list.Add(Value);

        Context.MemoryStack.Set(ListName, list);
        return Task.FromResult(Data.Ok(new types.list { count = list.Count, value = list }, PLang.Runtime2.Engine.Memory.Type.FromName("list")));
    }
}
