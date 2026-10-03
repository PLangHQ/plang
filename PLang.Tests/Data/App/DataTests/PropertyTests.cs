namespace PLang.Tests.App.DataTests;

// A Data's Property is a property list: each row a name, a type and a plang value. A wire primitive set on it is held
// as the plang value it is; a Data or a host object is never one. The wire shape is pinned in PropertiesWireShapeTests
// and Cut4_PropertiesWireTests.
public class PropertyTests : System.IAsyncDisposable
{
    private readonly global::app.@this _app = new global::app.@this("/tmp/PropertyTests-" + System.Guid.NewGuid().ToString("N")[..6]).Testing();
    public async System.Threading.Tasks.ValueTask DisposeAsync() => await _app.DisposeAsync();

    [Test] public async Task Set_Text_IsHeldAsText()
    {
        var data = _app.Data("x", 0);
        data.Property.Set("key", "value");
        await Assert.That(await data.Property.Value("key")).IsTypeOf<global::app.type.item.text.@this>();
        await Assert.That(await data.Property.Get<string>("key")).IsEqualTo("value");
    }

    [Test] public async Task Set_Int_IsHeldAsANumber()
    {
        var data = _app.Data("x", 0);
        data.Property.Set("n", 42);
        await Assert.That(await data.Property.Value("n")).IsTypeOf<global::app.type.item.number.@this>();
        await Assert.That(await data.Property.Get<int>("n")).IsEqualTo(42);
    }

    [Test] public async Task SettingNull_RemovesIt()
    {
        var data = _app.Data("x", 0);
        data.Property.Set("k", "v");
        data.Property.Set("k", null);
        await Assert.That(data.Property.Contains("k")).IsFalse();
    }

    [Test] public async Task Set_SameName_Replaces_NamesIgnoreCase()
    {
        var data = _app.Data("x", 0);
        data.Property.Set("k", "v");
        data.Property.Set("K", "w");
        await Assert.That(data.Property.Count).IsEqualTo(1);
        await Assert.That(await data.Property.Get<string>("k")).IsEqualTo("w");
    }

    [Test] public async Task UnknownName_IsNull()
    {
        var data = _app.Data("x", 0);
        await Assert.That(await data.Property.Value("missing")).IsNull();
    }

    [Test] public async Task AData_IsRefused()
    {
        var data = _app.Data("x", 0);
        var inner = _app.Data("y", 1);
        await Assert.That(() => data.Property.Set("k", inner)).Throws<ArgumentException>();
    }

    [Test] public async Task AHostObject_IsRefused()
    {
        var data = _app.Data("x", 0);
        await Assert.That(() => data.Property.Set("k", new System.Threading.CancellationTokenSource())).Throws<ArgumentException>();
    }

    [Test] public async Task Clone_IsAListOfItsOwn()
    {
        var properties = new global::app.type.property.list.@this();
        properties.Set("k", "v");
        var clone = properties.Clone();
        clone.Set("k", "w");
        await Assert.That(await properties.Get<string>("k")).IsEqualTo("v");
        await Assert.That(await clone.Get<string>("k")).IsEqualTo("w");
    }
}
