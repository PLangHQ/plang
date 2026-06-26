namespace PLang.Tests.App.VariablesTests;

// Variables.Set contract — event subscribers follow the *name* across re-binding.
//
// Per Ingi (2026-05-01): subscribers attached to the placeholder Data for `%x%`
// (e.g. `--debug={"variable":[{"name":"x"}]}`) need to fire on every assignment
// to %x%, not just the first. Since Variables.Set replaces the Data instance per
// re-set (mints carry the concrete Data<T>), the events have to follow the name.
//
// What Variables.Set does on replacement (prev → dv under same name):
//   - Aliases prev's OnCreate/OnChange/OnDelete onto dv (events follow the name).
//   - Fires prev.FireOnChange(dv) — debug/watch subscribers see (prev, dv).
//   - dv replaces prev in the dictionary.
//
// What does NOT carry across replacement:
//   - Properties — those are result metadata bound to the Data instance, not
//     binding metadata. condition.if's branchIndex, source-line annotations, etc.
//     stay with the Data they were attached to.
//
// Variable.set itself is the binding-mint site (it picks Data<T> via MintTyped),
// but is not where state-survival lives — Variables.Set owns that.

public class SubscriberSurvivalTests
{
    private global::app.@this _app = null!;

    [Before(Test)]
    public void Setup() => _app = global::PLang.Tests.TestApp.Create("/app");

    [After(Test)]
    public async Task TearDown() { await _app.DisposeAsync(); }

    // First Set under a fresh name — OnCreate fires once, OnChange does NOT.
    [Test]
    public async Task Set_NewVariable_FiresOnCreate_NotOnChange()
    {
        var context = _app.User.Context;
        var dv = new global::app.data.@this<global::app.type.number.@this>("count", 1) { Context = context };
        var createCalls = 0;
        var changeCalls = 0;
        dv.OnCreate.Add(_ => createCalls++);
        dv.OnChange.Add((_, _) => changeCalls++);

        context.Variable.Set(dv);

        await Assert.That(createCalls).IsEqualTo(1);
        await Assert.That(changeCalls).IsEqualTo(0);
    }

    // Replacement flow — prev's OnChange fires once with (prev, newDv) so
    // existing subscribers see the replacement happen. Behavior preserved
    // from the original "fire on replace" design.
    [Test]
    public async Task Set_Replace_FiresOnChange_OnPrev_WithDvAsNewData()
    {
        var context = _app.User.Context;
        var prev = new global::app.data.@this<global::app.type.number.@this>("n", 1) { Context = context };
        context.Variable.Set(prev);

        Data? receivedOld = null;
        Data? receivedNew = null;
        prev.OnChange.Add((o, n) => { receivedOld = o; receivedNew = n; });

        var dv = new global::app.data.@this<global::app.type.number.@this>("n", 2) { Context = context };
        context.Variable.Set(dv);

        await Assert.That(ReferenceEquals(receivedOld, prev)).IsTrue();
        await Assert.That(ReferenceEquals(receivedNew, dv)).IsTrue();
    }

    // Events follow the name: after replacement, dv.OnChange IS aliased to prev.OnChange.
    [Test]
    public async Task Set_Replace_AliasesPrevOnChangeOntoDv()
    {
        var context = _app.User.Context;
        var prev = new global::app.data.@this<global::app.type.number.@this>("n", 1) { Context = context };
        context.Variable.Set(prev);
        var prevOnChangeRef = prev.OnChange;

        var dv = new global::app.data.@this<global::app.type.number.@this>("n", 2) { Context = context };
        context.Variable.Set(dv);

        await Assert.That(ReferenceEquals(prevOnChangeRef, dv.OnChange)).IsTrue();
    }

    // All three event lists alias — events follow the name.
    [Test]
    public async Task Set_Replace_AliasesAllEventLists()
    {
        var context = _app.User.Context;
        var prev = new global::app.data.@this<global::app.type.number.@this>("n", 1) { Context = context };
        context.Variable.Set(prev);

        var dv = new global::app.data.@this<global::app.type.number.@this>("n", 2) { Context = context };
        context.Variable.Set(dv);

        await Assert.That(ReferenceEquals(prev.OnCreate, dv.OnCreate)).IsTrue();
        await Assert.That(ReferenceEquals(prev.OnChange, dv.OnChange)).IsTrue();
        await Assert.That(ReferenceEquals(prev.OnDelete, dv.OnDelete)).IsTrue();
    }

