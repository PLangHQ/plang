namespace PLang.Tests.App.Serialization;

/// <summary>
/// A dict of nested typed entries (a plan {description, steps:[…]}) keeps its nested types
/// across a Store round-trip — the LLM-cache scenario. The value is read lazily (deferred
/// source), so nested types materialize through the async Value door, not the sync Peek door:
/// %plan.steps% resolves to a typed list (foreach sees it), not a raw dict.
/// </summary>
public class NestedTypedRoundTripTests
{
    [Test]
    public async Task PlanDict_StoreRoundTrip_KeepsNestedListType()
    {
        var app = global::PLang.Tests.TestApp.Create("/nest");
        var ctx = app.User.Context;
        var steps = new global::app.type.list.@this { Context = ctx }
            .Add(new global::app.data.@this("", new global::app.type.dict.@this(ctx).Set("index", 1L), context: ctx));
        var plan = new global::app.type.dict.@this(ctx)
            .Set("description", "a plan")
            .Set("steps", steps);

        var plang = new global::app.channel.serializer.plang.@this(ctx);
        using var ms = new System.IO.MemoryStream();
        await plang.SerializeAsync(ms, ctx.Ok(plan), global::app.View.Store);
        ms.Position = 0;
        var back = await plang.DeserializeAsync(ms, global::app.View.Store);

        await back.IsSuccess();
        // Materialize through the async door — Peek returns the deferred source.
        var dict = (await back.Value()) as global::app.type.dict.@this;
        await Assert.That(dict).IsNotNull();
        await Assert.That(dict!.Get("steps")!.Type?.Name).IsEqualTo("list");
        await Assert.That(dict.Get("description")!.Type?.Name).IsEqualTo("text");
        // The nested list materializes as a real list.
        var stepsVal = await dict.Get("steps")!.Value();
        await Assert.That(stepsVal is global::app.type.list.@this).IsTrue();
    }
}
