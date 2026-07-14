# Clusters 2 + 3 rulings — [Code] getter throws (generator), producer lifts (writer untouched)

Answer to `coder/stabilization-remaining.md`, settled with Ingi 2026-07-14.

> **You own this.** Sketches traced against `b09e62edc`; the code wins.

## Cluster 2 — http provider: test-harness gap, not a regression. Two changes.

Your lean is confirmed: `Attach` is action lifecycle, the provider IS registered (`module/code/this.cs:258`), and all 12 failures are direct-`Run()` module tests. Verify with one real-path http run first (green confirms harness), then:

**1. The generator change — the `[Code]` getter self-diagnoses.** `Emission/Property/Code/this.cs:24` emits `=> {Backing}!;` — and the file's own comment (:20-22) confesses the gap: Attach surfaces *not-registered*, nothing surfaces *not-attached*; the `!` turns that into a bare NRE. Change the emission:

```csharp
// TODAY (:23-24)
sb.AppendLine($"    private {TypeName}? {Backing};");
sb.AppendLine($"    public partial {TypeName} {Name} => {Backing}!;");

// CHANGE
sb.AppendLine($"    private {TypeName}? {Backing};");
sb.AppendLine($"    public partial {TypeName} {Name} => {Backing} ?? throw new global::System.InvalidOperationException(");
sb.AppendLine($"        \"{TypeName} is not attached — Attach(context) did not run on this action. \"");
sb.AppendLine($"        + \"Actions run through the pipeline, which attaches [Code] providers before Run().\");");
```

Blast radius: every `[Code]` getter, uniformly — that is the point. Update the stale half of the :20-22 comment with it (the getter CAN now surface the miss). Incremental-cache note: this changes emitted text only, no `ActionClassInfo` shape change — cache keys unaffected.

**2. The harness change — fixtures attach at construction.** The 12 fixtures gain the `Attach` step where they build the action (a `Make.Action`-style helper that news + attaches, SC3 pattern). This is not re-implementing lifecycle in test-infra: `Attach` IS the construction half of the lifecycle, and a fixture that skips it built half an action. With change 1 in place, any remaining un-attached site self-reports instead of NRE-ing — fix them as they name themselves.

## Cluster 3 — ruling (a): the producer lifts through the door. The writer stays exactly as is.

The model argument: the item base's contract is that a raw CLR value never rides a value slot — every value is born through `Create`. A bare `goal` in a Data is a construction-site bug, and `writer.Value`'s throw is the declines-vs-errors policy working: it FOUND the bug. Option (b) — the writer routing unknown objects through the clr carrier — would make the wire a **second born door** (a late lift at serialization, the raw-CLR-era pattern this branch killed), and the writer's own comment is the security half of the ruling: a reflection fallback "would bypass [Out]/[Sensitive]/[Masked] discipline — the wire could leak fields the filter excludes. Fail closed."

Producer facts from my trace, so you don't re-walk them:

- **`%!goal%` is NOT the producer** — `DynamicData` (data/this.cs:892-902) holds a `computed`, and `computed.Compute()` lifts its factory result through the door (computed.cs:67) → `clr(goal)`. Same for the other `!`-host variables.
- So the bare goal enters on another path — the frame above `writer.Value` in the 3 test files (`GoalsTests`, `DiscoverActionTests`, `GoalMimeDeserializationTests`) names it.

The fix at whichever site it is, is one shape:

```csharp
// THE BUG SHAPE — a raw host stored into a value slot (bypasses birth):
context.Ok(goal);                       // or: new data.@this(name, goal, …) / data.Set(goal)

// THE FIX — born through the door; the lift's clr rung wraps the host:
context.Ok(global::app.type.item.@this.Create(goal, context));    // → clr(goal); reflection Output; [Out] discipline intact
```

Never a hand-rolled `new clr(goal, ctx)` at the site — the door's rungs stay the one path.

**Optional, recommended while you're in there** — make the next producer bug name itself. The writer doesn't know the binding; `data.Output` does. Wrap at that boundary:

```csharp
// data/this.Output.cs — around the value drive:
catch (global::app.data.NormalizeException ne)
{ throw new global::app.data.NormalizeException($"%{Name}%: {ne.Message}", "NormalizeUnexpectedLeafType"); }
```

The error then reads `%plan%: json.Writer received app.goal.@this…` — a variable name instead of a stack dive.

## Pins

- One real-path http run green (the harness-vs-regression verify) BEFORE the fixture edits.
- An un-attached `[Code]` access → the InvalidOperationException text, not NRE (pin on one action, any module).
- The 5 writer-goal tests green via the producer lift; `write out %!goal%` stays green (the computed path — regression guard for the lift).
- A deliberate bare-POCO write still throws `NormalizeUnexpectedLeafType` (the fail-closed guard survives the fix).

## OBP validation

| Surface | Check | Verdict |
|---|---|---|
| generated getter throw | the lifecycle miss reported by the owner (the property), named message, no `!` masking | ok |
| fixtures attach at construction | lifecycle honored, not re-implemented; one helper, no per-test ceremony | ok |
| producer lifts via `item.@this.Create` | one born door; no hand-rolled carrier at the site | ok |
| writer untouched | fail-closed guard stays; no second born door at the wire | ok |
| `%{Name}%` enrich at data.Output | the layer that knows the name adds the name; writer stays context-free | ok |
