# Resume mechanics — the issue/resume movie

The conceptual mechanic (bind, jump, run) lives in [resume.md](resume.md). This file walks the same flow concretely across the serialization boundary — issue, persist, deserialize, verify, run.

## Issue (developer's `on error` runs)

```plang
- insert into users, name=%name%
   on error call goal HandleError

HandleError
- write %!error.callback% to file callbacks/%!error.id%.bin
```

Step-by-step:

1. `insert into users` blows up. Error is captured in `App.Errors`; handler dispatch begins.
2. `HandleError` runs as the recovery body.
3. `%!error.callback%` is read → lazy materialization triggers (see [callback-schema.md](callback-schema.md#lazy-materialization-of-errorcallback)).
4. `- write %!error.callback% to file ...` invokes file-channel write → serializer kicks in.
5. Serializer sees `Data<Callback>` with no `Signature` → calls `signing.SignAsync` with no expiry → populates `Data.Signature` (see [transparent-signing.md](transparent-signing.md)).
6. Serialized bytes (Callback record + Signature) hit disk.

## Resume (developer triggers it)

```plang
Recover
- read file callbacks/%id%.bin, write to %callback%
- run %callback%
```

Step-by-step:

1. `read file ...` reads bytes. Deserializer reconstructs `Data<Callback>` with `Signature` populated (unverified).
2. `- run %callback%` invokes the callback module:
   - `signing.verify` checks signature integrity, expiry (no expiry by default → always passes), identity, contracts. **Hard error on mismatch.**
   - Look up goal: `app.Goals.LoadByPrPath(callback.GoalPrPath)`. **Hard error if not found** (referent-integrity failure — same shape as a deleted identity).
   - Compare `goal.Hash` against `callback.GoalHash`. **Hard error on mismatch** (signed but stale — redeployed goal).
   - Construct fresh `App` with the Callback as entry point.
   - `App.Run(callback)`:
     - `RegisterDefaults()` runs.
     - Replay runtime provider registrations + apply default selections from `Selections` (see [snapshotted-system.md](snapshotted-system.md#providers--two-layers)).
     - Populate per-actor Variables from `VariablesByActor`.
     - Populate `Errors.Trail` from `ErrorTrail`.
     - Populate `App._statics` from `Statics`.
     - Apply mode flags (`BuildEnabled`, `TestingEnabled`).
     - Main loop's first tick lands at `(GoalPrPath → goal, StepIndex, ActionIndex)` → re-executes the failed action with the captured state.

That's the whole flow.

## Goal lookup on resume

`app.Goals.LoadByPrPath(prPath)` is the lookup. Goals load lazily; the resumer needs the path to find one. If the file doesn't exist (developer deleted/moved it between issue and resume), the resume fails as a referent-integrity error — same shape as a deleted identity.

The actual API name (`LoadByPrPath` vs `GetByPath` vs whatever) is a coder lookup task. **The architectural requirement:** the App must be able to load a goal by its `PrPath` from the on-disk goals registry. Whether that involves filesystem scanning or an in-memory index is an implementation detail.

## `Goal.Hash` subtlety — known limitation

`Goal.Hash` (at `PLang/App/Goals/Goal/this.cs:121`) covers the developer's prose: goal name + concatenated step text. It does **not** cover:

- The compiled `.pr.json` (so a recompile that doesn't change source produces the same hash — good, this is what we want).
- The behavior of modules referenced by steps. If `file.write`'s implementation changes between issue and resume in a way that changes step semantics without the prose changing, the hash stays the same and the resume runs against the new behavior.

**Accept this.** The dominant invalidation case is "developer changed the goal file," and `Goal.Hash` catches that. Module-behavior drift is a rare cross-cut concern. A future branch could extend the hash to include module signatures (or the resolved `.pr.json` schema), but it's not warranted now.

Documented as a known limitation in [open-threads.md](open-threads.md), not blocking.

## No cross-process causal trace

There is **no cross-process causal trace** in the runtime data model. Telemetry stitching between the original run and the resumed run happens at the log layer by correlating callback identity (signature digest, expiry timestamp). `Call.Cause` stays same-process only — its invariant ("live ref, same process only") is preserved.
