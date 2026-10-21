using PLangPath = global::app.type.item.path.@this;
using SchemeKind = global::app.type.item.path.scheme.@this;

namespace PLang.Tests.App.Types;

/// <summary>A type's kinds looked up by key: the apps sharing a type look their keys up at once, without a lock, and a
/// kind added later answers its key at the next look.</summary>
public class KindLookupTests
{
    private static SchemeKind Scheme(global::app.@this app, string name)
        => new(name, app.type.list[typeof(PLangPath)], (raw, c) => c.File("/" + name));

    [Test] public async Task AKindAddedAfterALookup_AnswersItsKey()
    {
        await using var app = global::PLang.Tests.Session.App("/app").Testing();
        var path = app.type.list[typeof(PLangPath)].kind;
        await Assert.That(path["late"]).IsNull();

        app.type.list.Add(Scheme(app, "late"));

        await Assert.That(path["late"]?.Name).IsEqualTo("late")
            .Because("the keys made by the first look are dropped when a kind comes in, and made again");
    }

    [Test] public async Task ManyLookupsAtOnce_WhileKindsComeIn_EachAnswerIsWhole()
    {
        await using var app = global::PLang.Tests.Session.App("/app").Testing();
        var path = app.type.list[typeof(PLangPath)].kind;
        var wrong = 0;
        using var adding = new System.Threading.CancellationTokenSource();

        var readers = Enumerable.Range(0, 8).Select(_ => Task.Run(() =>
        {
            for (var i = 0; i < 20_000; i++)
                if (path["file"]?.Name != "file" || path["http"]?.Name != "http" || path["nothing-by-this-name"] != null)
                    System.Threading.Interlocked.Increment(ref wrong);
        })).ToArray();
        var writer = Task.Run(() =>
        {
            // a kind of the same name replaces the one before it: the type stays its size while its keys are made again and again
            for (var i = 0; !adding.IsCancellationRequested; i++) app.type.list.Add(Scheme(app, "s" + i % 50));
        });

        await Task.WhenAll(readers);
        adding.Cancel();
        await writer;

        await Assert.That(wrong).IsEqualTo(0)
            .Because("a lookup reads a table made whole, never one being filled");
        await Assert.That(path["s0"]?.Name).IsEqualTo("s0");
    }
}
