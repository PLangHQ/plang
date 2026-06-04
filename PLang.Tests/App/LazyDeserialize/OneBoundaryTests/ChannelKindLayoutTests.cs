using System.Linq;
using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;

namespace PLang.Tests.App.LazyDeserialize.OneBoundaryTests;

// All channel kinds — the existing stream/session/message/event/goal/noop
// plus the new file/http — live under `channel/type/`. The architect's
// shape: file and http aren't peers of channel, they're channels.
public class ChannelKindLayoutTests
{
    [Test] public async Task ChannelKinds_AllLiveUnder_channel_type()
    {
        var baseType = typeof(global::app.channel.@this);
        var kinds = baseType.Assembly.GetTypes()
            .Where(t => t.IsClass && !t.IsAbstract && baseType.IsAssignableFrom(t))
            .ToList();
        await Assert.That(kinds).IsNotEmpty();
        foreach (var k in kinds)
            await Assert.That(k.Namespace!.StartsWith("app.channel.type."))
                .IsTrue().Because($"{k.FullName} is a channel kind and must live under app.channel.type.*");
    }

    // New kinds — the surface that lets every read enter through the one
    // boundary.
    // Metadata name of `@this` is `this` (the @ is C# escaping, not part of the name).
    [Test] public async Task FileChannel_Exists_AtAppChannelTypeFile()
        => await Assert.That(typeof(global::app.channel.@this).Assembly
            .GetType("app.channel.type.file.this")).IsNotNull();

    [Test] public async Task HttpChannel_Exists_AtAppChannelTypeHttp()
        => await Assert.That(typeof(global::app.channel.@this).Assembly
            .GetType("app.channel.type.http.this")).IsNotNull();
}
