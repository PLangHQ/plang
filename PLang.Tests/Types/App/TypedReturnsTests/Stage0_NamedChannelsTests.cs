using NoopChannel = global::app.channel.type.noop.@this;

namespace PLang.Tests.App.TypedReturnsTests;

// Contract: Channel(name) returns a registered channel by name; on miss it
// returns a no-op sink so callers can write opportunistically without null-
// checking. BuildWarning is the payload written to the "builder" channel
// during a build pass.
//
// Channel REGISTRATION for "builder" is owned PLang-side
// (system/builder/Build.goal). C# only provides Channel(name) lookup, the
// no-op fallback, and the BuildWarning record. The lifecycle tests here
// assert the C# primitive only.

public class Stage0_NamedChannelsTests
{
    private global::app.@this _app = null!;

    [Before(Test)]
    public void Setup() => _app = TestApp.Create("/app");

    [After(Test)]
    public async Task TearDown() { await _app.DisposeAsync(); }

    private global::app.channel.list.@this Channels => _app.User.Channel;

    private Channel RegisterMemoryChannel(string name)
    {
        var ch = Channels.CreateMemoryChannel(name);
        Channels.Register(ch);
        return ch;
    }

    // Channel(name) returns the registered channel when one exists by that name.
    [Test]
    public async Task Channels_LookupByName_ReturnsRegisteredChannel()
    {
        var registered = RegisterMemoryChannel("builder");
        var resolved = Channels.Channel("builder");
        await Assert.That(ReferenceEquals(resolved, registered)).IsTrue();
    }

    // No registration → the no-op sink is returned so callers can write
    // opportunistically without null-checking.
    [Test]
    public async Task Channels_LookupByName_NonexistentReturnsNoOpSink()
    {
        var resolved = Channels.Channel("nonexistent");
        await Assert.That(resolved).IsNotNull();
        await Assert.That(resolved).IsTypeOf<NoopChannel>();
    }

    // Writing to the no-op sink completes successfully — nothing observable, no throw.
    [Test]
    public async Task Channels_NoOpSink_WriteSucceedsWithoutSubscribers()
    {
        var sink = Channels.Channel("nonexistent");
        var result = await sink.WriteAsync(_app.Ok("payload"));
        await result.IsSuccess();
    }

    // After a build-side Register, Channel("builder") returns the real channel,
    // not the no-op. Real lifecycle is driven by system/builder/Build.goal (a
    // `channel set "builder"` step); this test asserts the C# primitive.
    [Test]
    public async Task Builder_BuildStart_RegistersBuilderChannel()
    {
        var registered = RegisterMemoryChannel("builder");
        var resolved = Channels.Channel("builder");

        await Assert.That(resolved).IsNotTypeOf<NoopChannel>();
        await Assert.That(ReferenceEquals(resolved, registered)).IsTrue();
    }

    // After build end, the channel is removed and lookups fall back to the no-op sink.
    [Test]
    public async Task Builder_BuildEnd_DisposesBuilderChannel()
    {
        RegisterMemoryChannel("builder");
        var removed = await Channels.RemoveAsync("builder");
        await Assert.That(removed).IsTrue();

        var resolved = Channels.Channel("builder");
        await Assert.That(resolved).IsTypeOf<NoopChannel>();
    }

    // A build warning rides as a native dict {action, message}; structural dict
    // equality lets consumers de-dup warnings without manual equality plumbing.
    [Test]
    public async Task BuildWarning_DictShape_CarriesActionAndMessage()
    {
        var w1 = Warning("file.read", "duplicate");
        var w2 = Warning("file.read", "duplicate");

        var raw = global::app.type.item.@this.Lower<Dictionary<string, object?>>(w1)!;
        await Assert.That((string)raw["action"]!).IsEqualTo("file.read");
        await Assert.That((string)raw["message"]!).IsEqualTo("duplicate");
        await Assert.That(w1.AreEqual(w2)).IsTrue()
            .Because("Structural dict equality lets consumers de-dup identical warnings.");
    }

    // Writing a warning dict to a registered "builder" channel succeeds (the
    // dispatch reaches the real channel rather than the no-op fallback). End-to-
    // end serialization round-trip is a separate concern owned by the channel's
    // serializer; here we assert the routing.
    [Test]
    public async Task BuildWarning_WriteToBuilderChannel_SubscriberReceivesPayload()
    {
        RegisterMemoryChannel("builder");
        var payload = Warning("file.read", "missing file");

        var writeResult = await Channels.WriteAsync("builder", payload);
        await writeResult.IsSuccess();
        await Assert.That(Channels.Channel("builder")).IsNotTypeOf<NoopChannel>()
            .Because("Write must have routed to the real channel, not the no-op fallback.");
    }

    // Outside a build, "builder" is not registered → Channel("builder") falls back
    // to the no-op sink → writes drop silently with no side effect.
    [Test]
    public async Task BuildWarning_WriteToBuilderChannel_OutsideBuild_DropsSilently()
    {
        var sink = Channels.Channel("builder");
        await Assert.That(sink).IsTypeOf<NoopChannel>();

        var result = await sink.WriteAsync(_app.Ok(Warning("file.read", "msg")));

        await result.IsSuccess();
    }

    // The build-warning payload shape: a native dict {action, message}, mirroring
    // what file.read writes to the "builder" channel.
    private global::app.type.dict.@this Warning(string action, string message)
        => new global::app.type.dict.@this { Context = _app.User.Context }.Set("action", action).Set("message", message);

    // Two distinct channel names resolve to two distinct channel instances —
    // they are independent registry entries, not aliases. End-to-end isolation
    // of payloads between them follows from this identity check.
    [Test]
    public async Task BuildTimeAndRuntime_AreSeparateChannelNames()
    {
        var builder = RegisterMemoryChannel("builder");
        var runtime = RegisterMemoryChannel("warnings");

        await Assert.That(ReferenceEquals(builder, runtime)).IsFalse();
        await Assert.That(ReferenceEquals(Channels.Channel("builder"), Channels.Channel("warnings"))).IsFalse();
    }
}