    // The Debug-watch contract: a subscriber attached to prev (the placeholder for
    // %n%) DOES fire when dv replaces it AND on subsequent re-bindings — because the
    // event list aliased onto dv is the same list the subscriber lives on.
    [Test]
    public async Task Set_PostReplacement_SubscribeViaPrev_FiresOnFurtherReplacements()
    {
        var context = _app.User.Context;
        var prev = new global::app.data.@this<global::app.type.number.@this>("n", 1) { Context = context };
        context.Variable.Set(prev);
        var calls = 0;
        prev.OnChange.Add((_, _) => calls++);

        var dv = new global::app.data.@this<global::app.type.number.@this>("n", 2) { Context = context };
        context.Variable.Set(dv);
        await Assert.That(calls).IsEqualTo(1);

        // Further re-set fires the same subscriber (alias means dv.OnChange === prev.OnChange).
        var dv2 = new global::app.data.@this<global::app.type.number.@this>("n", 3) { Context = context };
        context.Variable.Set(dv2);
        await Assert.That(calls).IsEqualTo(2);
    }

    // Idempotent re-Set: setting the same Data instance twice triggers no events
    // and does no extra work. The Data is already at that key by reference.
    [Test]
    public async Task Set_SameInstance_NoFire()
    {
        var context = _app.User.Context;
        var dv = new global::app.data.@this<global::app.type.number.@this>("n", 1) { Context = context };
        context.Variable.Set(dv);

        var changeCalls = 0;
        dv.OnChange.Add((_, _) => changeCalls++);
        context.Variable.Set(dv); // same instance

        await Assert.That(changeCalls).IsEqualTo(0);
    }

    // dv has its own Properties; prev's metadata does not carry over.
    // (Per-binding metadata, not per-name metadata.)
    [Test]
    public async Task Set_PropertiesNotAliased_NewBindingHasOwnProperties()
    {
        var context = _app.User.Context;
        var prev = new global::app.data.@this<global::app.type.number.@this>("n", 1) { Context = context };
        prev.Properties.Set("annot", "from-prev");
        context.Variable.Set(prev);

        var dv = new global::app.data.@this<global::app.type.number.@this>("n", 2) { Context = context };
        context.Variable.Set(dv);

        await Assert.That(ReferenceEquals(prev.Properties, dv.Properties)).IsFalse();
        await Assert.That(dv.Properties["annot"]).IsNull();
    }

    // Variables.Remove fires OnDelete on the removed Data. Pinned so Phase 3
    // wires it up — today's Remove (Variables/this.cs:357) just TryRemoves
    // without firing.
    [Test]
    public async Task Remove_FiresOnDelete_OnRemovedData()
    {
        var context = _app.User.Context;
        var dv = new global::app.data.@this<global::app.type.number.@this>("doomed", 1) { Context = context };
        context.Variable.Set(dv);
        var deleteCalls = 0;
        dv.OnDelete.Add(_ => deleteCalls++);

        context.Variable.Remove("doomed");
        await Assert.That(deleteCalls).IsEqualTo(1);
    }

    // Regression: `--debug={"variable":[{"name":"x","event":"OnChange"}]}` attaches
    // OnChange to a placeholder and Set(placeholder)s under "x". Every subsequent
    // `set %x% = ...` must fire the placeholder's subscriber, not just the first.
    // Caught by auditor/v1: dumb storage broke this — placeholder events were lost
    // on the very first replacement.
    [Test]
    public async Task DebugWatch_OnChange_FiresOnEveryReplacement()
    {
        var context = _app.User.Context;
        var placeholder = global::app.data.@this.Uninitialized("x");
        var calls = 0;
        placeholder.OnChange.Add((_, _) => calls++);
        context.Variable.Set(placeholder);

        context.Variable.Set(new global::app.data.@this<global::app.type.number.@this>("x", 1) { Context = context });
        context.Variable.Set(new global::app.data.@this<global::app.type.number.@this>("x", 2) { Context = context });
        context.Variable.Set(new global::app.data.@this<global::app.type.number.@this>("x", 3) { Context = context });

        await Assert.That(calls).IsEqualTo(3);
    }

    // Properties stay attached to the Data instance — they're result metadata
    // (e.g. condition.if's branchIndex), not binding metadata. Without this rule,
    // engine-level aliases like `!data` would carry stale Properties between
    // step results.
    [Test]
    public async Task Set_Replace_DoesNotCarryProperties()
    {
        var context = _app.User.Context;
        var prev = new global::app.data.@this<global::app.type.number.@this>("n", 1) { Context = context };
        prev.Properties.Set("branchIndex", 0);
        context.Variable.Set(prev);

        var dv = new global::app.data.@this<global::app.type.number.@this>("n", 2) { Context = context };
        context.Variable.Set(dv);

        await Assert.That(dv.Properties.Contains("branchIndex")).IsFalse();
    }

    // .Value setter fires OnChange — direct mutation of a Data's wrapped value
    // notifies subscribers. Used by the non-Data Variables.Set fall-through and
    // by handlers that mutate live bindings in place.
    [Test]
    public async Task ValueSetter_FiresOnChange()
    {
        var context = _app.User.Context;
        var dv = new global::app.data.@this<global::app.type.number.@this>("n", 1) { Context = context };
        context.Variable.Set(dv);
        var calls = 0;
        dv.OnChange.Add((_, _) => calls++);

        dv.SetValue(42);
        await Assert.That(calls).IsEqualTo(1);
    }
}
