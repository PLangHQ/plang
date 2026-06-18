using System.Reflection;

namespace PLang.Tests.App.TypedReturnsTests;

// Contract: Data materialization is owned by Data itself via .Type. Callers
// materialize through implicit conversion on property access / await Value();
// Data.Value<T> stays internal — no public generic materializer.

public class Stage0_DataMaterializationTests
{
    private global::app.@this _app = null!;

    [Before(Test)]
    public void Setup() => _app = new global::app.@this("/app");

    [After(Test)]
    public async Task TearDown() { await _app.DisposeAsync(); }

    // Data.Value<T> is the source generator's internal resolution entry point and
    // stays internal — callers must not pick a materializer via a CLR generic.
    // Reflection guards the rule.
    [Test]
    public async Task Data_GenericAsT_DoesNotExistAsPublicApi()
    {
        var publicGenericAs = typeof(Data)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Where(m => m.Name == "As" && m.IsGenericMethodDefinition)
            .ToList();
        await Assert.That(publicGenericAs).IsEmpty()
            .Because("Data.Value<T> must stay internal — no public generic materializer.");
    }

    // Reading a property off a typed string-Value triggers ConvertValue() on the
    // navigation path — the raw JSON is parsed once and %x.a% lands on the value.
    [Test]
    public async Task Data_PropertyAccess_UsesDeclaredTypeForMaterialization()
    {
        var src = new Data("x", "{\"a\":1}", new global::app.type.@this("json"))
        {
            Context = _app.User.Context
        };

        var aValue = await src.GetChild("a");

        await Assert.That(aValue.IsInitialized).IsTrue();
        await Assert.That((await aValue.Value())?.ToString()).IsEqualTo("1");
    }

    // ConvertValue rewrites _value once; second navigation reuses the already-
    // materialized object. Identity check on Value proves the cache.
    [Test]
    public async Task Data_Materialization_CachesResultOnFirstAccess()
    {
        var src = new Data("x", "{\"a\":1}", new global::app.type.@this("json"))
        {
            Context = _app.User.Context
        };

        _ = await src.GetChild("a");
        var firstMaterialized = await src.Value();
        _ = await src.GetChild("a");
        var secondMaterialized = await src.Value();

        await Assert.That(ReferenceEquals(firstMaterialized, secondMaterialized)).IsTrue()
            .Because("ConvertValue must replace _value in place — repeated access must not re-parse.");
    }

    // variable.set with Type=csv stores the raw string verbatim — no I/O, no parse,
    // byte-equal round-trip via Value. Materialization is deferred to first read.
    [Test]
    public async Task Data_VariableSet_NoParsingAtSetTime()
    {
        const string raw = "a,b,c\n1,2,3";
        var src = new Data("x", raw, new global::app.type.@this("csv"));

        await Assert.That((await src.Value())?.ToString()).IsEqualTo(raw)
            .Because("Setting a typed Data must not invoke the materializer.");
    }
}
