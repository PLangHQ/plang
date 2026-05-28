using System.Threading;
using System.Threading.Tasks;

namespace PLang.Tests.App.Types.PathTests.Contract;

/// <summary>
/// Test channel that answers every Authorize prompt with a fixed string
/// ("a" to grant-and-persist, "n" to refuse). Registered as the actor's
/// "input" channel by the contract suite to drive the Permission gate
/// deterministically.
/// </summary>
internal sealed class CannedAnswerChannel : global::app.channels.channel.@this
{
    private readonly string _answer;

    public CannedAnswerChannel(string answer)
    {
        _answer = answer;
        Name = "input";
        Direction = global::app.channels.channel.ChannelDirection.Bidirectional;
    }

    public override Task<global::app.data.@this> Write(global::app.data.@this data, CancellationToken ct = default)
        => Task.FromResult(global::app.data.@this.Ok());

    public override Task<global::app.data.@this> Read(CancellationToken ct = default)
        => Task.FromResult(global::app.data.@this.Ok((object?)null));

    public override Task<global::app.data.@this> Ask(global::app.modules.output.ask action, CancellationToken ct = default)
        => Task.FromResult(global::app.data.@this.Ok(_answer));
}
