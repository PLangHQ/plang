namespace PLang.Tests.App.Modules.variable;

/// <summary>
/// `set %x% = … as T` reads its declared type off the program row, which every run shares. A run
/// never changes that type object: a canonicalised kind (`markdown` → `md`) or a derived kind
/// (`5` → `int`) is the run's own type, and the row keeps what the builder wrote.
/// </summary>
public class SetLeavesProgramTypeTests
{
    private global::app.@this _app = null!;

    [Before(Test)]
    public void Setup() => _app = TestApp.Create("/tmp/setprogramtype-" + System.Guid.NewGuid().ToString("N")[..8]);

    [After(Test)]
    public async Task Cleanup() => await _app.DisposeAsync();

    // A C#-composed variable.set: its Type row holds the type object itself, so every run shares it.
    // (A .pr-read row holds the type unloaded; each run's copy loads its own.)
    private (global::app.goal.step.action.@this Action, global::app.type.@this RowType) SetComposed(object? value, global::app.type.@this type)
    {
        var ctx = _app.User.Context;
        var action = global::PLang.Tests.Shared.Make.Action("variable", "set",
            ("Name", new Data("Name", "x", new global::app.type.@this("variable"), context: ctx)),
            ("Value", value),
            ("Type", type));
        return (action, type);
    }

    [Test]
    public async Task CanonicalisedKind_RunTwice_RowTypeUnchanged()
    {
        var (set, rowType) = SetComposed("hello", new global::app.type.@this("text", "markdown"));

        await (await set.Run(_app.User.Context)).IsSuccess();
        await (await set.Run(_app.User.Context)).IsSuccess();

        await Assert.That(rowType.Kind?.Name).IsEqualTo("markdown");
    }

    [Test]
    public async Task UnknownTypeName_IsThePlangErrorUnknownType()
    {
        var (set, _) = SetComposed(5, new global::app.type.@this("foo"));

        var result = await set.Run(_app.User.Context);

        await result.IsFailure();
        await Assert.That(result.Error!.Key).IsEqualTo("UnknownType");
        await Assert.That(result.Error!.Message).Contains("Unknown type 'foo'");
    }

    [Test]
    public async Task DerivedKind_RunTwice_RowTypeUnchanged()
    {
        var (set, rowType) = SetComposed(5, new global::app.type.@this("number"));

        await (await set.Run(_app.User.Context)).IsSuccess();
        await (await set.Run(_app.User.Context)).IsSuccess();

        await Assert.That(rowType.Kind).IsNull();
    }
}
