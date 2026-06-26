namespace PLang.Tests.App.Types;

// plang-types — Stage 1
// The flat Primitives/PrimitiveNames dicts in app/type/this.cs fold into the
// [PlangType] registry — one source of truth for name↔type and IsPrimitive.
// CLR primitives without a folder still resolve via a bootstrap RegisterRuntime.
// Bar: no behavior regresses.

public class RegistryFoldTests
{
    private global::app.type.catalog.@this _types = null!;
    private global::app.@this _app = null!;

    [Before(Test)]
    public void Setup()
    {
        _app = global::PLang.Tests.TestApp.Create("/tmp/regfold-" + System.Guid.NewGuid().ToString("N")[..6]);
        _types = new global::app.type.catalog.@this();
    }

    [After(Test)]
    public async Task Cleanup() => await _app.DisposeAsync();

    [Test]
    public async Task Get_NumberByName_ResolvesViaRegistry_NotFlatPrimitivesDict()
    {
        // After the fold, Get(name) and ResolveType(name) share the registry's
        // _nameToType map. Pre-fold "string" resolved via the flat dict but was
        // invisible to ResolveType. Number proper lands Stage 3 — until then,
        // the assertion is the routing rule, exercised on the primitive that
        // historically lived in the flat dict only.
        await Assert.That(_types.Get("string")).IsEqualTo(typeof(string));
        await Assert.That(_types.ResolveType("string")).IsEqualTo(typeof(string));
        await Assert.That(_types.Get("decimal")).IsEqualTo(typeof(decimal));
        await Assert.That(_types.ResolveType("decimal")).IsEqualTo(typeof(decimal));
    }

    [Test]
    public async Task IsPrimitive_AllPriorTrueAnswers_StillTrue()
    {
        await Assert.That(global::app.type.catalog.@this.IsPrimitive(typeof(string))).IsTrue();
        await Assert.That(global::app.type.catalog.@this.IsPrimitive(typeof(int))).IsTrue();
        await Assert.That(global::app.type.catalog.@this.IsPrimitive(typeof(long))).IsTrue();
        await Assert.That(global::app.type.catalog.@this.IsPrimitive(typeof(double))).IsTrue();
        await Assert.That(global::app.type.catalog.@this.IsPrimitive(typeof(decimal))).IsTrue();
        await Assert.That(global::app.type.catalog.@this.IsPrimitive(typeof(bool))).IsTrue();
        await Assert.That(global::app.type.catalog.@this.IsPrimitive(typeof(System.DateTime))).IsTrue();
        await Assert.That(global::app.type.catalog.@this.IsPrimitive(typeof(System.Guid))).IsTrue();
    }

    [Test]
    public async Task ResolveName_And_ResolveType_RoundTrip_PerBuiltIn()
    {
        // Round-trip for primitives whose CLR↔name mapping is 1:1.
        // Numerics (int/long/decimal/double) collapse to name "number" so they
        // don't per-CLR-type round-trip — covered separately below.
        foreach (var (name, clr) in new (string, System.Type)[]
        {
            ("text", typeof(string)),
            ("bool", typeof(bool)),
            ("datetime", typeof(System.DateTimeOffset)),
        })
        {
            await Assert.That(_types.ResolveType(name)).IsEqualTo(clr);
            await Assert.That(_types.ResolveName(clr)).IsEqualTo(name);
        }
        // Numerics: many-to-one — every numeric CLR primitive names "number"
        // (the kind carries the precision on the entity).
        await Assert.That(_types.ResolveName(typeof(int))).IsEqualTo("number");
        await Assert.That(_types.ResolveName(typeof(long))).IsEqualTo("number");
        await Assert.That(_types.ResolveName(typeof(decimal))).IsEqualTo("number");
        await Assert.That(_types.ResolveName(typeof(double))).IsEqualTo("number");
    }

    [Test]
    public async Task ClrPrimitivesWithoutFolder_StillRegistered_ViaBootstrap()
    {
        // string / int / decimal have no folder under app/type/ and carry no
        // [PlangType] attribute. Registry.SeedClrPrimitives is what makes
        // ResolveType see them. KnownTypes() must include them.
        var known = new System.Collections.Generic.HashSet<System.Type>(_types.KnownTypes());
        await Assert.That(known.Contains(typeof(string))).IsTrue();
        await Assert.That(known.Contains(typeof(int))).IsTrue();
        await Assert.That(known.Contains(typeof(decimal))).IsTrue();
    }

    [Test]
    public async Task Conversion_TryConvertTo_RoutesThroughRegistry_NotPrimitivesDict()
    {
        var (value, error) = global::app.type.catalog.@this.TryConvert("42", typeof(int), _app.User.Context);
        await Assert.That(error).IsNull();
        await Assert.That(value).IsEqualTo(42);
    }

    [Test]
    public async Task Formats_ExtensionToPlangName_ReadsThroughRegistry()
    {
        // Once an extension like "csv" / "json" resolves to a PLang name, the
        // registry's Get must accept that name. (app.formats produces the name;
        // the registry resolves it — the two halves meet at the same lookup.)
        await Assert.That(_types.Get("csv")).IsEqualTo(typeof(string));
        await Assert.That(_types.Get("json")).IsEqualTo(typeof(System.Text.Json.Nodes.JsonNode));
        await Assert.That(_types.Get("yaml")).IsEqualTo(typeof(string));
    }
}
