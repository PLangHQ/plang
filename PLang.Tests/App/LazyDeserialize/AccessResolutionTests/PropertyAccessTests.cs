using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;

namespace PLang.Tests.App.LazyDeserialize.AccessResolutionTests;

// Property access (`%x!prop%`) reads from Data.Properties; the value is
// never touched. Status checks on an http response, for example, must
// not materialise the body.
public class PropertyAccessTests
{
    [Test] public async Task PropertyRead_ReadsFromProperties_NotValue() { throw new System.NotImplementedException("not implemented"); }
    [Test] public async Task PropertyRead_NeverMaterialisesValue() { throw new System.NotImplementedException("not implemented"); }
}
