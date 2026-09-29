using PLang.Tests.Shared;
using app.error;
using AssertEquals = global::app.module.assert.Equals;
using AssertNotEquals = global::app.module.assert.NotEquals;
using AssertIsTrue = global::app.module.assert.IsTrue;
using AssertIsFalse = global::app.module.assert.IsFalse;
using AssertIsNull = global::app.module.assert.IsNull;
using AssertIsNotNull = global::app.module.assert.IsNotNull;
using AssertContains = global::app.module.assert.Contains;
using AssertGreaterThan = global::app.module.assert.GreaterThan;
using AssertLessThan = global::app.module.assert.LessThan;

namespace PLang.Tests.App.Tester;

/// <summary>
/// Batch 5 — AssertionError.Variables + assert handlers.
/// AssertionError gains a nullable Variables property (Dictionary&lt;string, object?&gt;).
/// Each assert handler, on failure, populates this by calling Context.Variable.Snapshot().
/// The runner then reads AssertionError.Variables to render the failure diagnostic with
/// the variable state at the moment of failure.
/// </summary>
public class AssertionErrorVariablesTests
{
    private global::app.@this _app = null!;

    [Before(Test)]
    public void Setup()
    {
        _app = new global::app.@this("/test").Testing();
    }

    private Data D(object? value) => value == null ? new Data("") : _app.actor.list.User.Context.Ok(value);

    // New AssertionError has Variables == null. Only handlers populate it; unrelated error
    // construction paths leave it null.
    [Test]
    public async Task AssertionError_Variables_DefaultNull()
    {
        var err = new AssertionError("expected x, got y");
        await Assert.That(err.Variables).IsNull();

        var err2 = new AssertionError("Expected: 42, Actual: 99", 42, 99, "msg");
        await Assert.That(err2.Variables).IsNull();
    }

    // Property is settable and gettable — handlers assign Context.Variable.Snapshot()
    // on failure; readers (renderer, JSON export) pull the dict back out.
    [Test]
    public async Task AssertionError_Variables_PropertyRoundtrip()
    {
        var err = new AssertionError("Expected: 1, Actual: 2", 1, 2, null);
        await _app.actor.list.User.Context.Variable.Set("x", 1);
        var captured = _app.actor.list.User.Context.Variable.Snapshot();
        err.Variables = captured;
        await Assert.That(err.Variables).IsNotNull();
        await Assert.That(err.Variables!.Held("x")?.ToString()).IsEqualTo("1");
    }

    // Canonical failure path: assert.equals fails → returned Data.Error is AssertionError, and the frame that
    // records it keeps the variables as they are (an assertion always keeps them).
    [Test]
    public async Task EqualsHandler_OnFailure_PopulatesVariablesFromSnapshot()
    {
        var context = _app.actor.list.User.Context;
        context.Variable.Set("score", 42);
        context.Variable.Set("label", "foo");

        var result = await _app.Run(new AssertEquals(context) { Expected = D(1), Actual = D(2) }, context);

        await result.IsFailure();
        var err = result.Error as AssertionError;
        await Assert.That(err).IsNotNull();
        await Assert.That(err!.Variables).IsNotNull();
        await Assert.That(err.Variables!.Held("score")?.ToString()).IsEqualTo("42");
        await Assert.That((err.Variables!.Held("label"))?.ToString()).IsEqualTo("foo");
    }

    // Any other error keeps no variables unless --debug is on (variables can hold secrets); an assertion
    // always keeps them.
    [Test]
    public async Task APlainError_WithoutDebug_KeepsNoVariables_AnAssertionDoes()
    {
        var context = _app.actor.list.User.Context;
        context.Variable.Set("secret", "s3cr3t");
        await using var frame = context.CallStack.Push(global::PLang.Tests.App.CallStackTests.CallStackTestHelpers.MakeAction(context, "Goal"));

        var plain = new global::app.error.Error("boom", "Boom", 500);
        frame.Record(plain, context);
        var assertion = new AssertionError("Expected: 1, Actual: 2", 1, 2, null);
        frame.Record(assertion, context);

        await Assert.That(plain.Variables).IsNull();
        await Assert.That(assertion.Variables).IsNotNull();
        await Assert.That(assertion.Variables!.Held("secret")?.ToString()).IsEqualTo("s3cr3t");
    }

    // Guard (architect spec): no snapshot cost on passing assertions. A successful
    // equals does not touch Variables — stays null on the success Data path.
    [Test]
    public async Task EqualsHandler_OnSuccess_VariablesNotPopulated()
    {
        var context = _app.actor.list.User.Context;
        context.Variable.Set("x", 1);

        var action = new AssertEquals(context) { Expected = D(5), Actual = D(5) };
        await action.Attach(null, context);
        var result = await action.Start();

        await result.IsSuccess();
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
        var context = _app.actor.list.User.Context;
        context.Variable.Set("watched", "sentinel");

        // Through the production door: the dispatch binds each handler and its frame records the failure.
        async Task<Data> AR(global::app.module.ICodeGenerated a) => await _app.Run((dynamic)a, context);

        var failures = new List<Data>
        {
            await AR(new AssertEquals(context) { Expected = D(1), Actual = D(2) }),
            await AR(new AssertNotEquals(context) { Expected = D(1), Actual = D(1) }),
            await AR(new AssertIsTrue(context) { Value = D(false) }),
            await AR(new AssertIsFalse(context) { Value = D(true) }),
            await AR(new AssertIsNull(context) { Value = D(42) }),
            await AR(new AssertIsNotNull(context) { Value = D(null) }),
            await AR(new AssertContains(context) { Value = D("zzz"), Container = D("hello") }),
            await AR(new AssertGreaterThan(context) { A = D(1), B = D(5) }),
            await AR(new AssertLessThan(context) { A = D(5), B = D(1) })
        };

        foreach (var result in failures)
        {
            await result.IsFailure();
            var err = result.Error as AssertionError;
            await Assert.That(err).IsNotNull();
            await Assert.That(err!.Variables).IsNotNull();
            await Assert.That((err.Variables!.Held("watched"))?.ToString()).IsEqualTo("sentinel");
        }
    }
}
