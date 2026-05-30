using TUnit.Core;
using TUnit.Assertions;

namespace PLang.Tests.App.TypeKindStrict.TypeValueModelTests;

// The normalising `type(name, kind?, strict?)` factory — the single entry point
// the LLM, build pipeline, and tests reach to construct a type value.
// Slash-tolerance lives here too: a single "text/markdown" must split to
// {name:text, kind:markdown} so the LLM's occasional slash-emission doesn't
// escape into the wire.

public class TypeFactoryTests
{
    [Test] public async Task Factory_NameKindStrict_CarriesAllThree()
    {
        // type("image", "gif", strict:true) → {Name:"image", Kind:"gif", Strict:true}
        Assert.Fail("Not implemented");
        await Task.CompletedTask;
    }

    [Test] public async Task Factory_String_CanonicalisesNameToText()
    {
        // type("string") → Name == "text". Aliases still accept "string" input;
        // canonical render is "text". Wires into primitive.Canonical change.
        Assert.Fail("Not implemented");
        await Task.CompletedTask;
    }

    [Test] public async Task Factory_SingleStringWithSlash_SplitsToNameAndKind()
    {
        // type("text/markdown") → {Name:"text", Kind:"markdown"} (kind canonicalised
        // to "md" — pinned in KindCanonicalisationTests, not here).
        Assert.Fail("Not implemented");
        await Task.CompletedTask;
    }

    [Test] public async Task Factory_SingleStringNoSlash_KindIsNull()
    {
        // type("text") → {Name:"text", Kind:null}
        Assert.Fail("Not implemented");
        await Task.CompletedTask;
    }

    [Test] public async Task Factory_MultiSlash_SplitsOnFirst()
    {
        // type("a/b/c") → {Name:"a", Kind:"b/c"}. A multi-slash type string is
        // deliberately NOT an error; first slash splits, rest is a free-string
        // kind that canonicalises or passes through.
        Assert.Fail("Not implemented");
        await Task.CompletedTask;
    }

    [Test] public async Task Factory_StrictDefaultsFalse()
    {
        // type("text") and type("text", "md") both produce Strict == false.
        Assert.Fail("Not implemented");
        await Task.CompletedTask;
    }

    [Test] public async Task Factory_CaseInsensitiveName()
    {
        // primitive.Aliases is OrdinalIgnoreCase; the factory
        // must preserve that on Name. type("Text") and type("TEXT") both → Name:"text".
        Assert.Fail("Not implemented");
        await Task.CompletedTask;
    }

    [Test] public async Task Factory_EmptyName_Rejected()
    {
        // empty/whitespace Name is not a valid type. The factory
        // throws (or returns a sentinel; the contract pins "throws" as the
        // contract — coder, flip if there's a strong reason for soft handling).
        Assert.Fail("Not implemented");
        await Task.CompletedTask;
    }

    [Test] public async Task Factory_NullSentinel_NameKindStrictPreserved()
    {
        // type.@this.Null after the rename has Name == "null",
        // Kind == null, Strict == false. Easy-to-break during the Value→Name rename.
        Assert.Fail("Not implemented");
        await Task.CompletedTask;
    }

    [Test] public async Task Factory_StrictTrueOnTextFamily_NoThrowAtConstruction()
    {
        // strict on a family without IKindValidatable is fine
        // at construction; the validation path degrades to "kind-name-accepted"
        // (see ValidateBuild_StrictTextMdWithLiteral_ReturnsNull).
        Assert.Fail("Not implemented");
        await Task.CompletedTask;
    }
}
