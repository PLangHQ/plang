namespace PLang.Tests.Generator.Matrix.WithDefault;

// Matrix entries for [Default(...)] Data<T> properties.
// v4 contract: missing parameter → Action.GetParameter falls back to Defaults list,
//   returning the [Default]-attribute-driven Data; As<T> wraps/converts as usual.

public class StringWithDefaultTests
{
    // Parameter missing → property reads as Data<string> with the [Default("hello")] value.
    [Test] public async Task StringWithDefault_Missing_UsesDefault() => Assert.Fail("Not implemented");

    // Parameter present → wins over default; default ignored.
    [Test] public async Task StringWithDefault_Present_OverridesDefault() => Assert.Fail("Not implemented");
}

public class IntWithDefaultTests
{
    // Numeric default 42 surfaces when parameter missing.
    [Test] public async Task IntWithDefault_Missing_Returns42() => Assert.Fail("Not implemented");

    // String parameter "7" → converts via TypeMapping; default not used.
    [Test] public async Task IntWithDefault_Present_OverridesAndConverts() => Assert.Fail("Not implemented");
}

public class EnumWithDefaultTests
{
    // [Default(MyEnum.A)] surfaces when parameter missing — typed enum, not boxed int.
    [Test] public async Task EnumWithDefault_Missing_ReturnsDefaultMember() => Assert.Fail("Not implemented");

    // String parameter "B" naming a member → converts via TypeMapping to MyEnum.B.
    [Test] public async Task EnumWithDefault_StringValue_ConvertsToMember() => Assert.Fail("Not implemented");
}

public class BoolWithDefaultTests
{
    // [Default(false)] surfaces when parameter missing.
    [Test] public async Task BoolWithDefault_Missing_ReturnsFalse() => Assert.Fail("Not implemented");

    // Parameter "true" overrides default; conversion via TypeMapping.
    [Test] public async Task BoolWithDefault_StringTrue_OverridesDefault() => Assert.Fail("Not implemented");
}
