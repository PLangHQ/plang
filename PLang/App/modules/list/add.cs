using App.Variables;

namespace App.modules.list;

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

        if (AtIndex.Value >= 0 && AtIndex.Value <= list.Count)
            list.Insert(AtIndex.Value, Value.Value);
        else
            list.Add(Value.Value);

        return Task.FromResult(Data(new types.list { count = list.Count, value = list }, App.Data.Type.FromName("list")));
    }
}
