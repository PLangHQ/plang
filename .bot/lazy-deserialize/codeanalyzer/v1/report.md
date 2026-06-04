# codeanalyzer — lazy-deserialize — v1 report

**Commit reviewed:** `ead0caa83` (code); HEAD `19f4d8119` adds only the coder v3 report.
**Base:** `git merge-base runtime2 lazy-deserialize` = `d96ec269f`.

## Deterministic baseline (rebuilt clean this session)
- `dotnet build PlangConsole` — **0 errors, 0 PLNG002**.
- C# suite (`dotnet run --project PLang.Tests`) — **4021 / 4021 pass, 0 fail** (33s).
- Goal suite (`cd Tests && plang --test`) — **271 pass / 1 fail**. The one fail is
  `/LazyDeserialize/HttpStatusRead_DoesNotMaterialiseBody.test.goal`: it hits live
  `https://httpbin.org/json` and got a **503 Service Temporarily Unavailable** (captured in
  the run output). It **passed at 14485ms earlier in the same session** and failed at 574ms
  now — a network-flaky endpoint, not a code regression. See finding **R1**.

The coder's GREEN claim holds. No correctness regression found in the review.

## Mechanical bans — all clean across the changed production surface
- **System.IO** (File/Directory/FileInfo/Path.Combine/GetDirectoryName/GetFullPath) outside
  `type/path/**`: **0 hits**.
- **Console.\*** writes: **0 hits**.
- **OBP #9** (courier reaching into `.Value is/as/switch`): **0 hits** in changed files.

## Pass 1c — parked list honored
`Documentation/Runtime2/obp-cleanup.md` #1/#4 already track the `app.type.list.@this`
registry verb/`Get` accretion. The `Conversion.cs` changes touch that file but **reduce**
the surface (per-type `Convert` hooks via `OwnerOf`, public API narrowed to one `Convert`
door). Not re-raised. `ToBoolean()`'s per-numeric cases (`data/this.cs:1118`) pre-date this
branch (present on base) — not this branch's finding.

---

## What this feature does well (call-outs, not problems)
- **`Conversion.cs` is exemplary OBP.** The central target-type switch is gone; construction
  is asked of the owning family (`OwnerOf(clrTarget)` → `Conversions.Of(family, …)`), and the
  file holds *no per-type arms*. This is the textbook "root-cause fix" shape from Pass 4.5 —
  a type/invariant that makes the per-type branching unrepresentable.
- **The v3 substantive work is root-cause, not symptom.** SnapshotClone's default-options
  JSON round-trip was flattening nested `Data` (dropping Signature/Type, materializing lazily).
  The fix changes the *producer* (`variable.set`/`list.add` now `ShallowClone` instead of
  deep-JSON) and `MintTyped` was deleted — consumers unchanged. Diff smaller than the bug.
- **Reader registry** (`type/reader/this.cs`) is a clean mirror of the renderer — same
  discovery, same precedence ladder. Symmetric deserialize, as the proposal intended.

---

## Findings

### F1 — Medium — List elements are stored non-uniformly (raw vs `Data`) · OBP smell #5
`PLang/app/module/list/add.cs`

`list.add` now stores the **whole `Data`** as the element (`snapshot = Value.ShallowClone(...)`,
lines 43–48) — correct, that's what preserves Signature/Type through a collection. But the
list-*creation* path (when the target var doesn't yet hold a `List<object?>`) seeds the list
with the **raw** pre-existing value (lines 24–34): `list.Add(item)` / `new List { existing }`.

The result: a list can hold a mix of bare values and `Data` wrappers, and the "element might
be either" discipline is then enforced at **every consumer**:
- `PLang/app/variable/navigator/List.cs` `Element()` — `if (raw is @this inner) return inner;`
- `PLang/app/data/this.cs:510` `WrapItem()` — `item is @this data ? data : new @this(...)`

That is exactly OBP shape smell #5 (producer hands back two shapes; consumers each learn to
recognize them). It's currently green because both read sites defensively unwrap, and a list
built purely by `list.add` from empty is in fact uniform — the asymmetry only bites when you
`add` to a variable that held a scalar (element [0] stays raw) or when a future consumer
forgets the unwrap.

**Fix:** make list storage uniform — wrap the seeded pre-existing items as `Data` in the
creation path too (or, if raw storage is the intended contract, document it and drop the
`Data`-store in `add`). One shape, and the two consumer unwraps collapse.

