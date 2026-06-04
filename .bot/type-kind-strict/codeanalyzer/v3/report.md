# codeanalyzer v3 — type-kind-strict + lazy-deserialize merged state

**HEAD:** `f971f98e6` · **Re-review scope:** the merged branch (Ingi's "branch
declared done" call that coder v13 parked for). codeanalyzer v2 already PASSed
the type-kind-strict work at coder v8 (`fd7ee4812`); lazy-deserialize was reviewed
and PASSed on its own branch (`d8456e26f` → `050464852`). This pass does **not**
re-litigate either body — it reviews the **integration seams** where the two
features collide, the merge resolution, and coder v9–v13.

## Ground truth (clean rebuild)

- `rm -rf bin/obj` across all four projects → `dotnet build PlangConsole` → **0 errors**.
- **No PLNG001 / PLNG002.** No `System.IO.*` / `Console.*` in any changed
  production file (`PLang/**/*.cs` diff vs `fd7ee4812`).
- **PLang suite: 273 / 273 pass, 0 fail** (deterministic, `Tests/`).
- **C# suite: 4025 total, 0 failed, 0 skipped.**

## Integration seam: strict-kind × lazy materialization — **CLEAN** (traced)

This is the one place the two features genuinely overlap. The risk: lazy-
deserialize added the `RawUntouched` verbatim-passthrough early-return in
`variable/set.cs` Run() (lines 203–209) that returns *before* the
`IStrictKindEnforcer` stamp (lines 264–274) — so could a strict, lazy,
content-mismatched value slip through unenforced?

Traced end to end; it cannot:

- For any `IKindValidatable` type (image is the only strict-kind family today),
  the run-time probe block at `set.cs:181` fires first and reads `Value.Value`
  at line 184. `data.@this.Value` (`this.cs:182`) materializes `_raw` into
  `_value` and caches it, so `RawUntouched` (`_raw!=null && _value==null`) is
  **false** by the time the passthrough at line 203 is tested. A strict image
  therefore always falls through to the enforcer stamp at line 264.
- The passthrough only catches **non-`IKindValidatable`** raw-backed values
  (`{object,json}`, `{table,csv}` via `write to %var%`) where strict-kind
  enforcement does not apply — exactly its intent.
- At line 264 the path-backed (still-unloaded) image gets `RequireStrictKind`
  imprinted; `CheckStrictKind()` returns null while `_bytes==null`, deferring to
  `image.BytesAsync` (`image/this.cs:124–126`), which throws
  `StrictKindMismatchException` at first byte-load. The imprint survives because
  `typedData.Value` is the same image instance that `Variables.Set` aliases by
  reference. This is precisely the v2-F1 resolution I mutation-verified at coder
  v8, and the merge did not touch `image/this.cs`'s strict logic.

**Note (readability, non-blocking):** the passthrough's safety depends on the
implicit ordering — line 184 having materialized `Value` before line 203's
`RawUntouched` test. It holds for every reachable strict path, but the coupling
is implicit (nothing at line 203 states "strict families already materialized
above"). A one-line comment at the passthrough naming that dependency would make
the invariant local instead of inferred. Left to the coder's discretion; not a
finding.

## Other integration seams — covered

- **Signature carry × shallow clone** (`variable.set` no-type arm sharing `_value`
  by reference): `SignedDataSurvivesVariableSetListTests.cs` drives the real
  `variable.set` + `verify` handlers, asserts `Signature` non-null *and*
  `verify.Value == true`. Mutation-verified per coder v13 (deep clone drops the
  `[JsonIgnore]` Signature → red).
- **MaterializeFailed on the set path** (malformed-JSON parent): the two new
  `MaterialiseErrorPathTests` set-path cases assert `Error.Key == "MaterializeFailed"`
  distinctly from `NotFound`, including a nested-path arrival via an already-failed
  parent. Real assertions, not assertion-free.

coder v9–v13 touched **test files only** — no production source changed after the
merge commit (`d4fdd030c..HEAD` is exactly the two test files above). Verified.

## Findings

### F1 (MINOR, systemic, non-blocking) — `Stage N` / provenance comments
Pervasive across the lineage: `set.cs:27` ("post-Stage-4 — a type, not a string"),
`set.cs:72` ("replaces the historical bare string"), plus ~18 `// Stage N` /
`Stage N cleanup` comments across `type/**` and `data/**` (`Field.cs:5`,
`datetime/this.cs:5`, `list/Conversion.cs:21`, `data/this.Navigation.cs:284,358`,
`data/JsonString.cs:50,60,70`, …). Per Pass 2, code states what *is*; provenance
belongs in `git blame`. This is house style accepted by every prior pass on this
lineage and is **systemic, not local to the merge** — flagging per character, but
the cleanup is a docs/architect sweep, not a coder blocker for this branch.
Does not affect the verdict.

## Clean (verified, won't re-litigate)
- v2-F1 strict-kind ride-to-load-seam: still resolved (image strict logic
  untouched by merge; mutation-verified at v8).
- The lazy-deserialize body (reader registry, lazy `_raw` backing, Wire
  passthrough, channel.read boundary, number tower): reviewed and PASSed on its
  own branch; no merge-introduced regression in the integration files.
- No new courier-reaches-into-`Data.Value` (smell #7), no new public-mutable-
  collection or flat-copy smells introduced by the merge seam files.

## Verdict: PASS
Merged type-kind-strict + lazy-deserialize is sound — the strict-kind × lazy
passthrough seam enforces correctly (traced), both suites green on a clean
rebuild, post-merge work is test-only with real assertions; the only finding is a
systemic provenance-comment nit that prior passes already accepted.
