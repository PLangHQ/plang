namespace PLang.Tests.App.actions.list;

/// <summary>
/// A list-typed slot that holds exactly one whole <c>%people%</c> is that reference: it answers <c>IsVariable</c>, as a
/// text slot holding <c>%people%</c> does — so the builder asks the value, never compares texts.
/// </summary>
public class WholeReferenceTests
{
    private readonly global::app.@this _app = new global::app.@this("/test").Testing();

    [After(Test)]
    public async Task Dispose() => await _app.DisposeAsync();

    private global::app.data.@this Slot(string formal, string name)
    {
        var ctx = _app.actor.list.User.Context;
        return ctx.Action(formal)[name]!.Data(ctx);
    }

    [Test]
    public async Task AListSlotHoldingOneWholeReference_IsThatReference()
        => await Assert.That(Slot("list.query(List=%people%, Query={order: \"age\"})", "List").IsVariable).IsTrue();

    // the property's value as the program holds it — what the builder asks
    [Test]
    public async Task AListPropertyHoldingOneWholeReference_IsThatReference()
    {
        var value = _app.actor.list.User.Context.Action("list.query(List=%people%, Query={order: \"age\"})")["List"]!.Value;
        await Assert.That(value?.GetType().FullName).IsNotNull();
        await Assert.That(value!.IsVariable).IsTrue();
    }

    [Test]
    public async Task AListSlotHoldingAListLiteral_IsNoReference()
        => await Assert.That(Slot("list.query(List=[1, 2], Query={order: \"age\"})", "List").IsVariable).IsFalse();
}
