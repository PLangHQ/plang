namespace PLang.Tests.App.Types;

// plang-types — Stage 1
// The flat Primitives/PrimitiveNames dicts in app/type/this.cs fold into the
// [PlangType] registry — one source of truth for name↔type and IsPrimitive.
// CLR primitives without a folder still resolve via a bootstrap RegisterRuntime.
// Bar: no behavior regresses.

public class RegistryFoldTests
{
    private global::app.type.list.@this _types = null!;
    private global::app.@this _app = null!;

    [Before(Test)]
    public void Setup()
    {
        _app = global::PLang.Tests.TestApp.Create("/tmp/regfold-" + System.Guid.NewGuid().ToString("N")[..6]);
        _types = new global::app.type.list.@this();
    }

    [After(Test)]
    public async Task Cleanup() => await _app.DisposeAsync();

    [Test]
    public async Task Get_AnAlias_ResolvesToTheItemThatOwnsIt()
    {
        // A primitive's spelled names are owned by its item: string → text, decimal → number.
        await Assert.That(_types.Get("string")).IsEqualTo(typeof(global::app.type.item.text.@this));
        await Assert.That(_types.ResolveType("string")).IsEqualTo(typeof(global::app.type.item.text.@this));
        await Assert.That(_types.Get("decimal")).IsEqualTo(typeof(global::app.type.item.number.@this));
        await Assert.That(_types.ResolveType("decimal")).IsEqualTo(typeof(global::app.type.item.number.@this));
    }

    [Test]
    public async Task ResolveName_And_ResolveType_RoundTrip_PerBuiltIn()
    {
        // A primitive name resolves to its item, and the item's C# mate names that item back.
        foreach (var (name, item, mate) in new (string, System.Type, System.Type)[]
        {
            ("text", typeof(global::app.type.item.text.@this), typeof(string)),
            ("bool", typeof(global::app.type.item.@bool.@this), typeof(bool)),
            ("datetime", typeof(global::app.type.item.datetime.@this), typeof(System.DateTimeOffset)),
        })
        {
            await Assert.That(_types.ResolveType(name)).IsEqualTo(item);
            await Assert.That(_types[mate]?.Name).IsEqualTo(name);
        }
        // Numerics: many-to-one — every numeric CLR primitive names "number"
        // (the kind carries the precision on the entity).
        await Assert.That(_types[typeof(int)]?.Name).IsEqualTo("number");
        await Assert.That(_types[typeof(long)]?.Name).IsEqualTo("number");
        await Assert.That(_types[typeof(decimal)]?.Name).IsEqualTo("number");
        await Assert.That(_types[typeof(double)]?.Name).IsEqualTo("number");
    }

    [Test]
    public async Task Conversion_TextScalar_LowersToNumericTarget()
    {
        // A value lowers itself to a CLR target through its own Clr — no central door.
        await Assert.That(new global::app.type.item.text.@this("42").Clr<int>()).IsEqualTo(42);
    }

    [Test]
    public async Task Formats_ExtensionToPlangName_ReadsThroughRegistry()
    {
        // The format registry names a file's type ({binary, kind: <extension>} — the bytes, with
        // the extension as the decode hint); the type registry must know that name. The two halves
        // meet at the same lookup.
        foreach (var extension in new[] { ".csv", ".json", ".yaml" })
        {
            var type = _app.Type.Extension(extension);
            await Assert.That(_app.Type.Contains(type.Name)).IsTrue();
            await Assert.That(type.Kind).IsNotNull();   // the extension's canonical kind (.yaml → yml)
        }
    }
}
