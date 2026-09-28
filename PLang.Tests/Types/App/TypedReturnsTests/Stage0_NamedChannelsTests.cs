namespace PLang.Tests.App.TypedReturnsTests;

// Contract: Get(name) answers the registered channel by name, or null — a miss on a user-named channel is the
// caller's result to make; nothing writes into a sink that pretends. BuildWarning is the payload written to the
// "builder" channel during a build pass.
//
// Channel REGISTRATION for "builder" is owned PLang-side (system/builder/Build.goal). C# only provides the Get
// lookup and the BuildWarning shape; the lifecycle tests here assert the C# primitive only.

public class Stage0_NamedChannelsTests
{
    private global::app.@this _app = null!;

    [Before(Test)]
    public void Setup() => _app = TestApp.Create("/app");

    [After(Test)]
    public async Task TearDown() { await _app.DisposeAsync(); }

    private global::app.channel.list.@this Channels => _app.actor.list.User.Channel;

    [Test]
    public async Task Channels_LookupByName_ReturnsRegisteredChannel()
    {
        var registered = Channels.CreateMemoryChannel("builder");
        await Assert.That(ReferenceEquals(Channels.Get("builder"), registered)).IsTrue();
    }

    // No registration → no channel: the miss is the caller's to answer.
    [Test]
    public async Task Channels_LookupByName_NonexistentIsNull()
        => await Assert.That(Channels.Get("nonexistent")).IsNull();

    // After build end, the channel is removed and the lookup finds none.
    [Test]
    public async Task Builder_BuildEnd_DisposesBuilderChannel()
    {
        Channels.CreateMemoryChannel("builder");
        var removed = await Channels.RemoveAsync("builder");
        await Assert.That(removed).IsTrue();
        await Assert.That(Channels.Get("builder")).IsNull();
    }

    // A build warning rides as a native dict {action, message}; structural dict
    // equality lets consumers de-dup warnings without manual equality plumbing.
    [Test]
    public async Task BuildWarning_DictShape_CarriesActionAndMessage()
    {
        var w1 = Warning("file.read", "duplicate");
        var w2 = Warning("file.read", "duplicate");

        var raw = Lower<Dictionary<string, object?>>(w1)!;
        await Assert.That((string)raw["action"]!).IsEqualTo("file.read");
        await Assert.That((string)raw["message"]!).IsEqualTo("duplicate");
        await Assert.That(await w1.AreEqual(w2, global::PLang.Tests.TestApp.SharedContext)).IsTrue()
            .Because("Structural dict equality lets consumers de-dup identical warnings.");
    }

    // Writing a warning dict to a registered "builder" channel succeeds.
    [Test]
    public async Task BuildWarning_WriteToBuilderChannel_Succeeds()
    {
        Channels.CreateMemoryChannel("builder");
        var writeResult = await Channels["builder"].WriteAsync(_app.actor.list.User.Context.Ok(Warning("file.read", "missing file")));
        await writeResult.IsSuccess();
    }

    // Outside a build, "builder" is not registered: there is nothing to write to.
    [Test]
    public async Task BuildWarning_OutsideBuild_ThereIsNoBuilderChannel()
        => await Assert.That(Channels.Get("builder")).IsNull();

    // The build-warning payload shape: a native dict {action, message}, mirroring
    // what file.read writes to the "builder" channel.
    private global::app.type.item.dict.@this Warning(string action, string message)
        => new global::app.type.item.dict.@this().Set("action", action).Set("message", message);

    // Two distinct channel names resolve to two distinct channel instances.
    [Test]
    public async Task BuildTimeAndRuntime_AreSeparateChannelNames()
    {
        var builder = Channels.CreateMemoryChannel("builder");
        var runtime = Channels.CreateMemoryChannel("warnings");

        await Assert.That(ReferenceEquals(builder, runtime)).IsFalse();
        await Assert.That(ReferenceEquals(Channels.Get("builder"), Channels.Get("warnings"))).IsFalse();
    }
}
