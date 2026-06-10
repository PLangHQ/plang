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

    // --- Equals ---

    [Test]
    public async Task Equals_SameInts_Passes()
    {
        var (context, _) = CreateContext();
        var action = new AssertEquals { Context = context, Expected = D(42), Actual = D(42) };
        var result = await action.Run();
        await result.IsSuccess();
    }

    [Test]
    public async Task Equals_DifferentValues_Fails()
    {
        var (context, _) = CreateContext();
        var action = new AssertEquals { Context = context, Expected = D(42), Actual = D(99) };
        var result = await action.Run();
        await result.IsFailure();
        await Assert.That(result.Error).IsNotNull();
        await Assert.That(result.Error is AssertionError).IsTrue();
    }

    [Test]
    public async Task Equals_IntAndDouble_CoercesAndPasses()
    {
        var (context, _) = CreateContext();
        var action = new AssertEquals { Context = context, Expected = D(5), Actual = D(5.0) };
        var result = await action.Run();
        await result.IsSuccess();
    }

    [Test]
    public async Task Equals_Strings_Passes()
    {
        var (context, _) = CreateContext();
        var action = new AssertEquals { Context = context, Expected = D("hello"), Actual = D("hello") };
        var result = await action.Run();
        await result.IsSuccess();
    }

    [Test]
    public async Task Equals_NullBothSides_Passes()
    {
        var (context, _) = CreateContext();
        var action = new AssertEquals { Context = context, Expected = D(null), Actual = D(null) };
        var result = await action.Run();
        await result.IsSuccess();
    }

    [Test]
    public async Task Equals_NullVsValue_Fails()
    {
        var (context, _) = CreateContext();
        var action = new AssertEquals { Context = context, Expected = D(null), Actual = D(5) };
        var result = await action.Run();
        await result.IsFailure();
    }

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

    // --- NotEquals ---

    [Test]
    public async Task NotEquals_DifferentValues_Passes()
    {
        var (context, _) = CreateContext();
        var action = new AssertNotEquals { Context = context, Expected = D(1), Actual = D(2) };
        var result = await action.Run();
        await result.IsSuccess();
    }

    [Test]
    public async Task NotEquals_SameValues_Fails()
    {
        var (context, _) = CreateContext();
        var action = new AssertNotEquals { Context = context, Expected = D(5), Actual = D(5) };
        var result = await action.Run();
        await result.IsFailure();
    }

    // --- IsTrue ---

    [Test]
    public async Task IsTrue_TrueValue_Passes()
    {
        var (context, _) = CreateContext();
        var action = new AssertIsTrue { Context = context, Value = D(true) };
        var result = await action.Run();
        await result.IsSuccess();
    }

    [Test]
    public async Task IsTrue_FalseValue_Fails()
    {
        var (context, _) = CreateContext();
        var action = new AssertIsTrue { Context = context, Value = D(false) };
        var result = await action.Run();
        await result.IsFailure();
    }

    [Test]
    public async Task IsTrue_NonZeroNumber_Passes()
    {
        var (context, _) = CreateContext();
        var action = new AssertIsTrue { Context = context, Value = D(42) };
        var result = await action.Run();
        await result.IsSuccess();
    }

    [Test]
    public async Task IsTrue_Zero_Fails()
    {
        var (context, _) = CreateContext();
        var action = new AssertIsTrue { Context = context, Value = D(0) };
        var result = await action.Run();
        await result.IsFailure();
    }

    [Test]
    public async Task IsTrue_Null_Fails()
    {
        var (context, _) = CreateContext();
        var action = new AssertIsTrue { Context = context, Value = D(null) };
        var result = await action.Run();
        await result.IsFailure();
    }

    // --- IsFalse ---

    [Test]
    public async Task IsFalse_FalseValue_Passes()
    {
        var (context, _) = CreateContext();
        var action = new AssertIsFalse { Context = context, Value = D(false) };
        var result = await action.Run();
        await result.IsSuccess();
    }

    [Test]
    public async Task IsFalse_TrueValue_Fails()
    {
        var (context, _) = CreateContext();
        var action = new AssertIsFalse { Context = context, Value = D(true) };
        var result = await action.Run();
        await result.IsFailure();
    }

    [Test]
    public async Task IsFalse_Null_Passes()
    {
        var (context, _) = CreateContext();
        var action = new AssertIsFalse { Context = context, Value = D(null) };
        var result = await action.Run();
        await result.IsSuccess();
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

    // --- IsNull ---

    [Test]
    public async Task IsNull_NullValue_Passes()
    {
        var (context, _) = CreateContext();
        var action = new AssertIsNull { Context = context, Value = D(null) };
        var result = await action.Run();
        await result.IsSuccess();
    }

    [Test]
    public async Task IsNull_NonNullValue_Fails()
    {
        var (context, _) = CreateContext();
        var action = new AssertIsNull { Context = context, Value = D("hello") };
        var result = await action.Run();
        await result.IsFailure();
    }

    // --- IsNotNull ---

    [Test]
    public async Task IsNotNull_NonNullValue_Passes()
    {
        var (context, _) = CreateContext();
        var action = new AssertIsNotNull { Context = context, Value = D("hello") };
        var result = await action.Run();
        await result.IsSuccess();
    }

    [Test]
    public async Task IsNotNull_NullValue_Fails()
    {
        var (context, _) = CreateContext();
        var action = new AssertIsNotNull { Context = context, Value = D(null) };
        var result = await action.Run();
        await result.IsFailure();
    }

    // --- Contains ---

    [Test]
    public async Task Contains_StringContainsSubstring_Passes()
    {
        var (context, _) = CreateContext();
        var action = new AssertContains { Context = context, Value = D("hello world"), Container = D("world") };
        var result = await action.Run();
        await result.IsSuccess();
    }

    [Test]
    public async Task Contains_StringDoesNotContain_Fails()
    {
        var (context, _) = CreateContext();
        var action = new AssertContains { Context = context, Value = D("hello world"), Container = D("xyz") };
        var result = await action.Run();
        await result.IsFailure();
    }

    [Test]
    public async Task Contains_ListContainsElement_Passes()
    {
        var (context, _) = CreateContext();
        var list = new List<object> { 1, 2, 3 };
        var action = new AssertContains { Context = context, Value = D(list), Container = D(2) };
        var result = await action.Run();
        await result.IsSuccess();
    }

    [Test]
    public async Task Contains_ListDoesNotContain_Fails()
    {
        var (context, _) = CreateContext();
        var list = new List<object> { 1, 2, 3 };
        var action = new AssertContains { Context = context, Value = D(list), Container = D(99) };
        var result = await action.Run();
        await result.IsFailure();
    }

    [Test]
    public async Task Contains_NullValue_Fails()
    {
        var (context, _) = CreateContext();
        var action = new AssertContains { Context = context, Value = D(null), Container = D("x") };
        var result = await action.Run();
        await result.IsFailure();
    }

    // --- GreaterThan ---

    [Test]
    public async Task GreaterThan_LargerValue_Passes()
    {
        var (context, _) = CreateContext();
        var action = new AssertGreaterThan { Context = context, A = D(10), B = D(5) };
        var result = await action.Run();
        await result.IsSuccess();
    }

    [Test]
    public async Task GreaterThan_EqualValues_Fails()
    {
        var (context, _) = CreateContext();
        var action = new AssertGreaterThan { Context = context, A = D(5), B = D(5) };
        var result = await action.Run();
        await result.IsFailure();
    }

    [Test]
    public async Task GreaterThan_SmallerValue_Fails()
    {
        var (context, _) = CreateContext();
        var action = new AssertGreaterThan { Context = context, A = D(3), B = D(5) };
        var result = await action.Run();
        await result.IsFailure();
    }

    // --- LessThan ---

    [Test]
    public async Task LessThan_SmallerValue_Passes()
    {
        var (context, _) = CreateContext();
        var action = new AssertLessThan { Context = context, A = D(3), B = D(5) };
        var result = await action.Run();
        await result.IsSuccess();
    }

    [Test]
    public async Task LessThan_EqualValues_Fails()
    {
        var (context, _) = CreateContext();
        var action = new AssertLessThan { Context = context, A = D(5), B = D(5) };
        var result = await action.Run();
        await result.IsFailure();
    }

    [Test]
    public async Task LessThan_LargerValue_Fails()
    {
        var (context, _) = CreateContext();
        var action = new AssertLessThan { Context = context, A = D(10), B = D(5) };
        var result = await action.Run();
        await result.IsFailure();
    }
}
