using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;

namespace PLang.Tests.App.SingularNamespaces.AccessorTests;

// Batch B — actor.channel collection + I/O on the element (Stage 3).
// Registry is selection + lifecycle only — no WriteText, no type-switch.
// channel.@this owns Write(data)/Read(); channel.stream.@this overrides for the stream path.
// Index-miss throws; writing to a wrong-direction channel returns a typed data.Fail (CanWrite on the element).
public class ChannelAccessorTests
{
    [Test] public async Task ActorChannel_IndexByName_SelectsTheRegisteredChannel()
        => Assert.Fail("Not implemented");

    [Test] public async Task ActorChannelList_Enumerates_RegisteredChannels()
        => Assert.Fail("Not implemented");

    [Test] public async Task ChannelEntity_Write_RoundTripsDataThroughTheElement()
        => Assert.Fail("Not implemented");

    [Test] public async Task ChannelEntity_Read_ReturnsTheLastWrittenData()
        => Assert.Fail("Not implemented");

    // Polymorphism replaces the registry's `is channel.stream.@this` type-switch.
    [Test] public async Task StreamChannel_Write_UsesTheStreamOptimizedOverride()
        => Assert.Fail("Not implemented");

    // The contract: registry has selection + lifecycle, nothing else. No WriteAsync/WriteTextAsync/ReadTextAsync/ReadChannelAsync.
    [Test] public async Task ChannelListType_ExposesNoIoMethods_OnTheRegistry()
        => Assert.Fail("Not implemented");

    [Test] public async Task ActorChannel_IndexOfUnknownName_ThrowsTypedError()
        => Assert.Fail("Not implemented");

    // CanWrite lives on channel.@this; writing to a read-only channel returns a typed data.Fail (not a throw).
    [Test] public async Task ChannelEntity_Write_OnReadOnlyChannel_ReturnsTypedFail()
        => Assert.Fail("Not implemented");
}
