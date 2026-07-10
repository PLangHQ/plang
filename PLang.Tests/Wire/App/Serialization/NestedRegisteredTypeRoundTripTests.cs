using System.Text.Json;

namespace PLang.Tests.App.Serialization;

// plang-types — Stage 2
// A registered-type value nested inside another (e.g., a Data containing an Image whose
// Path is a registered path) round-trips through the writer — each registered node hits
// the dispatch independently, no Normalize recursion bug.

public class NestedRegisteredTypeRoundTripTests
{
    private static global::app.@this NewApp()
        => global::PLang.Tests.TestApp.Create(System.IO.Path.Combine(System.IO.Path.GetTempPath(),
            "plang-nested-" + System.Guid.NewGuid().ToString("N")[..8]));

    [Test]
    public async Task Image_WithPathFacet_BothNodesDispatched_OnWire()
    {
        // Stage 5 lands the image type. Until then, demonstrate the pattern by
        // putting a registered-type value (path) at two nested positions: a Data
        // whose Value is a list containing two path-typed Datas.
        await using var app = NewApp();
        var context = app.User.Context;
        var p1 = global::app.type.item.path.@this.Resolve("/srv/a.txt", context);
        var p2 = global::app.type.item.path.@this.Resolve("/srv/b.txt", context);
        var outer = new global::app.data.@this("outer", new[] {
            new global::app.data.@this("p1", p1, context: context),
            new global::app.data.@this("p2", p2, context: context),
        }, context: context);

        var plang = (global::app.channel.serializer.plang.@this)
            app.User.Channel.Serializers.GetByMimeType("application/plang");
        using var ms = new System.IO.MemoryStream();
        await plang.SerializeAsync(ms, outer, global::app.View.Out);
        var json = System.Text.Encoding.UTF8.GetString(ms.ToArray());
        // Both nested path values must appear as strings, not as reflected
        // property bags ("\"absolute\":" would mean reflection fired).
        await Assert.That(json.Contains("a.txt")).IsTrue();
        await Assert.That(json.Contains("b.txt")).IsTrue();
        await Assert.That(json.Contains("\"absolute\":")).IsFalse();
    }
}
