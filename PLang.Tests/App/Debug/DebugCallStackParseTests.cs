namespace PLang.Tests.App.Debug;

// App.Debug.@this parses --debug={callstack:...} into CallStackFlags.
// Shorthand `callstack:true` and full object form both supported.
public class DebugCallStackParseTests
{
    [Test]
    public async Task Parse_NoCallstackKey_AllFalse()
    {
        // --debug without a callstack key: Flags is default (all false, MaxFrames 1000).
        await Task.Yield();
        Assert.Fail("Not implemented");
    }

    [Test]
    public async Task Parse_ShorthandTrue_TimingAndTagsOnOthersOff()
    {
        // {callstack:true} → Flags{Timing=true, Tags=true, Diff=false, DeepDiff=false, History=false, MaxFrames=1000}.
        await Task.Yield();
        Assert.Fail("Not implemented");
    }

    [Test]
    public async Task Parse_FullObject_AllFlagsHonored()
    {
        // {callstack:{timing:true,diff:true,deepDiff:false,tags:true,history:true,maxFrames:500}}
        await Task.Yield();
        Assert.Fail("Not implemented");
    }

    [Test]
    public async Task Parse_PartialObject_UnspecifiedFlagsDefaultFalse()
    {
        // {callstack:{diff:true}} → only Diff true; rest default.
        await Task.Yield();
        Assert.Fail("Not implemented");
    }

    [Test]
    public async Task Parse_MaxFramesDefaults1000_WhenOmitted()
    {
        await Task.Yield();
        Assert.Fail("Not implemented");
    }

    [Test]
    public async Task Parse_BadJson_FallsBackToAllFalse()
    {
        // Malformed --debug payload: callstack flags safe-default to all-false rather than throwing.
        await Task.Yield();
        Assert.Fail("Not implemented");
    }
}
