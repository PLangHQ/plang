namespace PLang.Tests.App.Serialization;

// data-serialize-cleanup — Stage 2
// Json (the JSON-engine custodian) gains composition extensions so callers compose
// with it instead of duplicating JsonSerializerOptions blocks.
// Coverage matrix rows 2.4, 2.5. ForInbound symmetry added by test-designer because
// the new-surfaces inventory lists it but no matrix row pins it.

public class JsonCompositionTests
{
    // 2.4 — WithConverter returns a new Json instance with the converter added.
    [Test] public async Task Json_WithConverter_ReturnsNewInstance_WithConverterRegistered()
    { await Task.CompletedTask; Assert.Fail("Not implemented"); }

    // 2.4b — WithConverter does not mutate the source instance.
    [Test] public async Task Json_WithConverter_DoesNotMutateOriginalInstance()
    { await Task.CompletedTask; Assert.Fail("Not implemented"); }

    // 2.5 — WithModifier returns a new Json instance with the modifier added to the resolver.
    [Test] public async Task Json_WithModifier_ReturnsNewInstance_WithModifierOnResolver()
    { await Task.CompletedTask; Assert.Fail("Not implemented"); }

    // 2.5b — WithModifier does not mutate the source instance.
    [Test] public async Task Json_WithModifier_DoesNotMutateOriginalInstance()
    { await Task.CompletedTask; Assert.Fail("Not implemented"); }

    // Test-designer addition — Json.ForInbound applies Transport.ForInbound, symmetric
    // to existing Json.ForView. Listed in new-surfaces, not pinned by matrix.
    [Test] public async Task Json_ForInbound_AppliesTransportForInboundModifier()
    { await Task.CompletedTask; Assert.Fail("Not implemented"); }

    [Test] public async Task Json_ForView_StillApplies_TransportForViewModifier()
    { await Task.CompletedTask; Assert.Fail("Not implemented"); }
}
