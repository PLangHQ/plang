# Condition orchestration — a `Decision` type; the four methods leave the action collection (settled w/ Ingi 2026-07-18)

**Corrects the items-answer re-home table**, which sends `Chain`/`Branches`/`FirstConditionIndex`/`IsFirstCondition` from `actions.@this` → `step`. **Do not re-home to step** — that relocates a misplaced smell to another wrong owner. They belong to the condition module, as a `Decision` type.

> **You own this.** The code below is a suggestion — bodies, naming inside, factoring are yours. The SHAPE (Decision owns structure, condition.if owns execution, no plural type, off the action collection) is the ruling.

## The finding

`condition.if.Orchestrate` legitimately owns branch **execution** — it's the control-flow action. That stays. The problem is the four helpers on `actions.@this`:
- `SplitAtConditions` — condition.if-ONLY (group actions into branches).
- `ComputeBranchChain` — SHARED (condition.if publishes it, test.discover seeds coverage from it).
- `FirstConditionIndex` / `IsFirstCondition` — test.discover + coverage (locate the head).

Their verb names (`Split…`, `Compute…Chain`) were the tell: the branch layout is a **type**, not a computation repeated per call. And it's condition's logic sitting on a generic collection.

## The shape — `Decision` (the structure) vs `condition.if` (the evaluator)

`condition.if` OWNS execution. `Decision` OWNS the structure — built once, read by everyone who needs the shape without running it. No plural `Branches` type; `Decision` is singular and IS its branches.

### NEW — `app/module/action/condition/decision/this.cs`

```csharp
using System.Collections;
using Action = global::app.goal.steps.step.actions.action.@this;

namespace app.module.action.condition.decision;

/// <summary>The if/elseif/else structure at a step's condition point — built once, read by
/// condition.if (to run), test.discover (to seed coverage), coverage (head guard). It IS its
/// branches and carries their label Chain. condition.if owns the RUNNING; this owns the STRUCTURE.</summary>
public sealed class @this : IReadOnlyList<Branch>
{
    private readonly List<Branch> _branches;
    public IReadOnlyList<string> Chain { get; }               // was ComputeBranchChain

    private @this(List<Branch> branches, IReadOnlyList<string> chain)
    { _branches = branches; Chain = chain; }

    /// <summary>The decision at the head condition.if of this sequence — null when there is none.</summary>
    public static @this? Of(IList<Action> actions)
    {
        var head = Head(actions);
        return head < 0 ? null : new @this(Split(actions, head), Labels(actions, head));
    }

    /// <summary>Is this action the head condition.if? (coverage ignores inner-elseif firings.)</summary>
    public static bool HeadIs(IList<Action> actions, Action action)   // was IsFirstCondition
    {
        var head = Head(actions);
        return head >= 0 && ReferenceEquals(actions[head], action);
    }

    private static int Head(IList<Action> actions)                    // was FirstConditionIndex
    {
        for (int i = 0; i < actions.Count; i++) if (actions[i].IsCondition) return i;
        return -1;
    }

    // condition.if starts a branch; non-conditions append to the current body; a trailing tail
    // attaches to the last. Index THROUGH the source so each action carries its Step (the executed
    // branch needs it for the orchestration guard / DisableChildrenOf / coverage keys).
    private static List<Branch> Split(IList<Action> actions, int start) { /* SplitAtConditions body */ }

    // {true,false} for a bare single-action if; else one label per condition (if / elseif[N] / else).
    private static List<string> Labels(IList<Action> actions, int start) { /* ComputeBranchChain body */ }

    public int Count => _branches.Count;
    public Branch this[int i] => _branches[i];
    public IEnumerator<Branch> GetEnumerator() => _branches.GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}

public sealed record Branch(Action? Condition, IReadOnlyList<Action> Body);
```

### Call sites (alias `using Decision = app.module.action.condition.decision.@this;`)

```csharp
// condition/if.cs — Orchestrate walks the Decision (execution logic UNCHANGED)
var decision = Decision.Of(actions);          // was actions.SplitAtConditions(myIndex)
for (int b = 0; b < decision.Count; b++)
{
    var (condition, body) = decision[b];      // Branch deconstructs
    ...                                       // evaluate + run branch — as today
    lastResult.Properties.Set("branchChain", decision.Chain);   // was actions.ComputeBranchChain(myIndex)
}

// test/discover.cs:313-318
if (Decision.Of(step.Actions) is { } decision)                  // was FirstConditionIndex + ComputeBranchChain
    coverage.RecordBranchChain($"{goalId}:{step.Index}", decision.Chain);

// action/this.cs:104
public bool IsFirstConditionInStep
    => Step?.Actions is { } acts && Decision.HeadIs(acts, this); // was Step?.Actions.IsFirstCondition(this)
```

### DELETE from `actions/this.cs`
`FirstConditionIndex`, `IsFirstCondition`, `ComputeBranchChain`, `SplitAtConditions` (44–131). Their logic is the `Decision` type. `actions.@this` then carries no condition logic and can delete clean.

## The flow (runtime + discovery + coverage)

Step `if %x%>5 then write "big" else write "small"` → actions `[condition.if, write big, condition.else, write small]`.

**Runtime:** `step.RunAsync` → `condition.if.Run` → evaluate → `Orchestrate` builds `Decision.Of(step.Actions)` (branches `[(if,[big]),(else,[small])]`, chain `[if,else]`), walks it, runs the taken branch, returns `Handled=true` → the step loop breaks (condition.if consumed the branch actions).

**Discovery:** `test.discover` walks steps, never runs; per step `Decision.Of(step.Actions)?.Chain` → `coverage.RecordBranchChain(site, chain)` declaring which branches exist. Runtime marks fired branches tested; the report shows untested ones.

**Coverage guard:** when a condition fires, `Decision.HeadIs(step.Actions, action)` — head → count; inner-elseif → ignore.

## Sequencing
Land `Decision` + move the four methods **before** deleting `actions.@this` — otherwise the deletion cements the smell on step per the old table.

## ObpScan protocol (going forward)
Run `dotnet run --project Tools/ObpScan -- <type>` before deleting/re-homing a type or pushing a new one. **A MISPLACED or behavioral VERB+NOUN flag is a design call → escalate to architect, don't silently relocate it** (this whole finding is what that rule catches — the tool flagged `SplitAtConditions` VERB+NOUN + MISPLACED). PLURAL/REDUNDANT name flags → fix in the singular sweep.
