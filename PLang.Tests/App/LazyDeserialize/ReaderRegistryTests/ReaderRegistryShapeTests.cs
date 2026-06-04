using System.Reflection;
using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;

namespace PLang.Tests.App.LazyDeserialize.ReaderRegistryTests;

// The reader registry mirrors `app.type.renderer.@this` — same structural
// shape: a `(Type, Kind)` keyed `Of` lookup, a `Register` runtime seam, a
// `"*"` wildcard, and a precedence order of
// runtime-exact > generated-exact > runtime-wildcard > generated-wildcard.
// Shape-equivalence with the renderer is the primary contract here; per-type
// `Read` entries are pinned separately in PerTypeReadEntriesTests.
public class ReaderRegistryShapeTests
{
    private global::app.type.reader.@this _r = null!;

    [Before(Test)]
    public void Setup() => _r = new global::app.type.reader.@this();

    [Test] public async Task Reader_TypeExists_AtAppTypeReaderNamespace()
    {
        System.Type? t = typeof(global::app.@this).Assembly.GetType("app.type.reader.this");
        await Assert.That(t).IsNotNull();
    }

    [Test] public async Task Reader_HasOf_TakingTypeAndKind()
    {
        MethodInfo? of = typeof(global::app.type.reader.@this).GetMethod("Of");
        await Assert.That(of).IsNotNull();
        var ps = of!.GetParameters();
        await Assert.That(ps.Length).IsEqualTo(2);
        await Assert.That(ps[0].ParameterType).IsEqualTo(typeof(string));
        // kind is nullable string (string? erases to string at runtime).
        await Assert.That(ps[1].ParameterType).IsEqualTo(typeof(string));
    }

    [Test] public async Task Reader_HasRegister_TakingTypeKindAndDelegate()
    {
        MethodInfo? reg = typeof(global::app.type.reader.@this).GetMethod("Register");
        await Assert.That(reg).IsNotNull();
        var ps = reg!.GetParameters();
        await Assert.That(ps.Length).IsEqualTo(3);
        await Assert.That(ps[0].ParameterType).IsEqualTo(typeof(string));
        await Assert.That(ps[1].ParameterType).IsEqualTo(typeof(string));
        await Assert.That(ps[2].ParameterType).IsEqualTo(typeof(global::app.type.reader.@this.Read));
    }

    // Independent #1 — renderer exposes its wildcard via `AnyFormat`; the
    // reader mirrors with `AnyKind`. Both publish the one wildcard string "*".
    [Test] public async Task Reader_HasWildcardConstant_MatchingRendererAnyFormat()
    {
        await Assert.That(global::app.type.reader.@this.AnyKind).IsEqualTo("*");
        await Assert.That(global::app.type.reader.@this.AnyKind)
            .IsEqualTo(global::app.type.renderer.@this.AnyFormat);
    }

    // The Read delegate is the read-side mirror of `Write(object, IWriter)`:
    // takes raw + kind + a context, returns the materialised value (object).
    [Test] public async Task Reader_DelegateSignature_RawKindContext_ReturnsObject()
    {
        System.Type del = typeof(global::app.type.reader.@this.Read);
        MethodInfo invoke = del.GetMethod("Invoke")!;
        var ps = invoke.GetParameters();
        await Assert.That(ps.Length).IsEqualTo(3);
        await Assert.That(ps[0].ParameterType).IsEqualTo(typeof(object));
        await Assert.That(ps[1].ParameterType).IsEqualTo(typeof(string));
        await Assert.That(ps[2].ParameterType).IsEqualTo(typeof(global::app.type.reader.ReadContext));
        await Assert.That(invoke.ReturnType).IsEqualTo(typeof(object));
    }

    // Independent #1 — precedence: a runtime registration shadows a discovered
    // (generated) one for the same (type, kind). `path` ships a discovered
    // wildcard `Read`; a runtime entry for path wins over it. Mirror of
    // renderer line 52 (runtime tier consulted before generated tier).
    [Test] public async Task Reader_PrecedenceProbe_RuntimeExactBeatsGeneratedExact()
    {
        object sentinel = new();
        _r.Register("path", "json", (raw, kind, ctx) => sentinel);
        var read = _r.Of("path", "json");
        await Assert.That(read).IsNotNull();
        // The runtime entry wins over the discovered path Read (which would
        // resolve a path object, never our sentinel).
        await Assert.That(read!("ignored", "json", new global::app.type.reader.ReadContext(null)))
            .IsSameReferenceAs(sentinel);
    }

    // Independent #1 — an exact (type, kind) match beats a wildcard at the
    // same level. Mirror of renderer lines 52–55.
    [Test] public async Task Reader_PrecedenceProbe_ExactBeatsWildcard()
    {
        object exact = new();
        object wildcard = new();
        _r.Register("fixture", "json", (raw, kind, ctx) => exact);
        _r.Register("fixture", global::app.type.reader.@this.AnyKind, (raw, kind, ctx) => wildcard);
        var read = _r.Of("fixture", "json");
        await Assert.That(read!("x", "json", new global::app.type.reader.ReadContext(null)))
            .IsSameReferenceAs(exact);
        // A kind with no exact entry falls back to the wildcard.
        var other = _r.Of("fixture", "yaml");
        await Assert.That(other!("x", "yaml", new global::app.type.reader.ReadContext(null)))
            .IsSameReferenceAs(wildcard);
    }

    // Negative path — no entry registered, neither exact nor wildcard;
    // `Of` returns null and the dispatch caller surfaces a typed error
    // (the `TypeUnknown` path, exercised in ReadFailureTests).
    [Test] public async Task Reader_Of_ReturnsNull_WhenNoEntry()
    {
        await Assert.That(_r.Of("never-registered", "json")).IsNull();
    }

    // Independent #2 — discovery's static-Read scan mirrors the renderer's
    // static-Write scan (renderer/this.cs:100): a `serializer/Default.cs`
    // file's `static Read` is indexed without central wiring. path ships one.
    [Test] public async Task Reader_DiscoversStaticReadInSerializerDefault()
    {
        await Assert.That(_r.Of("path", "json")).IsNotNull();
        await Assert.That(_r.Of("path", "anything")).IsNotNull();
    }
}
