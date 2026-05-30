using TUnit.Core;
using TUnit.Assertions;

namespace PLang.Tests.App.TypeKindStrict.TypeValueModelTests;

// The new `IKindValidatable` marker. Sibling to IBooleanResolvable in
// app/data/. The marker is the seam strict uses in ValidateBuild — the
// design lives here even before image implements it.

public class IKindValidatableMarkerTests
{
    [Test] public async Task Marker_Defined_InAppDataNamespace()
    {
        // Reflection: typeof(app.data.IKindValidatable) exists and is an interface.
        Assert.Fail("Not implemented");
        await Task.CompletedTask;
    }

    [Test] public async Task Marker_Signature_BoolAndActualKindTuple()
    {
        // (bool ok, string? actualKind) ValidateKind(object value, string requiredKind).
        // Pin the signature so can rely on it.
        Assert.Fail("Not implemented");
        await Task.CompletedTask;
    }

    [Test] public async Task Image_ImplementsIKindValidatable()
    {
        // app.type.image.@this : IKindValidatable. The byte-sniff lives here, not
        // in variable.set or a switch in build.validate.
        Assert.Fail("Not implemented");
        await Task.CompletedTask;
    }

    [Test] public async Task Text_DoesNotImplementIKindValidatable()
    {
        // there is no "plain vs markdown" probe from content. text strict
        // degrades to "kind name accepted", never raises a content-mismatch error.
        // Negative: reflection probe confirms text does NOT implement IKindValidatable.
        Assert.Fail("Not implemented");
        await Task.CompletedTask;
    }

    [Test] public async Task Number_DoesNotImplementIKindValidatable()
    {
        // the strict path calls ValidateKind on the resolved
        // CLR type. number is not byte-sniffable; the strict path must skip cleanly
        // (not throw "not implemented"). Negative-presence pin.
        Assert.Fail("Not implemented");
        await Task.CompletedTask;
    }
}
