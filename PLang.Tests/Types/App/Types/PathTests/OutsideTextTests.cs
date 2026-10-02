using PLang.Tests.Shared;
using PLangPath = global::app.type.item.path.@this;

namespace PLang.Tests.App.Types.PathTests;

// A path made from outside text is plain: its location is the text it was given, never a template. A
// developer's path template is a built source (its .pr row lists its variables), rendered through text at
// the source's door and then read as a path (source.cs). So a file named `%!app.type.list.count%.txt`, a
// wire value, or a string taken as a path stays as written; `read %dir%/x.txt` still fills %dir%.
public class OutsideTextTests
{
    private const string AppVariable = "%!app.type.list.count%";

    private static async Task<(global::app.@this app, string root, global::app.actor.context.@this ctx)> NewApp()
    {
        var root = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "paths-" + Guid.NewGuid().ToString("N")[..8]);
        System.IO.Directory.CreateDirectory(root);
        var app = new global::app.@this(root).Testing();
        return (app, root, app.actor.list.User.Context);
    }

    private static async Task<PLangPath> Opened(global::app.data.@this data)
    {
        var value = await data.Value();
        await data.IsSuccess();
        return (PLangPath)value;
    }

    // A directory listing: the file's name is the disk's, not a template.
    [Test] public async Task AListedFileNamedAnAppVariable_StaysThatName()
    {
        var (app, root, ctx) = await NewApp();
        await using var _ = app;
        System.IO.Directory.CreateDirectory(System.IO.Path.Combine(root, "dir"));
        System.IO.File.WriteAllText(System.IO.Path.Combine(root, "dir", AppVariable + ".txt"), "x");

        var listed = await PLangPath.Resolve("dir", ctx).List(ctx);
        await listed.IsSuccess();
        var file = (await listed.Value())!.Items(ctx).Single();

        await Assert.That((await Opened(file)).FileName).IsEqualTo(AppVariable + ".txt");
    }

    // A value off the wire (a peer's Data, the app's store) typed a path: as it arrived, marked or not.
    [Test]
    [Arguments("{\"name\":\"path\"}")]
    [Arguments("{\"name\":\"path\",\"template\":\"plang\"}")]
    public async Task AWireValueAsPath_StaysAsWritten(string type)
    {
        var (app, root, ctx) = await NewApp();
        await using var _ = app;
        var row = "{\"name\":\"p\",\"type\":" + type + ",\"value\":\"" + AppVariable + ".txt\"}";

        var read = await ctx.App.type.list["wire"].kind["plang"]!.Decode(System.Text.Encoding.UTF8.GetBytes(row), ctx,
            view: global::app.View.Store);
        await read.IsSuccess();

        await Assert.That((await Opened(read)).ToString()).IsEqualTo(AppVariable + ".txt");
    }

    // A string the program holds (read from outside), taken as a path: through the typed ask and through the
    // path type's birth. Held and opened again, it is still the text it was.
    [Test] public async Task AStringAsPath_StaysAsWritten()
    {
        var (app, root, ctx) = await NewApp();
        await using var _ = app;
        var text = new global::app.data.@this("s", AppVariable + ".txt", context: ctx);

        var asked = await text.Value<PLangPath>();
        var born = await ctx.App.type.list["path"].Create(AppVariable + ".txt", ctx);

        await Assert.That((await Opened(new global::app.data.@this("p", asked!, context: ctx))).ToString()).IsEqualTo(AppVariable + ".txt");
        await Assert.That((await Opened(born)).ToString()).IsEqualTo(AppVariable + ".txt");
    }

    // The developer's own path template (`read %dir%/x.txt`), loaded through the .pr door: %dir% fills and the
    // file is read. What %dir% holds is filled once, never read as a template again.
    [Test] public async Task ADevelopersPathTemplate_FillsItsVariable_AndReads()
    {
        var (app, root, ctx) = await NewApp();
        await using var _ = app;
        System.IO.Directory.CreateDirectory(System.IO.Path.Combine(root, "data"));
        System.IO.File.WriteAllText(System.IO.Path.Combine(root, "data", "x.txt"), "hello");
        await ctx.Variable.Set("dir", "data");

        var read = await Run(app, ctx, "%dir%/x.txt");

        await Assert.That((await read.Value())?.ToString()).IsEqualTo("hello");
    }

    [Test] public async Task ADevelopersPathTemplate_WhatItsVariableHolds_IsNotFilledAgain()
    {
        var (app, root, ctx) = await NewApp();
        await using var _ = app;
        await ctx.Variable.Set("dir", AppVariable);
        var path = Make.Built(ctx, "Path", "%dir%/x.txt", new global::app.type.@this("path", template: new global::app.type.item.template.kind.plang.@this()));

        var once = await Opened(path);
        var again = await Opened(new global::app.data.@this("p", once, context: ctx));

        await Assert.That(once.ToString()).IsEqualTo(AppVariable + "/x.txt");
        await Assert.That(again.ToString()).IsEqualTo(AppVariable + "/x.txt");
    }

    // The build's own path may name the app's variables (the builder saves to `/.build/traces/%!trace.id%/…`):
    // the build grants every variable its row lists, so loaded through the .pr door it still renders.
    [Test] public async Task ABuiltPathHoldingAnAppVariable_StillRenders()
    {
        var (app, root, ctx) = await NewApp();
        await using var _ = app;
        var count = (await (await new global::app.type.item.variable.@this("!app.type.list.count").Start(ctx)).Value()).ToString();
        var goal = await RealGoalLoad.ViaChannel(app, Make.Goal(ctx, "G", "/g.goal",
            Make.Step("save", Make.Action(ctx, "file", "save",
                Make.Param(ctx, "Path", "/.build/traces/" + AppVariable + "/manifest.json", new global::app.type.@this("path", template: new global::app.type.item.template.kind.plang.@this()))))));

        var path = await Opened(goal.Step[0].Code[0].Property["Path"]!.Data(ctx));

        await Assert.That(path.ToString()).IsEqualTo($"/.build/traces/{count}/manifest.json");
    }

    // A path template naming a variable that is not set: the read fails, saying so.
    [Test] public async Task APathTemplateNamingAnUnsetVariable_FailsNotSet()
    {
        var (app, root, ctx) = await NewApp();
        await using var _ = app;

        var read = await Run(app, ctx, "%missing%/x.txt");

        await read.IsFailure();
        await Assert.That(read.Error!.Key).IsEqualTo("VariableNotFound");
        await Assert.That(read.Error.Message).Contains("%missing% is not set");
    }

    // `read <path>` as the build writes it, loaded through the goal loader (the build's grant), run.
    private static async Task<global::app.data.@this> Run(global::app.@this app, global::app.actor.context.@this ctx, string path)
    {
        var goal = await RealGoalLoad.ViaChannel(app, Make.Goal(ctx, "G", "/g.goal",
            Make.Step("read", Make.Action(ctx, "file", "read",
                Make.Param(ctx, "Path", path, new global::app.type.@this("path", template: new global::app.type.item.template.kind.plang.@this()))))));
        return await goal.Step[0].Code[0].Start(ctx);
    }
}
