namespace PLang.Tests.App.Types;

/// <summary>A type's kinds are looked up without the type's lock: a type is shared by every app made from the same
/// code, so its lookups are read off what an addition last published — whole, and never a half-made table.</summary>
public class KindLookupTests
{
    private static global::app.type.kind.empty.@this Kinds(string name)
        => (global::app.type.kind.empty.@this)new global::app.type.@this(name).kind;

    [Test]
    public async Task AKindAddedAfterALookup_AnswersItsKey()
    {
        var kinds = Kinds("probe");
        await Assert.That(kinds["late"]).IsNull();

        var late = new global::app.type.kind.@this("late");
        kinds.Add(late);

        await Assert.That(kinds["late"]).IsSameReferenceAs(late);
        await Assert.That(kinds.Kinds).Contains(late);
    }

    [Test]
    public async Task ReadersLookingUpWhileKindsComeIn_AlwaysFindWhatWasThere()
    {
        var kinds = Kinds("probe");
        var first = new global::app.type.kind.@this("first");
        kinds.Add(first);
        var missed = 0;
        var faulted = 0;
        using var stop = new System.Threading.CancellationTokenSource();

        var readers = Enumerable.Range(0, 8).Select(_ => Task.Run(() =>
        {
            while (!stop.IsCancellationRequested)
            {
                try
                {
                    if (!ReferenceEquals(kinds["first"], first)) System.Threading.Interlocked.Increment(ref missed);
                    if (!kinds.Kinds.Contains(first)) System.Threading.Interlocked.Increment(ref missed);
                }
                catch (System.Exception) { System.Threading.Interlocked.Increment(ref faulted); }
            }
        })).ToArray();
        for (var i = 0; i < 2_000; i++) kinds.Add(new global::app.type.kind.@this("k" + i));
        stop.Cancel();
        await Task.WhenAll(readers);

        await Assert.That(faulted).IsEqualTo(0).Because("a lookup never meets a table while it is being written");
        await Assert.That(missed).IsEqualTo(0).Because("a kind that was there is always found");
        await Assert.That(kinds["k1999"]).IsNotNull();
    }
}
