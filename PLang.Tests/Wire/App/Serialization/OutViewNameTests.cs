using System.Text.Json.Nodes;

namespace PLang.Tests.App.Serialization;

// Between actors (the Out view) a Data's name is no part of what crosses: the writer leaves it out and the signature's
// digest doesn't cover it. A Data read off a peer's wire is named by its receiver — the name the decode is given — so a
// name added in transit is never taken, and the signature still verifies.
public class OutViewNameTests : System.IAsyncDisposable
{
    private readonly global::app.@this app = new global::app.@this(System.IO.Path.Combine(System.IO.Path.GetTempPath(),
        "plang-outname-" + System.Guid.NewGuid().ToString("N")[..8]), global::PLang.Tests.Parent.Actor).Testing();
    public async System.Threading.Tasks.ValueTask DisposeAsync() => await app.DisposeAsync();

    [Test] public async Task ANameAddedInTransit_IsNotTaken_TheReceiversNameIs()
    {
        var ctx = app.actor.list.User.Context;
        var plang = app.type.list.Mime("application/plang");
        using var ms = new System.IO.MemoryStream();
        await (await plang.Encode(ms, new global::app.data.@this("", "hi", context: ctx), ctx, global::app.View.Out)).IsSuccess();

        // the data layer, under its signature layer, given a name and a variable list it was never signed with
        var wire = JsonNode.Parse(ms.ToArray())!.AsObject();
        var layer = wire["@schema"]?.GetValue<string>() == "signature" ? wire["value"]!.AsObject() : wire;
        layer["name"] = "forged";
        layer["variable"] = new JsonArray();

        var decoded = await plang.Decode(System.Text.Encoding.UTF8.GetBytes(wire.ToJsonString()), ctx, global::app.View.Out, "received");
        await decoded.IsSuccess();
        await Assert.That(decoded.Name).IsEqualTo("received");
        await Assert.That((await decoded.Value())?.ToString()).IsEqualTo("hi");
    }
}
