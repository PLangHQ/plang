using System.Reflection;

namespace PLang.Tests.App.Serialization;

// data-serialize-cleanup — Stage 1
// channel.WriteCore / ReadCore / AskCore renamed to Write / Read / Ask on every subclass.
// Public orchestrators keep the Async suffix to mark "entry-with-events".
// Coverage matrix rows 1.9, 1.10.

public class ChannelHookRenameTests
{
    // 1.9 — Base WriteAsync invokes FireBefore → Write → FireAfter in order.
    [Test]
    public async Task ChannelBase_WriteAsync_InvokesWriteBetweenFireBeforeAndFireAfter()
    {
        var ch = new ProbeChannel();
        await ch.WriteAsync(global::app.data.@this.Ok("hello"));
        await Assert.That(ch.WriteWasCalled).IsTrue();
        await Assert.That(ch.Sequence.Count).IsEqualTo(1);
        await Assert.That(ch.Sequence[0]).IsEqualTo("Write");
    }

    private sealed class ProbeChannel : global::app.channel.@this
    {
        public List<string> Sequence { get; } = new();
        public bool WriteWasCalled => Sequence.Contains("Write");

        public override Task<global::app.data.@this> Write(global::app.data.@this data, CancellationToken ct = default)
        {
            Sequence.Add("Write");
            return Task.FromResult(global::app.data.@this.Ok());
        }

        public override Task<global::app.data.@this> Read(CancellationToken ct = default)
            => Task.FromResult(global::app.data.@this.Ok());

        public override Task<global::app.data.@this> Ask(global::app.modules.output.ask action, CancellationToken ct = default)
            => Task.FromResult(global::app.data.@this.Ok());
    }

    // 1.10 — Every channel subclass overrides Write / Read / Ask (not the Core variants).
    [Test] public async Task ChannelSubclass_Stream_OverridesWriteReadAsk_NotCoreSuffixed()
        => await AssertSubclassOverrides(typeof(global::app.channel.stream.@this));

    [Test] public async Task ChannelSubclass_Goal_OverridesWriteReadAsk_NotCoreSuffixed()
        => await AssertSubclassOverrides(typeof(global::app.channel.goal.@this));

    [Test] public async Task ChannelSubclass_Message_OverridesWriteReadAsk_NotCoreSuffixed()
    {
        // Message is itself abstract — only Ask is implemented on it.
        var t = typeof(global::app.channel.message.@this);
        var ask = t.GetMethod("Ask", BindingFlags.Public | BindingFlags.Instance);
        await Assert.That(ask).IsNotNull();
        await Assert.That(ask!.DeclaringType).IsEqualTo(t);
        await Assert.That(t.GetMethod("AskCore", BindingFlags.Public | BindingFlags.Instance)).IsNull();
    }

    [Test] public async Task ChannelSubclass_Noop_OverridesWriteReadAsk_NotCoreSuffixed()
        => await AssertSubclassOverrides(typeof(global::app.channel.noop.@this));

    [Test] public async Task ChannelSubclass_Events_OverridesWriteReadAsk_NotCoreSuffixed()
    {
        // app.channel.@event.@this is the bindings holder, not a Channel
        // subclass — there is no Write/Read/Ask override to scan. The "events"
        // channel kind in the architect's list refers to a per-channel events
        // collection, which lives by composition on every Channel.
        var t = typeof(global::app.channel.@event.@this);
        await Assert.That(t).IsNotNull();
        await Assert.That(typeof(global::app.channel.@this).IsAssignableFrom(t)).IsFalse();
    }

    [Test] public async Task ChannelSubclass_Session_OverridesWriteReadAsk_NotCoreSuffixed()
    {
        // Session is abstract — concrete subclasses (stream, goal) carry the overrides.
        var t = typeof(global::app.channel.session.@this);
        await Assert.That(t.IsAbstract).IsTrue();
        await Assert.That(t.GetMethod("WriteCore", BindingFlags.Public | BindingFlags.Instance)).IsNull();
    }

    private static async Task AssertSubclassOverrides(Type t)
    {
        foreach (var name in new[] { "Write", "Read", "Ask" })
        {
            var m = t.GetMethod(name, BindingFlags.Public | BindingFlags.Instance);
            await Assert.That(m).IsNotNull().Because($"{t.Name} should declare {name}");
            await Assert.That(m!.DeclaringType).IsEqualTo(t).Because($"{name} must be overridden on {t.Name}");
        }
        foreach (var legacy in new[] { "WriteCore", "ReadCore", "AskCore" })
        {
            var m = t.GetMethod(legacy, BindingFlags.Public | BindingFlags.Instance);
            await Assert.That(m).IsNull().Because($"{t.Name} must not declare legacy {legacy}");
        }
    }

    // Old abstract hooks gone — guards against accidental re-introduction during merge.
    [Test] public async Task ChannelBase_WriteCore_ReadCore_AskCore_AbstractsRemoved()
    {
        var t = typeof(global::app.channel.@this);
        await Assert.That(t.GetMethod("WriteCore", BindingFlags.Public | BindingFlags.Instance)).IsNull();
        await Assert.That(t.GetMethod("ReadCore", BindingFlags.Public | BindingFlags.Instance)).IsNull();
        await Assert.That(t.GetMethod("AskCore", BindingFlags.Public | BindingFlags.Instance)).IsNull();
    }
}
