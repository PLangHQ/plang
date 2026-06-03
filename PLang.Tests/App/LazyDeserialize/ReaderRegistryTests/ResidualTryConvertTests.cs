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
    [Test] public async Task TryConvert_NullableUnwrap_StillWorks() { throw new System.NotImplementedException("not implemented"); }
    [Test] public async Task TryConvert_AssignableFastPath_StillWorks() { throw new System.NotImplementedException("not implemented"); }
    [Test] public async Task TryConvert_ListElementWalk_StillWorks() { throw new System.NotImplementedException("not implemented"); }
}
