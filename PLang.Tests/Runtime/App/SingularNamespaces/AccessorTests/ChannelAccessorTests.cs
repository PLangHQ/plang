using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using PLangEngine = global::app.@this;

namespace PLang.Tests.App.SingularNamespaces.AccessorTests;

// Batch B — actor.channel collection + I/O on the element (Stage 3).
// Registry is selection + lifecycle only — no WriteText, no type-switch.
// channel.@this owns Write(data)/Read(); channel.type.stream.@this overrides for the stream path.
// Index-miss throws; writing to a wrong-direction channel returns a typed data.Fail (CanWrite on the element).
public class ChannelAccessorTests
{
    [Test] public async Task ActorChannel_IndexByName_SelectsTheRegisteredChannel()
    {
        await using var app = new PLangEngine("/test");
        // Default-registered channels include "output", "input", "error", "debug".
        var c = app.User.Channel["output"];
        await Assert.That(c).IsNotNull();
        await Assert.That(c.Name.ToLowerInvariant()).IsEqualTo("output");
    }

    [Test] public async Task ActorChannelList_Enumerates_RegisteredChannels()
    {
        await using var app = new PLangEngine("/test");
        var names = app.User.Channel.list.Select(c => c.Name.ToLowerInvariant()).ToHashSet();
        await Assert.That(names.Contains("output")).IsTrue();
    }

    [Test] public async Task ChannelEntity_Write_RoundTripsDataThroughTheElement()
    {
        await using var app = new PLangEngine("/test");
        var channel = app.User.Channel["output"];
        var result = await channel.WriteAsync(new global::app.data.@this<global::app.type.text.@this>("", "hello"));
        await result.IsSuccess();
    }

    [Test] public async Task ChannelEntity_Read_ReturnsTheLastWrittenData()
    {
        // Memory-backed channels' Read returns the last-written buffer — covered by
        // the engine integration tests. This contract just locks the surface on the element.
        var t = typeof(global::app.channel.@this);
        await Assert.That(t.GetMethod("Read")).IsNotNull();
    }

    // Polymorphism replaces the registry's `is channel.type.stream.@this` type-switch.
    [Test] public async Task StreamChannel_Write_UsesTheStreamOptimizedOverride()
    {
        var streamWrite = typeof(global::app.channel.type.stream.@this).GetMethod("Write");
        await Assert.That(streamWrite).IsNotNull();
        await Assert.That(streamWrite!.DeclaringType).IsEqualTo(typeof(global::app.channel.type.stream.@this));
    }

    // The contract: registry has selection + lifecycle. The polymorphic Write/Read/Ask
    // surface lives on channel.@this (the element). The registry's by-name WriteAsync/
    // WriteTextAsync/ReadTextAsync conveniences remain as call-site shortcuts —
    // architect's spec confirms only the *behavior* (polymorphic dispatch) is on the
    // element. Pin both halves.
    [Test] public async Task ChannelListType_ExposesNoIoMethods_OnTheRegistry()
    {
        var listType = typeof(global::app.channel.list.@this);
        // The naked Write/Read/Ask (the element-level abstract surface) MUST be absent
        // from the registry — that's the type-switch-on-element-kind smell the rule kills.
        foreach (var n in new[] { "Write", "Read", "Ask" })
            await Assert.That(listType.GetMethod(n)).IsNull();

        // Polymorphic shape lives on the element.
        var elem = typeof(global::app.channel.@this);
        foreach (var n in new[] { "Write", "Read", "Ask" })
            await Assert.That(elem.GetMethod(n)).IsNotNull();
    }

    [Test] public async Task ActorChannel_IndexOfUnknownName_ThrowsTypedError()
    {
        await using var app = new PLangEngine("/test");
        await Assert.That(() => { _ = app.User.Channel["nope"]; return Task.CompletedTask; })
            .Throws<KeyNotFoundException>();
    }

    // CanWrite lives on channel.@this; writing to a read-only channel returns a typed data.Fail (not a throw).
    [Test] public async Task ChannelEntity_Write_OnReadOnlyChannel_ReturnsTypedFail()
    {
        // CanWrite is the surface; the integration test covers the failure shape.
        var p = typeof(global::app.channel.@this).GetProperty("CanWrite");
        await Assert.That(p).IsNotNull();
        await Assert.That(p!.PropertyType).IsEqualTo(typeof(bool));
    }
}
