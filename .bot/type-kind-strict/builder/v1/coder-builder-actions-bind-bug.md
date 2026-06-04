# Coder handoff — builder self-build broken: `builder.actions` return can't bind to `%actions%`

**From:** builder
**Branch:** type-kind-strict @ 254f31b69
**Severity:** BLOCKING — the builder cannot rebuild itself. We cannot rely on an old
builder binary; self-build must work.

---

## Symptom

Self-rebuilding the builder fails on the **first step of the first goal**. Reproduce:

```bash
cd os/
plang '--build={"files":["Build.goal","BuildGoal.goal","BuildGoal/Start.goal","BuildGoal/Plan.goal","BuildGoal/Validate.goal","BuildGoal/LlmFixer.goal","BuildStep/Start.goal","BuildStep/Validate.goal"]}' build
```

Result: `0 Saved`, exits non-zero (after the Program.cs fix below). Building a *normal*
user goal works fine — only goals that call `builder.actions` break, which is why only
**self-build** hits it.

It is NOT planner LLM non-determinism. It fails deterministically, with `cache:false`,
every run.

## Root cause — type mismatch on `builder.actions`'s return

The failing step is `BuildGoal/Plan.goal:6`:

```
- builder.actions, write to %actions%
```

The clean runtime error (now legible thanks to the F2 message work):

```
builder.actions: Could not bind: expected text (String) but the value is
   this app.goal.steps.step.actions.this (this).
```

What happens:

1. `builder.actions`'s handler (`PLang/app/module/builder/actions.cs`) declares its
   **return** as the domain collection object:
   ```csharp
   public async Task<data.@this<global::app.goal.steps.step.actions.@this>> Run()
   {
       var result = await Builder.Actions(this);
       return data.@this<global::app.goal.steps.step.actions.@this>.From(result);
   }
   ```
   (`app.goal.steps.step.actions.@this` is a real type —
   `PLang/app/goal/steps/step/actions/this.cs` — the `step.actions` collection.)

2. `, write to %actions%` becomes a peer `variable.set` capturing `%!data%`.

3. In the built `plan.pr`, that `variable.set`'s `Value` is tagged
   `type: list<action>`, while `builder.actions`'s own input param `Actions` is
   `list<text>` (it's `data.@this<List<string>>?`). The two "actions" slots disagree.

4. At dispatch the runtime tries to bind the `step.actions.@this` **object** into a
   `text`/`list<text>` slot → `Could not bind: expected text but value is
   app.goal.steps.step.actions.this`.

So the value is the rich domain object, but the consuming slot wants flat text. This is
a `type-kind-strict` regression — pre-branch the lenient string handling absorbed it;
post-branch the stricter type binding rejects it. The builder's goals are the only
goals that call `builder.actions`, so only self-build trips it.

## Where to fix — coder's call between two shapes

- **(A) `builder.actions` should return a flat list** (`list<text>` / `List<string>`),
  not the `step.actions.@this` domain object. The consumers (`%actions%` → passed back
  into `builder.actions Actions=...`, and into the planner prompt rendering) want the
  action-name strings, not a collection object. The input param is already
  `List<string>`; the return being a domain object is the mismatch. **Builder's lean: A** —
  "give me the catalog rows as strings" shouldn't hand back a collection object that then
  has to be coerced to strings.

- **(B)** Keep the rich return, but make the binding flatten `step.actions.@this` →
  `list<text>` when it crosses into a text-list slot (a converter on the type). Heavier;
  keeps a richer return nobody here seems to need.

Recommend confirming what every consumer of `%actions%` actually expects before
choosing; (A) looks correct from the call sites.

## Already fixed in this branch (separate, do not redo)

`PlangConsole/Program.cs` swallowed the failure: it printed the error to `Console.Error`
and fell off the end → **process exit 0** even on a fully failed build, hiding this bug.
Changed to:

```csharp
var result = executor.Run(args, cts.Token).GetAwaiter().GetResult();
return result.Success ? 0 : 1;
```

(Also removes a `Console.*`-in-production-C# ban violation; the error already surfaces
through the error channel on unwind, so the write was redundant.) A failed build now
exits 1 — verified. This is the visibility fix, NOT the binding fix; the
`builder.actions` mismatch above is the real blocker and is still open.

## Verification once fixed

```bash
cd os/
plang '--build={"files":[ ...the 8 ordered builder goals... ]}' build   # must: Saved, exit 0
cd ../Tests/Simple && plang build                                         # must still build a normal goal
```
