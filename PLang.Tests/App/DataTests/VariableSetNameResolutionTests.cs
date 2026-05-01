namespace PLang.Tests.App.DataTests;

// Proof for `runtime2-variablename-migration` — architect/v1/plan.md §Phase 0.
//
// Claim under test: for `set %x% = 5`, the .pr parameter slot is
// {Name="Name", Value="%x%"}. After As<string>(ctx) the resulting Data's .Name is "x"
// — i.e. the literal variable name the developer wrote, NOT the handler slot name.
//
// All four tests pass. The mechanism works as documented. The migration was nevertheless
// declined by Ingi (2026-05-01) because the bare-name case (test 3) is a real regression
// vs the existing [VariableName] / __StripPercent path. Tests retained as documentation
// of the As<T> Name-propagation contract — they would catch any future drift.

public class VariableSetNameResolutionTests
{
    private global::App.@this _app = null!;

    [Before(Test)]
    public void Setup() => _app = new global::App.@this("/app");

    [After(Test)]
    public async Task TearDown() { await _app.DisposeAsync(); }

    // Architect claim part 1: `x` doesn't exist yet (the common case for `set %x% = 5`).
    // The slot Data's .Value is "%x%". After As<string>, .Name == "x" via TryFullVarMatch,
    // and IsInitialized is false (so handlers can detect "var doesn't exist" later).
    [Test]
    public async Task SetX_VarMissing_NameSlotResolvesTo_x()
    {
        var ctx = _app.User.Context;

        // Exact shape variable.set's "Name" parameter has after .pr deserialization:
        // {"name":"Name","value":"%x%"}.
        var nameSlot = new Data("Name", "%x%") { Context = ctx };

        var resolved = nameSlot.As<string>(ctx);

        await Assert.That(resolved.Name).IsEqualTo("x");
        await Assert.That(resolved.IsInitialized).IsFalse();
    }

    // Architect claim part 2: `x` already exists (e.g. `set %x% = 5` re-running, or in a
    // sequence that already initialized x). As<T> recurses onto the live variable's Data,
    // so .Name propagates through ConstructWrap<T> as "x".
    [Test]
    public async Task SetX_VarExists_NameSlotResolvesTo_x_WithLiveValue()
    {
        var ctx = _app.User.Context;
        ctx.Variables.Set(new global::App.Data.@this<int>("x", 10) { Context = ctx });

        var nameSlot = new Data("Name", "%x%") { Context = ctx };
        var resolved = nameSlot.As<string>(ctx);

        // .Name is the LIVE variable's name — not the slot "Name".
        await Assert.That(resolved.Name).IsEqualTo("x");
        // Sanity: IsInitialized is true because x exists.
        await Assert.That(resolved.IsInitialized).IsTrue();
    }

    // Negative — the load-bearing assertion the architect flagged. If the LLM emits
    // a bare name "x" (no `%`), TryFullVarMatch fails and .Name falls back to the
    // slot name "Name". This is the failure mode that made Ingi decline the migration:
    // [VariableName] / __StripPercent handles both "%x%" and "x" natively.
    [Test]
    public async Task SetX_BareName_NoPercent_NameStaysAsSlotName()
    {
        var ctx = _app.User.Context;

        // LLM mistake: bare "x" instead of "%x%".
        var nameSlot = new Data("Name", "x") { Context = ctx };
        var resolved = nameSlot.As<string>(ctx);

        // .Name is the slot name — variable.set would write to a variable called "Name".
        await Assert.That(resolved.Name).IsEqualTo("Name");
        await Assert.That(resolved.Value).IsEqualTo("x");
    }

    // Sanity: Variables.Set keyed by the resolved Name does what the migrated handler's
    // Run() would need. Confirms the architect's planned line `Variables.Set(Name.Name,
    // value, ...)` writes to the correct key — independently of whether we ship the
    // migration.
    [Test]
    public async Task SetX_RoundTrip_ResolvedNameWritesToCorrectKey()
    {
        var ctx = _app.User.Context;
        var nameSlot = new Data("Name", "%x%") { Context = ctx };
        var resolved = nameSlot.As<string>(ctx);

        // Simulate the migrated handler's write path.
        ctx.Variables.Set(new global::App.Data.@this<int>(resolved.Name, 5) { Context = ctx });

        var stored = ctx.Variables.Get("x");
        await Assert.That(stored.IsInitialized).IsTrue();
        await Assert.That(stored.Value).IsEqualTo(5);

        // Negative — the slot name "Name" must NOT have leaked as a variable.
        var leaked = ctx.Variables.Get("Name");
        await Assert.That(leaked.IsInitialized).IsFalse();
    }
}
