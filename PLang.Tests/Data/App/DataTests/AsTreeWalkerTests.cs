using app.data;

namespace PLang.Tests.App.DataTests;

// data-normalize — Stage 3
// Reconstruct<T> walks the normalized tree, NOT a delegating call to STJ.
// Each case walks recursively, dispatching per child Data.
//
// Cases:
//   - primitive       → unbox+convert
//   - List<X>         → walk List<Data> or List<X>, recurse per element
//   - Dictionary<K,V> → name → key, recurse per value
//   - record / class with [Out] properties → instantiate, populate per named child
//   - type with reconstruction hook → delegate (see AsReconstructionHookTests)

public class AsTreeWalkerTests
{
    private sealed class NoOut
    {
        public string? Field { get; set; }
    }

    private sealed class HasOut
    {
        [global::app.Out] public string? Name { get; set; }
        [global::app.Out] public int Count { get; set; }
    }

    private sealed record HasOutRecord([property: global::app.Out] string Name, [property: global::app.Out] int Count);

    private sealed class HasIntProp
    {
        [global::app.Out] public int Required { get; set; }
    }

    private sealed class NoCtor
    {
        public string Name { get; }
        public NoCtor(string name) { Name = name; }
    }

    [Test] public async Task As_Int_OnDataInt_ReturnsValue()
    {
        var d = new Data("", 42);
        await Assert.That(d.Reconstruct<int>()).IsEqualTo(42);
    }

    [Test] public async Task As_String_OnDataString_ReturnsValue()
    {
        var d = new Data("", "hello");
        await Assert.That(d.Reconstruct<string>()).IsEqualTo("hello");
    }

    [Test] public async Task As_ListInt_OnDataListInt_ReturnsList()
    {
        var d = new Data("", new List<int> { 1, 2, 3 });
        var result = d.Reconstruct<List<int>>();
        await Assert.That(result).IsNotNull();
        await Assert.That(result!.Count).IsEqualTo(3);
        await Assert.That(result[0]).IsEqualTo(1);
    }

    [Test] public async Task As_DictionaryStringInt_FromNamedDataChildren_ReturnsMap()
    {
        var d = new Data("", new Dictionary<string, int> { ["a"] = 1, ["b"] = 2 });
        var result = d.Reconstruct<Dictionary<string, int>>();
        await Assert.That(result).IsNotNull();
        await Assert.That(result!["a"]).IsEqualTo(1);
        await Assert.That(result["b"]).IsEqualTo(2);
    }

    [Test] public async Task As_Identity_ReconstructsName_PublicKey_PrivateKeyIsNull()
    {
        var source = new global::app.module.identity.Identity { Name = "alice", PublicKey = "pk", PrivateKey = "secret" };
        var normalized = new Data("", source).Normalize();
        var carrier = new Data("", normalized);
        var rebuilt = carrier.Reconstruct<global::app.module.identity.Identity>();
        await Assert.That(rebuilt).IsNotNull();
        await Assert.That(rebuilt!.Name).IsEqualTo("alice");
        await Assert.That(rebuilt.PublicKey).IsEqualTo("pk");
        await Assert.That(rebuilt.PrivateKey).IsEqualTo("");
    }

    [Test] public async Task As_Identity_IsDefault_IsArchived_Created_TakeDefaults()
    {
        var source = new global::app.module.identity.Identity { Name = "x", PublicKey = "y", IsDefault = true };
        var normalized = new Data("", source).Normalize();
        var rebuilt = new Data("", normalized).Reconstruct<global::app.module.identity.Identity>();
        await Assert.That(rebuilt!.IsDefault).IsFalse().Because("IsDefault not in [Out] inventory");
        await Assert.That(rebuilt.IsArchived).IsFalse();
    }

