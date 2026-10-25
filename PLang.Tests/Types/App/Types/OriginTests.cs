namespace PLang.Tests.App.Types;

/// <summary>Every app is made from something, and its lists are a layer over what it is made from: an app made from
/// the code holds what it loads in its own layer, as a sub app does, and may still change the code under it (the code
/// was made for it); a sub app may not change its maker's.</summary>
public class OriginTests
{
    /// <summary>A kind a test adds to a type: born holding the type it is a kind of.</summary>
    private sealed class ProbeKind(global::app.type.@this owner) : global::app.type.kind.@this("webpo", owner);

    // a class to register as an action — the registry reads its attribute and interfaces only
    private sealed class Probe;

    [Test]
    public async Task AnAppMadeFromTheCode_HoldsWhatItLoads_InItsOwnLayer()
    {
        await using var app = new global::app.@this("/root").Testing();
        await Assert.That(app.type.list.HasOwn).IsFalse().Because("an app is born with an empty layer over its code");

        var added = app.type.list.Add(typeof(SharedTypeListTests.SharedProbe), app.actor.list.System.Context);

        await added.IsSuccess();
        await Assert.That(app.type.list.HasOwn).IsTrue();
    }

    [Test]
    public async Task AnAppMadeFromTheCode_AddsAKindToItsCodesType()
    {
        await using var app = new global::app.@this("/root").Testing();
        var image = app.type.list["image"];

        app.type.list.Add([new ProbeKind(image)]);

        await Assert.That(image.kind["webpo"]).IsNotNull().Because("the code under an app is its own to change");
    }

    [Test]
    public async Task AnAppMadeFromTheCode_AddsAnActionToABuiltInModule()
    {
        await using var app = new global::app.@this("/root").Testing();

        app.module.Register("file", "probe", typeof(Probe));

        await Assert.That(app.Module("file")["probe"]).IsNotNull();
    }

    [Test]
    public async Task TwoAppsMadeFromTheCode_ShareNothingTheyLoad()
    {
        await using var one = new global::app.@this("/one").Testing();
        await using var two = new global::app.@this("/two").Testing();

        one.type.list.Add([new ProbeKind(one.type.list["image"])]);

        await Assert.That(two.type.list["image"]).IsNotSameReferenceAs(one.type.list["image"]).Because("each app's code is its own");
        await Assert.That(two.type.list["image"].kind["webpo"]).IsNull();
    }
}
