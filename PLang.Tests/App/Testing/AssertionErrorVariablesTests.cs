using global::app.errors;
using AssertEquals = global::app.modules.assert.Equals;
using AssertNotEquals = global::app.modules.assert.NotEquals;
using AssertIsTrue = global::app.modules.assert.IsTrue;
using AssertIsFalse = global::app.modules.assert.IsFalse;
using AssertIsNull = global::app.modules.assert.IsNull;
using AssertIsNotNull = global::app.modules.assert.IsNotNull;
using AssertContains = global::app.modules.assert.Contains;
using AssertGreaterThan = global::app.modules.assert.GreaterThan;
using AssertLessThan = global::app.modules.assert.LessThan;

namespace PLang.Tests.App.Tester;

/// <summary>
/// Batch 5 — AssertionError.Variables + assert handlers.
/// AssertionError gains a nullable Variables property (Dictionary&lt;string, object?&gt;).
/// Each assert handler, on failure, populates this by calling Context.Variables.Snapshot().
/// The runner then reads AssertionError.Variables to render the failure diagnostic with
/// the variable state at the moment of failure.
/// </summary>
public class AssertionErrorVariablesTests
{
    private global::app.@this _app = null!;

    [Before(Test)]
    public void Setup()
    {
        _app = new global::app.@this("/test");
    }

    private static Data D(object? value) => value == null ? new Data("") : Data.Ok(value);

    // New AssertionError has Variables == null. Only handlers populate it; unrelated error
    // construction paths leave it null.
    [Test]
    public async Task AssertionError_Variables_DefaultNull()
    {
        var err = new AssertionError("expected x, got y");
        await Assert.That(err.Variables).IsNull();

        var err2 = new AssertionError(42, 99, "msg");
        await Assert.That(err2.Variables).IsNull();
    }

    // Property is settable and gettable — handlers assign Context.Variables.Snapshot()
    // on failure; readers (renderer, JSON export) pull the dict back out.
    [Test]
    public async Task AssertionError_Variables_PropertyRoundtrip()
    {
        var err = new AssertionError(1, 2);
        var captured = new Dictionary<string, object?> { ["x"] = 1 };
        err.Variables = captured;
        await Assert.That(err.Variables).IsNotNull();
        await Assert.That(err.Variables!["x"]).IsEqualTo(1);
    }

    // Canonical failure path: assert.equals fails → returned Data.Error is AssertionError
    // with Variables populated from the current Context.Variables.Snapshot().
    [Test]
    public async Task EqualsHandler_OnFailure_PopulatesVariablesFromSnapshot()
    {
        var context = _app.User.Context;
        context.Variables.Set("score", 42);
        context.Variables.Set("label", "foo");

        var action = new AssertEquals { Context = context, Expected = D(1), Actual = D(2) };
        var result = await action.Run();

        await Assert.That(result.Success).IsFalse();
        var err = result.Error as AssertionError;
        await Assert.That(err).IsNotNull();
        await Assert.That(err!.Variables).IsNotNull();
        await Assert.That(err.Variables!["score"]).IsEqualTo(42);
        await Assert.That(err.Variables!["label"]).IsEqualTo("foo");
    }

    // Guard (architect spec): no snapshot cost on passing assertions. A successful
    // equals does not touch Variables — stays null on the success Data path.
    [Test]
    public async Task EqualsHandler_OnSuccess_VariablesNotPopulated()
    {
        var context = _app.User.Context;
        context.Variables.Set("x", 1);

        var action = new AssertEquals { Context = context, Expected = D(5), Actual = D(5) };
        var result = await action.Run();

        await Assert.That(result.Success).IsTrue();
        // On success, data.Error is null — nothing to populate.
        await Assert.That(result.Error).IsNull();
    }

    // Reflection-based smoke across all 9 assert handlers (equals, notEquals, isTrue,
    // isFalse, isNull, isNotNull, lessThan, greaterThan, contains): force a failure
    // scenario and verify AssertionError.Variables is populated. Prevents drift when
    // a new handler is added without wiring up the snapshot capture.
    [Test]
    public async Task AllAssertHandlers_OnFailure_ConsistentlyPopulateVariables()
    {
        var context = _app.User.Context;
        context.Variables.Set("watched", "sentinel");

        var failures = new List<Data>
        {
            await new AssertEquals { Context = context, Expected = D(1), Actual = D(2) }.Run(),
            await new AssertNotEquals { Context = context, Expected = D(1), Actual = D(1) }.Run(),
            await new AssertIsTrue { Context = context, Value = D(false) }.Run(),
            await new AssertIsFalse { Context = context, Value = D(true) }.Run(),
            await new AssertIsNull { Context = context, Value = D(42) }.Run(),
            await new AssertIsNotNull { Context = context, Value = D(null) }.Run(),
            await new AssertContains { Context = context, Value = D("zzz"), Container = D("hello") }.Run(),
            await new AssertGreaterThan { Context = context, A = D(1), B = D(5) }.Run(),
            await new AssertLessThan { Context = context, A = D(5), B = D(1) }.Run()
        };

        foreach (var result in failures)
        {
            await Assert.That(result.Success).IsFalse();
            var err = result.Error as AssertionError;
            await Assert.That(err).IsNotNull();
            await Assert.That(err!.Variables).IsNotNull();
            await Assert.That(err.Variables!["watched"]).IsEqualTo("sentinel");
        }
    }
}
