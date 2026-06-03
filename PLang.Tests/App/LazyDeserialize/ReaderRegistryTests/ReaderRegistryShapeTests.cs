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
    [Test] public async Task Reader_TypeExists_AtAppTypeReaderNamespace()
    {
        throw new System.NotImplementedException("not implemented");
    }

    [Test] public async Task Reader_HasOf_TakingTypeAndKind()
    {
        throw new System.NotImplementedException("not implemented");
    }

    [Test] public async Task Reader_HasRegister_TakingTypeKindAndDelegate()
    {
        throw new System.NotImplementedException("not implemented");
    }

    // Independent #1 — renderer exposes its wildcard via `AnyFormat`; the
    // reader should mirror with an equivalent constant. Name flex; the
    // contract is "there is one published wildcard string".
    [Test] public async Task Reader_HasWildcardConstant_MatchingRendererAnyFormat()
    {
        throw new System.NotImplementedException("not implemented");
    }

    // The Read delegate is the read-side mirror of `Write(object, IWriter)`:
    // takes raw + kind + a context, returns the materialised value (object).
    [Test] public async Task Reader_DelegateSignature_RawKindContext_ReturnsObject()
    {
        throw new System.NotImplementedException("not implemented");
    }

    // Independent #1 — precedence: a runtime registration shadows a generated
    // one for the same (type, kind). Mirror of renderer line 52.
    [Test] public async Task Reader_PrecedenceProbe_RuntimeExactBeatsGeneratedExact()
    {
        throw new System.NotImplementedException("not implemented");
    }

    // Independent #1 — an exact (type, kind) match beats a wildcard at the
    // same level. Mirror of renderer lines 52–55.
    [Test] public async Task Reader_PrecedenceProbe_ExactBeatsWildcard()
    {
        throw new System.NotImplementedException("not implemented");
    }

    // Negative path — no entry registered, neither exact nor wildcard;
    // `Of` returns null and the dispatch caller surfaces a typed error
    // (the `TypeUnknown` path, exercised in ReadFailureTests).
    [Test] public async Task Reader_Of_ReturnsNull_WhenNoEntry()
    {
        throw new System.NotImplementedException("not implemented");
    }

    // Independent #2 — discovery's static-Read scan mirrors the renderer's
    // static-Write scan (renderer/this.cs:100): a `serializer/Default.cs`
    // file's `static Read` is indexed without central wiring.
    [Test] public async Task Reader_DiscoversStaticReadInSerializerDefault()
    {
        throw new System.NotImplementedException("not implemented");
    }
}
