# `action.Step` — I did not implement the throwing getter. Here is why.

**Branch:** `goal-graph-singular` · **Commit:** `1e5c67c63` (birth-fact pass) · **Author:** coder

Everything else in the birth-fact pass landed exactly as ruled. This one item I
implemented differently, and I want it settled before step (3).

---

## What was ruled

From `error-model-and-backref-answer.md`, Addendum 2 §3:

> `action.Step` uses the `Module` pattern — non-nullable with a throwing getter,
> because every action in a program has a step. The backing stays null only for
> catalog elements, so reading it there throws as the bug it is.

I implemented that first. Shape:

```csharp
private Step? _step;

[JsonIgnore]
public Step Step
{
    get => _step ?? throw new InvalidOperationException(
        $"action '{Name}' has no step — it was constructed outside a construction door, " +
        $"or it is a catalog element.");
    internal set => _step = value;
}
```

## What happened

**349 failures in Modules, 117 in Types.** Every one the same stack:

```
InvalidOperationException: action 'sign' has no step
  at app.goal.step.action.this.get_Step()
  at app.callstack.this.Push(action, variables)         // callstack/this.cs:101
  at app.goal.step.action.this.DispatchAsync(context)
  at app.goal.step.action.this.Run(context)
  at app.channel.serializer.plang.this.SerializeAsync(...)   // PRODUCTION
```

Note the bottom frame. That is not a test authoring an action badly — that is
the plang serializer signing a Data on its way to the wire.

## The premise that does not hold

The ruling assumes catalog elements are the only stepless case. There is a third
kind, and it is production: **the C#-composition seam.**

`PLang/app/this.cs:458` — `App.Run<TAction>(handler, context)` takes a
strongly-typed handler instance composed in C# and wraps it in an action entity
so it can go through the real dispatch path (Push / Anchor / lifecycle):

```csharp
public Task<data.@this> Run<TAction>(TAction handler, actor.context.@this context)
    where TAction : module.ICodeGenerated
{
    var entity = new global::app.goal.step.action.@this
    {
        Module = Module[ResolveModuleName(typeof(TAction))],
        Name   = ResolveActionName(typeof(TAction)),
        Seed   = handler,
        // ← no step. There is none. This action is not in any program.
    };
    return entity.Run(context);
}
```

Four production callers, all of them infrastructure that runs *underneath* a
program rather than inside one:

| site | action |
|---|---|
| `app/channel/serializer/plang/this.cs:81` | `signing.sign` — sign-if-missing at the I/O boundary |
| `app/channel/serializer/plang/this.cs:181` | `signing.verify` — verify on read |
| `app/type/item/path/this.Authorize.cs:54` | `output.ask` — the permission prompt from `AuthGate` |
| `app/type/item/path/file/this.Operations.cs:372` | `output.ask` — same, from a file verb |

These have no step because there is no step to have. `path.Authorize` fires from
inside a `path` verb; the sign action fires from inside a serializer. Neither is
a program node.

And `CallStack.Push` — the one place that reads it — already knows this. It asks
the question defensively:

```csharp
// callstack/this.cs:100
var goalPath       = action.Step?.Goal?.PrPath;
var callerGoalPath = caller?.Action.Step?.Goal?.PrPath;
if (goalPath != null && !goalPath.Equals(callerGoalPath) && ContainsGoal(goalPath))
    throw new CallStackOverflowException(MaxDepth);
```

The cycle check is *goal-boundary* detection. An action with no goal identity has
no boundary to cross — `goalPath == null` is the correct, meaningful answer, and
the guard is written to take it. With a throwing getter the `?.` becomes a lie:
the null-conditional operator still invokes the getter, so every synthetic action
crashes the runtime at Push.

## Why I think nullable is the right shape, not a concession

A throwing getter says *"null here is always a bug."* That claim is false for
this property — three legitimate kinds of action have no step:

1. a **catalog element** (a module-minted descriptor — no program)
2. a **synthetic action** composed in C# (`app.Run(new sign{...})` — no program)
3. a **recovery action** materialised from a parameter value (until step 3)

`Module` earns its throwing getter because there is no such thing as an action
without a module — the claim is true, so the getter enforces a real invariant.
`Step` does not have that property, so the same shape would not be enforcing an
invariant; it would be crashing on a state the design deliberately allows.

