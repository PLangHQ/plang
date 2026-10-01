using query = global::app.module.list.type.query.@this;

namespace PLang.Tests.App.actions.list;

/// <summary>
/// A query is made from its dict, each key a part the query has; a key that is no part, or a part that doesn't read,
/// is refused with why — the parts named from those that exist.
/// </summary>
public class QueryPartTests
{
    private readonly global::app.@this _app = new global::app.@this("/test").Testing();

    [After(Test)]
    public async Task Dispose() => await _app.DisposeAsync();

    private Task<(query? Made, global::app.data.@this Data)> Made(Dictionary<string, object?> written)
    {
        var context = _app.actor.list.User.Context;
        var data = new global::app.data.@this("q", null, context: context);
        var made = query.Create(global::app.type.item.@this.Create(written, context), null, data);
        return Task.FromResult((made, data));
    }

    [Test] public async Task EveryPart_Reads()
    {
        var (made, data) = await Made(new()
        {
            ["where"] = new Dictionary<string, object?> { ["field"] = "age", ["op"] = ">", ["value"] = 20 },
            ["group"] = "name",
            ["distinct"] = true,
            ["order"] = "age",
        });
        await Assert.That(made).IsNotNull();
        await Assert.That(data.Success).IsTrue();
    }

    [Test] public async Task OneClause_IsAQueryListOfOne()
    {
        var (made, _) = await Made(new() { ["order"] = "age" });
        await Assert.That(made).IsTypeOf<global::app.module.list.type.query.list.@this>();
        await Assert.That(made!.Type.Name).IsEqualTo("query");
    }

    [Test] public async Task AKeyThatIsNoPart_IsRefused_NamingThePartsThatExist()
    {
        var (made, data) = await Made(new() { ["limit"] = 5 });
        await Assert.That(made).IsNull();
        await Assert.That(data.Error!.Message).IsEqualTo("'limit' is no part of a query — its parts are distinct, group, order, where");
    }

    [Test] public async Task AnUnknownOperator_IsRefused_ByTheWhere()
    {
        var (made, data) = await Made(new() { ["where"] = new Dictionary<string, object?> { ["field"] = "age", ["op"] = "~~", ["value"] = 1 } });
        await Assert.That(made).IsNull();
        await Assert.That(data.Error!.Message).StartsWith("where: Unsupported operator: '~~'.");
    }

    [Test] public async Task ANestedConditionThatDoesntRead_IsRefused_ByTheWhere()
    {
        var (made, data) = await Made(new() { ["where"] = new Dictionary<string, object?> { ["or"] = new List<object?>() } });
        await Assert.That(made).IsNull();
        await Assert.That(data.Error!.Message).IsEqualTo("where: or holds a list of conditions, at least one");
    }

    [Test] public async Task AGroupWithNoField_IsRefused()
    {
        var (made, data) = await Made(new() { ["group"] = "" });
        await Assert.That(made).IsNull();
        await Assert.That(data.Error!.Message).IsEqualTo("group: names the field to group by — group: \"name\"");
    }

    [Test] public async Task ADistinctWrittenAsText_ReadsAsBoolReadsIt()
    {
        var (made, data) = await Made(new() { ["distinct"] = "true" });
        await Assert.That(made).IsNotNull();
        await Assert.That(data.Success).IsTrue();
    }

    [Test] public async Task ADistinctThatIsNoBool_IsRefused()
    {
        var (made, data) = await Made(new() { ["distinct"] = "yes" });
        await Assert.That(made).IsNull();
        await Assert.That(data.Error!.Message).IsEqualTo("distinct: Cannot parse 'yes' as bool — expected true or false.");
    }

    [Test] public async Task AnOrderDescThatIsNoBool_IsRefused()
    {
        var (made, data) = await Made(new() { ["order"] = new Dictionary<string, object?> { ["field"] = "age", ["desc"] = "yes" } });
        await Assert.That(made).IsNull();
        await Assert.That(data.Error!.Message).IsEqualTo("order: Cannot parse 'yes' as bool — expected true or false.");
    }

    [Test] public async Task AnOrderKeyThatIsNeitherFieldNorKey_IsRefused()
    {
        var (made, data) = await Made(new() { ["order"] = 5 });
        await Assert.That(made).IsNull();
        await Assert.That(data.Error!.Message).IsEqualTo("order: a key is a field (\"age\") or {field, desc}");
    }
}
