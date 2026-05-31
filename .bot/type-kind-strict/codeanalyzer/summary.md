# codeanalyzer — type-kind-strict (v1)

**Verdict: FAIL** → next: **coder** (F1 blocking). HEAD `502d43d0e`.

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
  while asserting nothing — a PNG is silently accepted as a strict GIF. Root:
  unresolved Stage-9 (lazy path-handles, no I/O at set) ↔ Stage-4 (validate
  content) tension; no deferred `BytesAsync` check either. Coder picks the
  resolution (read-at-set / defer-to-load / reject-at-build) but must make
  `ValidateKind` accept path/instance shapes and give the goals real assertions.

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
