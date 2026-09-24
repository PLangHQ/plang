using PLang.Tests.Shared;

namespace PLang.Tests.App.Errors;

/// <summary>
/// An error is shown by plang: <c>/system/error/Show</c> renders the template for the error's status
/// code (<c>400.txt</c> short, <c>500.txt</c> full) and writes it to the error channel. The app here is
/// rooted at the repo's <c>os/</c> folder, so the real goal and templates run.
/// </summary>
public class ErrorShowTests : System.IAsyncDisposable
{
    private readonly global::app.@this _app = TestApp.Create(System.IO.Path.Combine(RepoRoot(), "os"));
    private readonly System.IO.MemoryStream _errorOut = new();

    public ErrorShowTests()
    {
        _app.User.Channel.Register(new StreamChannel(
            global::app.channel.list.@this.Error, _errorOut, ChannelDirection.Output, ownsStream: true) { Mime = "text/plain" });
    }

    public async System.Threading.Tasks.ValueTask DisposeAsync() => await _app.DisposeAsync();

    private static string RepoRoot()
    {
        var dir = System.AppContext.BaseDirectory;
        while (dir != null && !System.IO.Directory.Exists(System.IO.Path.Combine(dir, "PLang", "app")))
            dir = System.IO.Directory.GetParent(dir)?.FullName;
        return dir!;
    }

    // Show, as the app runs it after a failed run: the error handed in by name.
    private async Task<string> Show(global::app.error.Error error)
    {
        var context = _app.User.Context;
        var loaded = await _app.Goal.Load(global::app.type.item.path.@this.Resolve("/system/error/.build/show.pr", context));
        await loaded.IsSuccess();
        var show = (await loaded.Value() as Goal)!;
        global::app.data.@this shown;
        await using (context.Variable.Calls.Push(new[] { new global::app.data.@this("error", error, context: context) }))
            shown = await show.Run(context);
        await shown.IsSuccess();
        return System.Text.Encoding.UTF8.GetString(_errorOut.ToArray());
    }

    private async Task<global::app.goal.@this> Goal(string text)
    {
        var built = Make.Goal("Start", "/Start.goal",
            Make.Step(text, Make.Action("variable", "set", Make.Param("Name", "x", "variable"), ("Value", 1))));
        built.Step[0].LineNumber = 3;
        return await RealGoalLoad.ViaChannel(_app, built);
    }

    [Test]
    public async Task Show_A400_IsShort()
    {
        var goal = await Goal("read file %name%");
        var error = new global::app.error.ValidationError("name is required", goal.Step[0]) { FixSuggestion = "set %name% first" };

        var text = await Show(error);

        await Assert.That(text).Contains("ValidationError (400): name is required");
        await Assert.That(text).Contains("/Start.goal:3");
        await Assert.That(text).Contains("read file %name%");
        await Assert.That(text).Contains("Fix: set %name% first");
        await Assert.That(text).DoesNotContain("==================");
    }

    [Test]
    public async Task Show_A500_IsFull()
    {
        var goal = await Goal("call api");
        System.Exception thrown;
        try { throw new System.InvalidOperationException("Boom"); } catch (System.Exception ex) { thrown = ex; }
        var error = new global::app.error.Error("API call failed", goal.Step[0], "ApiError", 500)
        {
            Exception = thrown,
            FixSuggestion = "Check the key",
            HelpfulLinks = "https://docs.example.com/api",
        };
        error.list.Add(new global::app.error.Error("retry failed too", goal.Step[0], "RetryFailed", 500));

        var text = await Show(error);

        await Assert.That(text).Contains("ApiError (500)");
        await Assert.That(text).Contains("/Start.goal:3");
        await Assert.That(text).Contains("call api");
        await Assert.That(text).Contains("API call failed");
        await Assert.That(text).Contains("Check the key");
        await Assert.That(text).Contains("https://docs.example.com/api");
        await Assert.That(text).Contains("InvalidOperationException: Boom");
        await Assert.That(text).Contains("Error during error handling [1]");
        await Assert.That(text).Contains("RetryFailed (500): retry failed too");
    }

    // Repeated (recursive) frames fold into one line with ×N; the failing frame stays on its own.
    [Test]
    public async Task Show_A500_FoldsRepeatedCallFrames()
    {
        var goal = await Goal("recurse");
        var action = goal.Step[0].Action[0];
        var context = _app.User.Context;
        global::app.error.Error error;
        await using (context.CallStack.Push(action, context.Variable))
        await using (context.CallStack.Push(action, context.Variable))
        await using (context.CallStack.Push(action, context.Variable))
        await using (var failing = context.CallStack.Push(action, context.Variable))
        {
            error = new global::app.error.Error("too deep", goal.Step[0], "Deep", 500);
            failing.Record(error, context);
        }

        var text = await Show(error);

        await Assert.That(text).Contains("Start ×3 - /Start.goal:3");
        await Assert.That(text.Split('\n').Count(l => l.Trim() == "Start - /Start.goal:3")).IsEqualTo(1);
    }
}
