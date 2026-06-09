using System.Reflection;

namespace PLang.Tests.App.TypedReturnsTests;

// Contract: Data materialization is owned by Data itself via .Type. Public
// surface is Data.As(string typeName) (cross-type coercion) and implicit
// conversion on property access; Data.As<T> stays internal.

public class Stage0_DataMaterializationTests
{
    private global::app.@this _app = null!;

    [Before(Test)]
    public void Setup() => _app = new global::app.@this("/app");

    [After(Test)]
    public async Task TearDown() { await _app.DisposeAsync(); }

    // Data.As<T> is the source generator's internal resolution entry point; the
    // public materialization surface is Data.As(string) so callers don't pick the
    // materializer via a CLR generic. Reflection guards the rule.
    [Test]
    public async Task Data_GenericAsT_DoesNotExistAsPublicApi()
    {
        var publicGenericAs = typeof(Data)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Where(m => m.Name == "As" && m.IsGenericMethodDefinition)
            .ToList();
        await Assert.That(publicGenericAs).IsEmpty()
            .Because("Data.As<T> must stay internal — callers materialize via As(string typeName).");
    }

    // Data.As("json") on a raw-string Value looks up the json materializer and
    // returns a Data whose Value is the parsed shape (Dictionary for json).
    [Test]
    public async Task Data_AsString_LooksUpMaterializerByTypeName()
    {
        var src = new Data("x", "{\"a\":1}") { Context = _app.User.Context };
        var materialized = src.As("json");

        await materialized.IsSuccess();
        await Assert.That((await materialized.Value())).IsTypeOf<app.type.dict.@this>();
    }

    // Reading a property off a typed string-Value triggers ConvertValue() on the
    // navigation path — the raw JSON is parsed once and %x.a% lands on the value.
    [Test]
    public async Task Data_PropertyAccess_UsesDeclaredTypeForMaterialization()
    {
        var src = new Data("x", "{\"a\":1}")
        {
            Type = new global::app.type.@this("json"),
            Context = _app.User.Context
        };

        var aValue = src.GetChild("a");

        await Assert.That(aValue.IsInitialized).IsTrue();
        await Assert.That((await aValue.Value())?.ToString()).IsEqualTo("1");
    }

    // ConvertValue rewrites _value once; second navigation reuses the already-
    // materialized object. Identity check on Value proves the cache.
    [Test]
    public async Task Data_Materialization_CachesResultOnFirstAccess()
    {
        var src = new Data("x", "{\"a\":1}")
        {
            Type = new global::app.type.@this("json"),
            Context = _app.User.Context
        };

        _ = src.GetChild("a");
        var firstMaterialized = src.Value;
        _ = src.GetChild("a");
        var secondMaterialized = src.Value;

        await Assert.That(ReferenceEquals(firstMaterialized, secondMaterialized)).IsTrue()
            .Because("ConvertValue must replace _value in place — repeated access must not re-parse.");
    }

    // variable.set with Type=csv stores the raw string verbatim — no I/O, no parse,
    // byte-equal round-trip via Value. Materialization is deferred to first read.
    [Test]
    public async Task Data_VariableSet_NoParsingAtSetTime()
    {
        const string raw = "a,b,c\n1,2,3";
        var src = new Data("x", raw) { Type = new global::app.type.@this("csv") };

        await Assert.That((await src.Value())).IsEqualTo(raw)
            .Because("Setting a typed Data must not invoke the materializer.");
    }

    // Unknown type names land an error at the As-call site — clear feedback to the
    // caller, no surprise crash downstream.
    [Test]
    public async Task Data_AsString_UnknownType_SurfacesErrorAtAccess_NotAtSet()
    {
        var src = new Data("x", "anything") { Type = new global::app.type.@this("bogus") };

        await Assert.That((await src.Value())).IsEqualTo("anything")
            .Because("Setting with an unknown declared type stays cleanly stored.");

        var result = src.As("bogus", _app.User.Context);
        await result.IsFailure();
        await Assert.That(result.Error).IsNotNull();
        await Assert.That(result.Error!.Key).IsEqualTo("UnknownType");
    }

    // The argument to As(typeName) wins — declared .Type only influences implicit
    // property-access materialization. As is explicit, callsite-chosen coercion.
    [Test]
    public async Task Data_AsString_CrossTypeCoercion_LooksUpRequestedNotDeclared()
    {
        var src = new Data("x", "{\"a\":1}")
        {
            Type = new global::app.type.@this("csv"),
            Context = _app.User.Context
        };

        var asJson = src.As("json");

        await asJson.IsSuccess();
        await Assert.That((await asJson.Value())).IsTypeOf<app.type.dict.@this>()
            .Because("As('json') must dispatch on the argument, not src.Type.");
    }
}
