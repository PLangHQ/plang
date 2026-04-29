namespace PLang.Tests.Generator.Matrix.IsNotNull;

// Matrix entry for [IsNotNull] Data<T> properties — null Value rejected at validation time.
// v4 contract: per-class validation runs in generated ExecuteAsync after backing-field reset, before Run().
// A null Value on an [IsNotNull] property short-circuits with Data.FromError; Run() is not invoked.

public class IsNotNullPropTests
{
    // Parameter present with non-null Value → validation passes, Run() invoked.
    [Test] public async Task IsNotNullProp_NonNullValue_PassesValidation() => Assert.Fail("Not implemented");

    // Parameter present with null Value → validation fails with Data.FromError, Run() NOT invoked.
    [Test] public async Task IsNotNullProp_NullValue_RejectedWithError() => Assert.Fail("Not implemented");

    // Parameter missing entirely → also rejected (NotFound has null Value, equivalent to null).
    [Test] public async Task IsNotNullProp_Missing_RejectedWithError() => Assert.Fail("Not implemented");

    // Error returned identifies the property name that failed validation.
    [Test] public async Task IsNotNullProp_ErrorMessage_IdentifiesProperty() => Assert.Fail("Not implemented");
}
