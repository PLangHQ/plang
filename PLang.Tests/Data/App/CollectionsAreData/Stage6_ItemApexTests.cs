namespace PLang.Tests.App.CollectionsAreData;

// Stage 6 — `item` apex. type.Is ancestry already walks the lattice transitively;
// this stage only registers item as the apex (≈ C# object) and, if needed, adds a
// name-string Is(string) overload so the PLang `if %x% is dict` surface resolves
// from a type name without minting a comparison type.
public class Stage6_ItemApexTests : System.IAsyncDisposable
{
    private readonly global::app.@this app = global::PLang.Tests.TestApp.Create("/tmp/Stage6ItemApex-" + System.Guid.NewGuid().ToString("N")[..6]);
    public async System.Threading.Tasks.ValueTask DisposeAsync() => await app.DisposeAsync();

    [Test]
    public async Task TypeIs_ItemApex_TrueForAnyValue()
    {
        // data.Type.Is("item") is true for every value (number, dict, list, bool, text).
        // Item is the top of the lattice; nothing falls outside it.
        await Assert.That(app.Data("", 1L).Type.Is("item")).IsTrue();
        await Assert.That(app.Data("", "x").Type.Is("item")).IsTrue();
        await Assert.That(app.Data("", true).Type.Is("item")).IsTrue();
        await Assert.That(app.Data("", new global::app.type.dict.@this(app.User.Context)).Type.Is("item")).IsTrue();
        await Assert.That(app.Data("", new global::app.type.list.@this(app.User.Context)).Type.Is("item")).IsTrue();

        // The is-query reaches down the lattice from item to the concrete type:
        // `is dict` is true for a dict, false for a list; `is number` for a literal.
        await Assert.That(app.Data("", new global::app.type.dict.@this(app.User.Context)).Type.Is("dict")).IsTrue();
        await Assert.That(app.Data("", new global::app.type.list.@this(app.User.Context)).Type.Is("dict")).IsFalse();
        await Assert.That(app.Data("", 1L).Type.Is("number")).IsTrue();
    }
}
