namespace PLang.Tests.App.Core;

/// <summary>
/// A computed (system variable) and a clr carrier store no context — their Data passes its own
/// (DynamicData for a computed, the navigating Data for a clr). %Now%, %!app%-style hosts and json
/// navigation still answer.
/// </summary>
public class ComputedAndClrStoreNoContextTests
{
    private global::app.@this _app = null!;
    private global::app.actor.context.@this Ctx => _app.User.Context;

    [Before(Test)]
    public void Setup() => _app = TestApp.Create("/tmp/computedclr-" + System.Guid.NewGuid().ToString("N")[..8]);

    [After(Test)]
    public async Task Cleanup() => await _app.DisposeAsync();

    [Test]
    public async Task Now_AnswersADatetime_ThroughItsData()
    {
        var now = await Ctx.Variable.Get("Now");
        await Assert.That(now.Peek()).IsTypeOf<global::app.type.item.datetime.@this>();
        await Assert.That(now.ToBoolean()).IsTrue();
        await Assert.That(await now.Value()).IsTypeOf<global::app.type.item.datetime.@this>();
    }

    [Test]
    public async Task ClrJson_NavigatesWithTheAskersContext()
    {
        var json = System.Text.Json.JsonDocument.Parse("{\"a\":{\"b\":7}}").RootElement;
        var d = new Data("x", new global::app.type.clr.@this(json, Ctx), context: Ctx);
        var b = await d.Get("a.b");
        await Assert.That((await b.Value())?.ToString()).IsEqualTo("7");
    }

    [Test]
    public async Task ComputedAndClr_DeclareNoContextMember()
    {
        foreach (var t in new[] { typeof(global::app.type.item.computed), typeof(global::app.type.clr.@this) })
        {
            var members = t.GetProperties(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic)
                .Where(p => p.PropertyType == typeof(global::app.actor.context.@this));
            await Assert.That(members.Any()).IsFalse();
        }
    }
}
