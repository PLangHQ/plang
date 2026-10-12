using FilePath = global::app.type.item.path.file.@this;

namespace PLang.Tests.App.actions.terminal;

// A bare program name is the terminal's to find (its provider, beside Start): a test provider fakes finding as well as
// starting. A name with a folder in it is a plang path, never looked up on PATH.
public class ProgramFindTests : IDisposable
{
    private readonly string _root;
    private readonly global::app.@this _app;
    private readonly Fake _terminal = new();

    // finds what it is told it has, starts nothing: what it was asked and given is what the test reads
    private sealed class Fake : global::app.module.terminal.code.ITerminal
    {
        public string Name { get; init; } = "fake";
        public bool IsBuiltIn { get; set; }
        public string? Source { get; set; }
        public string? Has;
        public readonly List<string> Asked = new();
        public FilePath? Started;

        public FilePath? Find(string name, global::app.actor.context.@this context)
        {
            Asked.Add(name);
            return name == "tool" && Has != null ? new FilePath(Has) : null;
        }

        public Task<global::app.data.@this<global::app.module.terminal.type.process.@this>> Start(
            global::app.module.terminal.type.program.@this program, global::app.actor.context.@this context)
        {
            Started = program.File;
            return Task.FromResult(global::app.data.@this<global::app.module.terminal.type.process.@this>.From(
                context.Error(new global::app.error.ActionError("started nothing", "FakeStarted", 418))));
        }
    }

    public ProgramFindTests()
    {
        _root = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "plang_find_" + Guid.NewGuid().ToString("N"))).FullName;
        File.WriteAllText(Path.Combine(_root, "tool"), "");
        _app = new global::app.@this(_root).Testing();
        _app.Code.Register<global::app.module.terminal.code.ITerminal>(_terminal);
        _app.Code.SetDefault<global::app.module.terminal.code.ITerminal>("fake");
    }

    public void Dispose()
    {
        _app.DisposeAsync().AsTask().GetAwaiter().GetResult();
        Directory.Delete(_root, true);
    }

    private global::app.actor.context.@this Context => _app.actor.list.User.Context;

    private Task<global::app.data.@this> Start(string app) => Make.Action(Context, "terminal", "start", ("App", app)).Start(Context);

    [Test] public async Task ABareName_IsTheTerminalsToFind()
    {
        _terminal.Has = Path.Combine(_root, "tool");

        var started = await Start("tool");

        await Assert.That(_terminal.Asked).Contains("tool");
        await Assert.That(started.Error!.Key).IsEqualTo("FakeStarted");
        await Assert.That(_terminal.Started!.Absolute).IsEqualTo(new FilePath(Path.Combine(_root, "tool")).Absolute);
    }

    [Test] public async Task ABareNameTheTerminalDoesntFind_IsNotFound()
    {
        var started = await Start("tool");

        await Assert.That(started.Error!.Key).IsEqualTo("ProgramNotFound");
    }

    [Test] public async Task ANameWithAFolder_IsAPlangPath_NeverLookedUp()
    {
        var started = await Start("/tool");

        await Assert.That(_terminal.Asked).IsEmpty();
        await Assert.That(started.Error!.Key).IsEqualTo("FakeStarted");
    }
}
