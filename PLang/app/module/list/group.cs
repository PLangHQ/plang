using app.variable;

namespace app.module.list;

[Action("group")]
public partial class Group : IContext
{
    public partial data.@this<app.variable.@this> ListName { get; init; }
    [IsNotNull]
    public partial data.@this<global::app.type.text.@this> Key { get; init; }

    public async Task<data.@this<type.list>> Run()
    {
        var data = Context.Variable.Get(await ListName.Value());
        var key = (await Key.Value())!;

        // Buckets are native lists of the element Data — each bucket is itself
        // navigable (you can sort/where inside one). Insertion order preserved.
        var buckets = new Dictionary<string, app.type.list.@this>();
        var order = new List<string>();
        foreach (var (_, item) in data.EnumerateItems())
        {
            var keyData = item.GetChild(key);
            var keyValue = keyData.IsInitialized ? (await keyData.Value())?.ToString() ?? "" : "";
            if (!buckets.TryGetValue(keyValue, out var bucket))
            {
                bucket = new app.type.list.@this { Context = Context };
                buckets[keyValue] = bucket;
                order.Add(keyValue);
            }
            bucket.Add(item);
        }

        var result = new app.type.list.@this { Context = Context };
        foreach (var k in order)
        {
            var bucketDict = new app.type.dict.@this { Context = Context };
            bucketDict.Set(new global::app.data.@this("key", k));
            bucketDict.Set(new global::app.data.@this("items", buckets[k]));
            result.Add(new global::app.data.@this("", bucketDict));
        }

        return global::app.data.@this<type.list>.Ok(
            new type.list { count = result.Count, value = result }, app.type.@this.FromName("list"));
    }
}
