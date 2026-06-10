using Discover = global::app.module.test.discover;

namespace PLang.Tests.App.Modules.test;

// Pins the skip-directive matcher (discover.IsSkipTagStep) — the gate between an honest
// Skipped and a run. Over- or under-matching here would either silently skip real tests or
// fail to park a deferred one.
public class DiscoverSkipTagTests
{
    [Test]
    public async Task SingleQuoted_Skip_Matches()
        => await Assert.That(Discover.IsSkipTagStep("tag this test 'skip'")).IsTrue();

    [Test]
    public async Task DoubleQuoted_Skip_Matches()
        => await Assert.That(Discover.IsSkipTagStep("tag this test \"skip\"")).IsTrue();

    [Test]
    public async Task MixedCase_AndSpacing_Matches()
        => await Assert.That(Discover.IsSkipTagStep("  TAG  this   test  'Skip' ")).IsTrue();

    [Test]
    public async Task DifferentTagValue_DoesNotMatch()
        => await Assert.That(Discover.IsSkipTagStep("tag this test 'flaky'")).IsFalse();

    [Test]
    public async Task SkipPlusExtraArgs_DoesNotMatch()
        => await Assert.That(Discover.IsSkipTagStep("tag this test 'skip', 'slow'")).IsFalse();

    [Test]
    public async Task NotATagStep_DoesNotMatch()
    {
        await Assert.That(Discover.IsSkipTagStep("write out 'skip'")).IsFalse();
        await Assert.That(Discover.IsSkipTagStep("sign \"hello world\"")).IsFalse();
    }
}
