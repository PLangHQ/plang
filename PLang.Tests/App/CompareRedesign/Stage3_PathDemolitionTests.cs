using System.Reflection;
using System.Text;
using System.Text.Json;

namespace PLang.Tests.App.CompareRedesign;

// Stage 3 — `path` demolition. Drop `Content`/`Source`; backing
// `_absolutePath` → private `_location` (the as-typed string verbatim);
// `absolute`/`relative`/`extension` are derived (cached). `path.Write` reads
// the private `_location` directly — no public `value` property. `text`
// loses `.Path`. `directory.list : list<path>`. `read %url%` fetches over http.
public class Stage3_PathDemolitionTests
{
    [Test]
    public async Task Path_NoLongerCarriesContent_NoSourceField()
    {
        // path.@this has no Content, no Source — content moved to file
        var t = typeof(global::app.type.path.@this);
        await Assert.That(t.GetProperty("Content")).IsNull();
        await Assert.That(t.GetProperty("Source")).IsNull();
    }

    [Test]
    public async Task PathWrite_EmitsPrivateLocation_AsTyped()
    {
        // path.Write(IWriter) emits the as-typed location string verbatim
        foreach (var loc in new[] { "//file.txt", "/file.txt", "test/try.txt", "c:/my/path.txt" })
        {
            var p = new global::app.type.path.file.@this(loc) { Raw = loc };
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
        var p = new global::app.type.path.file.@this("/some/file.json") { Raw = "/some/file.json" };
        await Assert.That(p.ToString()).IsEqualTo("/some/file.json");
    }

    [Test]
    public async Task PathBacking_RenamedToLocation_Private()
    {
        // `_absolutePath` no longer exists on path.@this; `_location` is private
        var t = typeof(global::app.type.path.@this);
        var all = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
        await Assert.That(t.GetField("_absolutePath", all)).IsNull();
        var loc = t.GetField("_location", all);
        await Assert.That(loc).IsNotNull();
        await Assert.That(loc!.IsPrivate).IsTrue();
    }

    [Test]
    public async Task PathBangAbsolute_DerivedProjection_GatedAndUnserialised()
    {
        // %path!absolute% is gated (Authorize) and excluded from Write — it leaks the install root
        Assert.Fail("Not implemented");
        await Task.CompletedTask;
    }

    [Test]
    public async Task PathBangExtension_Derived_Serialised()
    {
        // %path!extension% is derived from _location and IS serialised
        Assert.Fail("Not implemented");
        await Task.CompletedTask;
    }

    [Test]
    public async Task TextType_HasNoPath_Property()
    {
        // text stays pure content — no Path member; path lives on file/url
        var t = typeof(global::app.type.text.@this);
        await Assert.That(t.GetProperty("Path")).IsNull();
        await Assert.That(t.GetField("Path")).IsNull();
    }

    [Test]
    public async Task Directory_List_IsListOfPath_NotFiles()
    {
        // dir.@this.List is typed list<path> (locations of children), not list<file> (content-bearing)
        Assert.Fail("Not implemented");
        await Task.CompletedTask;
    }

    [Test]
    public async Task Directory_WriteOut_EmitsFlatListingOfLocations_NotContents()
    {
        // directory.Write serialises the list<path> → flat listing of location strings; recursive tree is explicit
        Assert.Fail("Not implemented");
        await Task.CompletedTask;
    }

    [Test]
    public async Task ReadUrl_Fetches_OverHttp()
    {
        // url.@this materialises content via HttpPath read (the scheme owns the I/O)
        Assert.Fail("Not implemented");
        await Task.CompletedTask;
    }

    [Test]
    public async Task UrlBangHost_AndBangPath_DoNotFetch_MaterializeCountZero()
    {
        // %url!host% / %url!path% live on the location; never trigger fetch
        Assert.Fail("Not implemented");
        await Task.CompletedTask;
    }

    [Test]
    public async Task FileWriteOut_UnNarrowed_EmitsRawContentBytes()
    {
        // bare-scalar contract — `write out %file%` pre-narrow emits the raw bytes (passthrough)
        Assert.Fail("Not implemented");
        await Task.CompletedTask;
    }

    [Test]
    public async Task FileWriteOut_AfterNarrow_Reserialises_FromTypedContent()
    {
        // post-narrow the raw is gone (single-storage); write-out reserialises the parsed item
        Assert.Fail("Not implemented");
        await Task.CompletedTask;
    }

    [Test]
    public async Task PathWriteOut_LocationOnly_NotContent()
    {
        // a `path` value has one face — the renderer entry emits the location string
        var p = new global::app.type.path.file.@this("docs/readme.md") { Raw = "docs/readme.md" };
        using var ms = new MemoryStream();
        using var jw = new Utf8JsonWriter(ms);
        global::app.type.path.serializer.Default.Write(p, new global::app.channel.serializer.json.Writer(jw));
        jw.Flush();
        await Assert.That(Encoding.UTF8.GetString(ms.ToArray())).IsEqualTo("\"docs/readme.md\"");
    }

    [Test]
    public async Task SetDotField_RebindsFreshDict_InvalidatesPassthrough()
    {
        // `set %config.y% = 1` rebinds %config% to a fresh dict — the only thing that changes write-out across branches
        Assert.Fail("Not implemented");
        await Task.CompletedTask;
    }
}
