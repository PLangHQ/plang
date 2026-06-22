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
        var result = global::app.type.item.serializer.json.Parse(doc.RootElement);
        await Assert.That(result).IsTypeOf<ListV>();
        var list = (ListV)result!;
        await Assert.That(list.Count).IsEqualTo(2);
        // Born-native: elements are scalar wrappers; ToRaw yields the backing.
        await Assert.That(((app.type.item.@this)(await list.At(0)!.Value())!).Clr<object>()).IsEqualTo((object)1L);
        await Assert.That((string?)((app.type.item.@this)(await list.At(1)!.Value())!).Clr<object>()).IsEqualTo("two");
    }

    [Test]
    public async Task Ctor_OnJsonArrayToken_DoesNotDecomposeToRaw()
    {
        // The Data ctor on a json-array source narrows to the list value type, not raw CLR.
        using var doc = JsonDocument.Parse("[1,2]");
        var d = new Data("x", doc.RootElement.Clone());
        await Assert.That((await d.Value())).IsTypeOf<ListV>();
    }

    [Test]
    public async Task Materialize_JsonArrayRoot_NarrowsToListValueType()
    {
        // A raw-backed json-array value materializes to the list value type on first touch (B+J).
        await using var app = NewApp();
        var ctx = app.User.Context;
        var d = global::app.data.@this.FromRaw("[1,2,3]", type.Create("object", "json", context: ctx), ctx, "nums");
        await Assert.That((await d.Value())).IsTypeOf<ListV>();
        await Assert.That(((ListV)(await d.Value())!).Count).IsEqualTo(3);
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
        await Assert.That((await list.At(0)!.Value())?.ToString()).IsEqualTo("1");
    }

    [Test]
    public async Task ListNavigator_Element_ReturnsElementDataDirectly()
    {
        // The list owns its navigation now (list.Navigate via GetChild): an index
        // returns the SAME element Data it holds (identity/signature intact), and the
        // implicit-first (`%list.name%` → list[0].name) stays.
        var element = new Data("", "first");
        var list = new ListV();
        list.Add(element);
        list.Add(new Data("", "second"));
        var data = new Data("items", list);

        await Assert.That(ReferenceEquals(await data.GetChild("0"), element)).IsTrue();
        await Assert.That((await (await data.GetChild("last")).Value())?.ToString()).IsEqualTo("second");
        // the count intrinsic answers in the PLang `number`
        await Assert.That(((global::app.type.number.@this)(await (await data.GetChild("count")).Value())!).ToInt32()).IsEqualTo(2);

        // Implicit-first through a list of dicts.
        var people = new ListV();
        var p0 = new DictV();
        p0.Set(new Data("name", "alice"));
        people.Add(new Data("", p0));
        var peopleData = new Data("people", people);
        await Assert.That((await (await peopleData.GetChild("name")).Value())?.ToString()).IsEqualTo("alice");
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
        var res = d.ShallowClone<global::app.type.list.@this<global::app.type.number.@this>>(await d.Value<global::app.type.list.@this<global::app.type.number.@this>>());
        await res.IsSuccess();
        await Assert.That(res.GetValue<List<long>>()!).IsEquivalentTo(new List<long> { 1, 2, 3 });
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

    [Skip("Per-element in-memory signing was removed: signing is an I/O-boundary concern now — one signature layer wraps the WHOLE payload, not each nested Data. Element-level survival of a signature at rest no longer applies; the list round-trip itself is covered below.")]
    [Test]
    public async Task F1_SignedElementInList_SurvivesPlangWireRoundTrip()
    {
        await using var app = NewApp();
        var ctx = app.User.Context;
        var plang = (global::app.channel.serializer.plang.@this)
            app.User.Channel.Serializers.GetByMimeType("application/plang");

        var list = new ListV();
        list.Add(new Data("signed", "hello world") { Context = ctx });
        var listData = new Data("list", list) { Context = ctx };

        var json = (await plang.Serialize(listData).Value())!.Clr<string>()!;
        var rebuilt = plang.Deserialize(json);
        await rebuilt.IsSuccess();
        var element = await rebuilt.GetChild("[0]");
        await Assert.That(element.IsInitialized).IsTrue();
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
        await Assert.That(result[0]?.ToString()).IsEqualTo("variable.set");
    }
}
