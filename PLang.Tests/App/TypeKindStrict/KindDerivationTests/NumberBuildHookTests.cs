using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;

namespace PLang.Tests.App.TypeKindStrict.KindDerivationTests;

public class NumberBuildHookTests
{
    [Test] public async Task Build_IntegerLiteral_ReturnsInt()
        => await Assert.That(global::app.type.number.@this.Build("5")).IsEqualTo("int");

    [Test] public async Task Build_DecimalLiteral_ReturnsDecimal()
        => await Assert.That(global::app.type.number.@this.Build("3.14")).IsEqualTo("decimal");

    [Test] public async Task Build_ScientificLiteral_ReturnsDouble()
        => await Assert.That(global::app.type.number.@this.Build("1e5")).IsEqualTo("double");

    [Test] public async Task Build_VarReference_ReturnsNull()
        => await Assert.That(global::app.type.number.@this.Build("%var%")).IsNull();
}
