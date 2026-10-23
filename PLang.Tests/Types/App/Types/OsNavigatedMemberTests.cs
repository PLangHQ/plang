namespace PLang.Tests.App.Types;

/// <summary>
/// A member an os goal navigates reads: `%!app.OsAbsolutePath%` (the browser goals' blank page) and `%goal.Cache%`
/// (BuildGoal's cached goal) answer what they hold. A member plang can't read stays as written in a text, so a miss
/// here is a literal `%…%` handed on with no error.
/// </summary>
public class OsNavigatedMemberTests : System.IAsyncDisposable
{
    private readonly global::app.@this _app = new global::app.@this("/tmp/os-member-" + System.Guid.NewGuid().ToString("N")[..6]).Testing();
    public async System.Threading.Tasks.ValueTask DisposeAsync() => await _app.DisposeAsync();

    private global::app.actor.context.@this Ctx => _app.actor.list.User.Context;

    private async Task<global::app.data.@this> Read(string text)
        => await new global::app.type.item.variable.parser.@this(text, _app.type.list).Variable[0].Start(Ctx);

    [Test]
    public async Task TheAppsOsFolder_Reads()
    {
        var read = await Read("%!app.filesystem.os%");

        await Assert.That(read.Error?.Key).IsNotEqualTo("NotFound");
        await Assert.That(((global::app.type.item.path.@this)(await read.Value())!).Absolute).IsEqualTo(_app.filesystem.os.Absolute);
    }

    // a fresh goal holds no cache, so what reads it is the goal's member list: Cache is on it
    [Test]
    public async Task AGoalsCache_IsAMemberPlangReads()
        => await Assert.That(_app.type.list.Member(typeof(global::app.goal.@this))["cache"]).IsNotNull();
}
