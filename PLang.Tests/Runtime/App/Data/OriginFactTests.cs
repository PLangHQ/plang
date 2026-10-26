namespace PLang.Tests.App.DataTests;

// `!` reaches the facts of what a value came from, after it was opened: a read file narrowed to the dict it parses to
// still answers the file's !path; the dict's own facts (!key) answer first.
public class OriginFactTests
{
    [Test]
    public async Task AReadFileNarrowedByAUse_StillAnswersItsPath_AndTheDictItsOwnKey()
    {
        var dir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "plang_origin_" + System.Guid.NewGuid().ToString("N")[..8]);
        System.IO.Directory.CreateDirectory(dir);
        await using var app = new global::app.@this(dir, global::PLang.Tests.Parent.Actor).Testing();
        var ctx = app.actor.list.User.Context;
        System.IO.File.WriteAllText(System.IO.Path.Combine(dir, "config.json"), "{\"db\": \"main\"}");

        // the file through the program's door: an OS location handed in is shown in its plang form
        await ctx.Variable.Set("cfg", await global::app.type.item.path.file.@this.Resolve(System.IO.Path.Combine(dir, "config.json"), ctx).Read(ctx));
        var db = await new global::app.type.item.variable.@this("cfg.db", app.type.list).Start(ctx);
        await Assert.That((await db.Value())?.ToString()).IsEqualTo("main");

        var path = await new global::app.type.item.variable.@this("cfg!path", app.type.list).Start(ctx);
        await path.IsSuccess();
        await Assert.That((await path.Value())?.ToString()).EndsWith("/config.json");

        // the dict's own fact answers first: its key, never the file's
        var key = await new global::app.type.item.variable.@this("cfg!key", app.type.list).Start(ctx);
        await key.IsSuccess();
        await Assert.That((await key.Value())?.ToString()).IsNotEqualTo((await path.Value())?.ToString());
    }
}
