using TUnit.Core;
using TUnit.Assertions;

namespace PLang.Tests.App.TypeKindStrict.TypeValueModelTests;

// The wire emits flat `type` + `kind` keys (NOT `type:kind`, NOT `"type":"null"`).
// `Data.Kind` is sourced from `Type.Kind` — one home — but the serialised shape
// keeps two flat keys for backward compatibility.

public class WireKindShapeTests
{
    [Test] public async Task Wire_Write_EmitsFlatTypeAndKindKeys()
    {
        // var d = new data.@this("x", "hi") { Type = type("text","md") };
        // ToJson(d) contains "\"type\":\"text\"" AND "\"kind\":\"md\"" — two keys.
        Assert.Fail("Not implemented");
        await Task.CompletedTask;
    }

    [Test] public async Task Wire_Write_OmitsKindKey_WhenNull()
    {
        // var d = new data.@this("x", "hi") { Type = type("text") };
        // ToJson(d) contains "\"type\":\"text\"" but NOT "\"kind\"".
        Assert.Fail("Not implemented");
        await Task.CompletedTask;
    }

    [Test] public async Task Wire_Write_NoTypeColonKindCompositeString()
    {
        // Negative — "text:md" or "text/md" as the value of "type" never appears.
        // The two keys remain separate.
        Assert.Fail("Not implemented");
        await Task.CompletedTask;
    }

    [Test] public async Task Wire_RoundTrip_PreservesNameKindStrict()
    {
        // Round-trip: serialize a data with type("image","gif",strict:true), parse
        // back, the entity's Name/Kind/Strict are intact. (Strict on the wire: TBD
        // — pin presence of Strict in the round-trip; if Strict is build-only and
        // not serialised, flip this to assert it's gone — but one contract pinned.)
        Assert.Fail("Not implemented");
        await Task.CompletedTask;
    }

    [Test] public async Task Wire_Read_LegacyPrWithSeparateTypeAndKindFields_Deserializes()
    {
        // backward compat for .pr.json files written before the fold.
        // The legacy shape had "type":"text", "kind":"md" as two top-level fields on Data,
        // which is the SAME shape post-fold (the fold is internal). Parse one as written
        // by KindFieldTests today and assert the new Data exposes Type.Name=="text",
        // Type.Kind=="md", d.Kind=="md".
        Assert.Fail("Not implemented");
        await Task.CompletedTask;
    }

    [Test] public async Task Wire_Read_NoTypeNullStringEmitted()
    {
        // When Type is the Null sentinel, the wire omits the type key
        // (does not emit "\"type\":\"null\"").
        Assert.Fail("Not implemented");
        await Task.CompletedTask;
    }
}
