using app.actor.context;
using app;
using app.error;
using app.variable;
using AssertEquals = global::app.module.assert.Equals;
using AssertNotEquals = global::app.module.assert.NotEquals;
using AssertIsTrue = global::app.module.assert.IsTrue;
using AssertIsFalse = global::app.module.assert.IsFalse;
using AssertIsNull = global::app.module.assert.IsNull;
using AssertIsNotNull = global::app.module.assert.IsNotNull;
using AssertContains = global::app.module.assert.Contains;
using AssertGreaterThan = global::app.module.assert.GreaterThan;
using AssertLessThan = global::app.module.assert.LessThan;

namespace PLang.Tests.App.actions.assert;

public class AssertTests
{
    private (global::app.actor.context.@this context, Variables memory) CreateContext()
    {
        var app = new global::app.@this("/app");
        return (app.User.Context, app.User.Context.Variable);
    }

    private static Data D(object? value) => value == null ? new Data("") : Data.Ok(value);








    [Test]
    public async Task Equals_CustomMessage_IncludedInError()
    {
        var (context, _) = CreateContext();
        var action = new AssertEquals { Context = context, Expected = D(1), Actual = D(2), Message = (global::app.type.text.@this)"Sum check" };
        var result = await action.Run();
        await result.IsFailure();
        var error = result.Error as AssertionError;
        await Assert.That(error).IsNotNull();
        await Assert.That(error!.UserMessage).IsEqualTo("Sum check");
    }














    // --- IsTrue/IsFalse over a path ---
    //
    // ResolveTruthy must route an IBooleanResolvable value (a path) through
    // Data.ToBooleanAsync — not fall to IsTruthy, whose `return true`
    // catch-all for any non-null object is the always-true bug class.
    // Deleting the IBooleanResolvable branch in ResolveTruthy flips both
    // Missing_Fails / Existing_Passes red — they're the regression guard.

    private static (global::app.@this app, string root) MakeAppRoot(string tag)
    {
        var root = System.IO.Path.Combine(System.IO.Path.GetTempPath(),
            "plang-assertpath-" + tag + "-" + System.Guid.NewGuid().ToString("N"));
        System.IO.Directory.CreateDirectory(root);
        return (new global::app.@this(root), root);
    }

    [Test]
    public async Task IsTrue_PathToExistingFile_Passes()
    {
        var (app, root) = MakeAppRoot("istrue-yes");
        var filePath = System.IO.Path.Combine(root, "exists.txt");
        System.IO.File.WriteAllText(filePath, "x");
        var fp = new global::app.type.path.file.@this(filePath, app.User.Context);
        var action = new AssertIsTrue { Context = app.User.Context, Value = D(fp) };
        var result = await action.Run();
        await result.IsSuccess();
        System.IO.Directory.Delete(root, true);
    }

    [Test]
    public async Task IsTrue_PathToMissingFile_Fails()
    {
        var (app, root) = MakeAppRoot("istrue-no");
        var missing = System.IO.Path.Combine(root, "nope.txt");
        var fp = new global::app.type.path.file.@this(missing, app.User.Context);
        var action = new AssertIsTrue { Context = app.User.Context, Value = D(fp) };
        var result = await action.Run();
        await result.IsFailure();
        await Assert.That(result.Error is AssertionError).IsTrue();
        System.IO.Directory.Delete(root, true);
    }

    [Test]
    public async Task IsFalse_PathToMissingFile_Passes()
    {
        var (app, root) = MakeAppRoot("isfalse-yes");
        var missing = System.IO.Path.Combine(root, "still-nope.txt");
        var fp = new global::app.type.path.file.@this(missing, app.User.Context);
        var action = new AssertIsFalse { Context = app.User.Context, Value = D(fp) };
        var result = await action.Run();
        await result.IsSuccess();
        System.IO.Directory.Delete(root, true);
    }

    [Test]
    public async Task IsFalse_PathToExistingFile_Fails()
    {
        var (app, root) = MakeAppRoot("isfalse-no");
        var filePath = System.IO.Path.Combine(root, "really-here.txt");
        System.IO.File.WriteAllText(filePath, "x");
        var fp = new global::app.type.path.file.@this(filePath, app.User.Context);
        var action = new AssertIsFalse { Context = app.User.Context, Value = D(fp) };
        var result = await action.Run();
        await result.IsFailure();
        await Assert.That(result.Error is AssertionError).IsTrue();
        System.IO.Directory.Delete(root, true);
    }





















