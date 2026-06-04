# auditor — lazy-deserialize — v1

**Verdict:** NEEDS WORK
**HEAD audited:** `ca6e2fb7c` (security v1 commit)
**Upstream bots verified:** codeanalyzer v2 PASS, tester v3 PASS, security v1 PASS — all on same source tree.

## Scope

Cross-check the three upstream verdicts (codeanalyzer/tester/security) and trace error
propagation across the new lazy-materialization seams. Did the bots converge on a coherent
story? Are there cross-file paths none of them owned?

## Verdict rationale

Three upstream bots passed. I am overriding to **NEEDS WORK** on one finding the upstream
trio didn't cover: a cross-file error-visibility regression at the navigation surface.
Codeanalyzer focused on shape (F1 list-element contract, F2 naming). Tester focused on
suite quality and false-green probes. Security focused on the lazy↔signing boundary.
**Nobody traced the failure path of `Materialize()` itself across instances.**

## Findings

### F1 (Major) — `MaterializeFailed` error is stamped on the Data, then lost by every navigation surface that triggers it

**Path:** `PLang/app/data/this.cs:281-305` → `PLang/app/data/this.Navigation.cs:248,270,321` and `PLang/app/variable/list/this.cs:274-278`

`Materialize()` (this.cs:281) catches reader exceptions and stamps:

```csharp
Error = new Error(
    $"failed to read %{Name}% as {t?.Kind ?? t?.Name ?? "value"}: {real.Message}",
    "MaterializeFailed", 400) { Exception = real };
return null;
```

The error is set on `this` (the raw-backed Data). `Value` getter then caches `_value = null`.
Now look at every navigation entry that touches `.Value`:

**Read path — `GetChildValue` (this.Navigation.cs):**
- Line 248 `var val = Value;` — triggers Materialize, stamps `this.Error`, returns null.
- Line 268 `if (val is string && _type != null)` — val is null, skipped.
- Line 279 `if (val != null)` — false, no navigator dispatch.
- Line 296 `Properties[key]` — typically empty for a freshly-read raw Data.
- Line 318 `if (val is string) return TypeUnknownError(key);` — false (val is null).
- Line 321 `return NotFound(key);` — **a fresh Data with no error stamped**.

The actionable diagnostic (`"failed to read %config% as json: Unexpected token at line 5"`)
is on `this.Error`, but the navigator returns a NEW Data instance with no error. The
developer doing `%config.host%` on a malformed JSON file sees a downstream
`%config.host%` resolves-to-nothing or NotFound message, not the parse error that
explains why.

**Set path — `SetValueOnObjectByPath` (variable/list/this.cs):**
- Line 274 `if (!parent.IsInitialized && parent.Value == null) return data.@this.NotFound(name);`
  Triggers Materialize via `.Value`. Stamps `parent.Error`. Returns `NotFound(name)`.
- Line 278 `parent.Materialise();` — alternate entry, same outcome.
- Line 303 `var target = parent.Value; if (target == null) return data.@this.NotFound(name);`
  Same swallow.

The `MaterializeFailed` error stays on `parent`; the caller gets a generic NotFound.

**Cross-instance smell.** This is the auditor pattern: the error is stamped on instance A,
but the function returns instance B. Surfacing requires either (a) returning a Data
constructed via `FromError(this.Error)` when materialization failed, or (b) routing
through a shared error pipeline. Today neither happens.

**Zero test coverage.** `grep -r MaterializeFailed Tests/` returns nothing. The error
key has no goal or C# test asserting it surfaces. Combined with the swallow, a
regression here is invisible.

**Why this is major (per `feedback_error_visibility.md` + `feedback_review_discipline.md`):**
A developer reading malformed JSON via `set %x% = file.read ... as json` then writing
`set %y% = %x.host%` will see "NotFound" and chase the wrong cause. A user *would* file a
bug. "Bugs producing wrong output are never minor."

**Suggested fix shape** (coder decides):
- In `GetChildValue` after the failed Materialise at line 270-271, check `Error != null`
  and `return FromError(Error)` before falling through to NotFound. Mirror in
  `SetValueOnObjectByPath` lines 275/303.
- Add a goal regression test: `set %cfg% = file.read "bad.json" as json` (a fixture with
  malformed JSON) → `set %x% = %cfg.host%` → assert `%!error.key% == "MaterializeFailed"`
  and the message names the path.

### F2 (Minor) — codeanalyzer's deferred F2 is still untracked

Codeanalyzer v2 closed with one open item: the `Materialize()`/`Materialise()` one-vowel
naming footgun was "routed to the collections-are-data architect handoff," but no
handoff/todo artifact was filed. I verified:

- `Documentation/Runtime2/todos.md` — no entry.
- `.bot/lazy-deserialize/architect/` — no collections-are-data plan referencing the rename.
- `grep -rin "collections-are-data" Documentation/` — no hits.

Both methods exist on HEAD (`PLang/app/data/this.cs:281` `Materialize`, `:314` `Materialise`).
The deferral is defensible (rename pairs with a larger storage-unification design call), but
without a tracked artifact it will be lost. Either (a) rename now, or (b) file the entry in
`Documentation/Runtime2/todos.md` before merge.

### F3 (Info) — Tester's missing-regression gap is real and benign

Tester v3 noted the `variable.set` List arm (`set %bundle% = [%signed%]`) is not pinned by a
goal test, while `list.add` is (`SignedDataSurvivesInList`). Probe confirmed `ShallowClone`
shares `_value` by reference, so the inner signed Data's Signature survives. Recommend
adding the symmetric goal test as cheap insurance against a future ShallowClone change in
variable/set.cs that doesn't update list/add.cs.

### F4 (Info) — Security's F2 ties into F1

Security v1 F2 noted `Materialize()`'s catch-all (`Exception ex when ex is not (NRE|OOM|SOE)`)
would silence a future `CryptographicException`/`SecurityException` from a kind reader.
That's the same swallow site as my F1 — fix F1's error-visibility leak and security F2's
"silenced exception" becomes "surfaced as MaterializeFailed with the inner exception in
`Error.Exception`," which is the desired shape.

## What I verified directly

- Read `PLang/app/data/this.cs` lines 170-330 (Value getter, Materialize, Materialise, ScalarValue).
- Read `PLang/app/data/this.Navigation.cs` lines 200-340 (GetChildValue, TypeUnknownError).
- Read `PLang/app/variable/list/this.cs` lines 260-310 (SetValueOnObjectByPath).
- Read `PLang/app/module/variable/set.cs` lines 270-308 (ShallowClone mint).
- Confirmed no `MaterializeFailed` test coverage (`grep -r MaterializeFailed Tests PLang`).
- Confirmed no `collections-are-data` handoff (`grep -rin` Documentation tree).
- Confirmed HEAD = `ca6e2fb7c`; all three bots ran on same source tree.

## What I did NOT re-verify (trusted upstream)

- Suite green (codeanalyzer rebuilt + ran 273/273 + 4021/0 on this HEAD; tester independently confirmed).
- Wire round-trip byte-identity for `RawUntouched` signed Data (security walked the path).
- Stage 4/5 channel-boundary refactor end-to-end.

## Next bot

**coder** — to address F1 (surface MaterializeFailed at the navigation seam + add a goal
regression test) and either rename or file the F2 todo. F3/F4 are flag-don't-block.

After coder lands the fix:
- codeanalyzer/tester re-review the navigation seam delta and the new regression test.
- Then back to auditor for sign-off.
