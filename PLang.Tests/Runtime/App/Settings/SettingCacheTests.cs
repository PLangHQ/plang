using math = global::app.module.math.setting.@this;

namespace PLang.Tests.App.Settings;

/// <summary>
/// A setting is built once per version: reading it again is the same instance until a setting is written anywhere
/// (any scope — a write in the system's reaches the user's read), and what the settings hand out is read-only (a write
/// through it answers a copy). Reads run beside writes from parallel branches.
/// </summary>
public class SettingCacheTests
{
    private global::app.@this _app = null!;

    [Before(Test)]
    public void Setup() => _app = new global::app.@this("/tmp/setting-cache-" + System.Guid.NewGuid().ToString("N")[..8]).Testing();

    [After(Test)]
    public async Task Cleanup() => await _app.DisposeAsync();

    private global::app.actor.context.@this User => _app.actor.list.User.Context;

    [Test]
    public async Task ASecondRead_IsTheSameSetting()
        => await Assert.That(User.Setting.Of<math>()).IsSameReferenceAs(User.Setting.Of<math>());

    [Test]
    public async Task AWrite_IsReadByTheNextRead()
    {
        var before = User.Setting.Of<math>();
        await (await User.Setting.Set("math.setting.equal.digits", User.Ok(3))).IsSuccess();
        var after = User.Setting.Of<math>();
        await Assert.That(after).IsNotSameReferenceAs(before);
        await Assert.That(after.Equal.Digits.ToInt32()).IsEqualTo(3);
    }

    [Test]
    public async Task AWriteInTheSystemsSettings_ReachesTheUsersRead()
    {
        _ = User.Setting.Of<math>();
        await _app.actor.list.System.Setting.Set("math.setting.equal.digits", _app.actor.list.System.Context.Ok(4));
        await Assert.That(User.Setting.Of<math>().Equal.Digits.ToInt32()).IsEqualTo(4);
    }

    [Test]
    public async Task AWriteThroughASetting_LeavesTheOneHandedOutAsItWas()
    {
        var handed = User.Setting.Of<math>();
        await handed.Equal.Set("digits", false, User.Ok(2), User);
        await Assert.That(handed.Equal.Digits.ToInt32()).IsEqualTo(15);
        await Assert.That(User.Setting.Of<math>().Equal.Digits.ToInt32()).IsEqualTo(2);
    }

    [Test]
    public async Task ReadsBesideWrites_NeitherThrowNorStayStale()
    {
        var reads = Enumerable.Range(0, 64).Select(_ => Task.Run(() =>
        {
            for (var i = 0; i < 200; i++) _ = User.Setting.Of<math>().Equal.Digits.ToInt32();
        }));
        var writes = Task.Run(async () =>
        {
            for (var i = 1; i <= 50; i++) await User.Setting.Set("math.setting.equal.digits", User.Ok(i));
        });
        await Task.WhenAll(reads.Append(writes));
        await Assert.That(User.Setting.Of<math>().Equal.Digits.ToInt32()).IsEqualTo(50);
    }
}