    // --- value-based assertions (data-driven; a failing row names itself) ---

    private static async Task Expect(global::app.data.@this result, bool pass, string label)
    {
        if (pass) await result.IsSuccess();
        else { await result.IsFailure(); await Assert.That(result.Error is AssertionError).IsTrue().Because(label); }
    }

    [Test]
    public async Task Equals_Classifies()
    {
        var (context, _) = CreateContext();
        (object? exp, object? act, bool pass, string label)[] table =
        {
            (42, 42, true, "same ints"), (42, 99, false, "different"),
            (5, 5.0, true, "int coerces to double"), ("hello", "hello", true, "strings"),
            (null, null, true, "both null"), (null, 5, false, "null vs value"),
        };
        foreach (var (exp, act, pass, label) in table)
            await Expect(await new AssertEquals { Context = context, Expected = D(exp), Actual = D(act) }.Run(), pass, label);
    }

    [Test]
    public async Task NotEquals_Classifies()
    {
        var (context, _) = CreateContext();
        (object? a, object? b, bool pass, string label)[] table = { (1, 2, true, "different"), (1, 1, false, "same") };
        foreach (var (a, b, pass, label) in table)
            await Expect(await new AssertNotEquals { Context = context, Expected = D(a), Actual = D(b) }.Run(), pass, label);
    }

    [Test]
    public async Task IsTrue_Classifies()
    {
        var (context, _) = CreateContext();
        (object? v, bool pass, string label)[] table =
        { (true, true, "true"), (false, false, "false"), (42, true, "nonzero"), (0, false, "zero"), (null, false, "null") };
        foreach (var (v, pass, label) in table)
            await Expect(await new AssertIsTrue { Context = context, Value = D(v) }.Run(), pass, label);
    }

    [Test]
    public async Task IsFalse_Classifies()
    {
        var (context, _) = CreateContext();
        (object? v, bool pass, string label)[] table = { (false, true, "false"), (true, false, "true"), (null, true, "null") };
        foreach (var (v, pass, label) in table)
            await Expect(await new AssertIsFalse { Context = context, Value = D(v) }.Run(), pass, label);
    }

    [Test]
    public async Task IsNull_Classifies()
    {
        var (context, _) = CreateContext();
        await Expect(await new AssertIsNull { Context = context, Value = D(null) }.Run(), true, "null");
        await Expect(await new AssertIsNull { Context = context, Value = D("x") }.Run(), false, "non-null");
    }

    [Test]
    public async Task IsNotNull_Classifies()
    {
        var (context, _) = CreateContext();
        await Expect(await new AssertIsNotNull { Context = context, Value = D("hello") }.Run(), true, "non-null");
        await Expect(await new AssertIsNotNull { Context = context, Value = D(null) }.Run(), false, "null");
    }

    [Test]
    public async Task Contains_Classifies()
    {
        var (context, _) = CreateContext();
        var list = new List<object> { 1, 2, 3 };
        (object? value, object? container, bool pass, string label)[] table =
        {
            ("hello world", "world", true, "string contains"),
            ("hello", "world", false, "string missing"),
            (list, 2, true, "list contains"),
            (list, 5, false, "list missing"),
            (null, "x", false, "null value"),
        };
        foreach (var (value, container, pass, label) in table)
            await Expect(await new AssertContains { Context = context, Value = D(value), Container = D(container) }.Run(), pass, label);
    }

    [Test]
    public async Task GreaterThan_Classifies()
    {
        var (context, _) = CreateContext();
        (object? a, object? b, bool pass, string label)[] table = { (10, 5, true, "larger"), (5, 5, false, "equal"), (3, 5, false, "smaller") };
        foreach (var (a, b, pass, label) in table)
            await Expect(await new AssertGreaterThan { Context = context, A = D(a), B = D(b) }.Run(), pass, label);
    }

    [Test]
    public async Task LessThan_Classifies()
    {
        var (context, _) = CreateContext();
        (object? a, object? b, bool pass, string label)[] table = { (3, 5, true, "smaller"), (5, 5, false, "equal"), (10, 5, false, "larger") };
        foreach (var (a, b, pass, label) in table)
            await Expect(await new AssertLessThan { Context = context, A = D(a), B = D(b) }.Run(), pass, label);
    }
}
