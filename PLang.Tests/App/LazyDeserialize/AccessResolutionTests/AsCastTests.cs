using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;

namespace PLang.Tests.App.LazyDeserialize.AccessResolutionTests;

// `as <type>` reads toward that type. This is how a developer resolves a
// type-unknown value when navigation would otherwise error.
public class AsCastTests
{
    [Test] public async Task AsJson_OnTypeUnknownValue_ReadsTowardJson() { throw new System.NotImplementedException("not implemented"); }

    // Open question (test-designer open item 2): does `as <type>` on an
    // already-typed value no-op, or retype? Pinned here as no-op for the
    // already-correct case; the coder picks the contract for cross-type
    // recasts (e.g. `as bytes` on a text value) and the test flips.
    [Test] public async Task AsType_OnAlreadyTypedValue_NoOp_OrRetypes() { throw new System.NotImplementedException("not implemented"); }
}
