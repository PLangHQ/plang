namespace PLang.Tests.App.Serialization;

// A format's own view is what it is written and read in when no one names one: a .pr is what plang keeps (Store);
// every other format crosses to others (Out).
public class FormatViewTests : System.IAsyncDisposable
{
    private readonly global::app.@this app = new global::app.@this("/app").Testing();
    public async System.Threading.Tasks.ValueTask DisposeAsync() => await app.DisposeAsync();

    [Test]
    [Arguments("goal", null, "Store")]
    [Arguments("wire", "plang", "Out")]
    [Arguments("item", "json", "Out")]
    [Arguments("text", null, "Out")]
    public async Task EachFormat_HasItsOwnView(string type, string? kind, string view)
    {
        var format = kind is null ? app.type.list[type].kind : app.type.list[type].kind[kind]!;

        await Assert.That(format.View.ToString()).IsEqualTo(view);
    }

    // a .pr written with no view named keeps what plang keeps: a sensitive value whole, never ****
    [Test]
    public async Task AGoalWrittenWithNoViewNamed_IsWrittenAsPlangKeepsIt()
    {
        var ctx = app.actor.list.User.Context;
        var secret = new global::app.type.item.sensitive.@this("s3cret", app.type.list["sensitive"]);
        using var stream = new System.IO.MemoryStream();

        await (await app.type.list["goal"].kind.Encode(stream, ctx.Ok(secret), ctx)).IsSuccess();

        await Assert.That(System.Text.Encoding.UTF8.GetString(stream.ToArray())).Contains("s3cret");
    }
}
