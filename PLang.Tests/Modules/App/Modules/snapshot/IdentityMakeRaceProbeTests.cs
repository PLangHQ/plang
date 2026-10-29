using Identity = global::app.module.identity.Identity;

namespace PLang.Tests.App.Modules.snapshot;

// Security probes (snapshot 335), real signer and the real identity store: the app's default identity is made once.
[NotInParallel]
public class IdentityMakeRaceProbeTests
{
    private string _dir = null!;

    [Before(Test)]
    public void Setup()
    {
        _dir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "plang_probe_idrace_" + Guid.NewGuid().ToString("N")[..8]);
        System.IO.Directory.CreateDirectory(System.IO.Path.Combine(_dir, "sub"));
    }

    [After(Test)]
    public void Teardown()
    {
        try { System.IO.Directory.Delete(_dir, true); } catch { /* best effort */ }
    }

    private global::app.@this App(string? folder = null) => new global::app.@this(folder ?? _dir).Consenting();

    private static async Task<global::app.data.@this> Ask(global::app.@this app)
    {
        var system = app.actor.list.System.Context;
        var get = new global::app.module.identity.Get(system);
        await get.Attach(null, system);
        return await get.Start();
    }

    private static async Task<List<Identity>> Stored(global::app.@this app)
    {
        var found = new List<Identity>();
        var system = app.actor.list.System.Context.Setting;
        var loaded = new { Success = true };
        var setting = await system.Get<global::app.module.identity.setting.@this>();
        foreach (var row in setting.Identity.Items(app.actor.list.System.Context))
            if (await row.Value<Identity>() is { } id) found.Add(id);
        await TestContext.Current!.OutputWriter.WriteLineAsync($"IDRACE-LOAD ok={loaded.Success} rows={found.Count}");
        return found;
    }

    private static async Task<T> Within<T>(Task<T> task, int seconds = 30)
    {
        var done = await Task.WhenAny(task, Task.Delay(TimeSpan.FromSeconds(seconds)));
        if (!ReferenceEquals(done, task)) throw new TimeoutException("deadlock: no answer in " + seconds + "s");
        return await task;
    }

    // Control: one ask, no race — is the made identity stored at all in this harness?
    [Test]
    public async Task G_Control_OneAsk_IsStored()
    {
        await using var app = App();
        var answer = await Ask(app);
        var stored = await Stored(app);
        var again = await Stored(app);
        await TestContext.Current!.OutputWriter.WriteLineAsync($"IDRACE-G ask={answer.Success} stored={stored.Count}/{again.Count} root={app.filesystem.root.Absolute}");
        await Assert.That(stored.Count).IsEqualTo(1);
    }

    // Control 2: an identity made by identity.create is stored (the store works in this harness); then an ask answers it.
    [Test]
    public async Task H_Control_Create_IsStored_AndAnsweredByAsk()
    {
        await using var app = App();
        var system = app.actor.list.System.Context;
        var make = new global::app.module.identity.Create(system) { Name = (global::app.type.item.text.@this)"mine", Default = (global::app.type.item.@bool.@this)true };
        await make.Attach(null, system);
        var created = await make.Start();
        var stored = await Stored(app);
        var answer = await Ask(app);
        var key = ((Identity)(await answer.Value())!).PublicKey;
        await TestContext.Current!.OutputWriter.WriteLineAsync($"IDRACE-H create={created.Success} stored={stored.Count} askSameKey={stored.Count == 1 && stored[0].PublicKey == key}");
        await Assert.That(stored.Count).IsEqualTo(1);
    }

    // N concurrent first asks answer one identity, the one stored.
    [Test]
    public async Task A_ConcurrentFirstAsks_AnswerOneIdentity_TheStoredOne()
    {
        await using var app = App();
        var answers = await Within(Task.WhenAll(Enumerable.Range(0, 16).Select(_ => Task.Run(() => Ask(app)))));
        var keys = new HashSet<string>();
        foreach (var answer in answers) { await answer.IsSuccess(); keys.Add(((Identity)(await answer.Value())!).PublicKey); }
        var stored = await Stored(app);
        await TestContext.Current!.OutputWriter.WriteLineAsync("IDRACE-FILES " + string.Join(",", System.IO.Directory.EnumerateFileSystemEntries(_dir, "*", System.IO.SearchOption.AllDirectories).Select(f => f.Replace(_dir, ""))));

        await TestContext.Current!.OutputWriter.WriteLineAsync($"IDRACE-A answered={keys.Count} stored={stored.Count} storedKeyAnswered={stored.Count == 1 && keys.Contains(stored[0].PublicKey)}");
        await Assert.That(keys.Count).IsEqualTo(1);
        await Assert.That(stored.Count).IsEqualTo(1);
        await Assert.That(keys.Contains(stored[0].PublicKey)).IsTrue();
    }

    // Concurrent first signs all verify against the one stored key.
    [Test]
    public async Task B_ConcurrentFirstSigns_AllVerify_AgainstTheStoredKey()
    {
        await using var app = App();
        var user = app.actor.list.User.Context;
        var tasks = Enumerable.Range(0, 12).Select(i => Task.Run(async () =>
        {
            var d = new global::app.data.@this("d" + i, "payload" + i, context: user);
            await user.Variable.Set("d" + i, d);
            var signed = await (await user.Action($"signing.sign(Data=%d{i}%)")).Start(user);
            return signed;
        })).ToArray();
        var results = await Within(Task.WhenAll(tasks));
        var keys = new HashSet<string>();
        var failed = 0;
        foreach (var r in results)
            if (!r.Success || r.Signature == null) failed++; else keys.Add(r.Signature.Identity.ToString());
        var stored = await Stored(app);

        await TestContext.Current!.OutputWriter.WriteLineAsync($"IDRACE-B failed={failed} signers={keys.Count} stored={stored.Count} storedSigned={stored.Count == 1 && keys.Contains(stored[0].PublicKey)}");
        await Assert.That(failed).IsEqualTo(0);
        await Assert.That(keys.Count).IsEqualTo(1);
        await Assert.That(stored.Count).IsEqualTo(1);
        await Assert.That(keys.Contains(stored[0].PublicKey)).IsTrue();
    }

    // The make's own save signs and asks again from inside the gate. First make: answered by Held (origin). Promote path: a
    // non-archived identity stored with no default; the save inside the make signs, and its ask finds stored rows but none default.
    [Test]
    public async Task C_NestedAskFromTheMakesOwnSave_DoesNotDeadlock_FirstMake()
    {
        await using var app = App();
        var answer = await Within(Ask(app), 20);
        await Assert.That(answer.Success).IsTrue();
    }

    [Test]
    public async Task D_NestedAskFromThePromotePath_DoesNotDeadlock()
    {
        await using var app = App();
        var system = app.actor.list.System.Context;
        // an identity that is not the default: the store holds one non-archived row and no default
        var make = new global::app.module.identity.Create(system) { Name = (global::app.type.item.text.@this)"other", Default = (global::app.type.item.@bool.@this)false };
        await make.Attach(null, system);
        var created = await Within(make.Start(), 20);
        await TestContext.Current!.OutputWriter.WriteLineAsync($"IDRACE-D create.success={created.Success}/{created.Error?.Key}");
        string outcome;
        try
        {
            var answer = await Within(Ask(app), 20);
            outcome = $"answered success={answer.Success}/{answer.Error?.Key}";
            await TestContext.Current!.OutputWriter.WriteLineAsync("IDRACE-D " + outcome);
            await Assert.That(answer.Success).IsTrue();
        }
        catch (TimeoutException ex)
        {
            await TestContext.Current!.OutputWriter.WriteLineAsync("IDRACE-D " + ex.Message);
            throw;
        }
    }

    // Same promote path reached by archiving the forced default while another stands.
    [Test]
    public async Task E_AfterArchivingTheDefaultWithForce_TheNextAsk_DoesNotDeadlock()
    {
        await using var app = App();
        var system = app.actor.list.System.Context;
        await Within(Ask(app), 20);
        var other = new global::app.module.identity.Create(system) { Name = (global::app.type.item.text.@this)"other", Default = (global::app.type.item.@bool.@this)false };
        await other.Attach(null, system);
        await (await Within(other.Start(), 20)).IsSuccess();
        var archive = new global::app.module.identity.Archive(system) { Name = (global::app.type.item.text.@this)"default", Force = (global::app.type.item.@bool.@this)true };
        await archive.Attach(null, system);
        var archived = await Within(archive.Start(), 20);
        await TestContext.Current!.OutputWriter.WriteLineAsync($"IDRACE-E archive={archived.Success}/{archived.Error?.Key}");
        string text;
        try
        {
            var answer = await Within(Ask(app), 20);
            text = $"success={answer.Success}/{answer.Error?.Key}";
            await TestContext.Current!.OutputWriter.WriteLineAsync("IDRACE-E " + text);
            await Assert.That(answer.Success).IsTrue();
        }
        catch (TimeoutException ex)
        {
            await TestContext.Current!.OutputWriter.WriteLineAsync("IDRACE-E " + ex.Message);
            throw;
        }
    }

    // A sub app and its maker, asking at the same time: each answers one identity, and no app ends with two stored.
    [Test]
    public async Task F_ASubApp_AndItsMaker_AskingTogether_MakeOneIdentityEach()
    {
        await using var maker = App();
        await using var sub = new global::app.@this(System.IO.Path.Combine(_dir, "sub"), maker.actor.list.User);
        var asks = Enumerable.Range(0, 8).SelectMany(_ => new[] { Task.Run(() => Ask(maker)), Task.Run(() => Ask(sub)) }).ToArray();
        var answers = await Within(Task.WhenAll(asks));
        var makerKeys = new HashSet<string>(); var subKeys = new HashSet<string>();
        for (var i = 0; i < answers.Length; i++)
        {
            await answers[i].IsSuccess();
            (i % 2 == 0 ? makerKeys : subKeys).Add(((Identity)(await answers[i].Value())!).PublicKey);
        }
        var makerStored = await Stored(maker); var subStored = await Stored(sub);

        await TestContext.Current!.OutputWriter.WriteLineAsync($"IDRACE-F maker answered={makerKeys.Count} stored={makerStored.Count}; sub answered={subKeys.Count} stored={subStored.Count}; sameKey={makerKeys.SetEquals(subKeys)}");
        await Assert.That(makerKeys.Count).IsEqualTo(1);
        await Assert.That(subKeys.Count).IsEqualTo(1);
        await Assert.That(makerStored.Count).IsEqualTo(1);
        // a sub app's identity is its maker's, never a row of its own (SubAppFullPassProbeTests): no second identity is made
        await Assert.That(subStored.Count).IsEqualTo(0);
        await Assert.That(subKeys.SetEquals(makerKeys)).IsTrue();
    }

    // Same read-then-write shape, the rest of the door: identity.create reads every row, checks the name, then writes the whole
    // list back, with no gate. Concurrent creates of distinct names lose rows (last write wins).
    [Test]
    public async Task I_ConcurrentCreates_OfDistinctNames_AllStay()
    {
        await using var app = App();
        var system = app.actor.list.System.Context;
        await Within(Ask(app), 20);
        var made = await Within(Task.WhenAll(Enumerable.Range(0, 8).Select(i => Task.Run(async () =>
        {
            var make = new global::app.module.identity.Create(system) { Name = (global::app.type.item.text.@this)("n" + i), Default = (global::app.type.item.@bool.@this)false };
            await make.Attach(null, system);
            return await make.Start();
        }))));
        var stored = await Stored(app);
        await TestContext.Current!.OutputWriter.WriteLineAsync($"IDRACE-I created={made.Count(m => m.Success)} stored={stored.Count} (expect 9 with the default)");
        await Assert.That(stored.Count).IsEqualTo(1 + made.Count(m => m.Success));
    }
}
