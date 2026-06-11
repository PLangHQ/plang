using System.Reflection;
using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using data = global::app.data.@this;
using type = global::app.type.@this;

namespace PLang.Tests.App.LazyDeserialize.LazyDataTests;

// `_raw` stays authoritative until a mutation; a mutation invalidates it.
// If anything cleared `_raw` on a read, verbatim passthrough + signing break.
public class MutationInvalidatesRawTests
{
    private static global::app.@this NewApp()
        => new(System.IO.Path.Combine(System.IO.Path.GetTempPath(),
            "plang-mutraw-" + System.Guid.NewGuid().ToString("N")[..8]));

    private static data RawBacked(global::app.actor.context.@this ctx)
        => data.FromRaw("5", type.Create("number", "int", context: ctx), ctx, "n");

    [Test] public async Task SetValueDirect_InvalidatesRaw()
    {
        await using var app = NewApp();
        var d = RawBacked(app.User.Context);
        await Assert.That(d.HasRaw).IsTrue();
        // SetValueDirect is the private mutation seam (RehydrateNestedData etc.);
        // invoke it directly to pin that it clears _raw.
        var m = typeof(data).GetMethod("SetValueDirect", BindingFlags.NonPublic | BindingFlags.Instance);
        m!.Invoke(d, new object?[] { 99 });
        await Assert.That(d.HasRaw).IsFalse();
    }

    [Test] public async Task NavigationSet_InvalidatesRaw()
    {
        await using var app = NewApp();
        var d = RawBacked(app.User.Context);
        await Assert.That(d.HasRaw).IsTrue();
        // The public mutation (assigning Value) is what a navigation-set lands on.
        d.SetValue(99);
        await Assert.That(d.HasRaw).IsFalse();
    }

    // Independent #7 — end-to-end after a mutation, serialize renders from the
    // value (renderer), not the stale raw. (Wire raw-aware emission lands with
    // Stage 4's channel sources; pinned here once Wire.Write reads _raw.)
    [Test] public async Task AfterMutation_SerializeUsesRenderer_NotRaw()
    {
        var d = data.FromRaw("{\"port\":8080}", type.Create("object", "json"));
        d.Name = "cfg";
        d.SetValue("mutated");   // mutation clears _raw — raw is no longer authoritative

        var wire = (await global::app.channel.serializer.plang.@this.ContextLessFallback.Serialize(d).Value())!.Value;
        await Assert.That(wire).Contains("\"value\":\"mutated\""); // renderer output
        await Assert.That(wire).DoesNotContain("8080");           // not the stale raw
    }
}
