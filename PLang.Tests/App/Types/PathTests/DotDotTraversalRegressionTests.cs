using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using FilePath = global::app.types.path.file.@this;
using Path = global::app.types.path.@this;

namespace PLang.Tests.App.Types.PathTests;

/// <summary>
/// Security v1 F1 regression — <c>IsInRoot()</c>'s textual prefix-match used
/// to be bypassable with <c>..</c> segments. The attack: a relative
/// <c>rawPath</c> resolved by <c>file.Resolve</c> against a goal-anchored
/// <c>runtimeDir</c> inside root produces a string that <i>lexically</i>
/// starts with root but <i>OS-resolves</i> outside it. AuthGate auto-grants
/// on <c>IsInRoot=true</c>; <c>System.IO.File.*</c> then resolves the
/// <c>..</c> segments and reads outside root with no prompt and no
/// permission lookup.
///
/// <para>Fix: <c>file.@this</c>'s ctor canonicalizes <c>_absolutePath</c>
/// via <see cref="global::app.Utils.PathHelper.GetFullPath(string)"/>, so
/// <c>..</c> segments are resolved before being stored. <c>IsInRoot</c>'s
/// prefix-match then sees the truthful absolute form and correctly returns
/// false for out-of-root targets — AuthGate prompts/denies.</para>
/// </summary>
public class DotDotTraversalRegressionTests
{
    private sealed class CannedChannel : global::app.channels.channel.@this
    {
        private readonly string _answer;
        public CannedChannel(string answer) { _answer = answer; Name = "input"; Direction = global::app.channels.channel.ChannelDirection.Bidirectional; }
        public override Task<global::app.data.@this> WriteCore(global::app.data.@this data, CancellationToken ct = default) => Task.FromResult(global::app.data.@this.Ok());
        public override Task<global::app.data.@this> ReadCore(CancellationToken ct = default) => Task.FromResult(global::app.data.@this.Ok((object?)null));
        public override Task<global::app.data.@this> AskCore(global::app.modules.output.ask action, CancellationToken ct = default) => Task.FromResult(global::app.data.@this.Ok(_answer));
    }

    private static (global::app.@this app, global::app.actor.context.@this ctx, string root) MakeApp()
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
        var (_, ctx, root) = MakeApp();
        // Pre-canonicalization, this would be stored verbatim. Post-fix the
        // ctor resolves the .. so _absolutePath is the truthful target.
        var raw = System.IO.Path.Combine(root, "subdir", "..", "leaf.txt");
        var p = new FilePath(raw, ctx);
        await Assert.That(p.Absolute).DoesNotContain("..");
        await Assert.That(p.Absolute).EndsWith("leaf.txt");
    }

    [Test]
    public async Task FilePath_Resolve_RelativeWithDotDot_FromGoalRuntimeDir_LeavesRoot()
    {
        var (app, ctx, root) = MakeApp();
        // Stage a goal whose LoadedFromPrPath points inside root, so
        // GetRuntimeDirectory returns <root>/subdir/.
        var prPath = Path.Resolve(System.IO.Path.Combine(root, "subdir", ".build", "probe.pr"), ctx);
        var goal = new Goal
        {
            Name = "Probe",
            Path = Path.Resolve(System.IO.Path.Combine(root, "subdir", "probe.goal"), ctx),
            LoadedFromPrPath = prPath
        };
        ctx.Goal = goal;

        // The attack shape: a relative rawPath with enough .. to climb past
        // root. file.Resolve does Path.Combine(runtimeDir, raw) — pre-fix the
        // resulting _absolutePath textually starts with root and IsInRoot
        // auto-grants. Post-fix the ctor canonicalizes, _absolutePath now
        // names a file above root, and IsInRoot is correctly false.
        var p = Path.Resolve("../../SECRET-OUTSIDE.txt", ctx);

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
        var (app, ctx, root) = MakeApp();
        // Stage SECRET-OUTSIDE.txt one directory above engine root. If
        // AuthGate were bypassed (pre-fix), ReadText would return its content.
        var parent = System.IO.Directory.GetParent(root)!.FullName;
        var secretPath = System.IO.Path.Combine(parent, "F1-SECRET-" + System.Guid.NewGuid().ToString("N")[..8] + ".txt");
        var secretContent = "if-you-can-read-me-the-gate-was-bypassed-" + System.Guid.NewGuid().ToString("N");
        System.IO.File.WriteAllText(secretPath, secretContent);
        try
        {
            // Channel that denies any AuthGate prompt.
            app.User.Channels.Register(new CannedChannel("n"));

            var prPath = Path.Resolve(System.IO.Path.Combine(root, "subdir", ".build", "probe.pr"), ctx);
            ctx.Goal = new Goal
            {
                Name = "Probe",
                Path = Path.Resolve(System.IO.Path.Combine(root, "subdir", "probe.goal"), ctx),
                LoadedFromPrPath = prPath
            };

            var relative = "../../" + System.IO.Path.GetFileName(secretPath);
            var p = (FilePath)Path.Resolve(relative, ctx);

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
