using PLang.Tests.Shared;
using HttpTestServer = PLang.Tests.App.Types.PathTests.Http.HttpTestServer;

namespace PLang.Tests.App.Types.SourceTests;

// Content read from outside fills only the program's own variables. A template source decides, once, at
// its birth, what it holds by whose bytes these are (source.cs): the build's own .pr every variable its
// row lists; a file or url read with `load vars` only the program's own (no `!` name, no binding hop);
// anything else (a peer's value, the app's store) none. A variable not held stays as written.
public class LoadVarsTests
{
    private static async Task<(global::app.@this app, string root, global::app.actor.context.@this ctx)> NewApp()
    {
        var root = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "loadvars-" + Guid.NewGuid().ToString("N")[..8]);
        System.IO.Directory.CreateDirectory(root);
        var app = new global::app.@this(root).Testing();
        var ctx = app.actor.list.User.Context;
        await ctx.Variable.Set("name", "World");
        return (app, root, ctx);
    }

    // `read <file>, load vars`, its value as the program uses it.
    private static async Task<global::app.data.@this> LoadVars(global::app.actor.context.@this ctx, string root, string file, string content)
    {
        System.IO.File.WriteAllText(System.IO.Path.Combine(root, file), content);
        var read = await global::app.type.item.path.@this.Resolve(file, ctx).Read(ctx, new global::app.type.item.template.kind.plang.@this());
        await read.IsSuccess();
        return read;
    }

    private static async Task<string?> Text(global::app.data.@this data) => (await data.Value())?.ToString();

    [Test] public async Task AFileWhollyAnAppVariable_StaysAsWritten()
    {
        var (app, root, ctx) = await NewApp();
        await using var _ = app;
        var read = await LoadVars(ctx, root, "trace.txt", "%!trace.id%");

        await Assert.That(await Text(read)).IsEqualTo("%!trace.id%");
        await Assert.That(read.Success).IsTrue();
    }

    // The same with an app variable that is always set: before, it printed the app's type count.
    [Test] public async Task AFileWhollyAnAppVariable_IsNeverResolved()
    {
        var (app, root, ctx) = await NewApp();
        await using var _ = app;
        var read = await LoadVars(ctx, root, "count.txt", "%!app.type.list.count%");

        await Assert.That(await Text(read)).IsEqualTo("%!app.type.list.count%");
    }

    [Test] public async Task AFile_FillsTheProgramsOwnVariables_NotTheApps()
    {
        var (app, root, ctx) = await NewApp();
        await using var _ = app;
        var read = await LoadVars(ctx, root, "mix.txt", "Hi %name% n=%!app.type.list.count%");

        await Assert.That(await Text(read)).IsEqualTo("Hi World n=%!app.type.list.count%");
    }

    // A binding hop reaches past the value into what holds it (`!context` is the actor's context).
    [Test] public async Task AFile_ABindingHop_StaysAsWritten()
    {
        var (app, root, ctx) = await NewApp();
        await using var _ = app;
        await ctx.Variable.Set("x", "xx");
        var read = await LoadVars(ctx, root, "hop.txt", "a=%x!context% b=%x%");

        await Assert.That(await Text(read)).IsEqualTo("a=%x!context% b=xx");
    }

    // An index or a method handed an app variable reaches outside too.
    [Test] public async Task AFile_AnIndexOrMethodHandedAnAppVariable_StaysAsWritten()
    {
        var (app, root, ctx) = await NewApp();
        await using var _ = app;
        await ctx.Variable.Set("items", new List<object?> { "first" });
        var read = await LoadVars(ctx, root, "nested.txt", "%items[0]% %items[%!app.type.list.count%]% %name.replace(\"W\", %!app.type.list.count%)%");

        await Assert.That(await Text(read)).IsEqualTo("first %items[%!app.type.list.count%]% %name.replace(\"W\", %!app.type.list.count%)%");
    }

    // A guard: a json file read with load vars fills nothing today (json's owner reads its bytes as json,
    // never as a template), so an app variable in its value stays as written.
    [Test] public async Task AJsonFile_AnAppVariableInAValue_StaysAsWritten()
    {
        var (app, root, ctx) = await NewApp();
        await using var _ = app;
        var read = await LoadVars(ctx, root, "data.json", "{\"b\":\"%!app.type.list.count%\"}");
        await ctx.Variable.Set("j", read);

        var b = await new global::app.type.item.variable.@this("j.b").Start(ctx);
        await Assert.That(await Text(b)).IsEqualTo("%!app.type.list.count%");
    }

    [Test] public async Task AUrl_FillsTheProgramsOwnVariables_NotTheApps()
    {
        using var server = new HttpTestServer();
        var (app, root, ctx) = await NewApp();
        await using var _ = app;
        var address = server.MapStoredBody(System.Text.Encoding.UTF8.GetBytes("Hi %name% n=%!app.type.list.count%"), "text/plain");
        var grant = new global::app.type.item.permission.@this("User", new global::app.type.item.path.http.@this(address).Absolute,
            global::app.type.item.permission.@this.AllVerbs, global::app.type.item.permission.Match.Exact);
        await ctx.Actor!.Permission.Add(new global::app.data.@this<global::app.type.item.permission.@this>("", grant, context: ctx), persist: false);

        var read = await global::app.type.item.path.@this.Resolve(address, ctx).Read(ctx, new global::app.type.item.template.kind.plang.@this());

        await Assert.That(await Text(read)).IsEqualTo("Hi World n=%!app.type.list.count%");
    }

    // A value off the wire (another actor's, or the app's own store) marked a template holds none of the
    // variables it names — not even the program's own: it stays as it arrived.
    [Test] public async Task AValueOffTheWire_MarkedATemplate_HoldsNoVariable()
    {
        var (app, root, ctx) = await NewApp();
        await using var _ = app;
        var row = "{\"name\":\"x\",\"type\":{\"name\":\"text\",\"template\":\"plang\"},\"value\":\"Hi %name% n=%!app.type.list.count%\"}";

        var read = await Wire(ctx, row);

        await Assert.That(await Text(read)).IsEqualTo("Hi %name% n=%!app.type.list.count%");
        await Assert.That(read.Peek().HasVariable).IsFalse();
    }

    // A row's own variable list is the build's word only: one arriving off the wire is ignored, whatever
    // code it pairs with a harmless text.
    [Test] public async Task AValueOffTheWire_ItsOwnVariableList_IsIgnored()
    {
        var (app, root, ctx) = await NewApp();
        await using var _ = app;
        var row = "{\"name\":\"x\",\"type\":{\"name\":\"text\",\"template\":\"plang\"},\"value\":\"%name%\",\"variable\":[{\"text\":\"%name%\","
                  + "\"code\":[{\"variable\":\"!app\"},{\"property\":\"type\"},{\"property\":\"list\"},{\"property\":\"count\"}]}]}";

        var read = await Wire(ctx, row);

        await Assert.That(await Text(read)).IsEqualTo("%name%");
    }

    // plang's own format read back (the Store view reads an unsigned row, as the app's store does) — the door
    // a peer's Data and a stored one come through.
    private static async Task<global::app.data.@this> Wire(global::app.actor.context.@this ctx, string row)
    {
        var read = await ctx.App.type.list["wire"].kind["plang"]!.Decode(System.Text.Encoding.UTF8.GetBytes(row), ctx,
            view: global::app.View.Store);
        await read.IsSuccess();
        return read;
    }

    // A guard: the build's own .pr still resolves the app's variables in its templates — directly, and in a
    // row nested in a container (a goal call's parameters).
    [Test] public async Task ABuiltTemplate_StillResolvesTheAppsVariables()
    {
        var (app, root, ctx) = await NewApp();
        await using var _ = app;
        var count = await Text(await new global::app.type.item.variable.@this("!app.type.list.count").Start(ctx));
        var goal = Make.Goal(ctx, "G", "/g.goal",
            Make.Step("s",
                Make.Action(ctx, "output", "write",
                    Make.Template(ctx, "Data", "n=%!app.type.list.count% %name%"),
                    ("Nested", new List<object?> { new global::app.data.@this("place", "%!app.type.list.count%",
                        app.type.list[new global::app.type.@this("text", template: new global::app.type.item.template.kind.plang.@this()), ctx], context: ctx) }))));

        var loaded = await RealGoalLoad.ViaChannel(app, goal);
        var properties = loaded.Step[0].Code[0].Property;

        await Assert.That(await Text(properties["Data"]!.Data(ctx))).IsEqualTo($"n={count} World");
        var nested = ((global::app.type.item.list.@this)(await properties["Nested"]!.Data(ctx).Value())!).Items(ctx).Single();
        await Assert.That(await Text(nested)).IsEqualTo(count);
    }

    // Content holding none of the variables it may fill renders the same every time: kept once opened (the
    // holding Data keeps the text), not decoded again at each use.
    [Test] public async Task AFileHoldingNoOwnVariable_IsKeptOnceOpened()
    {
        var (app, root, ctx) = await NewApp();
        await using var _ = app;
        var read = await LoadVars(ctx, root, "plain.txt", "n=%!app.type.list.count%");

        var first = await read.Value();
        var second = await read.Value();

        await Assert.That(ReferenceEquals(first, second)).IsTrue();
        await Assert.That(read.Peek()).IsTypeOf<global::app.type.item.text.@this>();
    }

    // Content holding an own variable renders at each use: a change between two uses shows.
    [Test] public async Task AFileHoldingAnOwnVariable_ShowsAChangeBetweenUses()
    {
        var (app, root, ctx) = await NewApp();
        await using var _ = app;
        var read = await LoadVars(ctx, root, "hi.txt", "Hi %name%");

        await Assert.That(await Text(read)).IsEqualTo("Hi World");
        await ctx.Variable.Set("name", "Again");
        await Assert.That(await Text(read)).IsEqualTo("Hi Again");
    }
}
