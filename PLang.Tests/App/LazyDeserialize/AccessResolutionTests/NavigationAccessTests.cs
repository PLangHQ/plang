using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;

namespace PLang.Tests.App.LazyDeserialize.AccessResolutionTests;

// Navigation (`%x.field%`) materialises through the known type's reader;
// `kind` says how. If the type is unknown there is **no guessing** — the
// caller gets a clear error and is told to add `as <type>`.
public class NavigationAccessTests
{
    [Test] public async Task Navigation_KnownType_MaterialisesViaReader_AndNavigates() { throw new System.NotImplementedException("not implemented"); }

    // Architect 829785fbe — the type's *shape* decides the navigation
    // model. `object` navigates by key (`%cfg.port%`); `table`
    // navigates by row/column. The exact `table` surface is the
    // coder's call (open item).
    [Test] public async Task Navigation_ObjectShape_NavigatesByKey() { throw new System.NotImplementedException("not implemented"); }
    [Test] public async Task Navigation_TableShape_NavigatesByRowColumn() { throw new System.NotImplementedException("not implemented"); }

    // The contract error — "value has no type; add `as <type>`."
    [Test] public async Task Navigation_TypeUnknown_ProducesAddAsTypeError() { throw new System.NotImplementedException("not implemented"); }

    // Independent #18 — the exact phrasing is the LLM teaching surface.
    // Pin the literal substring so it's the contract, not styling. If the
    // coder picks different wording, flip the substring here.
    [Test] public async Task Navigation_TypeUnknownErrorMessage_ContainsLiteralAsType() { throw new System.NotImplementedException("not implemented"); }

    // An authored dict value is already structured — navigation walks it
    // directly without invoking the reader.
    [Test] public async Task Navigation_OnAuthoredDictValue_DoesNotTriggerReader() { throw new System.NotImplementedException("not implemented"); }
}
