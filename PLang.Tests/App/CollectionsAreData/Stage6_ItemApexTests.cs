namespace PLang.Tests.App.CollectionsAreData;

// Stage 6 — `item` apex. type.Is ancestry already walks the lattice transitively;
// this stage only registers item as the apex (≈ C# object) and, if needed, adds a
// name-string Is(string) overload so the PLang `if %x% is dict` surface resolves
// from a type name without minting a comparison type.
public class Stage6_ItemApexTests
{
    [Test]
    public async Task TypeIs_ItemApex_TrueForAnyValue()
    {
        // data.Type.Is("item") is true for every value (number, dict, list, path, image, …).
        // Item is the top of the lattice; nothing falls outside it.
        Assert.Fail("Not implemented");
        await Task.CompletedTask;
    }
}
