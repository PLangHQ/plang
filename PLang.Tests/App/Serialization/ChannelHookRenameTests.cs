namespace PLang.Tests.App.Serialization;

// data-serialize-cleanup — Stage 1
// channel.WriteCore / ReadCore / AskCore renamed to Write / Read / Ask on every subclass.
// Public orchestrators keep the Async suffix to mark "entry-with-events".
// Coverage matrix rows 1.9, 1.10.

public class ChannelHookRenameTests
{
    // 1.9 — Base WriteAsync invokes FireBefore → Write → FireAfter in order.
    [Test] public async Task ChannelBase_WriteAsync_InvokesWriteBetweenFireBeforeAndFireAfter()
    { await Task.CompletedTask; Assert.Fail("Not implemented"); }

    // 1.10 — Every channel subclass overrides Write / Read / Ask.
    //        Scanned via reflection across the 6 subclasses listed by architect:
    //        stream, goal, message, noop, events, session.
    [Test] public async Task ChannelSubclass_Stream_OverridesWriteReadAsk_NotCoreSuffixed()
    { await Task.CompletedTask; Assert.Fail("Not implemented"); }

    [Test] public async Task ChannelSubclass_Goal_OverridesWriteReadAsk_NotCoreSuffixed()
    { await Task.CompletedTask; Assert.Fail("Not implemented"); }

    [Test] public async Task ChannelSubclass_Message_OverridesWriteReadAsk_NotCoreSuffixed()
    { await Task.CompletedTask; Assert.Fail("Not implemented"); }

    [Test] public async Task ChannelSubclass_Noop_OverridesWriteReadAsk_NotCoreSuffixed()
    { await Task.CompletedTask; Assert.Fail("Not implemented"); }

    [Test] public async Task ChannelSubclass_Events_OverridesWriteReadAsk_NotCoreSuffixed()
    { await Task.CompletedTask; Assert.Fail("Not implemented"); }

    [Test] public async Task ChannelSubclass_Session_OverridesWriteReadAsk_NotCoreSuffixed()
    { await Task.CompletedTask; Assert.Fail("Not implemented"); }

    // Old abstract hooks gone — guards against accidental re-introduction during merge.
    [Test] public async Task ChannelBase_WriteCore_ReadCore_AskCore_AbstractsRemoved()
    { await Task.CompletedTask; Assert.Fail("Not implemented"); }
}
