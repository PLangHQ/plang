namespace PLang.Tests.App.Serialization;

/// <summary>
/// A wire (a still-encoded .pr slice) stores no context. Decoding it is a use: the Data that holds
/// it lowers it with its own context; asked without one, it says so by name.
/// </summary>
public class WireStoresNoContextTests
{
    private global::app.@this _app = null!;
    private global::app.actor.context.@this Ctx => _app.User.Context;

    [Before(Test)]
    public void Setup() => _app = TestApp.Create("/tmp/wirectx-" + System.Guid.NewGuid().ToString("N")[..8]);

    [After(Test)]
    public async Task Cleanup() => await _app.DisposeAsync();

    private global::app.type.item.@this Wire(string slice, string type)
        => _app.Type[type].Create(slice, Ctx.Actor!.Channel.Serializers!.Transport);

    [Test]
    public async Task UnloadedWire_LoweredThroughItsData_DecodesWithTheDatasContext()
    {
        var d = new Data("n", Wire("42", "number"), context: Ctx);
        await Assert.That(d.Clr<long>()).IsEqualTo(42L);
    }

    [Test]
    public async Task UnloadedWire_LoweredWithoutAContext_FailsByName()
    {
        var wire = Wire("42", "number");
        await Assert.That(() => wire.Clr<long>()).Throws<System.InvalidOperationException>();
    }

    [Test]
    public async Task WireType_DeclaresNoContextMember()
    {
        var members = typeof(global::app.type.item.wire.@this).GetProperties(
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic)
            .Where(p => p.PropertyType == typeof(global::app.actor.context.@this));
        await Assert.That(members.Any()).IsFalse();
    }
}
