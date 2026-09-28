namespace PLang.Tests.App.Types;

// A value's birth goes through its type's on.create (%!app.type.text.on.create%): before is handed the raw and
// may refuse or answer instead, after is handed the value. The build inside it (Make) fires nothing, so a birth
// fires once however many re-types it runs.
public class CreateEventTests : System.IAsyncDisposable
{
    private readonly global::app.@this app = global::PLang.Tests.TestApp.Create(
        System.IO.Path.Combine(System.IO.Path.GetTempPath(), "plang-create-event-" + System.Guid.NewGuid().ToString("N")[..8]));
    public async System.Threading.Tasks.ValueTask DisposeAsync() => await app.DisposeAsync();

    private global::app.actor.context.@this Ctx => app.User.Context;

    private global::app.@event.binding.@this On(string type, global::app.@event.When when,
        System.Func<global::app.data.@this, global::app.actor.context.@this, global::app.data.@this> handler)
        => app.type.list[type].Own().Bind("create", when,
            (_, data, c) => System.Threading.Tasks.Task.FromResult(handler(data, c)), app.User, global::app.@event.binding.Scope.actor);

    [Test] public async Task ABeforeThatRefuses_IsTheResult_NothingIsMade()
    {
        On("text", global::app.@event.When.before, (_, c) => c.Error(new global::app.error.Error("not today", "Refused", 403)));

        var born = await app.type.list["text"].Create("hello", Ctx);

        await born.IsFailure();
        await Assert.That(born.Error!.Key).IsEqualTo("Refused");
    }

    [Test] public async Task ABeforeThatAnswers_IsTheResult()
    {
        On("text", global::app.@event.When.before, (_, c) =>
        {
            var answer = c.Ok("instead");
            answer.Handled = true;
            return answer;
        });

        var born = await app.type.list["text"].Create("hello", Ctx);

        await born.IsSuccess();
        await Assert.That((await born.Value())?.ToString()).IsEqualTo("instead");
    }

    [Test] public async Task AnAfter_IsHandedTheMadeValue()
    {
        object? seen = null;
        On("text", global::app.@event.When.after, (data, _) => { seen = data.Peek(); return data; });

        var born = await app.type.list["text"].Create("hello", Ctx, "greeting");

        await born.IsSuccess();
        await Assert.That(born.Name).IsEqualTo("greeting");
        await Assert.That(seen).IsSameReferenceAs(born.Peek());
        await Assert.That(seen?.ToString()).IsEqualTo("hello");
    }

    [Test] public async Task NothingBound_TheValueIsMade_NothingFires()
    {
        var number = app.type.list["number"];
        await Assert.That(number.on.create.IsBound(number, Ctx)).IsFalse();

        var born = await number.Create(5, Ctx);

        await born.IsSuccess();
        await Assert.That((await born.Value())?.ToString()).IsEqualTo("5");
    }

    [Test] public async Task ABirthThatRetypes_FiresOnce()
    {
        // a text leaf born as a number re-types inside the build — the event is the birth's, once
        var fired = 0;
        On("number", global::app.@event.When.before, (data, _) => { fired++; return data; });

        var born = await app.type.list["number"].Create(new global::app.type.item.text.@this("42"), Ctx);

        await born.IsSuccess();
        await Assert.That((await born.Value())?.ToString()).IsEqualTo("42");
        await Assert.That(fired).IsEqualTo(1);
    }

    [Test] public async Task ReadingAFile_IsTheBirthOfAFile_ThroughItsTypesCreate()
    {
        System.IO.Directory.CreateDirectory(app.AbsolutePath);
        await System.IO.File.WriteAllTextAsync(System.IO.Path.Combine(app.AbsolutePath, "born.txt"), "x");
        object? seen = null;
        On("file", global::app.@event.When.after, (data, _) => { seen = data.Peek(); return data; });

        var landed = await global::app.type.item.path.@this.Resolve("born.txt", Ctx).Read(Ctx);

        await landed.IsSuccess();
        await Assert.That(seen).IsTypeOf<global::app.type.item.file.@this>();
        await Assert.That(seen).IsSameReferenceAs(landed.Peek());
    }

    [Test] public async Task AFileMadeFromAPath_IsTheReferenceToIt_WithTheDeclaredTemplate()
    {
        var path = global::app.type.item.path.@this.Resolve("some.txt", Ctx);
        var type = app.type.list[new global::app.type.@this("file", (string?)null, template: "plang"), Ctx];

        var born = await type.Create(path, Ctx);

        await born.IsSuccess();
        await Assert.That(born.Peek()).IsTypeOf<global::app.type.item.file.@this>();
        await Assert.That(((global::app.type.item.file.@this)born.Peek()).Path).IsSameReferenceAs(path);
        await Assert.That(born.Peek().Template).IsEqualTo("plang");
    }

    [Test] public async Task DecodedContent_IsABirth_ThroughItsTypesCreate()
    {
        object? seen = null;
        On("text", global::app.@event.When.after, (data, _) => { seen = data.Peek(); return data; });

        var decoded = await Ctx.Format("text/plain").Decode(System.Text.Encoding.UTF8.GetBytes("from a file"), Ctx, "content");

        await decoded.IsSuccess();
        await Assert.That(seen).IsNotNull();
        await Assert.That((await decoded.Value())?.ToString()).IsEqualTo("from a file");
    }
}
