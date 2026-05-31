# Code Analyzer — v2 — `type-kind-strict`

**Verdict: PASS** → next bot: **tester**. HEAD `fd7ee4812` (coder v8).

Re-review of coder v8's response to the v1 FAIL. All five findings addressed;
the blocking F1 fix is correct and — critically — **proven non-vacuous by a
mutation test** (the prior round's failure mode was tests that passed while
enforcing nothing).

**Build:** clean from clean (bin/obj wiped) → 0 errors. Warnings are
pre-existing style only (CS8981 lowercase type names `image`/`hash` in test
files; TUnit0023 disposal nags) — no new classes, no PLNG001/002.

**Tests (rebuilt-clean binary):** C# **3815/3815**, PLang **262/262** (0 fail, 0
stale). Counts dropped from v7 (3818 / 263) exactly as the plan states: deleted
`TextBuildHookTests` (F4), deleted `SetAsImageGifStrictRuntimeVarMismatch.test.goal`
(see below), added two `Cut2` read-lift tests + one `LazyPathHandle` throw test.

---

## F1 (was MAJOR/blocking) — FIXED, and the fix is verified

The strict `(kind)` requirement now rides **with the value** via
`app.data.IStrictKindEnforcer` (sibling to `IBooleanResolvable` — discipline on
the value, not the `set` call site). Traced all three paths:

- **Read-lift / already-loaded `image.@this`** (`set %img% = %u% as image/gif strict`):
  `variable/set.cs` imprints `RequireStrictKind("gif")` then `CheckStrictKind()`
  immediately — bytes are in hand → sniffs → **fails at the set** with a typed
  `StrictKindMismatch`. Covered by `Cut2.ReadLiftImagePngAsImageGifStrict_FailsAtSet`
  (constructs a real bytes-backed PNG `image.@this`, the realistic lift shape).
- **Lazy path-backed** (`set %x% = "photo.png" as image/gif strict`):
  `CheckStrictKind()` returns `null` (bytes not loaded) → set stays clean →
  `image.BytesAsync()` enforces at byte-materialization and throws
  `StrictKindMismatchException`. Covered by
  `LazyPathHandleTests.BytesAsync_StrictKindMismatch_ThrowsAtLoad_NotAtConstruction`.
- **Raw `byte[]`** — unchanged, still handled by the pre-existing
  `IKindValidatable` probe block; the new `IStrictKindEnforcer` block is skipped
  (a `byte[]` value isn't an enforcer). No double-validation: the
  instance/path arm and the byte[] arm are mutually exclusive on the value's
  shape. Covered by the original `Cut2` byte[] tests.

`image.ValidateKind` was widened to read a loaded `image.@this`'s own bytes
(`@this img => img.Bytes`), not only raw `byte[]` — closing the "probe's empty
Bytes" hole from v1.

**Imprint survives storage.** `_requiredKind` is in-memory only, so I checked
`Variables.Set`: it aliases the `Data` **by reference, no clone**
(`variable/list/this.cs:111`), so the stored value is the same `image.@this`
instance that carries the imprint — a later step's `BytesAsync` still enforces.
Verified by code-read, not just assumed.

**Mutation-verified the tests actually guard it.** I temporarily forced
`image.CheckStrictKind()` to return `(true, null)` (defeat enforcement);
`ReadLiftImagePngAsImageGifStrict_FailsAtSet` **and**
`BytesAsync_StrictKindMismatch_ThrowsAtLoad` both flipped to **failed** (3813/3815),
then passed again on revert. These are real guards, not the v1 assertion-free
goals. `git status` clean — nothing committed.

### On the deleted PLang goal

`SetAsImageGifStrictRuntimeVarMismatch.test.goal` was deleted. The plan's stated
reason ("a read-lift through a variable comes back path-backed, defers") is
**imprecise** — `file/read.cs:40` lifts to a *bytes-backed* `image.@this`, so a
read-lifted variable would actually fail at the *set*, not defer. But the real
justification holds: a 2-step `read … into %u%` + `set … as image/gif strict`
goal is exposed to LLM step-count non-determinism at build, and the enforcement
point (`variable.set` Run with a loaded image instance) is now covered
deterministically in C# by `ReadLiftImagePngAsImageGifStrict_FailsAtSet`.
Deleting a flaky end-to-end goal in favour of a deterministic unit is the right
call; the muddled prose doesn't change the outcome. The rewritten
`SetAsImageGifStrictMismatch.test.goal` now has **real assertions**
(`Type.Name == image`, `Type.Kind == gif`) and correctly documents the lazy
contract (set clean, throw deferred to load).

---

## F2–F5 — all fixed cleanly

- **F2** — `Data.Kind` is now `[JsonIgnore]` (was `[JsonPropertyName("kind")]
  [Out, Store]`). Kind rides the wire only inside the `type` entity; the
  duplicate flat sibling is gone. No loss: `Data.Kind` getter is `_type?.Kind`,
  and the `type` entity serializes `kind` via its own converter (and is `[Out,
  Store]` on `Data.Type`), so sqlite/wire round-trips keep the kind.
- **F3** — `type.@this.Scheme` → `Context?.App.Type.Scheme`. NRE closed.
- **F4** — `text/this.Build.cs` deleted; the `!= "text"` string special-case in
  `set.cs` removed. `text` has no kind hook now, so `KindHooks.Of(textClr, …)`
  returns null for any caller — the rule lives in the type's absence of a hook,
  not in call-site knowledge. `SetAsTextWithMdExtension`/`Uppercase` still assert
  kind-null and still pass.
- **F5** — dead `CanonicaliseKind` fast-path removed; `BuilderNames` initialises
  inline (wrapper gone).

---

## Residuals (minor, non-blocking — for the tester to weigh, not gate)

1. **Sync content reads don't enforce strict.** `Width`/`Height`/`Bytes` on an
   *unloaded* lazy strict image return 0/empty without triggering the load, so
   the strict throw only fires through the async `BytesAsync` seam. Consistent
   with the lazy model (no content materialised → nothing to check, and you got
   empty content anyway), but worth knowing: enforcement is bound to the async
   load, not to every content touch.
2. **The full lazy chain isn't end-to-end tested.** Throw-at-load is unit-tested
   directly on `image`; storage-survival of the imprint is established by
   `Variables.Set`'s by-reference aliasing (code-read). No single test walks
   set→store→retrieve→load→throw. Low risk given the aliasing, but a tester
   might add one PLang/integration test that forces the load (e.g. read
   `%img.Width%` after a strict set on a mismatched fixture) to lock the chain.
3. **Imprint is process-local.** `_requiredKind` is not serialized, so a lazy
   strict image serialised to wire/sqlite and rehydrated *before* first byte
   access loses the requirement. Edge case (strict on a not-yet-loaded image
   that crosses a process boundary); acceptable under the in-memory enforcement
   model, but the strict guarantee doesn't survive a cross-process round-trip.

None of these block. The blocking defect is fixed, the fix is proven, and the
clean items from v1 (signing/hash, prompt scoping, the entity fold) are
untouched.

## Verification notes
- Suite counts + mutation result from the rebuilt-clean binary (bin/obj wiped,
  `dotnet build PlangConsole`), not a stale `plang.exe`.
- F1 paths traced through `variable/set.cs` + `image/this.cs` + `Variables.Set`
  source, then confirmed live by the deletion-mutation (forced-pass →
  two tests fail → revert → pass).
