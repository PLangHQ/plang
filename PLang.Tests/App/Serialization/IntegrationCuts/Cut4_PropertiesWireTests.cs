namespace PLang.Tests.App.Serialization.IntegrationCuts;

// data-serialize-cleanup — Integration Cut 4: Properties wire shape + navigation.
//
// Setup: memory channel, Mime = "application/plang", actor context wired.
// Capture: d = Data.Ok("Hello!", name: "response");
//          d.Properties["cost"] = 100; d.Properties["model"] = "claude-opus-4-7";
//          await channel.WriteAsync(d); then ReadAsync.
//
// Proves end-to-end:
//   - Properties round-trip through the nested wire shape
//   - Property keys are unconstrained (no reserved-key throw)
//   - canonicalization binds Properties
//   - `!` navigation reaches the correct store (sibling .goal test asserts %response!cost%)

public class Cut4_PropertiesWireTests
{
    // Five reserved top-level fields including a nested `properties` object.
    [Test] public async Task Cut4_WireJson_HasFiveTopLevelFields_IncludingNestedProperties()
    { await Task.CompletedTask; Assert.Fail("Not implemented"); }

    // properties is nested: { "cost": 100, "model": "claude-opus-4-7" }. No top-level cost/model.
    [Test] public async Task Cut4_PropertiesObject_IsNested_NotFlattenedToRoot()
    { await Task.CompletedTask; Assert.Fail("Not implemented"); }

    // read.Properties["cost"] == 100L (JSON int promotion); ["model"] == string verbatim.
    [Test] public async Task Cut4_ReadBack_PropertiesValuesPreserved_IntPromotedToLong()
    { await Task.CompletedTask; Assert.Fail("Not implemented"); }

    // Mutating properties.cost in the wire JSON (re-encode without re-signing) fails verify.
    [Test] public async Task Cut4_TamperingPropertyValue_FailsOuterSignatureVerify()
    { await Task.CompletedTask; Assert.Fail("Not implemented"); }
}
