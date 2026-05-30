using TUnit.Core;
using TUnit.Assertions;

namespace PLang.Tests.App.TypeKindStrict.TypeValueModelTests;

// `App.Type.Kinds` (the build-hook dispatcher, app.type.kind.@this)
// renames to `App.Type.KindHooks`. The rename exists to stop "kind" colliding
// with the entity's `Kind` (subtype) and `Kinds` (advertised vocabulary).
// The rename does NOT change `Of(clrType, value)` semantics.

public class DispatcherRenameTests
{
    [Test] public async Task AppType_HasKindHooks_NotKinds()
    {
        // Reflection probe: app.type.list.@this has a KindHooks property.
        // The old `Kinds` property is gone (or has become an alias forwarding
        // to KindHooks — coder picks; pin presence of KindHooks either way).
        Assert.Fail("Not implemented");
        await Task.CompletedTask;
    }

    [Test] public async Task KindHooks_Of_StillReturnsStringOrNull()
    {
        // The renamed dispatcher's Of(clrType, value) still:
        //   - returns "int" for typeof(int) + 5
        //   - returns null for typeof(int) + "%var%"
        //   - returns null for a CLR type with no Build hook
        // Smoke that the signature/semantics survived the rename.
        Assert.Fail("Not implemented");
        await Task.CompletedTask;
    }
}
