namespace PLang.Tests.App.ScalarsAsNative;

// A duration or size born with no standard is a kind of its own (a span, a count) that writes in its asker's standard.
// No program names it, so it is never one of the standards a setting chooses from, and its text read back is that kind
// again.
public class UnnamedKindTests
{
    [Test]
    public async Task AnUnnamedDuration_IsASpan()
    {
        await using var app = new global::app.@this("/app").Testing();
        var context = app.actor.list.User.Context;
        var span = global::app.type.item.duration.@this.Create(System.TimeSpan.FromSeconds(90), context);

        await Assert.That(span is global::app.type.item.duration.span.@this).IsTrue();
        await Assert.That(span.Type.kind.Name).IsEqualTo("span");
    }

    [Test]
    public async Task AnUnnamedSize_IsACount()
    {
        await using var app = new global::app.@this("/app").Testing();
        var context = app.actor.list.User.Context;
        var size = global::app.type.item.size.@this.Create(100_000_000L, context);

        await Assert.That(size is global::app.type.item.size.count.@this).IsTrue();
        await Assert.That(size.Type.kind.Name).IsEqualTo("count");
    }

    [Test]
    public async Task TheDurationStandards_LeaveTheSpanOut()
    {
        await using var app = new global::app.@this("/app").Testing();
        var values = app.type.list[typeof(global::app.type.item.choice.@this<global::app.type.kind.@this<global::app.type.item.duration.@this>>)].Values;

        await Assert.That(values).Contains("short");
        await Assert.That(values).DoesNotContain("span");
    }

    [Test]
    public async Task TheSizeStandards_LeaveTheCountOut()
    {
        await using var app = new global::app.@this("/app").Testing();
        var values = app.type.list[typeof(global::app.type.item.choice.@this<global::app.type.kind.@this<global::app.type.item.size.@this>>)].Values;

        await Assert.That(values).Contains("iec");
        await Assert.That(values).DoesNotContain("count");
    }

    [Test]
    public async Task ASpanReadBack_IsASpan()
    {
        await using var app = new global::app.@this("/app").Testing();
        var context = app.actor.list.User.Context;
        var type = app.type.list[new global::app.type.@this("duration", "span"), context];
        var read = global::app.type.item.duration.span.@this.Create("1m30s", type, new global::app.data.@this("d", context: context));

        await Assert.That(read!.Value).IsEqualTo(System.TimeSpan.FromSeconds(90));
    }

    [Test]
    public async Task ACountReadBack_IsACount()
    {
        await using var app = new global::app.@this("/app").Testing();
        var context = app.actor.list.User.Context;
        var type = app.type.list[new global::app.type.@this("size", "count"), context];
        var read = global::app.type.item.size.count.@this.Create("100 MB", type, new global::app.data.@this("s", context: context));

        await Assert.That(read!.Value).IsEqualTo(100_000_000L);
    }
}
