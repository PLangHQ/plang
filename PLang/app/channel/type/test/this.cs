using System.Text;

namespace app.channel.type.test;

/// <summary>
/// A test run's session — a kept-open channel on the actor the run's setting names. While one is open on
/// that actor, the app is testing (<see cref="app.test.list.@this.Session"/>). The run's own session holds
/// no test and is registered as <c>test</c>; a test's session holds its test and is registered as
/// <c>output</c> on the test's App, so everything the test writes out lands here, one line a write.
/// </summary>
public sealed class @this : global::app.channel.type.session.@this
{
    private readonly StringBuilder _text = new();

    /// <summary>The run's own session (<paramref name="test"/> null) or one test's.</summary>
    public @this(global::app.test.@this? test = null)
    {
        Test = test;
        Name = test == null ? "test" : global::app.channel.list.@this.Output;
        Direction = global::app.channel.ChannelDirection.Output;
    }

    /// <summary>The test this session runs; null for the run's own.</summary>
    public global::app.test.@this? Test { get; }

    /// <summary>What was written, a line a write; null when nothing was.</summary>
    public global::app.type.item.text.@this? Text
    {
        get { lock (_text) return _text.Length > 0 ? _text.ToString() : null; }
    }

    public override async Task<global::app.data.@this> Write(global::app.data.@this data, CancellationToken ct = default)
    {
        var value = await data.Value();
        if (value != null)
            lock (_text) _text.Append(value).Append('\n');
        return data.Context.Ok();
    }

    public override Task<global::app.data.@this> Read(CancellationToken ct = default)
        => Task.FromResult(global::app.data.@this.FromError(new global::app.error.ServiceError(
            $"Channel '{Name}' is a test's output; it is written, not read", "ChannelWriteOnly", 400)));

    public override Task<global::app.data.@this> Ask(global::app.module.output.ask action, CancellationToken ct = default)
        => Task.FromResult(action.Context.Error(new global::app.error.ServiceError(
            $"Channel '{Name}' is a test's output; it has no one to answer", "ChannelWriteOnly", 400)));
}
