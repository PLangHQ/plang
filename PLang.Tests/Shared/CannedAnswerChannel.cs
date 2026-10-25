using System.Threading;
using System.Threading.Tasks;

namespace PLang.Tests.App.Types.PathTests.Contract;

/// <summary>
/// Test channel that answers every Authorize prompt with a fixed string
/// ("a" to grant-and-persist, "n" to refuse). Registered as the actor's
/// "input" channel by the contract suite to drive the Permission gate
/// deterministically.
/// </summary>
internal sealed class CannedAnswerChannel : global::app.channel.@this
{
    private readonly string _answer;

    public CannedAnswerChannel(global::app.channel.list.@this channels, string answer) : base(channels)
    {
        _answer = answer;
        Name = "input";
        Direction = global::app.channel.ChannelDirection.Bidirectional;
    }

    public override Task<global::app.data.@this> Write(global::app.data.@this data, CancellationToken ct = default)
        => Task.FromResult(data.Context.Ok());

    public override Task<global::app.data.@this> Read(CancellationToken ct = default)
        => Task.FromResult(Context!.Null());

    public override Task<global::app.data.@this> Ask(global::app.module.output.ask action, CancellationToken ct = default)
        => Task.FromResult(action.Context.Ok(_answer));
}
