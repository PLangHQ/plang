namespace PLang.Tests.App.DataTests;

// data-normalize — Stage 2 failure paths
// Normalize is bounded: visited-set (reference cycles) + max-depth cap (deep but acyclic trees
// past the limit). Both raise typed errors, hard, at serialize-time — no silent truncation.
// Max-depth suggested 128 (mirrors MaxRehydrationDepth in this.Transport.cs).

public class NormalizeCycleAndDepthTests
{
    [Test] public async Task Normalize_DirectSelfReference_ThrowsCycleDetectedError()
        { Assert.Fail("Not implemented"); await Task.CompletedTask; }

    [Test] public async Task Normalize_IndirectCycle_A_to_B_to_A_ThrowsCycleDetectedError()
        { Assert.Fail("Not implemented"); await Task.CompletedTask; }

    [Test] public async Task Normalize_DeepButAcyclicTree_UnderCap_Succeeds()
        { Assert.Fail("Not implemented"); await Task.CompletedTask; }

    [Test] public async Task Normalize_DepthExceedsCap_ThrowsMaxDepthExceededError()
        { Assert.Fail("Not implemented"); await Task.CompletedTask; }

    [Test] public async Task Normalize_GetterThrows_ExceptionWrappedWithTypeAndPropertyContext()
        { Assert.Fail("Not implemented"); await Task.CompletedTask; }
}
