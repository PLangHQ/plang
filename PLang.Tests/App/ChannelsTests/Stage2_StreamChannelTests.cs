namespace PLang.Tests.App.ChannelsTests;

// Stage 2 — Channel.Stream concrete (the existing Channel refactored).
// Architect: stage-2-stream-channel.md, coverage Stage 2 rows.

public class Stage2_StreamChannelTests
{
    [Test]
    public async Task StreamChannel_WriteCore_WritesDataViaSerializer()
    {
        // Channel.Stream.WriteCore takes a full Data envelope, looks up the
        // serializer by Mime, writes serialised bytes to the underlying Stream.
        await Task.CompletedTask;
        Assert.Fail("Not implemented");
    }

    [Test]
    public async Task StreamChannel_ReadCore_ReadsBytes_DeserialisesViaMime()
    {
        // Inverse of WriteCore. Bytes from underlying stream → Data via Mime serializer.
        await Task.CompletedTask;
        Assert.Fail("Not implemented");
    }

    [Test]
    public async Task StreamChannel_MemoryFactory_CreatesBidirectionalChannel()
    {
        // Channel.Stream.Memory(name) — backed by MemoryStream, both read & write.
        // Used in tests as the "captureable stdout".
        await Task.CompletedTask;
        Assert.Fail("Not implemented");
    }

    [Test]
    public async Task StreamChannel_OutputFactory_CreatesWriteOnlyChannel()
    {
        // Channel.Stream.Output(name, stream) — direction = Output role,
        // CanRead=false. ReadCore raises ChannelWriteOnly.
        await Task.CompletedTask;
        Assert.Fail("Not implemented");
    }

    [Test]
    public async Task StreamChannel_InputFactory_CreatesReadOnlyChannel()
    {
        // Channel.Stream.Input(name, stream) — direction = Input role,
        // CanWrite=false. WriteCore raises ChannelReadOnly.
        await Task.CompletedTask;
        Assert.Fail("Not implemented");
    }

    [Test]
    public async Task StreamChannel_WriteCore_FailsWithWriteError_OnUnderlyingStreamThrow()
    {
        // Underlying Stream throws (e.g. IOException) → typed WriteError on the
        // Data result, not a raw exception bubbling up.
        await Task.CompletedTask;
        Assert.Fail("Not implemented");
    }

    [Test]
    public async Task StreamChannel_ReadCore_FailsWithReadError_OnUnderlyingStreamThrow()
    {
        // Mirror of WriteError for read-side failures.
        await Task.CompletedTask;
        Assert.Fail("Not implemented");
    }

    [Test]
    public async Task StreamChannel_Ask_BlocksOnStdinUntilInputArrives()
    {
        // Ask on a stdin-style Channel.Stream blocks until bytes arrive,
        // then returns Data containing the read line.
        await Task.CompletedTask;
        Assert.Fail("Not implemented");
    }

    [Test]
    public async Task StreamChannel_Ask_TimesOutPerChannelTimeoutConfig()
    {
        // Channel with Timeout=PT1S, ask, no input → AskTimeout error type after 1s.
        await Task.CompletedTask;
        Assert.Fail("Not implemented");
    }

    [Test]
    public async Task StreamChannel_OwnsStreamTrue_DisposesUnderlyingStream()
    {
        // ownsStream=true (default for Memory factory) — Channel.Dispose disposes
        // the underlying Stream. Verify via "post-dispose write throws ObjectDisposed".
        await Task.CompletedTask;
        Assert.Fail("Not implemented");
    }

    [Test]
    public async Task StreamChannel_OwnsStreamFalse_LeavesUnderlyingStreamOpen()
    {
        // ownsStream=false (entry-point-supplied Console.OpenStandard*) —
        // process keeps owning. Channel.Dispose leaves the stream usable.
        await Task.CompletedTask;
        Assert.Fail("Not implemented");
    }
}
