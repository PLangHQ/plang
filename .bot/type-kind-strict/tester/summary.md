# tester — type-kind-strict

## Version
v8 (matches coder v8; first tester run on this branch). Verdict: **FAIL**.

## What this is
The branch reshapes PLang's type value into a structured `{Name, Kind, Strict}`
entity, folds `Data.Kind`, and adds strict-kind enforcement. coder v8 was the
codeanalyzer-v1 response (F1 strict enforcement + F2–F5). codeanalyzer v2 PASSed
it. I validate test honesty and suite reproducibility.

## What was done
- **C# suite: 3815/3815** on a clean rebuild — matches coder's claim, deterministic.
- **F1 (strict-kind, the v1 false-green area) is now honest.** Traced both
  enforcement paths (read-lift/already-loaded fails at the `set` via
  `Cut2.ReadLiftImagePngAsImageGifStrict_FailsAtSet`; lazy path-backed throws at
  `image.BytesAsync` via `LazyPathHandleTests.BytesAsync_StrictKindMismatch...`),
  plus the raw-`byte[]` probe path. Builder `.pr` for the strict goal is faithful
  to its step text. codeanalyzer v2 mutation-verified; my code-read agrees.
- **F2–F5 confirmed** (Data.Kind `[JsonIgnore]`; Scheme null-guard; text Build-hook
  deleted; dead fast-path removed).
- **FAIL reason — PLang suite not reproducibly green.** `plang --test` on a clean
  binary flaps **0–4 failures across identical runs**. Root cause: committed `.pr`
  fixtures are stale vs the branch's own **stage-4** change to `variable.set`'s
  `Type` parameter (bare string → `{name,kind,strict}` entity). 688/703 committed
  `.pr` still use the old `"type":"<string>"` shape. The runtime either runs them
  wrong (e.g. `ReadPhotoStampsImage` → runtime `Cannot convert image to String`)
  or LLM-rebuilds them on the fly — and the builder is non-deterministic, with its
  cache (`Tests/.db/system.sqlite`) gitignored, so a fresh clone is cold. The
  "262/262" both prior bots reported is a warm-cache artifact.

## Code example (the staleness)
```json
// committed Tests/Types/.build/readphotostampsimage.test.pr  (PRE-stage-4):
{ "name": "Type", "value": "image", "type": "string" }
// what the current builder produces (stage-4 entity):
{ "name": "Type", "value": { "name": "image" }, "type": { "name": "type" } }
```

## What to do next
Coder: `plang build` to regenerate the whole `Tests/` tree, **commit** the
refreshed `.pr`, then show clean-binary `plang --test` green across **≥2
consecutive runs** with `git status` clean after each (no `.pr` rewritten by the
run). The production F1–F5 logic itself is sound — this is fixture hygiene /
reproducibility, not a logic defect.

## Files (this version)
- `v8/plan.md`, `v8/result.md` (detailed), `v8/verdict.json`
- `../test-report.json` (branch root, shared)
