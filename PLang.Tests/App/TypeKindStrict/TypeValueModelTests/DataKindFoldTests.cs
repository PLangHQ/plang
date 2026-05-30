using TUnit.Core;
using TUnit.Assertions;

namespace PLang.Tests.App.TypeKindStrict.TypeValueModelTests;

// The `Data.Kind` fold. plang-types shipped a stored `data.Kind` field
// alongside `Data.Type`. folds it into `Type.Kind` (one home) — there must
// not be a parallel stored field for the same logical thing (OBP smell #3).

public class DataKindFoldTests
{
    [Test] public async Task Data_HasNoStoredKindField()
    {
        // Reflection probe: no private/internal backing field for Kind on data.@this
        // (other than the Type reference itself). The previous stored field is gone.
        Assert.Fail("Not implemented");
        await Task.CompletedTask;
    }

    [Test] public async Task Data_KindGetter_ReadsTypeKind()
    {
        // var d = new data.@this("x", "y") { Type = type("text","md") };
        // d.Kind == "md" (sourced from Type.Kind, not stored separately).
        Assert.Fail("Not implemented");
        await Task.CompletedTask;
    }

    [Test] public async Task Data_KindSetter_WritesThroughToTypeKind()
    {
        // the contract pins "writes through to Type.Kind" as the
        // contract because it's least surprising for callers used to the current
        // mutable field. If coder picks "throws on direct set", flip the test
        // and add Data_KindSetter_Throws — but ONE behaviour must be pinned, not
        // a silent no-op.
        //
        // var d = new data.@this("x", "y") { Type = type("text") };
        // d.Kind = "md";
        // d.Type.Kind == "md".
        Assert.Fail("Not implemented");
        await Task.CompletedTask;
    }
}
