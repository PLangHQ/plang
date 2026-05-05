# `Callback` record schema

The wire/storage shape is a signed `Data<Callback>` envelope — single blob, one signature covering the whole content. Tampering with any field (actor name, goal_hash, position, captured values) breaks the signature; the envelope is rejected wholesale. Leans on `Data` already being the universal envelope type — no separate "header" sitting outside the signature.

## The record

```csharp
record Callback(
    string GoalPrPath,                             // relative path to .pr file (App.Goals loads goals lazily by path)
    string GoalHash,                               // = goal.Hash (SHA-256 of name + step text); drift = hard error on resume
    int StepIndex,
    int ActionIndex,
    string ActorName,                              // System / Service / User
    Dictionary<string, object?> VariablesByActor,  // per-actor name → value bag
    Dictionary<string, object?> Selections,        // App.Providers + identity choices, by name
    Dictionary<string, object?> Statics,           // App._statics until that TODO closes
    List<IError> ErrorTrail,                       // read-only at restore
    bool BuildEnabled,
    bool TestingEnabled
    // Signature lives on the Data<Callback> envelope (Data.Signature), not a Callback field.
    // Validity / expiry, when wanted, also lives in Data.Signature — populated by explicit
    // `- sign %callback% expires in N`. Default signing is integrity-only, no expiry.
);
```

## Field rationale

- **`GoalPrPath`** is required because `App.Goals` loads goals lazily by path — without it the resumer can't locate the goal to load.
- **`GoalHash`** is `Goal.Hash` (already on `PLang/App/Goals/Goal/this.cs:121`, SHA-256 of name + concatenated step text). No new hashing infrastructure. Drift between `GoalHash` and the loaded `goal.Hash` at resume time is a hard error (signed but stale; goal was redeployed). See [resume-mechanics.md](resume-mechanics.md#goalhash-subtlety--known-limitation) for the known limitation.
- **`StepIndex`, `ActionIndex`** are the resume position. Resume always lands at the action — see [resume.md](resume.md#position-semantics).
- **`VariablesByActor`** carries the values that survive resume. Per-actor partitioning means restore populates each actor's `App.Variables.@this` from its own slice. See [snapshotted-system.md](snapshotted-system.md) for the per-actor capture rules and [variable-capture.md](variable-capture.md) for the error-retry throw-time semantics.
- **`Selections`** carries names — provider defaults, identity, datasource — that resolve through their existing registries on restore. Names + referent integrity, not objects.
- **`Statics`** is the app-scoped mutable dict; provisional until the `App._statics` TODO closes (then drop this field).
- **`ErrorTrail`** lets the resumed run read `%!error.trail%` naturally.
- **`BuildEnabled`, `TestingEnabled`** are mode flags that live inside the Callback rather than reconstructing from CLI — they're part of *this* callback's run, not a process-level concern.

## No `Expiry` field

Validity / expiry, when wanted, lives in `Data.Signature.Expires` (on the renamed `SignedData`; see [signature-rename.md](signature-rename.md)). Having `Expiry` on `Callback` *and* on the signature would mean two sources of truth for the same value — guaranteed drift. One source of truth: the signature.

Default signing has no expiry — see [transparent-signing.md](transparent-signing.md) for why integrity-without-validity is the default.

## Lazy materialization of `%!error.callback%`

When developer code reads `%!error.callback%`, lazy materialization triggers:

1. Build `Callback` record from the Errors trail's most recent error: action position (`StepIndex`, `ActionIndex`), `GoalPrPath`, `GoalHash` from `Goal.Hash`.
2. Populate `VariablesByActor` from each actor's Variables (with the per-actor exclusion rules in [snapshotted-system.md](snapshotted-system.md)).
3. Snapshot `App.Providers` registry-layer state into `Selections`, `App._statics` into `Statics`, `Errors.Trail` into `ErrorTrail`.
4. Apply throw-time variables correction via Diff reverse-apply — see [variable-capture.md](variable-capture.md).
5. Wrap in `Data<Callback>`. **`Data.Signature` is not yet populated** — it's a synthesized PLang property, not a serialized one. The signature gets attached at serialize time by the IO hook in [transparent-signing.md](transparent-signing.md).
