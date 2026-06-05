namespace PLang.Tests.App.ScalarsAsNative;

// bool.@this — new wrapper, the truthiness primitive. Wraps a raw bool; this is
// where the IBooleanResolvable turtles stop. Equality + bare serialize; ordering
// optional (false<true) per coder's documented call. OwnedClrTypes = bool.
public class BoolWrapperTests
{
    [Test]
    public async Task Bool_WrapsRawBool_AsBooleanAsyncBottomsOutAtValue()
    {
        // bool.@this.AsBooleanAsync returns the raw bool it wraps — every other
        // type's truthiness may delegate; bool's is the value.
        await Task.CompletedTask;
        Assert.Fail("Not implemented");
    }

    [Test]
    public async Task Bool_Equality_TrueEqualsTrueAndHashEqual()
    {
        // bool.@this(true) and bool.@this(true) Equal AND hash-equal.
        await Task.CompletedTask;
        Assert.Fail("Not implemented");
    }

    [Test]
    public async Task Bool_BareSerialize_TrueFalseOnApplicationJson()
    {
        // Normalize emits bare `true`/`false`, not enveloped.
        await Task.CompletedTask;
        Assert.Fail("Not implemented");
    }

    [Test]
    public async Task Bool_OwnsClrBool_RegisteredInOwnedClrTypes()
    {
        // primitive map declares bool owns CLR bool; UnwrapJsonElement True/False
        // arms emit bool.@this.
        await Task.CompletedTask;
        Assert.Fail("Not implemented");
    }

    [Test]
    public async Task Bool_NotOrderable_OrderThrows()
    {
        // bool.@this does NOT implement IOrderableValue; Compare.Order(bool, bool)
        // throws NotOrderableException — matches collections-are-data's settled
        // equality-only-bool policy.
        await Task.CompletedTask;
        Assert.Fail("Not implemented");
    }
}
