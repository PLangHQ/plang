namespace PLang.Tests.App.Debug;

public class DebugCallStackParseTests
{
    [Test]
    public async Task Parse_NoCallstackKey_AllFalse()
    {
        await using var app = new global::app.@this("/app");
        app.Debug.Apply(new Dictionary<string, object?> { ["verbose"] = true });
        var f = app.callstack.Flags;
        await Assert.That(f.Timing).IsFalse();
        await Assert.That(f.Diff).IsFalse();
        await Assert.That(f.Tags).IsFalse();
        await Assert.That(f.History).IsFalse();
        await Assert.That(f.MaxFrames).IsEqualTo(1000);
    }

    [Test]
    public async Task Parse_ShorthandTrue_TimingAndTagsOnOthersOff()
    {
        await using var app = new global::app.@this("/app");
        app.Debug.Apply(new Dictionary<string, object?> { ["callstack"] = true });
        var f = app.callstack.Flags;
        await Assert.That(f.Timing).IsTrue();
        await Assert.That(f.Tags).IsTrue();
        await Assert.That(f.Diff).IsFalse();
        await Assert.That(f.DeepDiff).IsFalse();
        await Assert.That(f.History).IsFalse();
        await Assert.That(f.MaxFrames).IsEqualTo(1000);
    }

    [Test]
    public async Task Parse_FullObject_AllFlagsHonored()
    {
        await using var app = new global::app.@this("/app");
        app.Debug.Apply(new Dictionary<string, object?>
        {
            ["callstack"] = new Dictionary<string, object?>
            {
                ["timing"] = true,
                ["diff"] = true,
                ["deepDiff"] = false,
                ["tags"] = true,
                ["history"] = true,
                ["maxFrames"] = 500
            }
        });
        var f = app.callstack.Flags;
        await Assert.That(f.Timing).IsTrue();
        await Assert.That(f.Diff).IsTrue();
        await Assert.That(f.DeepDiff).IsFalse();
        await Assert.That(f.Tags).IsTrue();
        await Assert.That(f.History).IsTrue();
        await Assert.That(f.MaxFrames).IsEqualTo(500);
    }

    [Test]
    public async Task Parse_PartialObject_UnspecifiedFlagsDefaultFalse()
    {
        await using var app = new global::app.@this("/app");
        app.Debug.Apply(new Dictionary<string, object?>
        {
            ["callstack"] = new Dictionary<string, object?> { ["diff"] = true }
        });
        var f = app.callstack.Flags;
        await Assert.That(f.Diff).IsTrue();
        await Assert.That(f.Timing).IsFalse();
        await Assert.That(f.Tags).IsFalse();
    }

    [Test]
    public async Task Parse_MaxFramesDefaults1000_WhenOmitted()
    {
        await using var app = new global::app.@this("/app");
        app.Debug.Apply(new Dictionary<string, object?>
        {
            ["callstack"] = new Dictionary<string, object?> { ["history"] = true }
        });
        await Assert.That(app.callstack.Flags.MaxFrames).IsEqualTo(1000);
    }

    [Test]
    public async Task Parse_BadJson_FallsBackToAllFalse()
    {
        await using var app = new global::app.@this("/app");
        // Junk value for the callstack key — neither bool nor dict. Defensive parser
        // must fall back to all-off rather than throw.
        app.Debug.Apply(new Dictionary<string, object?> { ["callstack"] = "garbage" });
        var f = app.callstack.Flags;
        await Assert.That(f.Timing).IsFalse();
        await Assert.That(f.Diff).IsFalse();
        await Assert.That(f.Tags).IsFalse();
    }
}
