using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;

namespace PLang.Tests.App.Modules.Debug;

/// <summary>
/// <c>debug/this.cs</c> LLM trace writes: every block goes to the debug channel, whose backing (stderr, a
/// file) decides where it lands. Drives <c>WriteLlmBlock</c> directly — the LLM event lifecycle needs a
/// real LLM call.
/// </summary>
public class DebugTraceWriteTests
{
    [Test] public async Task LlmBlock_GoesToTheDebugChannel()
    {
        await using var app = new global::app.@this("/app").Testing();
        var captured = new System.IO.MemoryStream();
        app.actor.list.System.Channel.Register(new global::app.channel.type.stream.@this(
            global::app.channel.list.@this.Debug, captured,
            global::app.channel.ChannelDirection.Output, ownsStream: false) { Mime = "text/plain" });
        app.Debug = new global::app.module.debug.@this(app.actor.list.System.Context);

        await global::app.module.debug.@this.WriteLlmBlock("LLM TEST", new[] { "line one", "line two" },
            app.actor.list.User.Context);

        var written = System.Text.Encoding.UTF8.GetString(captured.ToArray());
        await Assert.That(written).Contains("=== LLM TEST ===");
        await Assert.That(written).Contains("line one");
        await Assert.That(written).Contains("line two");
        await Assert.That(written).Contains("=== END LLM TEST ===");
    }
}
