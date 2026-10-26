namespace PLang.Tests.App.SnapshotTests;

// What a snapshot holds, through the text a file or a db keeps of it — the one door a snapshot comes back by.
public class SnapshotThroughTextTests
{
    [Test]
    public async Task AnLlmAnswersConversation_InAVariable_ComesBackThroughText()
    {
        // an llm answer carries its conversation on its own properties — what `{continue: %answer%}` reads back
        var src = new global::app.@this("/src", global::PLang.Tests.Parent.Actor).Testing();
        var context = src.actor.list.User.Context;
        var answer = new global::app.data.@this("answer", "hello there", context: context);
        answer.Properties["Messages"] = new List<LlmMessage>
        {
            new() { Role = "user", Content = "say hello" },
            new() { Role = "assistant", Content = "hello there" },
        };
        answer.Properties["Schema"] = "{\"type\":\"string\"}";
        await context.Variable.Set("answer", answer);
        var text = await src.Snapshot(context).Saved().Text(context);

        var dst = new global::app.@this("/dst", global::PLang.Tests.Parent.Actor).Testing();
        var into = dst.actor.list.User.Context;
        await dst.Restore(await into.Snapshot(text), into);

        var back = await into.Variable.Get("answer");
        await Assert.That((await back.Value())?.ToString()).IsEqualTo("hello there");
        var history = (await back.Properties.Value("Messages") as global::app.type.item.@this)?.Clr<List<LlmMessage>>();
        await Assert.That(history).IsNotNull();
        await Assert.That(history!.Select(message => $"{message.Role}:{message.Content}"))
            .IsEquivalentTo(new[] { "user:say hello", "assistant:hello there" });
        await Assert.That((await back.Properties.Value("Schema"))?.ToString()).IsEqualTo("{\"type\":\"string\"}");
    }

    [Test]
    public async Task AOneTimeGrant_IsNeverInTheSnapshotsText()
    {
        // a "y" grant read back from a file would be consent nobody gave in the run that uses it — a resumed run asks again
        var app = new global::app.@this("/app", global::PLang.Tests.Parent.Actor).Testing();
        var user = app.actor.list.User;
        var place = "/granted-once-" + System.Guid.NewGuid().ToString("N")[..8];
        var path = global::app.type.item.path.@this.Resolve(place, user.Context);
        var permission = new global::app.type.item.permission.@this(
            Actor: user.Name, Path: path.Location(user.Context.Filesystem).Absolute,
            Verbs: new HashSet<global::app.type.item.permission.Verb> { global::app.type.item.permission.Verb.read },
            Match: global::app.type.item.permission.Match.exact, app.type.list[typeof(global::app.type.item.permission.@this)]);
        await user.Permission.Add(new global::app.data.@this<global::app.type.item.permission.@this>("", permission, context: user.Context), persist: false);

        var text = await app.Snapshot(user.Context).Saved().Text(user.Context);

        await Assert.That(text).DoesNotContain(place[1..]);
    }
}
