using System.Linq;
using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;

namespace PLang.Tests.App.LazyDeserialize.OneBoundaryTests;

// All channel kinds — stream/session/message/goal/noop/test — live under `channel/type/`.
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
}
