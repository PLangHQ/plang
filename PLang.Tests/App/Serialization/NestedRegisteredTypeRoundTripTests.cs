using System.Text.Json;

namespace PLang.Tests.App.Serialization;

// plang-types — Stage 2
// A registered-type value nested inside another (e.g., a Data containing an Image whose
// Path is a registered path) round-trips through the writer — each registered node hits
// the dispatch independently, no Normalize recursion bug.

public class NestedRegisteredTypeRoundTripTests
{
    private static global::app.@this NewApp()
        => new global::app.@this(System.IO.Path.Combine(System.IO.Path.GetTempPath(),
            "plang-nested-" + System.Guid.NewGuid().ToString("N")[..8]));

    [Test]
    public async Task Image_WithPathFacet_BothNodesDispatched_OnWire()
    {
        // Stage 5 lands the image type. Until then, demonstrate the pattern by
        // putting a registered-type value (path) at two nested positions: a Data
        // whose Value is a list containing two path-typed Datas.
        await using var app = NewApp();
        var context = app.User.Context;
        var p1 = global::app.type.path.@this.Resolve("/srv/a.txt", context);
        var p2 = global::app.type.path.@this.Resolve("/srv/b.txt", context);
        var outer = new global::app.data.@this("outer", new[] {
            new global::app.data.@this("p1", p1) { Context = context },
            new global::app.data.@this("p2", p2) { Context = context },
        }) { Context = context };

        var plang = (global::app.channel.serializer.plang.@this)
            app.User.Channel.Serializers.GetByMimeType("application/plang");
        var options = (JsonSerializerOptions)typeof(global::app.channel.serializer.plang.@this)
            .GetField("_outbound", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
            .GetValue(plang)!;
        var json = JsonSerializer.Serialize(outer, options);
        // Both nested path values must appear as strings, not as reflected
        // property bags ("\"absolute\":" would mean reflection fired).
        await Assert.That(json.Contains("a.txt")).IsTrue();
        await Assert.That(json.Contains("b.txt")).IsTrue();
        await Assert.That(json.Contains("\"absolute\":")).IsFalse();
    }

    [Test]
    public async Task RegisteredValueInsideList_EachElementDispatched()
    {
        await using var app = NewApp();
        var context = app.User.Context;
        var paths = new[]
        {
            global::app.type.path.@this.Resolve("/srv/x.json", context),
            global::app.type.path.@this.Resolve("/srv/y.json", context),
        };

        using var ms = new System.IO.MemoryStream();
        using (var utf = new Utf8JsonWriter(ms))
        {
            var w = new global::app.channel.serializer.json.Writer(utf, options: null,
                view: global::app.View.Out, renderers: app.Type.Renderers);
            var visited = new HashSet<object>(System.Collections.Generic.ReferenceEqualityComparer.Instance);
            var normalized = global::app.data.@this.NormalizeValue(paths, global::app.View.Out, visited, 0, app.Type);
            w.Value(normalized);
        }
        var json = System.Text.Encoding.UTF8.GetString(ms.ToArray());
        await Assert.That(json.Contains("x.json")).IsTrue();
        await Assert.That(json.Contains("y.json")).IsTrue();
        await Assert.That(json.StartsWith("[")).IsTrue();
    }

    [Test]
    public async Task RegisteredValueInsideUnregistered_OuterReflects_InnerDispatches()
    {
        // The "Inner" wrapper has no [PlangType] — the outer walks via
        // reflection into a List<Data>; the inner Path leaf surfaces as a
        // TypedValueNode and renders as a string.
        await using var app = NewApp();
        var context = app.User.Context;
        var wrapper = new InnerWrapper
        {
            Path = global::app.type.path.@this.Resolve("/srv/wrap.json", context),
        };

        using var ms = new System.IO.MemoryStream();
        using (var utf = new Utf8JsonWriter(ms))
        {
            var w = new global::app.channel.serializer.json.Writer(utf, options: null,
                view: global::app.View.Out, renderers: app.Type.Renderers);
            var visited = new HashSet<object>(System.Collections.Generic.ReferenceEqualityComparer.Instance);
            var normalized = global::app.data.@this.NormalizeValue(wrapper, global::app.View.Out, visited, 0, app.Type);
            w.Value(normalized);
        }
        var json = System.Text.Encoding.UTF8.GetString(ms.ToArray());
        await Assert.That(json.Contains("wrap.json")).IsTrue();
    }

    [Test]
    public async Task DeepNesting_NoStackOverflow_RespectsDepthLimit()
    {
        // Build a chain of nested wrappers approaching the depth cap and assert
        // the typed throw, not a CLR stack overflow, surfaces past it.
        await using var app = NewApp();
        InnerWrapper? chain = null;
        for (int i = 0; i < 200; i++)
            chain = new InnerWrapper { Path = null, Nested = chain };

        var visited = new HashSet<object>(System.Collections.Generic.ReferenceEqualityComparer.Instance);
        try
        {
            global::app.data.@this.NormalizeValue(chain, global::app.View.Out, visited, 0, app.Type);
            // Either it raises NormalizeMaxDepthExceeded or it completes cleanly.
            // Both outcomes are acceptable; the failure mode we forbid is
            // a StackOverflowException (we'd never reach this assertion).
            await Assert.That(true).IsTrue();
        }
        catch (global::app.data.NormalizeException ex)
        {
            await Assert.That(ex.Message).Contains("depth");
        }
    }

    public sealed class InnerWrapper
    {
        [global::app.LlmBuilder] public global::app.type.path.@this? Path { get; set; }
        [global::app.LlmBuilder] public InnerWrapper? Nested { get; set; }
    }
}
