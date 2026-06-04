# tester — v8 — result

**Verdict: FAIL** (HEAD `38d01b26a`, coder v8 + codeanalyzer v2 PASS).

The production work (F1–F5) is sound and the C# suite is solid. But the **PLang
test suite is not reproducibly green from a clean binary** — that is a real,
reproducible blocker, so the verdict is FAIL.

---

## Test runs

### C# — PASS, deterministic
Clean rebuild (`bin`/`obj` wiped, `dotnet build PlangConsole` → 0 errors), then
`dotnet run --project PLang.Tests`:

```
total: 3815, passed: 3815, failed: 0, skipped: 0
```

Matches coder v8's claim exactly. The C# runner recompiles in place, so it is
immune to the stale-binary / stale-fixture problem below.

### PLang — FAIL, NOT reproducible
`cd Tests && plang --test` on the freshly-rebuilt binary. I ran it **five+
times from a clean git state**. It does **not** converge to a stable green:

| Run | Failures | Failing goals |
|-----|---------:|---------------|
| early (cold-ish cache) | **4** | ReadPhotoStampsImage, LoadDllRegistersType, SetAsTextUppercase, SetIntLiteralIsNumberInt |
| mid | 1 | CompressRoundTrip |
| mid | 0 | — |
| back-to-back A→B (no revert) | 0 → **1** | (B) one fail, +2 `.pr` rebuilt |
| late (warm cache) | 0 | — |

The number and identity of failures **flaps run-to-run on identical input**. A
green result is luck, not a property of the branch.

The builder validates the cause aloud during a run:
```
builder.validate: Failed to deserialize ... to app.goal.steps.step.actions.action.this
The LLM couldn't produce a usable plan for this goal — its proposed step count
didn't match the goal, and the retry didn't recover. (the LLM is non-deterministic)
```

---

## Root cause (CRITICAL finding)

`plang --test` re-runs goals from their committed `.pr`. When a committed `.pr`
no longer matches the current code, the runtime either (a) runs it as-is and it
fails at runtime, or (b) LLM-rebuilds it on the fly. Two stale axes, both
branch-induced:

**A. Committed `.pr` encode the pre-stage-4 `variable.set.Type` shape.**
Stage 4 (coder v4) changed `variable.set`'s `Type` parameter from a bare string
to a structured `{name,kind,strict}` entity. But large parts of `Tests/` still
carry the **old** shape. Concretely, `Tests/Types/.build/readphotostampsimage.test.pr`:

```json
{ "name": "Type", "value": "image", "type": "string" }   // pre-stage-4 shape
```

vs. the new shape the same branch's builder now produces:

```json
{ "name": "Type", "value": { "name": "image", "kind": "gif", "strict": true },
  "type": { "name": "type" } }
```

Running the stale shape on the current binary is exactly what produced
`ReadPhotoStampsImage`'s failure:
`Cannot convert 'app.type.image.this' (this) to String: Object must implement IConvertible`
— a **runtime** error, not a build error. `Serialization/CompressRoundTrip`
(`Type:"text/plain"` string) is the same staleness. 688 of 703 committed `.pr`
are still in the old `"type":"<string>"` parameter format; only 15 are new-format
(the TypeKindStrict goals the coder actively regenerated).

