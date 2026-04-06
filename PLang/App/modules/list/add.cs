using App.Variables;

namespace App.modules.list;

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
        var data = Context.Variables.Get(ListName);
        var existing = data?.Value;
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

        if (AtIndex >= 0 && AtIndex <= list.Count)
            list.Insert(AtIndex, Value);
        else
            list.Add(Value);

        return Task.FromResult(Data.Ok(new types.list { count = list.Count, value = list }, App.Variables.Type.FromName("list")));
    }
}
