namespace PLang.Tests.App.CollectionsAreData;

// Stage 3 — arrays hold `Data`. The load-bearing stage where F1 dies.
// UnwrapJsonArray builds List<data>; ctor stops decomposing array tokens; Materialize
// narrows json arrays to the list value type; navigator.Element returns Data directly
// (no WrapItem); Conversion arms unwrap; writer disambiguates by type.
public class Stage3_ArraysAsDataTests
{
    [Test]
    public async Task UnwrapJsonArray_ProducesListOfData_NotRaw()
    {
        // data/this.cs:1329 — UnwrapJsonArray returns List<data.@this>, not List<object?>.
        // Each element carries its own Type/Signature; F1 closes (A).
        Assert.Fail("Not implemented");
        await Task.CompletedTask;
    }

    [Test]
    public async Task Ctor_OnJsonArrayToken_DoesNotDecomposeToRaw()
    {
        // The Data ctor (data/this.cs:151) on a json-array source no longer walks every
        // token to raw CLR. The lazy seam holds bytes; first access materializes to list.
        Assert.Fail("Not implemented");
        await Task.CompletedTask;
    }

    [Test]
    public async Task Materialize_JsonArrayRoot_NarrowsToListValueType()
    {
        // Materialize's array branch (B+J) produces the new list value type, not a raw
        // List<object?>. type.Convert's array arm matches.
        Assert.Fail("Not implemented");
        await Task.CompletedTask;
    }

    [Test]
    public async Task ListValueType_HoldsListOfData()
    {
        // app/type/list/'s value type holds List<data.@this> — symmetric to dict's
        // Dictionary<string,data.@this>. Each slot is an element Data.
        Assert.Fail("Not implemented");
        await Task.CompletedTask;
    }

    [Test]
    public async Task ListNavigator_Element_ReturnsElementDataDirectly()
    {
        // navigator/List Element returns the element Data without any raw-branch fallback
        // and without WrapItem. The implicit-first (`%list.name%` → list[0].name) stays (D).
        Assert.Fail("Not implemented");
        await Task.CompletedTask;
    }

    [Test]
    public async Task WrapItem_Removed_FromDataAndCallers()
    {
        // data/this.cs:516 WrapItem is gone; callers at :485/:493/:502 updated. There is
        // no longer a raw-to-Data wrapping path because elements are always Data already.
        Assert.Fail("Not implemented");
        await Task.CompletedTask;
    }

    [Test]
    public async Task Conversion_ListOfData_ToTypedList_UnwrapsEachElement()
    {
        // type/list/Conversion.cs arms (IList / JsonElement-array / JsonArray) read element
        // Data and produce a typed List<T> by reading each .Value (I).
        Assert.Fail("Not implemented");
        await Task.CompletedTask;
    }

    [Test]
    public async Task JsonWriter_DisambiguatesByValueType_DictBracesListBrackets()
    {
        // Once List<data> shares its CLR type with the deleted property-bag arm, the writer
        // disambiguates by value type: dict → `{}`, list → `[]`. No ambiguity, no fallback.
        Assert.Fail("Not implemented");
        await Task.CompletedTask;
    }

    [Test]
    public async Task PrimitiveMap_ListRegistered_RawListEntryRetired()
    {
        // type/primitive/this.cs:48 — "list" maps to the new value type. The raw List<object>
        // entry is gone (J).
        Assert.Fail("Not implemented");
        await Task.CompletedTask;
    }

    [Test]
    public async Task ResidualListObjectSweep_BuilderCodeSiteUpdated()
    {
        // module/builder/code/Default.cs:854 — the one `is List<object?>` site is swept to
        // List<data> where it's a value list (K). Infra IDictionary refs (callstack flags,
        // goal params) are deliberately untouched; this test pins the value-container site.
        Assert.Fail("Not implemented");
        await Task.CompletedTask;
    }
}
