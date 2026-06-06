using System.Reflection;
using System.Text.Json;
using PLang.Tests.App.Serialization;
using ListV = global::app.type.list.@this;
using DictV = global::app.type.dict.@this;
using type = global::app.type.@this;

namespace PLang.Tests.App.CollectionsAreData;

// Stage 3 — arrays hold `Data`. The load-bearing stage where F1 dies.
// UnwrapJsonArray builds a list value type holding List<data>; ctor stops decomposing
// array tokens; Materialize narrows json arrays to the list value type; navigator
// returns the element Data directly (no WrapItem); Conversion unwraps; writer
// disambiguates by wrapper type (dict→{}, list→[]).
public class Stage3_ArraysAsDataTests
{
    private static global::app.@this NewApp()
        => new(System.IO.Path.Combine(System.IO.Path.GetTempPath(),
            "plang-arrays-" + System.Guid.NewGuid().ToString("N")[..8]));

    [Test]
    public async Task UnwrapJsonArray_ProducesListOfData_NotRaw()
    {
        // UnwrapJsonElement on a json array returns the native list value type whose
        // elements are Data — not a raw List<object?>. F1 closes (A).
        using var doc = JsonDocument.Parse("[1,\"two\"]");
        var result = global::app.data.@this.UnwrapJsonElement(doc.RootElement);
        await Assert.That(result).IsTypeOf<ListV>();
        var list = (ListV)result!;
        await Assert.That(list.Count).IsEqualTo(2);
        // Born-native: elements are scalar wrappers; ToRaw yields the backing.
        await Assert.That(((app.type.item.@this)list.At(0)!.Value!).ToRaw()).IsEqualTo((object)1L);
        await Assert.That((string?)((app.type.item.@this)list.At(1)!.Value!).ToRaw()).IsEqualTo("two");
    }

    [Test]
    public async Task Ctor_OnJsonArrayToken_DoesNotDecomposeToRaw()
    {
        // The Data ctor on a json-array source narrows to the list value type, not raw CLR.
        using var doc = JsonDocument.Parse("[1,2]");
        var d = new Data("x", doc.RootElement.Clone());
        await Assert.That(d.Value).IsTypeOf<ListV>();
    }

    [Test]
    public async Task Materialize_JsonArrayRoot_NarrowsToListValueType()
    {
        // A raw-backed json-array value materializes to the list value type on first touch (B+J).
        await using var app = NewApp();
        var ctx = app.User.Context;
        var d = global::app.data.@this.FromRaw("[1,2,3]", type.Create("object", "json", context: ctx), ctx, "nums");
        d.ForceMaterialize();
        await Assert.That(d.Value).IsTypeOf<ListV>();
        await Assert.That(((ListV)d.Value!).Count).IsEqualTo(3);
    }

    [Test]
    public async Task ListValueType_HoldsListOfData()
    {
        // app/type/list/'s value type holds List<data.@this> — symmetric to dict.
        var list = new ListV();
        list.Add(new Data("", 1L));
        list.Add(new Data("", "x"));
        await Assert.That(list.Count).IsEqualTo(2);
        await Assert.That(list.At(0)).IsTypeOf<Data>();
        await Assert.That(list.At(0)!.Value).IsEqualTo((object)1L);
    }

    [Test]
    public async Task ListNavigator_Element_ReturnsElementDataDirectly()
    {
        // navigator/List returns the element Data with no raw-branch fallback and no
        // WrapItem — the SAME instance the list holds (identity/signature intact). The
        // implicit-first (`%list.name%` → list[0].name) stays (D).
        var element = new Data("", "first");
        var list = new ListV();
        list.Add(element);
        list.Add(new Data("", "second"));
        var data = new Data("items", list);

        var nav = new global::app.variable.navigator.List();
        await Assert.That(nav.CanNavigate(data)).IsTrue();
        await Assert.That(ReferenceEquals(nav.Navigate(data, "0"), element)).IsTrue();
        await Assert.That((string?)nav.Navigate(data, "last").Value).IsEqualTo("second");
        await Assert.That((int)nav.Navigate(data, "count").Value!).IsEqualTo(2);

        // Implicit-first through a list of dicts.
        var people = new ListV();
        var p0 = new DictV();
        p0.Set(new Data("name", "alice"));
        people.Add(new Data("", p0));
        var peopleData = new Data("people", people);
        await Assert.That((string?)nav.Navigate(peopleData, "name").Value).IsEqualTo("alice");
    }

