# Crash #3 is Crash #2 — provenance of the build StackOverflow (architect)

**Branch:** `fix-plang-build`. Companion to `architect/crash2-stackoverflow.md`; answers its one open item ("pin the write that stores the raw self-ref"). Responds to `coder/plang-build-findings.md` crash #3.
**Why:** asked to dig into the second problem (crash #3 — `builder.goals`/`EmitBuildEvent` `path=%path%`). Running the real builder shows crash #3 is not a separate "path cannot be empty" ArgumentException on the current tip — it is the **same `text.Value` StackOverflow as crash #2**, and the write that arms it is now located.

## Ran the real builder — it's the #2 SO

`plang build` against a throwaway app core-dumps (`SIGABRT`, exit 134) with the identical cycle from crash #2, top frame `Variable.Get`:

```
%!data%
Stack overflow.
   at app.variable.list.this.Get(System.String)
   at app.type.text.this.Value(app.data.this)     ← text/this.cs:72
   at app.data.this.Value()                         ← data/this.cs:260
   at app.type.text.this.Value(app.data.this)
   at app.data.this.Value()
   ... (repeats to stack exhaustion)
```

Outer frames are `output.Write → channel.Write` — i.e. it overflows while **`EmitBuildEvent` renders its template**, which resolves `%path%`. So crash #2 and crash #3 are one bug: a self-referential `%path%` binding. The coder's "path cannot be empty" ArgumentException was an earlier-tree manifestation of the same absent/garbage `path`; on this tip it surfaces as the SO.

## The full chain (each link run, not reasoned)

1. **Step 0** — `set default %path% = "/"` stores `path = "/"`. Driven in isolation (`variable.set` with the exact build.pr params, including the spurious `Type=string`): stores `"/"` fine. `Type=string` is harmless noise — `string` still resolves to text — not the bug.
2. **Step 4** — `call EmitBuildEvent kind="build-path", path=%path%` **clobbers `path` with the bare self-ref**. Driven through the real `goal.call` (`Call` action → `RunGoalAsync`): `path` goes `"/"` → self-ref. Observed via `Peek()` (no `.Value()`, so no crash):
   ```
   before=/   after.isSelfRef=True   after.raw=%path%
   ```
3. **Render** — `EmitBuildEvent` resolves `%path%` → `Variable.Get("path")` → the self-alias → `text.Value ⇄ data.Value` SO (the crash #2 mechanism).

## The write that arms it — `goal.call` param seam

`app/this.cs:585`, the call-by-value parameter injection:

```csharp
foreach (var param in goalCall.Parameters)
{
    // ... storing the bare reference would point the slot at itself
    // (call Foo x=%x% → x resolves %x% → x) and loop when read.
    param.Context = context;
    if (param.Peek() is global::app.variable.@this)               // ← guard
        await context.Variable.Set(param.Name,
            new global::app.data.@this(param.Name, await param.Value(), context: context));
    else
        await context.Variable.Set(param.Name, param);            // ← stores the raw self-ref
}
```

The author *knew* the loop existed and wrote the `if` to resolve the ref before storing. But the predicate is miscalibrated: it fires on `param.Peek() is variable.@this` — a **variable name-slot** (write targets) — whereas `build.pr` types `path=%path%` as `text`. A `text`-typed live ref is *not* a `variable.@this`, so it falls to the `else` and the bare `%path%` is stored under `path`. The guard means to ask *"is this a live full-match reference?"* (`param.IsVariable`) but instead asks *"is this a name-slot?"*. The comment's "a literal / partial template is stored as-is" is the blind spot — a **full-match** template is neither literal nor partial, and storing it as-is is the clobber.

## Fix — the crash #2 write-door collapse covers both

No new fix needed. The direction in `crash2-stackoverflow.md` — **collapse a full-match `%ref%` to its current instance at the variable write door (`Variable.Set`)** — dissolves this too: the seam's `else` becomes `Set("path", param)`, the door resolves `%path%` to `path`'s current value (`"/"`), and no self-ref is ever stored. One change, both crashes.

This is why the fix belongs at the write door, not at the seam: the seam is just *one* writer that bypassed canonicalization. Patching only the seam's predicate (e.g. `if (param.IsVariable)`) fixes this path but leaves the read door (`text.Value`) one forgotten writer away from the same SO. The door owns the invariant — *a binding never holds a live self/cyclic reference* — and every writer, the seam included, gets it for free. The seam's bespoke resolve-before-store then becomes redundant and can go.

## Repro (run, both green / core-dumped as noted)

Step-4 clobber, isolated (`PLang.Tests/Modules`), passes — documents the bug as it stands:

```csharp
var app = new global::app.@this("/app");
app.Goal.Add(new global::app.goal.@this { Name = "Stub", Path = "/Stub.goal" });
var ctx = app.User.Context;

await ctx.Variable.Set("path", "/");                                   // step 0
var liveRef = new global::app.data.@this(
    "path", new global::app.type.text.@this("%path%", "plang"), context: ctx); // step-4 arg shape
await new Call { Context = ctx,
    GoalName = new GoalCall { Name = "Stub", Parameters = new List<Data> { liveRef } } }.Run();

var after = await ctx.Variable.Get("path");
// after.IsVariable == true, after.Peek().ToString() == "%path%"  → caller's "/" was clobbered
// (do NOT call after.Value() — it StackOverflows)
```

Real builder: `plang build <empty-app>` core-dumps with the cycle above.
