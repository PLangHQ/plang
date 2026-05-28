using app.data;

namespace PLang.Tests.App.DataTests;

// data-normalize — Stage 2 failure paths
// Normalize is bounded: visited-set (reference cycles) + max-depth cap (deep but acyclic trees
// past the limit). Both raise typed errors, hard, at serialize-time — no silent truncation.
// Max-depth suggested 128 (mirrors MaxRehydrationDepth in this.Transport.cs).

public class NormalizeCycleAndDepthTests
{
    private sealed class Node
    {
        [global::app.Out] public string? Tag { get; set; }
        [global::app.Out] public Node? Next { get; set; }
    }

    private sealed class Throws
    {
        [global::app.Out] public string Boom => throw new System.InvalidOperationException("getter failed");
    }

    [Test] public async Task Normalize_DirectSelfReference_ThrowsCycleDetectedError()
    {
        var n = new Node { Tag = "a" };
        n.Next = n;
        var ex = await Assert.ThrowsAsync<NormalizeException>(async () =>
        {
            new Data("", n).Normalize();
            await Task.CompletedTask;
        });
        await Assert.That(ex!.Key).IsEqualTo("NormalizeCycleDetected");
    }

    [Test] public async Task Normalize_IndirectCycle_A_to_B_to_A_ThrowsCycleDetectedError()
    {
        var a = new Node { Tag = "a" };
        var b = new Node { Tag = "b" };
        a.Next = b;
        b.Next = a;
        var ex = await Assert.ThrowsAsync<NormalizeException>(async () =>
        {
            new Data("", a).Normalize();
            await Task.CompletedTask;
        });
        await Assert.That(ex!.Key).IsEqualTo("NormalizeCycleDetected");
    }

    [Test] public async Task Normalize_DeepButAcyclicTree_UnderCap_Succeeds()
    {
        // 100 nodes deep — under the 128 cap.
        Node head = new() { Tag = "head" };
        var cur = head;
        for (int i = 0; i < 100; i++)
        {
            cur.Next = new Node { Tag = $"n{i}" };
            cur = cur.Next;
        }
        var result = new Data("", head).Normalize();
        await Assert.That(result).IsTypeOf<List<Data>>();
    }

    [Test] public async Task Normalize_DepthExceedsCap_ThrowsMaxDepthExceededError()
    {
        // 200 nodes deep — past the 128 cap.
        Node head = new() { Tag = "head" };
        var cur = head;
        for (int i = 0; i < 200; i++)
        {
            cur.Next = new Node { Tag = $"n{i}" };
            cur = cur.Next;
        }
        var ex = await Assert.ThrowsAsync<NormalizeException>(async () =>
        {
            new Data("", head).Normalize();
            await Task.CompletedTask;
        });
        await Assert.That(ex!.Key).IsEqualTo("NormalizeMaxDepthExceeded");
    }

    [Test] public async Task Normalize_GetterThrows_ExceptionWrappedWithTypeAndPropertyContext()
    {
        var t = new Throws();
        var ex = await Assert.ThrowsAsync<NormalizeException>(async () =>
        {
            new Data("", t).Normalize();
            await Task.CompletedTask;
        });
        await Assert.That(ex!.Key).IsEqualTo("NormalizeGetterThrew");
        await Assert.That(ex.Message).Contains("Throws");
        await Assert.That(ex.Message).Contains("Boom");
    }
}
