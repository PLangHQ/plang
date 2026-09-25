namespace PLang.Tests.App.Modules.builder;

/// <summary>
/// The program holds no Data: an action's properties hold their values raw. The .pr carries what
/// the step set under "property" and what the build froze under "default"; the old "parameter"
/// key fails loudly. A program action is judged against its catalog twin.
/// </summary>
public class ActionPropertyPrTests
{
    private global::app.@this _app = null!;

    [Before(Test)]
    public void Setup() => _app = TestApp.Create("/tmp/actionprop-" + System.Guid.NewGuid().ToString("N")[..8]);

    [After(Test)]
    public async Task Cleanup() => await _app.DisposeAsync();

    private async Task<string> Write(global::app.goal.@this goal)
    {
        var serializer = (global::app.channel.serializer.plang.@this)
            _app.User.Channel.Serializers.GetOrDefault("application/plang");
        using var ms = new System.IO.MemoryStream();
        await serializer.SerializeItemAsync(ms, goal, global::app.View.Store);
        return System.Text.Encoding.UTF8.GetString(ms.ToArray());
    }

    private async Task<global::app.data.@this> Read(string pr)
    {
        var channel = new global::app.channel.type.stream.@this("pr", new System.IO.MemoryStream(System.Text.Encoding.UTF8.GetBytes(pr)),
            global::app.channel.ChannelDirection.Input, ownsStream: true) { Mime = "application/plang-goal" };
        _app.User.Channel.Register(channel);
        return await channel.Read();
    }

    private static global::app.goal.@this Sample() => Make.Goal("Start",
        Make.Step("read a file",
            Make.WithDefaults(Make.Action("file", "read", ("Path", "notes.txt")), ("ResolveVariables", false))));

    [Test] public async Task WrittenPr_ReadsBackAndWritesAgain_ByteIdentical()
    {
        var first = await Write(Sample());
        var read = await Read(first);
        var goal = (await read.Value()) as global::app.goal.@this;

        var second = await Write(goal!);

        await Assert.That(first).Contains("\"property\"");
        await Assert.That(first).Contains("\"default\"");
        await Assert.That(second).IsEqualTo(first);
    }

    [Test] public async Task OldParameterKey_FailsLoudly_WithTheNamedError()
    {
        var old = (await Write(Sample())).Replace("\"property\":", "\"parameter\":");

        global::app.error.AppException? thrown = null;
        global::app.data.@this? read = null;
        try { read = await Read(old); if (read.Success) await read.Value(); }
        catch (global::app.error.AppException ex) { thrown = ex; }

        var key = thrown?.Key ?? read?.Error?.Key ?? read?.Error?.list?.FirstOrDefault()?.Key;
        await Assert.That(key).IsEqualTo("PrFormatOutdated");
    }

    [Test] public async Task LoadedProgram_HoldsNoData()
    {
        var read = await Read(await Write(Sample()));
        var action = ((await read.Value()) as global::app.goal.@this)!.Step[0].Code[0];

        await Assert.That(action.Property.Count).IsEqualTo(1);
        await Assert.That(action.Default.Count).IsEqualTo(1);
        foreach (var property in action.Property.Concat(action.Default))
            await Assert.That(property.Value).IsNotTypeOf<global::app.data.@this>();
    }

    [Test] public async Task Validate_APropertyTheClassDoesNotDeclare_Fails()
    {
        var action = Make.Action("file", "read", ("Path", "notes.txt"), ("Bogus", 1));

        var verdict = await action.Validate(_app.User.Context);

        await Assert.That(verdict).IsNotNull();
        await Assert.That(verdict!.list!.Select(c => c.Key)).Contains("UnknownProperty");
    }

    [Test] public async Task Validate_AMissingRequiredProperty_Fails()
    {
        var action = Make.Action("file", "read");

        var verdict = await action.Validate(_app.User.Context);

        await Assert.That(verdict).IsNotNull();
        await Assert.That(verdict!.list!.Select(c => c.Key)).Contains("MissingProperty");
    }
}