**Directly related:** the coder's own flagged gap — *no test puts a signed `Data` through a
collection and verifies identity survives* ("sign → add %signed% to %list% → verify
%list[0]%"). That blind spot is what let the deep-clone corruption hide. This regression test
should be written (tester's domain) — it pins exactly the shape F1 is about.

### F2 — Low/Medium — `Materialise()` vs `Materialize()`: one-letter-apart method pair
`PLang/app/data/this.cs:281` (`private object? Materialize()`) and
`PLang/app/data/this.cs:314` (`internal void Materialise()`)

Two methods whose names differ only by British/American spelling, with **different**
behavior and signatures: `Materialize()` is the read-through that produces the value;
`Materialise()` is the in-place navigation seam that promotes a string `_value` to `_raw`
then forces a read. Callers (`this.Navigation.cs:270`, `variable/list/this.cs:278`) use the
British one. A future edit that types the wrong vowel compiles silently and does the wrong
thing. Rename the public seam to something intent-named (e.g. `ForceMaterialize()` /
`MaterializeInPlace()`) so the pair can't be confused.

### F3 — Low — Duplicated static-`Resolve` reflection block in the As<T> path
`PLang/app/data/this.cs:880–893` (`AsT_Impl`) and `:909–922` (`AsT_Convert`)

The ~14-line "look up `T.Resolve(string, context)`, invoke, wrap-or-error" block is
copy-pasted verbatim between the two methods. `AsT_Convert` is documented as "`AsT_Impl`
minus substitution," but the shared Resolve tail should be one private helper both call.
Pure extraction, no behavior change.

### F4 — Low — `variable.set` re-runs `CanonicaliseKind` to repair a lost-context round-trip
`PLang/app/module/variable/set.cs:136–140`

> `// The factory does this when a context is passed; the .pr round-trip loses the context,`
> `// so we run it again here.`

Pass 4.5 tell #8 (re-derive what upstream already knew). The `type` entity's canonical kind
(`markdown`→`md`, `jpeg`→`jpg`) is computed at the factory, then lost across `.pr`
serialization, so the consumer recomputes it. Symptom-level: the real producer is the
`type`-entity wire round-trip dropping the canonical form / Context. Minor (one site, cheap,
correct) — flag, don't block. If a *second* site starts re-canonicalising, fix the
round-trip instead.

### F5 — Low — `variable.set` `Run()` is ~190 lines with deep type-resolution branching
`PLang/app/module/variable/set.cs:81–283`

The binding site is inherently the most complex action, but `Run()` interleaves: malformed-name
guard, property-target path, AsDefault, the whole forced-`Type` block (kind canonicalise →
ClrType resolve → kind derive → **three** strict-kind validation sites at build/run/load-seam →
keepAsIs facet logic → byte-backed-family fallback → construct), then the shallow-bind tail.
A reader can't hold it in 30 seconds. Not wrong — but the forced-`Type` block (lines 130–272)
wants to be its own well-named method, and the three strict-kind checks (`ValidateBuild` 31–46,
`Run` 175–189, `IStrictKindEnforcer` 258–268) are worth a comment block naming *why* there are
three (they fire at genuinely different times — literal-at-build, %var%-at-run, bytes-at-load).
Simplification candidate, not a defect.

### R1 — Test hygiene — disable the flaky in-goal httpbin LazyDeserialize test
`Tests/LazyDeserialize/HttpStatusRead_DoesNotMaterialiseBody.test.goal`

This test depends on a live `httpbin.org` endpoint and flakes on 503/rate-limit — the exact
problem the parent branch already addressed ("tests: disable the 8 httpbin.org tests in-goal
(rate-limited / 503)"). The test's own comment says the body read "depends on the live
endpoint." It should be disabled in-goal like its 8 siblings (the C# `HttpChannelTests` already
covers the hard probe deterministically, per the test's comment). Leaving it live will
intermittently red the suite. Coder/tester call — not a code defect.

---

## Verdict: NEEDS WORK
No correctness regression; both suites are green (the one goal fail is a network 503, not
code). The blocker-level items are **F1** (list-element shape inconsistency introduced by this
branch's own fix — store `Data` uniformly, and add the signed-Data-in-collection regression
the coder already identified) and the latent **F2** naming footgun. F3–F5 are clean,
low-risk cleanups. Once F1/F2 are addressed (or F1 is consciously documented as the contract
+ the regression test added), this is a PASS.
