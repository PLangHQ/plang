using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;

namespace PLang.Tests.App.LazyDeserialize.ReaderRegistryTests;

// Stage 1 moves the type-owned branches of `AppTypes.TryConvert` onto each
// type's `Read`. The *generic plumbing* — nullable unwrap, the
// assignable-fast-path, list element-walk — stays as the registry's
// residual. These rows pin that the residual still works after the carve.
public class ResidualTryConvertTests
{
    private static global::app.@this NewApp()
        => new global::app.@this(System.IO.Path.Combine(System.IO.Path.GetTempPath(),
            "plang-residual-" + System.Guid.NewGuid().ToString("N")[..8]));

    [Test] public async Task TryConvert_NullableUnwrap_StillWorks()
    {
        await using var app = NewApp();
        var d = app.Type.Convert("5", typeof(int?), app.User.Context);
        await Assert.That(d.Value).IsEqualTo((object)5);
    }

    [Test] public async Task TryConvert_AssignableFastPath_StillWorks()
    {
        await using var app = NewApp();
        // int is assignable to object — returned as-is, no conversion.
        var d = app.Type.Convert(5, typeof(object), app.User.Context);
        await Assert.That(d.Value).IsEqualTo((object)5);
    }

    [Test] public async Task TryConvert_ListElementWalk_StillWorks()
    {
        await using var app = NewApp();
        var src = new System.Collections.Generic.List<object> { "1", "2", "3" };
        var d = app.Type.Convert(src, typeof(System.Collections.Generic.List<int>), app.User.Context);
        var list = d.Value as System.Collections.Generic.List<int>;
        await Assert.That(list).IsNotNull();
        await Assert.That(list!.Count).IsEqualTo(3);
        await Assert.That(list[0]).IsEqualTo(1);
        await Assert.That(list[2]).IsEqualTo(3);
    }
}
