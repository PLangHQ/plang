# codeanalyzer — type-kind-strict

## v3 — **PASS** → next: **tester**. HEAD `f971f98e6` (merged + coder v13).

Re-review of the merged `type-kind-strict` + `lazy-deserialize` state (Ingi's
"branch done" call). Scope = integration seams only; the two feature bodies were
each already PASSed (v2 here for type-kind-strict at `fd7ee4812`; lazy-deserialize
on its own branch). Clean rebuild: **PLang 273/273, C# 4025/0/0**, no PLNG001/002,
no System.IO/Console in changed prod files.

**Core finding — strict-kind × lazy passthrough is CLEAN (traced).** lazy added a
`RawUntouched` verbatim-passthrough early-return in `variable/set.cs` (203–209)
*before* the `IStrictKindEnforcer` stamp (264). It cannot leak a strict mismatch:
for `IKindValidatable` types (image, the only strict family) the run-time probe
at `set.cs:184` reads `Value.Value`, which materializes `_value` and so flips
`RawUntouched` to false → strict images always reach the enforcer stamp;
path-backed images get `RequireStrictKind` imprinted and enforce at
`image.BytesAsync`. The passthrough only catches non-strict raw types. Signature
carry and MaterializeFailed-on-set-path are covered by coder v9–v13's new tests
(test-only changes, real assertions, mutation-verified).

One non-blocking finding: F1 — pervasive `Stage N`/provenance comments
(`set.cs:27,72` + ~18 across `type/**`,`data/**`). Systemic house style accepted
by all prior passes; docs/architect cleanup, not a coder blocker. Verdict unaffected.

---

## v2 — **PASS** → next: **tester**. HEAD `fd7ee4812` (coder v8).

Coder v8 fixed all five v1 findings. F1 (blocking) is correct and **mutation-
verified**: strict `(kind)` rides with the value via `IStrictKindEnforcer` — a
read-lifted/already-loaded image fails at the `set`; a lazy path-backed image
throws at `BytesAsync` byte-load (Ingi's ruling); `ValidateKind` now reads a
loaded `image.@this`, not only `byte[]`; the imprint survives storage
(`Variables.Set` aliases by reference). Forcing `CheckStrictKind` to always-pass
flips both new tests to failed (3813/3815), passing again on revert — they
genuinely guard, unlike the v1 assertion-free goals. F2–F5 clean. Build clean,
C# 3815/3815, PLang 262/262. Three minor non-blocking residuals noted in
`v2/report.md` (sync reads don't enforce; no full end-to-end lazy-chain test;
imprint is process-local) — for the tester to weigh.

---

## v1 — FAIL → coder (F1 blocking). HEAD `502d43d0e`.

Build clean (0 err, no PLNG001/002, no System.IO/Console in changed prod files).
C# 3818/3818, PLang 263/263 — both green on a rebuilt-clean binary.

## Findings

- **F1 (MAJOR, blocking)** — `as image strict` enforces nothing for the two
  realistic value shapes: a path literal (`"real.gif"`) and a `read`-lifted
  `image.@this`. `TryInstantiateValidator` only builds a probe from raw `byte[]`,
  and `image.ValidateKind` only reads `byte[]`; a string/image-instance matches
  no image ctor → probe null → strict skipped at build *and* runtime. The C#
  `Cut2` tests pass only because they feed raw bytes (the one working path); the
  three PLang `.test.goal` files that claim to cover the mismatch cases pass
  while asserting nothing — a PNG is silently accepted as a strict GIF.
  **Ingi ruled (2026-05-31): validate at byte-load, wherever that is — if strict,
  throw.** So `set` stays lazy (the path-literal "fail-at-build" sub-claim is
  withdrawn), but nothing checks at byte-load today. Fix: the declared
  `(kind, strict)` must ride with the image value to the load seam (`BytesAsync`,
  or immediately at `set` for an already-loaded read-lift / raw bytes);
  `ValidateKind` must accept an `image.@this`, not only `byte[]`; throw
  `StrictKindMismatch` at first content access; give the mismatch goals real
  assertions (the `...Mismatch` goal's "fail at build" comment is now wrong under
  the lazy ruling — rewrite or delete).

- **F2 (MEDIUM)** — `kind` written twice on the wire/`.pr`: inside the `type`
  entity and as the flat `Data.Kind` sibling (`[JsonPropertyName("kind")]` kept
  after the fold). `Wire.ReadBody` no longer reads the sibling → write-only
  redundancy + order-fragility. OBP smell #6, branch-introduced.

- **F3 (MINOR)** — `type.@this.Scheme` does `Context.App…` without the `?.` every
  sibling accessor uses → latent NRE for a Context-less `path` entity.

- **F4 (MINOR)** — "text never derives kind from spelling" is gated by a string
  name-check in `set.cs`, not on `text`; `text.Build` still registers a
  spelling-kind hook (OBP smell #5).

- **F5 (NIT)** — `CanonicaliseKind` dead fast-path; `BuildBuilderNames()`
  redundant wrapper.

## Clean (verified, won't re-litigate)
Signing/hash relocation preserves invariants (digest in `ToSigningBytes`, sig is
the real boundary; `FixedTimeEquals` on header compare); LLM prompt scoping to
Fundamentals correct; text/number/int-literal kind behavior coherent + tested;
the `{Name,Kind,Strict}` fold is genuine single-owner with a loud `Promote()`
guard.
