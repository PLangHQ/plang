namespace PLang.Tests.App.Types.ConversionGapTests;

using Data = global::app.data.@this;
using Dict = global::app.type.item.dict.@this;
using PlangList = global::app.type.item.list.@this;
using LlmMessage = global::app.module.llm.LlmMessage;

/// <summary>
/// The ONE dict→record suite: a plang <c>dict</c> lowers to a settable-prop CLR
/// record via <c>dict.Clr(target)</c> → the reflection kind's slots door. Each value
/// lowers ITSELF (string→int, nested dict→record, list-of-dicts→list-of-records) —
/// no STJ round-trip. Consolidates the former Runtime/Modules dict-conversion suites.
/// </summary>
public class DictListToRecordTests
{
    private static readonly global::app.actor.context.@this Ctx = global::PLang.Tests.TestApp.SharedContext;

    private static Dict D(params (string Key, object? Value)[] entries)
    {
        var d = new Dict(Ctx);
        foreach (var (k, v) in entries) d.Set(k, v);
        return d;
    }

    private static PlangList L(params global::app.type.item.@this[] items)
    {
        var l = new PlangList(Ctx);
        foreach (var i in items) l.Add(i);
        return l;
    }

    [Test]
    public async Task FlatDict_SetsProperties()
    {
        var target = D(("Name", "Alice"), ("Age", 30)).Clr(typeof(SimpleTarget)) as SimpleTarget;
        await Assert.That(target).IsNotNull();
        await Assert.That(target!.Name).IsEqualTo("Alice");
        await Assert.That(target!.Age).IsEqualTo(30);
    }

    [Test]
    public async Task Keys_AreCaseInsensitive()
    {
        var target = D(("name", "Bob"), ("age", 25)).Clr(typeof(SimpleTarget)) as SimpleTarget;
        await Assert.That(target!.Name).IsEqualTo("Bob");
        await Assert.That(target!.Age).IsEqualTo(25);
    }

    [Test]
    public async Task Value_CoercesToPropertyType()
    {
        // "35" (text) lowers itself to the int property.
        var target = D(("Name", "Charlie"), ("Age", "35")).Clr(typeof(SimpleTarget)) as SimpleTarget;
        await Assert.That(target!.Age).IsEqualTo(35);
    }

    [Test]
    public async Task ExtraKeys_AreIgnored()
    {
        var target = D(("Name", "Dave"), ("Age", 40), ("ExtraField", "ignored")).Clr(typeof(SimpleTarget)) as SimpleTarget;
        await Assert.That(target!.Name).IsEqualTo("Dave");
    }

    [Test]
    public async Task NestedDict_RecursesToRecord()
    {
        var target = D(
            ("Text", "do something"),
            ("OnError", D(("RetryCount", 2), ("Goal", D(("Name", "HandleError"))))))
            .Clr(typeof(StepLike)) as StepLike;

        await Assert.That(target!.Text).IsEqualTo("do something");
        await Assert.That(target!.OnError).IsNotNull();
        await Assert.That(target!.OnError!.RetryCount).IsEqualTo(2);
        await Assert.That(target!.OnError!.Goal!.Name).IsEqualTo("HandleError");
    }

    [Test]
    public async Task ListOfDicts_BecomesListOfRecords()
    {
        var target = D(
            ("Text", "test step"),
            ("Actions", L(
                D(("Module", "file"), ("ActionName", "read")),
                D(("Module", "output"), ("ActionName", "write")))))
            .Clr(typeof(StepLike)) as StepLike;

        await Assert.That(target!.Actions).IsNotNull();
        await Assert.That(target!.Actions!.Count).IsEqualTo(2);
        await Assert.That(target!.Actions![0].Module).IsEqualTo("file");
        await Assert.That(target!.Actions![1].Module).IsEqualTo("output");
    }

    [Test]
    public async Task SingleDict_To_DomainRecord()
    {
        var msg = D(("Role", "user"), ("Content", "hi")).Clr(typeof(LlmMessage)) as LlmMessage;
        await Assert.That(msg).IsNotNull();
        await Assert.That(msg!.Role).IsEqualTo("user");
    }

    [Test]
    public async Task ListOfDicts_To_ListOfDomainRecords_LiveTagPath()
    {
        // The live path: a list bound to a list<T> slot re-tags (Value<list<T>>);
        // each row stays a dict, converting to T only when read.
        var lst = L(
            D(("Role", "system"), ("Content", "sys-msg")),
            D(("Role", "user"), ("Content", "hi")));

        var typed = await new Data("", lst, context: Ctx)
            .Value<global::app.type.item.list.@this<LlmMessage>>();

        await Assert.That(typed).IsNotNull();
        await Assert.That(typed!.Items.Count).IsEqualTo(2);
    }
}

public class SimpleTarget
{
    public string Name { get; set; } = "";
    public int Age { get; set; }
}

public class StepLike
{
    public string Text { get; set; } = "";
    public ErrorHandlerLike? OnError { get; set; }
    public List<ActionLike>? Actions { get; set; }
}

public class ErrorHandlerLike
{
    public int RetryCount { get; set; }
    public GoalRefLike? Goal { get; set; }
}

public class GoalRefLike
{
    public string Name { get; set; } = "";
}

public class ActionLike
{
    public string Module { get; set; } = "";
    public string ActionName { get; set; } = "";
}
