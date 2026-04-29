namespace PLang.Tests.Generator.Matrix.Nullable;

// Matrix entries for nullable Data<T>? properties — parameter may be missing.
// v4 contract: missing parameter → Action.GetParameter returns Data.NotFound;
//   As<T> on NotFound short-circuits to a Data<T>? with null Value (no error).

public class StringNullableTests
{
    // Parameter not in Parameters list → property reads as Data<string>? with null Value (NotFound semantics).
    [Test] public async Task StringNullable_Missing_ReadsAsNullData() => Assert.Fail("Not implemented");

    // Parameter present with literal → typed Data<string>? with the value.
    [Test] public async Task StringNullable_Present_ResolvesToValue() => Assert.Fail("Not implemented");

    // Parameter present but Value is explicit null → typed Data<string>? with null Value, IsInitialized=true.
    [Test] public async Task StringNullable_PresentWithNullValue_ReadsAsNullInitialized() => Assert.Fail("Not implemented");
}

public class IntNullableTests
{
    // Missing parameter → null Data; no exception, no validation failure.
    [Test] public async Task IntNullable_Missing_ReadsAsNull() => Assert.Fail("Not implemented");

    // Present integer parameter → typed Data<int>?.
    [Test] public async Task IntNullable_Present_ResolvesToInt() => Assert.Fail("Not implemented");
}
