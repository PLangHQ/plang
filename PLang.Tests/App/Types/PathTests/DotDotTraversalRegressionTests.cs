using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using FilePath = global::app.type.path.file.@this;
using Path = global::app.type.path.@this;

namespace PLang.Tests.App.Types.PathTests;

/// <summary>
/// Pins <c>IsInRoot()</c>'s prefix-match against <c>..</c>-traversal
/// bypass. The attack shape: a relative <c>rawPath</c> resolved by
/// <c>file.Resolve</c> against a goal-anchored <c>runtimeDir</c> inside
/// root produces a string that <i>lexically</i> starts with root but
/// <i>OS-resolves</i> outside it. The FilePath ctor canonicalizes
/// <c>_absolutePath</c>, so <c>IsInRoot</c> sees the truthful absolute
/// form and correctly returns false for out-of-root targets — AuthGate
/// prompts or denies instead of silently auto-granting.
/// </summary>
public class DotDotTraversalRegressionTests
{
    private sealed class CannedChannel : global::app.channel.@this
    {
        private readonly string _answer;
        public CannedChannel(string answer) { _answer = answer; Name = "input"; Direction = global::app.channel.ChannelDirection.Bidirectional; }
        public override Task<global::app.data.@this> Write(global::app.data.@this data, CancellationToken ct = default) => Task.FromResult(global::app.data.@this.Ok());
        public override Task<global::app.data.@this> Read(CancellationToken ct = default) => Task.FromResult(global::app.data.@this.Ok((object?)null));
        public override Task<global::app.data.@this> Ask(global::app.module.output.ask action, CancellationToken ct = default) => Task.FromResult(global::app.data.@this.Ok(_answer));
    }

    private static (global::app.@this app, global::app.actor.context.@this context, string root) MakeApp()
    {
        var dir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "plang-f1-" + System.Guid.NewGuid().ToString("N")[..8]);
        System.IO.Directory.CreateDirectory(dir);
        System.IO.Directory.CreateDirectory(System.IO.Path.Combine(dir, "subdir"));
        System.IO.Directory.CreateDirectory(System.IO.Path.Combine(dir, "subdir", ".build"));
        var app = new global::app.@this(dir);
        return (app, app.User.Context, dir);
    }

    [Test]
    public async Task FilePath_Ctor_Canonicalizes_RemovesDotDot()
    {
        var (_, context, root) = MakeApp();
        // Pre-canonicalization, this would be stored verbatim. Post-fix the
        // ctor resolves the .. so _absolutePath is the truthful target.
        var raw = System.IO.Path.Combine(root, "subdir", "..", "leaf.txt");
        var p = new FilePath(raw, context);
        await Assert.That(p.Absolute).DoesNotContain("..");
        await Assert.That(p.Absolute).EndsWith("leaf.txt");
    }

    [Test]
    public async Task FilePath_Resolve_RelativeWithDotDot_FromGoalRuntimeDir_LeavesRoot()
    {
        var (app, context, root) = MakeApp();
        // Stage a goal whose LoadedFromPrPath points inside root, so
        // GetRuntimeDirectory returns <root>/subdir/.
        var prPath = Path.Resolve(System.IO.Path.Combine(root, "subdir", ".build", "probe.pr"), context);
        var goal = new Goal
        {
            Name = "Probe",
            Path = Path.Resolve(System.IO.Path.Combine(root, "subdir", "probe.goal"), context),
            LoadedFromPrPath = prPath
        };
        context.Goal = goal;

        // The attack shape: a relative rawPath with enough .. to climb past
        // root. file.Resolve does Path.Combine(runtimeDir, raw) — pre-fix the
        // resulting _absolutePath textually starts with root and IsInRoot
        // auto-grants. Post-fix the ctor canonicalizes, _absolutePath now
        // names a file above root, and IsInRoot is correctly false.
        var p = Path.Resolve("../../SECRET-OUTSIDE.txt", context);

        await Assert.That(p.Absolute).DoesNotContain("..");
        // The canonical form must NOT live under the app root.
        var rootWithSeparator = root.EndsWith(System.IO.Path.DirectorySeparatorChar)
            ? root
            : root + System.IO.Path.DirectorySeparatorChar;
        await Assert.That(p.Absolute.StartsWith(rootWithSeparator)).IsFalse();
    }

    [Test]
    public async Task ReadText_RelativeDotDot_OutOfRoot_DeniedByAuthGate()
    {
        var (app, context, root) = MakeApp();
        // Stage a secret one directory above engine root. If AuthGate were
        // bypassed, ReadText would return its content.
        var parent = System.IO.Directory.GetParent(root)!.FullName;
        var secretPath = System.IO.Path.Combine(parent, "traversal-canary-" + System.Guid.NewGuid().ToString("N")[..8] + ".txt");
        var secretContent = "if-you-can-read-me-the-gate-was-bypassed-" + System.Guid.NewGuid().ToString("N");
        System.IO.File.WriteAllText(secretPath, secretContent);
        try
        {
            // Channel that denies any AuthGate prompt.
            app.User.Channel.Register(new CannedChannel("n"));

            var prPath = Path.Resolve(System.IO.Path.Combine(root, "subdir", ".build", "probe.pr"), context);
            context.Goal = new Goal
            {
                Name = "Probe",
                Path = Path.Resolve(System.IO.Path.Combine(root, "subdir", "probe.goal"), context),
                LoadedFromPrPath = prPath
            };

            var relative = "../../" + System.IO.Path.GetFileName(secretPath);
            var p = (FilePath)Path.Resolve(relative, context);

            // The Read MUST surface a permission decision (Fail), not the
            // secret file's bytes.
            var result = await p.ReadText();
            await Assert.That(result.Success).IsFalse();
            await Assert.That(result.Value?.ToString() ?? "").IsNotEqualTo(secretContent);
        }
        finally
        {
            try { System.IO.File.Delete(secretPath); } catch { }
        }
    }
}
