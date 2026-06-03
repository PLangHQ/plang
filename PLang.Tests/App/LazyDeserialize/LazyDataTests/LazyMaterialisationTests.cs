using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;

namespace PLang.Tests.App.LazyDeserialize.LazyDataTests;

// Decision 2 — materialisation fires only when `_value` is null and
// `_raw` is set. Inline-authored values (`set %x% = 5`) populate `_value`
// and leave `_raw` null; they never hit the byte path. There is no mode
// flag — which field is set tells you the origin.
public class LazyMaterialisationTests
{
    [Test] public async Task Value_MaterialisesViaReader_WhenValueNull_AndRawSet() { throw new System.NotImplementedException("not implemented"); }

    // Authored values short-circuit the reader. Pinned positively here;
    // negatively (zero reader invocations) in `Value_AuthoredPath_…` below.
    [Test] public async Task Value_ReturnsValueDirectly_WhenValueSet_AndRawNull() { throw new System.NotImplementedException("not implemented"); }

    // Independent #6 — probe-counted negative: the authored-value `.Value`
    // path increments the reader's dispatch counter by 0. Catches the bug
    // where the lazy path runs unconditionally and silently re-types the
    // authored value.
    [Test] public async Task Value_AuthoredPath_NeverInvokesReader() { throw new System.NotImplementedException("not implemented"); }

    [Test] public async Task Value_RawSurvivesMaterialisation() { throw new System.NotImplementedException("not implemented"); }

    // Independent #5 — the survival-through-courier row. After `.Value`
    // is read, a Wire.Write of the same Data still emits the raw verbatim.
    // (Otherwise a second read on the receiving side would re-materialise
    // from the renderer's output, not the original bytes.)
    [Test] public async Task RawBackedSerialize_AfterValueWasRead_StillEmitsRawVerbatim() { throw new System.NotImplementedException("not implemented"); }

    // app/data/this.cs:199 — ConvertValue gets folded into the materialise
    // path. The named method is gone; navigation reads `.Value` which
    // materialises on demand.
    [Test] public async Task ConvertValue_IsRemoved() { throw new System.NotImplementedException("not implemented"); }

    [Test] public async Task Navigation_ReadsValueWhichMaterialises() { throw new System.NotImplementedException("not implemented"); }

    // Unchanged contract — `%var%` substitution inside an authored value
    // resolves fresh on every read (app/data/this.cs:152). Stage 3 must
    // not break this; it concerns only `_raw`-backed Data.
    [Test] public async Task VarReference_InAuthoredValue_StillResolvesFreshPerRead() { throw new System.NotImplementedException("not implemented"); }
}