**B. On-the-fly LLM rebuilds are non-deterministic, and the cache is gitignored.**
When the runtime decides a `.pr` is stale it rebuilds via the LLM builder. The
builder is non-deterministic (it says so itself — "step count didn't match …
the LLM is non-deterministic") and sometimes fails outright. The builder's LLM
response cache lives in `Tests/.db/system.sqlite` (`LlmCache` table), which is
**not tracked by git** (`*.cache` / `.db` ignored). So a fresh clone / CI is
cold → every stale `.pr` needs a live, flaky rebuild. "262/262, 0 fail" only
reproduces once the cache is warm — which it was in the coder's and
codeanalyzer's sessions, and is now in mine. It is not the clean-checkout state.

This is why both prior bots honestly reported green and I still call FAIL: they
measured a warm-cache working tree; the committed branch does not reproduce it.

### Fix
Regenerate the whole `Tests/` tree against the current binary (`plang build`)
and **commit the refreshed `.pr`** so they match the stage-4 `Type` entity shape
and no rebuild is triggered at `--test` time. Then prove it: a clean-binary
`plang --test` must be green across **≥2 consecutive runs** with **zero `.pr`
files modified** by the run (`git status` clean after). Until non-builder test
goals run from committed `.pr` without an LLM rebuild, the suite stays flaky.

---

## F1 — strict-kind enforcement: tests are HONEST (the v1 false-green is closed)

This was the review-driven, highest-risk area. I traced it independently and it
holds up:

- **Two enforcement paths, two distinct tests, no overlap.**
  - Already-loaded / read-lift `image.@this` → `variable/set.cs` imprints
    `RequireStrictKind` then `CheckStrictKind()` immediately (bytes in hand →
    sniff → fail at the set). Covered by
    `Cut2.ReadLiftImagePngAsImageGifStrict_FailsAtSet` (real bytes-backed PNG
    `image.@this`, asserts `IsFailure` + message contains `gif`).
  - Lazy path-backed → `CheckStrictKind()` returns null at the set (deferred);
    `image.BytesAsync()` enforces and throws `StrictKindMismatchException`.
    Covered by `LazyPathHandleTests.BytesAsync_StrictKindMismatch_ThrowsAtLoad_NotAtConstruction`.
  - Raw `byte[]` → the pre-existing `IKindValidatable` probe block (mutually
    exclusive with the enforcer block on the value's shape). Covered by the
    `Cut2` byte[] build/runtime tests.
- **Deletion test (by code-read):** deleting `enforcer.RequireStrictKind(...)`
  in `set.cs` makes `CheckStrictKind()` return null → no error →
  `ReadLiftImagePngAsImageGifStrict_FailsAtSet` flips red. The line is guarded.
  (Codeanalyzer v2 ran the live mutation — forced `CheckStrictKind()` to
  `(true,null)` and both F1 tests flipped to failed, then green on revert.)
- **Builder false-green check:** `setasimagegifstrictmismatch.test.pr` step text
  `set %img% = "photo.png" as image/gif strict` maps to `variable.set` with
  `Type={name:image, kind:gif, strict:true}` — faithful, no action-index shift.
  The `SetAsImageGifStrictMismatch` goal documents the lazy contract correctly
  (set clean, throw deferred to load; throw-at-load is C#-covered) and asserts
  real values (`Type.Name==image`, `Type.Kind==gif`).
- **Builder works:** I built a throwaway `as image/png strict` goal with
  `--builder={"cache":false}`; it produced `variable.set` with
  `Type={image, png, true}`. The builder is not broken for this syntax.

**Minor (not the reason for FAIL):** no single end-to-end PLang/integration test
walks `set → store → retrieve → load → throw` for a *lazy* strict image
(codeanalyzer residual #2). The `SetAsImageGifStrictMismatch` goal sets it but
never forces a load, so the deferred throw never fires in PLang. Imprint-survival
is established only by code-read of `Variables.Set`'s by-reference aliasing.
Worth a goal that reads `%img.Width%`/`%img.Bytes%` after a mismatched strict set
once the fixtures are stable.

## F2–F5 — confirmed
- **F2** `Data.Kind` is `[JsonIgnore]`, getter delegates to `_type?.Kind`; no flat
  wire sibling. Good.
- **F3** `type.@this.Scheme => Name=="path" ? Context?.App.Type.Scheme : null` —
  null-guarded.
- **F4** `app/type/text/this.Build.cs` deleted; no `!= "text"` special-case left
  in `set.cs`.
- **F5** dead `CanonicaliseKind` fast-path gone; `BuilderNames` inlined.

## Coverage
Changed production files for F1–F5 are exercised by the C# suite (3815 green).
The gap is reproducibility of the PLang fixtures, not C# line coverage.
