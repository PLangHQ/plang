namespace PLang.Tests.App.TypedReturnsTests;

// Stage 4 — (type) hint Compile rule, multi-segment GetByExtension, precedence.
// Architect: .bot/typed-action-returns/architect/stages.md (Stage 4, items 4-6)
// Plan: .bot/typed-action-returns/architect/plan.md (A.5)

public class Stage4_TypeHintPrecedenceTests
{
    [Test]
    public async Task SerializersGetByExtension_SingleSegment_Resolves()
        // Existing behaviour: GetByExtension(".json") resolves the JsonSerializer.
        => Assert.Fail("Not implemented");

    [Test]
    public async Task SerializersGetByExtension_MultiSegment_Resolves()
        // New behaviour: GetByExtension(".junit.xml") walks multi-segment extension; resolves a registered serializer keyed on ".junit.xml".
        => Assert.Fail("Not implemented");

    [Test]
    public async Task SerializersGetByExtension_MultiSegment_FallsBackToSingleSegment()
        // If no ".junit.xml" registration exists but ".xml" does, walk falls back to single-segment.
        => Assert.Fail("Not implemented");

    [Test]
    public async Task CompileLlm_Kernel_ContainsTypeHintRule()
        // The Compile.llm cross-cutting kernel teaching includes the rule documented in Stage 4 item 4 (variable refs followed by (type)).
        => Assert.Fail("Not implemented");

    [Test]
    public async Task BuilderValidate_UserHintWinsOverBuildInference()
        // Compile emits Type="json" on terminal variable.set; file.read.Build() would return "csv".
        // Validate keeps "json"; does not overwrite with Build()'s inference.
        => Assert.Fail("Not implemented");

    [Test]
    public async Task BuilderValidate_BuildInferenceWinsOverDefaultObject()
        // Compile emits no explicit type (slot defaults to "object"); Build() returns "csv" → terminal variable.set Type becomes "csv".
        => Assert.Fail("Not implemented");

    [Test]
    public async Task BuilderValidate_DistinguishesExplicitObject_FromDefaultObject()
        // If the developer explicitly hints (object), validate does NOT overwrite even though Build() would infer.
        // (Edge case: a step LLM-emitting type="object" must be distinguishable from "no hint given".)
        => Assert.Fail("Not implemented");

    [Test]
    public async Task OutputAsk_Build_ReturnsBareOk_DefersToHint()
        // output.ask.Build() returns Data.Ok() always; the (type) hint on the write target is the only source of typing.
        => Assert.Fail("Not implemented");
}
