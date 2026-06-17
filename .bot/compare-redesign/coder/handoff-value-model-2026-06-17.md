# Handoff — value-model / binary-kind flip (compare-redesign, 2026-06-17)

**HEAD:** `711c2865b`. All pushed. Read this + `mime-binary-lazy-plan.md` first.
**Companion docs:** `mime-binary-lazy-plan.md` (the model + 3 open items),
`host-carrier-spec.md` (the original clr-closure plan), `host-carrier-handoff-2.md`
(prior session).

## Where the branch is

The branch's goal is the **value model**: the type instance IS the value; three
doors (sync `Peek` / async `Value()` / async `Write`); content decodes lazily on
access; `clr` reduced to a closed host-object carrier. That model is now **in and
load-bearing**, not just speced.

### Landed this arc (all green except the §C fallout below)
- **`binary/kind` + lazy decode** — content off I/O (`Format.TypeFromMime`) types as
  `{binary, kind}`; the kind names the inner type; `Value()` runs that type's reader
  and the holding Data rebinds. Verified: json→dict, csv→table (+cell nav, `foreach
  %t%`), image, md→text, text/plain→text, html→code, `.pr`→goal.
- **`kind` is a value** (`app.type.kind.@this`) — `new Kind(name, ctx).Type` owns
  kind→type (Readers.TypeOf ?? Format.TypeOf ?? binary). Build-hook dispatcher → `kind.Hooks`.
- **`StampValue` always hands bytes**; readers (json/csv/text/goal/code) decode via
  `new text.@this(raw)` (the text ctor owns bytes→string).
- **`table` is now an `item.@this`** (Kind, Mint{table,kind}, Navigate rows/headers,
  per-column-format TODO in the source). **`EnumerateItems` moved onto the types**
  (item virtual; dict/list/table override; `Data.EnumerateItems` delegates).
- **Deterministic `CanonicaliseKind`** (shortest-then-ordinal tie-break — `_extensionToMime`
  is a ConcurrentDictionary, no stable order).
- **`code` got a Read reader** + `.htm`→code (html narrows to a code value).
- **Snapshot reader peels the `@schema:data` envelope** on section-list elements
  (was a PRE-EXISTING bug — fixed ThrowTimeSnapshot + several others, Wire 16→8).
- **§C — clr courier-label cruft deleted** (`711c2865b`): the Judge label arms +
  `clr._declared`/`Labeled`/Mint-label-branch are gone. clr is host-objects-only now.
- `clr.Peek()=>self` and nested-Data-abolished landed in earlier sessions.

## ⚠️ FIX TESTS FIRST (the §C fallout — 7 red tests)

§C's delete breaks 7 tests that pin the OLD behavior: an **in-memory** `byte[]`
explicitly declared a media type reports that type (the clr label did it).
- **Types:** `Add_CustomType_LazyDerivationUsesEngineTypes`,
  `LiteralGifAsImageGifStrict_BuildsAndRunsClean`,
  `Run_StrictImageGifWithRuntimeVarResolvingToGif_Mints`
- **Data (DataTests.cs):** `Compress_NonCompressible_ReturnsSelf`,
  `Context_WhenSet_PropagesToType`, `Type_ExplicitType_NotOverridden`, `Type_Kind_WithContext`

They construct `new Data(byte[], Type.FromMime("image/jpeg"))` or `new Type("image/jpeg")`
and assert `Type.Name=="image/jpeg"` / `FamilyOf(Type.Name)=="image"`. Two things are
obsolete in them:
1. `new Type("image/jpeg")` / the **test-only** `Type.FromMime` (type/this.cs:315) keep
   the slash in the Name — Ingi: "type.Name is never with /". The real decomposer is
   `Format.TypeFromMime` → `{image-major→binary now, kind}`.
2. The clr label that made a `byte[]`-declared-image report `image` is gone.

**THE MODEL FORK to settle with Ingi before rewriting these** (same fork from the
start of the flip): an **in-memory** `byte[]` explicitly declared a media type
(`image/jpeg`) — does it (a) **narrow to the media item** (image.@this — the image
reader takes bytes), or (b) **stay `binary/kind`** like I/O content and narrow only on
`Value()`? I/O content is settled (b). In-memory explicit construction is the open
half. Once decided, rewrite the 7 to match (and stop using slash-name `Type`).

Strict-image (`LiteralGifAsImageGifStrict` etc.) is a related but distinct path —
those go through `set ... as image/gif, strict` (Build, with context), which validates
and elevates; check whether §C touched that or only the no-context Judge fallback.

## Remaining branch work (after the 7 tests)

From `host-carrier-spec.md`, the clr-closure cleanup still open:
- **§E raw-`Peek()` consumers** (~10 files: `data/ShouldExit.cs`, `condition/code/Default.cs`,
  `debug/this.cs`, `goal/Methods.cs`, `data/this.Diff.cs`, `actor/permission/this.cs`,
  `builder/code/Default.cs`, etc.) still do `Peek() is X` / `.GetType()` reflection —
  migrate to plang types / `.Type`. Some need per-class convert-to-item sign-off
  ([[feedback_confirm_class_to_item]]).
- **Reflect-write** + its actor-permission gate — deliberately deferred (`host-carrier-spec.md`).

## Open semantics / cosmetics (from `mime-binary-lazy-plan.md`)
- **invalid-json on access = ERROR** (Ingi's call) — that's already what `Value()` does
  (source surfaces `MaterializeFailed`). No code change; just don't pin a text fallback.
- **text/html kind is `htm`** (shortest-extension rule) not `html` — functional (narrows
  to code), reads oddly in `%x!type%`. Cosmetic; change the tie-break if it bothers.

## Workflow (do not relearn the hard way)
- Build the slice: `./dev.sh build` (analyzers off, ~1-20s). Test a suite:
  `PLang.Tests/<Suite>/bin/Debug/net10.0/PLang.Tests.<Suite> --timeout 40s` then
  `grep '^failed '`. Single test: `--treenode-filter "/*/*/*/<Name>"`.
- **Baseline `test-baseline.txt` is INCOMPLETE** — it missed `ThrowTimeSnapshot`
  (which failed at baseline). Don't trust it as the full pre-existing set; diff
  current-vs-baseline only flags *additions*, and some additions are pre-existing
  capture gaps or env-flaky (`Diff_DiffModeOverLargeListDoesNotOom` passes in isolation,
  fails under full-suite load). Verify a suspicious "new" failure in ISOLATION before
  treating it as a regression.
- LSP diagnostics for test files (TUnit `Test`/`Assert` "not found") are noise — the
  csharp-ls doesn't see the test framework; the real compiler (`./dev.sh build`) is truth.
- Never stash-to-baseline; never `rm Fixtures/pr/.db`; never delete `.build` folders.
- Probes: write a real `[Test]` file under the suite, run it, then `rm` it (don't let
  `git add -A` commit it — happened once).
