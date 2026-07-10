using app.variable;

namespace app.module.list;

[Action("group")]
public partial class Group : IContext
{
    public partial data.@this<app.variable.@this> ListName { get; init; }
    [IsNotNull]
    public partial data.@this<global::app.type.item.text.@this> Key { get; init; }

    public async Task<data.@this<type.list>> Run()
    {
        var data = await Context.Variable.Get(await ListName.Value());
        var key = (await Key.Value())!.Clr<string>()!;

        // Buckets are native lists of the element Data — each bucket is itself
        // navigable (you can sort/where inside one). Insertion order preserved.
        var buckets = new Dictionary<string, app.type.list.@this>();
        var order = new List<string>();
        foreach (var (_, item) in await data.EnumerateItems())
        {
            var keyData = await item.Get(key);
            var keyValue = keyData.IsInitialized ? (await keyData.Value())?.ToString() ?? "" : "";
            if (!buckets.TryGetValue(keyValue, out var bucket))
            {
                bucket = new app.type.list.@this(Context);
                buckets[keyValue] = bucket;
                order.Add(keyValue);
            }
            bucket.Add(item);
        }

        var result = new app.type.list.@this(Context);
        foreach (var k in order)
        {
            var bucketDict = new app.type.dict.@this(Context);
            bucketDict.Set(new global::app.data.@this("key", k, context: Context));
            bucketDict.Set(new global::app.data.@this("items", buckets[k], context: Context));
            result.Add(new global::app.data.@this("", bucketDict, context: Context));
        }

        return Context.Ok<type.list>(
            new type.list { count = result.CountRaw, value = result }, Context.Type.Create("list"));
    }
}
