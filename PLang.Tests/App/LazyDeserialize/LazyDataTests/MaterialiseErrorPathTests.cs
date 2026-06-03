using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;

namespace PLang.Tests.App.LazyDeserialize.LazyDataTests;

// Behaviour change to watch (from the architect's plan):
// "Parse errors move from read-time to touch-time." A malformed json file
// no longer errors at `read` — it errors at first touch of the value (via
// navigation or `As<T>`). This is the point of laziness; the touch-time
// error must name the source so a developer can debug.
public class MaterialiseErrorPathTests
{
    // Stage 3 negative — the error fires at first `.Value`, not when the
    // file/channel was originally read.
    [Test] public async Task MalformedJson_ErrorsAtFirstTouch_NotAtRead() { throw new System.NotImplementedException("not implemented"); }

    // Independent #17 — the error names the source. The error.Key or
    // error.Message contains the variable identifier or path so the
    // failure is actionable, not a generic STJ "unexpected token" message.
    [Test] public async Task MalformedJson_ErrorNamesTheSource() { throw new System.NotImplementedException("not implemented"); }

    // The courier-rule pin: a materialise failure surfaces as a Data.Error
    // on the Data being read, not as a thrown exception out of the courier
    // currently holding the Data. OBP rule #9.
    [Test] public async Task Materialise_Failure_SurfacedAs_DataError_NotThrown_ToCourier() { throw new System.NotImplementedException("not implemented"); }
}