    [Test]
    public async Task WrapItem_Removed_FromDataAndCallers()
    {
        // data/this.cs WrapItem is gone — every collection element is a Data already,
        // so there is no general raw→Data wrapping path on Data.
        var m = typeof(Data).GetMethod("WrapItem", BindingFlags.NonPublic | BindingFlags.Instance);
        await Assert.That(m).IsNull();
    }

    [Test]
    public async Task Conversion_ListOfData_ToTypedList_UnwrapsEachElement()
    {
        // Coercing the list value type to a typed List<T> reads each element Data's value (I).
        await using var app = NewApp();
        var ctx = app.User.Context;
        var list = new ListV();
        list.Add(new Data("", 1L));
        list.Add(new Data("", 2L));
        list.Add(new Data("", 3L));
        var d = new Data("nums", list) { Context = ctx };
        var res = d.As<global::app.type.list.@this<global::app.type.number.@this>>(ctx);
        await res.IsSuccess();
        await Assert.That(res.Value!).IsEquivalentTo(new List<long> { 1, 2, 3 });
    }

    [Test]
    public async Task JsonWriter_DisambiguatesByValueType_DictBracesListBrackets()
    {
        // The writer disambiguates by wrapper type: dict → `{}`, list → `[]`. No ambiguity.
        var list = new ListV();
        list.Add(new Data("", 1L));
        var dict = new DictV();
        dict.Set(new Data("a", 1L));

        var listJson = NormalizePipelineHelper.SerializeValueSlot(list);
        var dictJson = NormalizePipelineHelper.SerializeValueSlot(dict);
        await Assert.That(listJson.StartsWith("[")).IsTrue();
        await Assert.That(dictJson.StartsWith("{")).IsTrue();
    }

    [Test]
    public async Task PrimitiveMap_ListRegistered_RawListEntryRetired()
    {
        // type/primitive/this.cs maps "list"/"array" to the new value type; the raw
        // List<object> entry is gone (J).
        var aliases = global::app.type.primitive.@this.Aliases;
        await Assert.That(aliases["list"]).IsEqualTo(typeof(ListV));
        await Assert.That(aliases["array"]).IsEqualTo(typeof(ListV));
        await Assert.That(aliases["list"]).IsNotEqualTo(typeof(List<object>));
    }

    [Test]
    public async Task F1_SignedElementInList_SurvivesPlangWireRoundTrip()
    {
        // The load-bearing proof: a signed Data added to a list keeps its signature at
        // rest, and survives a round-trip through the application/plang wire — verify
        // on %list[0]% would pass. (Per the ruling, F1 rides .plang, not bare .json.)
        await using var app = NewApp();
        var ctx = app.User.Context;
        var plang = (global::app.channel.serializer.plang.@this)
            app.User.Channel.Serializers.GetByMimeType("application/plang");

        var signed = new Data("signed", "hello world") { Context = ctx };
        signed.EnsureSigned();
        await Assert.That(signed.Signature).IsNotNull();

        var list = new ListV();
        list.Add(signed);
        var listData = new Data("list", list) { Context = ctx };

        var json = plang.Serialize(listData).Value!;
        var roundtrip = plang.Deserialize(json);
        await roundtrip.IsSuccess();

        var rebuilt = (Data)roundtrip.Value!;
        var element = rebuilt.GetChild("[0]");
        await Assert.That(element.IsInitialized).IsTrue();
        await Assert.That(element.Signature).IsNotNull()
            .Because("F1: a signed Data survives at rest inside a list across the .plang wire.");
    }

    [Test]
    public async Task ResidualListObjectSweep_BuilderCodeSiteUpdated()
    {
        // module/builder/code/Default.cs's ToStepList reads the native list value type
        // (the one is-List<object?> value site swept, K). Reflection-invoke it.
        var method = typeof(global::app.module.builder.code.Default).GetMethod("ToStepList",
            BindingFlags.NonPublic | BindingFlags.Static);
        await Assert.That(method).IsNotNull();
        var list = new ListV();
        list.Add(new Data("", "variable.set"));
        list.Add(new Data("", "list.add"));
        var result = method!.Invoke(null, new object?[] { list }) as List<object>;
        await Assert.That(result).IsNotNull();
        await Assert.That(result!.Count).IsEqualTo(2);
        await Assert.That((string?)result[0]).IsEqualTo("variable.set");
    }
}
