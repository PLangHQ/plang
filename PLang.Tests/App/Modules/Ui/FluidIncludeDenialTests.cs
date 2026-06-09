using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using PLangEngine = global::app.@this;

namespace PLang.Tests.App.Modules.Ui;

/// <summary>
/// Fluid include-denial tests. Drives
/// <see cref="global::app.module.ui.code.Fluid.Render"/> with a real
/// <c>{% include %}</c> template. The handler instantiates
/// <c>PlangFileProvider</c>+<c>PlangFileInfo</c>, which route reads through
/// <c>path.ReadText</c>. A mutation that reverted to <c>System.IO.File.ReadAllText</c>
/// in <c>PlangFileInfo.CreateReadStream</c> would flip the denial test red.
/// </summary>
public class FluidIncludeDenialTests
{
    private sealed class CannedChannel : global::app.channel.@this
    {
        public int AskCount;
        private readonly string _answer;
        public CannedChannel(string answer) { _answer = answer; Name = "input"; Direction = global::app.channel.ChannelDirection.Bidirectional; }
        public override Task<global::app.data.@this> Write(global::app.data.@this data, CancellationToken ct = default) => Task.FromResult(global::app.data.@this.Ok());
        public override Task<global::app.data.@this> Read(CancellationToken ct = default) => Task.FromResult(global::app.data.@this.Ok((object?)null));
        public override Task<global::app.data.@this> Ask(global::app.module.output.ask action, CancellationToken ct = default)
        {
            System.Threading.Interlocked.Increment(ref AskCount);
            return Task.FromResult(global::app.data.@this.Ok(_answer));
        }
    }

    private static PLangEngine NewApp(out string root)
    {
        root = System.IO.Path.Combine(System.IO.Path.GetTempPath(),
            "plang-fluid-" + System.Guid.NewGuid().ToString("N")[..8]);
        System.IO.Directory.CreateDirectory(root);
        return new PLangEngine(root);
    }

    [Test] public async Task FluidInclude_TemplateOutsideRoot_DeniedByAuthGate()
    {
        var app = NewApp(out var root);
        app.User.Channel.Register(new CannedChannel("n"));
        // Anchor the goal under the App root so GetTemplateBaseDir picks
        // the goal's parent; the include path "../../foreign/secret.liquid"
        // walks out and AuthGate denies.
        var goal = new Goal
        {
            Name = "Host",
            Path = global::app.type.path.@this.Resolve("/host.goal", app.User.Context)
        };
        app.User.Context.Goal = goal;
        var outOfRoot = System.IO.Path.Combine(System.IO.Path.GetTempPath(),
            "plang-foreign-" + System.Guid.NewGuid().ToString("N")[..8]);
        System.IO.Directory.CreateDirectory(outOfRoot);
        System.IO.File.WriteAllText(System.IO.Path.Combine(outOfRoot, "secret.liquid"), "SECRET_TOKEN");

        var fluid = new global::app.module.ui.code.Fluid();
        var action = new global::app.module.ui.Render
        {
            Context = app.User.Context,
            Template = new global::app.data.@this<global::app.type.text.@this>("Template",
                "{% include '" + outOfRoot + "/secret.liquid' %}"),
            IsFile = new global::app.data.@this<global::app.type.@bool.@this>("IsFile", false)
        };
        var result = await fluid.Render(action);
        // The output MUST NOT contain the secret file's content. Either the
        // include silently no-ops to empty (Fluid's NotFoundFileInfo path) or
        // surfaces an error; either way, no leakage.
        await Assert.That(((await result.Value()) ?? "").ToString()!).DoesNotContain("SECRET_TOKEN");
    }

    [Test] public async Task FluidInclude_InRootTemplate_RendersSilently()
    {
        var app = NewApp(out var root);
        var ch = new CannedChannel("UNEXPECTED");
        app.User.Channel.Register(ch);
        // In-root partial: goal at /host.goal, partial at /partials/footer.liquid.
        var partialsDir = System.IO.Path.Combine(root, "partials");
        System.IO.Directory.CreateDirectory(partialsDir);
        System.IO.File.WriteAllText(System.IO.Path.Combine(partialsDir, "footer.liquid"), "Hello footer");

        var goal = new Goal
        {
            Name = "Host",
            Path = global::app.type.path.@this.Resolve("/host.goal", app.User.Context)
        };
        app.User.Context.Goal = goal;

        var fluid = new global::app.module.ui.code.Fluid();
        var action = new global::app.module.ui.Render
        {
            Context = app.User.Context,
            Template = new global::app.data.@this<global::app.type.text.@this>("Template", "{% include 'partials/footer.liquid' %}"),
            IsFile = new global::app.data.@this<global::app.type.@bool.@this>("IsFile", false)
        };
        var result = await fluid.Render(action);
        await result.IsSuccess();
        await Assert.That((await result.Value())!).Contains("Hello footer");
        await Assert.That(ch.AskCount).IsEqualTo(0);
    }
}