More to the point: **the throwing getter was never the enforcement.** The
enforcement is the construction doors, and those are in and working —

```csharp
// step/serializer/Reader.cs — the concrete construction door
var step = new step.@this { Goal = goal };                        // shell first
case "action": step.Action.Add(_action.Read(ref reader, ctx, step));   // born holding it

// action/serializer/Reader.cs
var action = new action.@this { Step = step, Synthetic = false };
```

Every action that IS part of a program is born holding its step, and there is no
longer any code path that stamps one in later (one exception, `error.handle`,
documented and dying in step 3). Deleting the repair getters is what made the
birth fact real. The getter on top adds no enforcement — it only removes the
runtime's ability to answer "this action has no program", which the runtime
genuinely needs to answer.

## What I shipped

```csharp
/// <summary>The step this action belongs to — a BIRTH FACT for every action that is part of a
/// PROGRAM: the reader builds the step shell first and hands it down at construction, so it is
/// never stamped in afterwards. Null is not a repair hole, it is a real state: three kinds of
/// action exist outside any program and therefore have no step — a catalog element (a
/// module-minted descriptor), a synthetic action composed in C# (<c>app.Run(new sign{...})</c>,
/// the signing/verify/ask seam), and — until recovery moves into <c>Child</c> — a recovery
/// action materialised from a parameter value.</summary>
/// <remarks><c>internal set</c>, not <c>init</c>, for exactly one more step: <c>error.handle</c>
/// must hand recovery actions the enclosing step. When recovery is read at load like every
/// other action, that last stamp goes and this tightens to <c>init</c>.</remarks>
[JsonIgnore]
public Step? Step { get; internal set; }
```

`internal set` (was public `set`) so nothing outside the assembly can stamp it,
and the doc names the three stepless kinds rather than calling them a bug.

Baseline verified — revert + rebuild + run both sides, 3× on Runtime. Failure
counts and failing-test **names** identical on both sides: Modules 67/989,
Types 27/721, Data 52/885, Generator 19/192, Runtime 44/701.

---

## What I want from you

**Q1 — Do you accept nullable `Step?`, or do you want the throwing getter kept
and the synthetic seam changed to satisfy it?**

If you want the throwing getter, the synthetic actions have to get a step from
somewhere, and I do not see a good source:

- *Take `context.CallStack.Current.Action.Step`* — Ingi already rejected reaching
  for the current step as an action's identity ("having context.Step to get a
  step of an action just introduces danger"). It is also wrong on the merits:
  the sign action does not belong to whatever step happened to be running when
  the serializer fired.
- *Mint a synthetic step + synthetic goal per composed action* — inventing fake
  program nodes so a type does not have to admit null. That is manufacturing
  data to satisfy a signature.

**Q2 — Is the C#-composition seam itself the thing you actually want gone?**

`App.Run<TAction>` already carries this note in its own doc comment:

> *Spec-deferred follow-up: this overload may be removed entirely when handlers
> grow their own RunAsync surface.*

If a handler could run itself without being wrapped in an action entity, the
whole stepless-synthetic-action category disappears, and `Step` could then be
non-nullable honestly (catalog elements being split out separately, as you
already queued). That looks like the real fix to me, but it is a much larger
change than step (2) and I did not want to start it unilaterally.

**Q3 — Does this change anything about step (3)?**

Two items from Addendum 2 are still blocking me there, unrelated to the above:

- **(a)** `Child` is a `step.list`, but recovery is a list of *actions*. Moving
  recovery into `Child` needs a step to put them in — invented by whom, named
  what?
- **(b)** §3 says the catalog blank-mint `Create()` becomes `Create(step)`, which
  contradicts the ruling that catalog elements legitimately have no step. If
  catalog elements get a step, what step is it?

I am holding step (3) until (a) and (b) are answered.

---

## Unrelated find, for the backlog

The **Wire suite has no usable result** and has not for some time. It aborts with
a hard `Stack overflow` — infinite recursion in `app.snapshot.serializer.Default.Render`
calling itself through `data.@this..ctor → type.Create`. Reproduces identically
on baseline (pre-existing, not from this pass). Every Wire number reported on
this branch is from a truncated run.
