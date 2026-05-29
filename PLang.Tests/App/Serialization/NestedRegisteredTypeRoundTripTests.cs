namespace PLang.Tests.App.Serialization;

// plang-types — Stage 2
// A registered-type value nested inside another (e.g., a Data containing an Image whose
// Path is a registered path) round-trips through the writer — each registered node hits
// the dispatch independently, no Normalize recursion bug.

public class NestedRegisteredTypeRoundTripTests
{
    [Test] public async Task Image_WithPathFacet_BothNodesDispatched_OnWire()
        => throw new global::System.NotImplementedException();

    [Test] public async Task RegisteredValueInsideList_EachElementDispatched()
        => throw new global::System.NotImplementedException();

    [Test] public async Task RegisteredValueInsideUnregistered_OuterReflects_InnerDispatches()
        => throw new global::System.NotImplementedException();

    [Test] public async Task DeepNesting_NoStackOverflow_RespectsDepthLimit()
        => throw new global::System.NotImplementedException();
}
