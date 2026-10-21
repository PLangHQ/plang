namespace PLang.Tests.Types.App.Types.PathTests;

/// <summary>Saving to a file streams: a file not yet read, saved to a file of its own format, pours its bytes from where
/// it is and stays unread; a save that fails leaves the file it was writing as it was, with nothing left beside it.</summary>
public class FileSaveStreamsTests
{
    private static readonly byte[] Zip = [0x50, 0x4B, 0x03, 0x04, 0x14, 0x00, 0x00, 0x00, 0x08, 0x00, 0xDE, 0xAD, 0xBE, 0xEF];

    private static async Task<T> InRoot<T>(Func<string, Task<T>> run)
    {
        var root = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "plang-stream-" + Guid.NewGuid().ToString("N")[..8]);
        System.IO.Directory.CreateDirectory(root);
        try { return await run(root); }
        finally { System.IO.Directory.Delete(root, recursive: true); }
    }

    // read file 'source', write to %held%  /  save %held% to file 'target'
    private static async Task<global::app.data.@this> ReadThenSave(global::app.@this app, string source, string target)
    {
        var ctx = app.actor.list.User.Context;
        var goal = await RealGoalLoad.ViaChannel(app, Make.Goal(ctx, "Copy", "/Copy.goal",
            Make.Step($"read file '{source}', write to %held%",
                Make.Action(ctx, "file", "read", ("path", source)),
                Make.Action(ctx, "variable", "set", Make.Param(ctx, "Name", "held", "variable"), ("Value", "%!data%"))),
            Make.Step($"save %held% to file '{target}'",
                Make.Action(ctx, "file", "save", ("path", target), ("value", "%held%")))));
        app.goal.list.Add(goal);
        return await app.Start(goal, ctx);
    }

    [Test] public async Task AFileSavedToItsOwnFormat_PoursItsBytes_AndStaysUnread() => await InRoot(async root =>
    {
        await System.IO.File.WriteAllBytesAsync(System.IO.Path.Combine(root, "in.zip"), Zip);
        await using var app = global::PLang.Tests.Session.App(root).Testing();

        var result = await ReadThenSave(app, "in.zip", "out.zip");

        await result.IsSuccess();
        await Assert.That(await System.IO.File.ReadAllBytesAsync(System.IO.Path.Combine(root, "out.zip"))).IsEquivalentTo(Zip);
        var held = (await app.actor.list.User.Context.Variable.Get("held"))!.Peek();
        await Assert.That(held is global::app.type.item.file.@this { IsLoaded: false }).IsTrue()
            .Because("the save poured the file from where it is: its content was never read into memory");
        return 0;
    });

    [Test] public async Task AFileSavedToAnotherFormatOfItsFamily_PoursItsBytes_AndStaysUnread() => await InRoot(async root =>
    {
        await System.IO.File.WriteAllBytesAsync(System.IO.Path.Combine(root, "in.zip"), Zip);
        await using var app = global::PLang.Tests.Session.App(root).Testing();

        var result = await ReadThenSave(app, "in.zip", "out.bin");

        await result.IsSuccess();
        await Assert.That(await System.IO.File.ReadAllBytesAsync(System.IO.Path.Combine(root, "out.bin"))).IsEquivalentTo(Zip);
        var held = (await app.actor.list.User.Context.Variable.Get("held"))!.Peek();
        await Assert.That(held is global::app.type.item.file.@this { IsLoaded: false }).IsTrue()
            .Because("binary content written to a binary format is its bytes: poured, never read");
        return 0;
    });

    [Test] public async Task AFileWhoseFamilyWritesItsOwnWay_IsReadAndWritten() => await InRoot(async root =>
    {
        await System.IO.File.WriteAllTextAsync(System.IO.Path.Combine(root, "in.csv"), "a,b\n1,2\n");
        await using var app = global::PLang.Tests.Session.App(root).Testing();

        var result = await ReadThenSave(app, "in.csv", "out.csv");

        await result.IsSuccess();
        var held = (await app.actor.list.User.Context.Variable.Get("held"))!.Peek();
        await Assert.That(held is global::app.type.item.file.@this { IsLoaded: true }).IsTrue()
            .Because("a table is not its bytes: csv is read and written by the table, never poured");
        return 0;
    });

    [Test] public async Task AUrlSavedToAFile_PoursTheResponse_AndStaysUnread() => await InRoot(async root =>
    {
        using var server = new PLang.Tests.App.Types.PathTests.Http.HttpTestServer();
        var url = server.MapStoredBody(Zip, "application/zip");
        await using var app = global::PLang.Tests.Session.App(root).Testing();
        await Grant(app, url);

        var result = await ReadThenSave(app, url, "out.zip");

        await result.IsSuccess();
        await Assert.That(await System.IO.File.ReadAllBytesAsync(System.IO.Path.Combine(root, "out.zip"))).IsEquivalentTo(Zip);
        var held = (await app.actor.list.User.Context.Variable.Get("held"))!.Peek();
        await Assert.That(held is global::app.type.item.url.@this { IsLoaded: false }).IsTrue()
            .Because("the response body was poured into the file as it came");
        return 0;
    });

    [Test] public async Task AUrlNotPoured_IsFetchedOnce() => await InRoot(async root =>
    {
        using var server = new PLang.Tests.App.Types.PathTests.Http.HttpTestServer();
        var json = "{ \"a\": 1 }"u8.ToArray();
        var url = server.MapStoredBody(json, "application/json");
        await using var app = global::PLang.Tests.Session.App(root).Testing();
        await Grant(app, url);

        var result = await ReadThenSave(app, url, "out.txt");

        await result.IsSuccess();
        await Assert.That(await System.IO.File.ReadAllBytesAsync(System.IO.Path.Combine(root, "out.txt"))).IsEquivalentTo(json);
        await Assert.That(server.Requests.Count(r => r.Method == "GET")).IsEqualTo(1)
            .Because("json is read and written its own way: sampled from the open response, never fetched again");
        return 0;
    });

    private static async Task Grant(global::app.@this app, string url)
    {
        var ctx = app.actor.list.User.Context;
        var grant = new global::app.type.item.permission.@this("User", ctx.Url(url).Absolute,
            global::app.type.item.permission.@this.AllVerbs, global::app.type.item.permission.Match.exact, ctx.App.type.list[typeof(global::app.type.item.permission.@this)]);
        await ctx.Actor!.Permission.Add(new global::app.data.@this<global::app.type.item.permission.@this>("", grant, context: ctx), persist: true);
    }

    [Test] public async Task ASaveThatFails_LeavesTheFileAsItWas_AndNothingBeside() => await InRoot(async root =>
    {
        await System.IO.File.WriteAllTextAsync(System.IO.Path.Combine(root, "in.zip"), "gone soon");
        await System.IO.File.WriteAllTextAsync(System.IO.Path.Combine(root, "out.zip"), "as it was");
        await using var app = global::PLang.Tests.Session.App(root).Testing();
        var ctx = app.actor.list.User.Context;
        var source = global::app.type.item.path.@this.Resolve(System.IO.Path.Combine(root, "in.zip"), ctx)!;
        var read = await source.Read(ctx);
        System.IO.File.Delete(System.IO.Path.Combine(root, "in.zip"));

        var target = global::app.type.item.path.@this.Resolve(System.IO.Path.Combine(root, "out.zip"), ctx)!;
        var saved = await target.Save(read, ctx);

        await Assert.That(saved.Success).IsFalse();
        await Assert.That(await System.IO.File.ReadAllTextAsync(System.IO.Path.Combine(root, "out.zip"))).IsEqualTo("as it was");
        await Assert.That(System.IO.Directory.GetFiles(root).Select(System.IO.Path.GetFileName)).IsEquivalentTo(new[] { "out.zip" });
        return 0;
    });
}
