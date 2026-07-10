using System.Reflection;
using System.Text;
using System.Text.Json;
using PLang.Tests.App.Types.PathTests.Http;
using HttpPath = global::app.type.item.path.http.@this;
using PLangFilePath = global::app.type.item.path.file.@this;

namespace PLang.Tests.App.CompareRedesign;

// Stage 3 — `path` demolition. Drop `Content`/`Source`; backing
// `_absolutePath` → private `_location` (the as-typed string verbatim);
// `absolute`/`relative`/`extension` are derived (cached). `path.Write` reads
// the private `_location` directly — no public `value` property. `text`
// loses `.Path`. `directory.list : list<path>`. `read %url%` fetches over http.
public class Stage3_PathDemolitionTests
{
    private static (global::app.@this app, global::app.actor.context.@this context, string dir) MakeApp()
    {
        var dir = Path.Combine(Path.GetTempPath(), "plang_st3pd_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var app = global::PLang.Tests.TestApp.Plain(dir);
        return (app, app.User.Context, dir);
    }

    private static async Task Grant(global::app.actor.context.@this context, string url)
    {
        var perm = new global::app.type.item.permission.@this(
            "User", new HttpPath(url, context).Absolute,
            global::app.type.item.permission.@this.AllVerbs,
            global::app.type.item.permission.Match.Exact);
        await context.Actor!.Permission.Add(new global::app.data.@this<global::app.type.item.permission.@this>("", perm, context: context), persist: true);
    }

    private static async Task<Data> Read(global::app.actor.context.@this context, global::app.type.item.path.@this p)
    {
        var action = new global::app.module.file.Read(context) { Path = new global::app.data.@this<global::app.type.item.path.@this>("", p),
        };
        var result = await action.Run();
        await result.IsSuccess();
        return result;
    }

    private static async Task<string> SerializePlang(global::app.@this app, Data data)
    {
        // A Data writes itself via Data.Output through the serializer's async path —
        // NOT JsonSerializer.Serialize (the Wire converter is read-only and throws).
        var plang = (global::app.channel.serializer.plang.@this)
            app.User.Channel.Serializers.GetByMimeType("application/plang");
        using var ms = new System.IO.MemoryStream();
        await plang.SerializeAsync(ms, data, global::app.View.Out);
        return System.Text.Encoding.UTF8.GetString(ms.ToArray());
    }

    [Test]
    public async Task Path_NoLongerCarriesContent_NoSourceField()
    {
        // path.@this has no Content, no Source — content moved to file
        var t = typeof(global::app.type.item.path.@this);
        await Assert.That(t.GetProperty("Content")).IsNull();
        await Assert.That(t.GetProperty("Source")).IsNull();
    }

    [Test]
    public async Task PathWrite_EmitsPrivateLocation_AsTyped()
    {
        // path.Write(IWriter) emits the as-typed location string verbatim
        foreach (var loc in new[] { "//file.txt", "/file.txt", "test/try.txt", "c:/my/path.txt" })
        {
            var p = new global::app.type.item.path.file.@this(loc, global::PLang.Tests.TestApp.SharedContext) { Raw = loc };
            using var ms = new MemoryStream();
            using var jw = new Utf8JsonWriter(ms);
            p.Write(new global::app.channel.serializer.json.Writer(jw));
            jw.Flush();
            await Assert.That(Encoding.UTF8.GetString(ms.ToArray())).IsEqualTo($"\"{loc}\"").Because(loc);
        }
    }

    [Test]
    public async Task PathToString_LocationOnly_NeverContentFirst()
    {
        // ToString returns the location string only; Content is gone entirely
        var p = new global::app.type.item.path.file.@this("/some/file.json", global::PLang.Tests.TestApp.SharedContext) { Raw = "/some/file.json" };
        await Assert.That(p.ToString()).IsEqualTo("/some/file.json");
    }

    [Test]
    public async Task PathBacking_RenamedToLocation_Private()
    {
        // `_absolutePath` no longer exists on path.@this; `_location` is private
        var t = typeof(global::app.type.item.path.@this);
        var all = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
        await Assert.That(t.GetField("_absolutePath", all)).IsNull();
        var loc = t.GetField("_location", all);
        await Assert.That(loc).IsNotNull();
        await Assert.That(loc!.IsPrivate).IsTrue();
    }

    [Test]
    public async Task PathBangAbsolute_DerivedProjection_GatedAndUnserialised()
    {
        // the resolved absolute stays OFF the wire — it leaks the install root
        var (app, context, dir) = MakeApp();
        await using var _ = app;
        var p = global::app.type.item.path.@this.Resolve("note.txt", context);
        var data = new Data("p", p, context: context);
        var json = await SerializePlang(app, data);
        await Assert.That(json).DoesNotContain(dir);
        // the projection itself is still derivable on the property plane
        var abs = await data.Get("!absolute");
        await Assert.That(abs.Peek()?.ToString()).Contains("note.txt");
    }

    [Test]
    public async Task PathBangExtension_Derived_Serialised()
    {
        // %path!extension% derives from the location; the wire form (the
        // location string) carries it
        var (app, context, _) = MakeApp();
        await using var __ = app;
        var p = global::app.type.item.path.@this.Resolve("docs/readme.md", context);
        var data = new Data("p", p, context: context);
        var ext = await data.Get("!extension");
        await Assert.That(ext.Peek()?.ToString()).IsEqualTo("md");
        var json = await SerializePlang(app, data);
        await Assert.That(json).Contains(".md");
    }

    [Test]
    public async Task TextType_HasNoPath_Property()
    {
        // text stays pure content — no Path member; path lives on file/url
        var t = typeof(global::app.type.item.text.@this);
        await Assert.That(t.GetProperty("Path")).IsNull();
        await Assert.That(t.GetField("Path")).IsNull();
    }

    [Test]
    public async Task Directory_List_IsListOfPath_NotFiles()
    {
        // dir.@this.List is typed list<path> (locations of children), not list<file>
        var (app, context, dir) = MakeApp();
        await using var _ = app;
        Directory.CreateDirectory(Path.Combine(dir, "docs"));
        File.WriteAllText(Path.Combine(dir, "docs", "a.txt"), "alpha");
        File.WriteAllText(Path.Combine(dir, "docs", "b.txt"), "beta");

        var result = await Read(context, new PLangFilePath(Path.Combine(dir, "docs"), context) {});
        var directory = (global::app.type.item.directory.@this)result.Peek()!;
        var listing = await directory.List();
        await Assert.That(listing).IsTypeOf<global::app.type.item.list.@this<global::app.type.item.path.@this>>();
        await Assert.That(listing.Count).IsEqualTo(2);
        foreach (var entry in listing.Items)
            await Assert.That(entry.Peek()).IsAssignableTo<global::app.type.item.path.@this>();
    }

    [Test]
    public async Task Directory_WriteOut_EmitsFlatListingOfLocations_NotContents()
    {
        var (app, context, dir) = MakeApp();
        await using var _ = app;
        Directory.CreateDirectory(Path.Combine(dir, "docs"));
        File.WriteAllText(Path.Combine(dir, "docs", "a.txt"), "TOP-SECRET-CONTENT");

        var result = await Read(context, new PLangFilePath(Path.Combine(dir, "docs"), context) {});
        var json = await SerializePlang(app, result);
        await Assert.That(json).Contains("a.txt");
        await Assert.That(json).DoesNotContain("TOP-SECRET-CONTENT");
    }

    [Test]
    public async Task ReadUrl_Fetches_OverHttp()
    {
        using var server = new HttpTestServer();
        var (app, context, _) = MakeApp();
        await using var __ = app;
        var url = server.NewResourceUrl();
        await Grant(context, url);
        await new HttpPath(url, context).WriteText("remote body");

        var result = await Read(context, new HttpPath(url, context));
        await Assert.That(result.Type!.Name).IsEqualTo("url");
        // scalar use fetches through the HttpPath (the scheme owns the I/O);
        // no extension on the resource → raw bytes
        var content = await result.Value();
        var text = content is global::app.type.item.binary.@this b ? Encoding.UTF8.GetString(b.Value) : content?.ToString();
        await Assert.That(text).Contains("remote body");
    }

    [Test]
    public async Task UrlBangHost_AndBangPath_DoNotFetch_MaterializeCountZero()
    {
        var (app, context, _) = MakeApp();
        await using var __ = app;
        var result = await Read(context, new HttpPath("http://example.com/data.json", context));
        var reference = (global::app.type.item.url.@this)result.Peek()!;

        var host = await result.Get("!host");
        await Assert.That(host.Peek()?.ToString()).IsEqualTo("example.com");
        var pathChild = await result.Get("!path");
        await Assert.That(pathChild.Peek()).IsNotNull();
        await Assert.That(reference.IsLoaded).IsFalse();
    }

    [Test]
    public async Task FileWriteOut_UnNarrowed_EmitsRawContentBytes()
    {
        // bare-scalar contract — write-out pre-narrow emits the raw content
        var (app, context, dir) = MakeApp();
        await using var _ = app;
        File.WriteAllText(Path.Combine(dir, "note.txt"), "the raw note");

        var result = await Read(context, new PLangFilePath(Path.Combine(dir, "note.txt"), context) {});
        var json = await SerializePlang(app, result);
        await Assert.That(json).Contains("the raw note");
        // serialization loads but never narrows — still the file headline
        await Assert.That(result.Type!.Name).IsEqualTo("file");
    }

    [Test]
    public async Task FileWriteOut_AfterNarrow_Reserialises_FromTypedContent()
    {
        var (app, context, dir) = MakeApp();
        await using var _ = app;
        File.WriteAllText(Path.Combine(dir, "config.json"), "{\"port\":8080}");

        var result = await Read(context, new PLangFilePath(Path.Combine(dir, "config.json"), context) {});
        await result.Get("port");                  // examination → narrow
        await Assert.That(result.Type!.Name).IsEqualTo("dict");
        var json = await SerializePlang(app, result);
        await Assert.That(json).Contains("8080");
        await Assert.That(json).Contains("port");
    }

    [Test]
    public async Task PathWriteOut_LocationOnly_NotContent()
    {
        // a `path` value has one face — the renderer entry emits the location string
        var p = new global::app.type.item.path.file.@this("docs/readme.md", global::PLang.Tests.TestApp.SharedContext) { Raw = "docs/readme.md" };
        using var ms = new MemoryStream();
        using var jw = new Utf8JsonWriter(ms);
        global::app.type.item.path.serializer.Default.Write(p, new global::app.channel.serializer.json.Writer(jw));
        jw.Flush();
        await Assert.That(Encoding.UTF8.GetString(ms.ToArray())).IsEqualTo("\"docs/readme.md\"");
    }

    [Test]
    public async Task SetDotField_RebindsFreshDict_InvalidatesPassthrough()
    {
        // `set %config.y% = 1` — the dotted write examines (narrows) and the
        // mutated dict is what write-out reserialises afterwards
        var (app, context, dir) = MakeApp();
        await using var _ = app;
        File.WriteAllText(Path.Combine(dir, "config.json"), "{\"port\":8080}");
        var result = await Read(context, new PLangFilePath(Path.Combine(dir, "config.json"), context) {});
        await context.Variable.Set("config", result);

        await context.Variable.Set("config.y", 1);

        var bound = await context.Variable.Get("config");
        await Assert.That(bound!.Type!.Is("dict")).IsTrue();
        var y = await bound.Get("y");
        await Assert.That((await y.Value())?.ToString()).IsEqualTo("1");
        var json = await SerializePlang(app, bound!);
        await Assert.That(json).Contains("8080");
        await Assert.That(json).Contains("\"y\"");
    }
}