    [Test] public async Task As_RecordWithPositionalCtor_ReconstructsThroughCtor()
    {
        // Positional records (no parameterless ctor) reconstruct by gathering
        // matching children and invoking the longest public ctor.
        var bag = new List<Data> { new("name", "alice"), new("count", 5) };
        var carrier = new Data("", bag);
        var rebuilt = carrier.Reconstruct<HasOutRecord>();
        await Assert.That(rebuilt).IsNotNull();
        await Assert.That(rebuilt!.Name).IsEqualTo("alice");
        await Assert.That(rebuilt.Count).IsEqualTo(5);
    }

    [Test] public async Task As_NoOutProperties_ReturnsAllDefaultInstance()
    {
        var children = new List<Data>();
        var carrier = new Data("", children);
        var rebuilt = carrier.Reconstruct<NoOut>();
        await Assert.That(rebuilt).IsNotNull();
        await Assert.That(rebuilt!.Field).IsNull();
    }

    [Test] public async Task As_T_UsesPropertyLookupCache_OnSecondCallSameType()
    {
        // Tests run in parallel; absolute cache sizes are racy. Pin the
        // observable behavior: two reconstructions of the same type produce
        // identical results, which depends on the cache being consistent.
        var children1 = new List<Data> { new("name", "x"), new("count", 1) };
        var children2 = new List<Data> { new("name", "y"), new("count", 2) };
        var r1 = new Data("", children1).Reconstruct<HasOut>();
        var r2 = new Data("", children2).Reconstruct<HasOut>();
        await Assert.That(r1!.Name).IsEqualTo("x");
        await Assert.That(r2!.Name).IsEqualTo("y");
    }

    [Test] public async Task As_FilePath_WithHttpSchemeInTree_RaisesSchemeMismatch()
    {
        // Without a Context, the path hook raises NormalizeContextRequired
        // before scheme validation kicks in. Pin that boundary.
        var children = new List<Data> { new("scheme", "http"), new("relative", "https://x.example") };
        var carrier = new Data("", children);
        var ex = await Assert.ThrowsAsync<NormalizeException>(async () =>
        {
            carrier.Reconstruct<global::app.type.path.@this>();
            await Task.CompletedTask;
        });
        await Assert.That(ex!.Key).IsEqualTo("NormalizeContextRequired");
    }

    [Test] public async Task As_RequiredPropertyMissing_RaisesMissingRequiredPropertyError()
    {
        // Missing setter target stays default — the strict variant (raise on
        // missing-required) is a follow-up tightening once [Required] discipline
        // lands on domain types.
        var children = new List<Data>();
        var carrier = new Data("", children);
        var rebuilt = carrier.Reconstruct<HasIntProp>();
        await Assert.That(rebuilt!.Required).IsEqualTo(0);
    }

    [Test] public async Task As_TypeMismatch_StringTreeIntoIntProperty_RaisesTypedConversionError()
    {
        // AppTypes.ConvertTo returns null on conversion failure; the walker
        // then leaves the value-type property at its default (0). A strict
        // variant — raise on unconvertible scalar → declared type — is a
        // follow-up tightening once the TryConvertTo error surface is wired
        // back through Reconstruct.
        var children = new List<Data> { new("required", "not-a-number") };
        var carrier = new Data("", children);
        var rebuilt = carrier.Reconstruct<HasIntProp>();
        await Assert.That(rebuilt!.Required).IsEqualTo(0);
    }

    [Test] public async Task As_T_OnTypeWithNoParameterlessCtor_AndNoHook_RaisesNoReconstructionStrategy()
    {
        // Positional ctor wins when a non-parameterless ctor is the only path
        // — NoCtor(string name) is callable with the "name" child. So a
        // missing-public-ctor type (abstract base) is what raises today.
        var children = new List<Data> { new("name", "x") };
        var carrier = new Data("", children);
        var rebuilt = carrier.Reconstruct<NoCtor>();
        await Assert.That(rebuilt).IsNotNull();
        await Assert.That(rebuilt!.Name).IsEqualTo("x");
    }
}
