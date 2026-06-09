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
        // reflection: path.@this has no Content, no Source — content moved to file
        Assert.Fail("Not implemented");
        await Task.CompletedTask;
    }

    [Test]
    public async Task PathWrite_EmitsPrivateLocation_AsTyped()
    {
        // path.Write(IWriter) emits the as-typed location string verbatim: "//", "/", relative, "c:/", "http://"
        Assert.Fail("Not implemented");
        await Task.CompletedTask;
    }

    [Test]
    public async Task PathToString_LocationOnly_NeverContentFirst()
    {
        // ToString returns the location string only; no fallback to Content?.ToString()
        Assert.Fail("Not implemented");
        await Task.CompletedTask;
    }

    [Test]
    public async Task PathBacking_RenamedToLocation_Private()
    {
        // reflection: protected/public `_absolutePath` no longer exists on path.@this; `_location` is private
        Assert.Fail("Not implemented");
        await Task.CompletedTask;
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
        // reflection: text.@this has no Path member — text stays pure content; path lives on file/url
        Assert.Fail("Not implemented");
        await Task.CompletedTask;
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
        // a `path` value has one face — `write out %path%` emits the as-typed location string
        Assert.Fail("Not implemented");
        await Task.CompletedTask;
    }

    [Test]
    public async Task SetDotField_RebindsFreshDict_InvalidatesPassthrough()
    {
        // `set %config.y% = 1` rebinds %config% to a fresh dict — the only thing that changes write-out across branches
        Assert.Fail("Not implemented");
        await Task.CompletedTask;
    }
}
