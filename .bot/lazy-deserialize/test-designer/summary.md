# test-designer — lazy-deserialize — summary

## v1 — PASS

- **33 C# TUnit files / 167 tests** under `PLang.Tests/App/LazyDeserialize/`, organised by stage:
  - `ReaderRegistryTests/` (Stage 1) — registry shape, per-type Read entries, converter deletions, distributed OwnerOf, parity-with-incumbents, failure path, residual TryConvert, snapshot carve-out.
  - `NumberTowerTests/` (Stage 2) — storage, kind derivation, Read parsing, arithmetic (promote-then-narrow + double⊕decimal error), per-family CLR declarations.
  - `LazyDataTests/` (Stage 3) — `_raw` shape, materialisation rules, mutation-invalidates-raw, raw-type discipline, Wire.Read lazy, materialise-error path.
  - `OneBoundaryTests/` (Stage 4) — `channel.read` as the boundary, kind layout under `channel/type/`, file channel, http channel, Format MIME remap.
  - `AccessResolutionTests/` (Stage 5) — scalar/navigation/as-cast/property access, no-content-sniffing negatives.
  - `IntegrationCutsTests/` — Cut1 verbatim passthrough, Cut2 touch materialises, Cut3 sign→wire→verify, Cut4 http body lazy / metadata eager, Cut5 number tower round-trip.

- **9 PLang `.test.goal` files** under `Tests/LazyDeserialize/`, covering the developer surface: read-config / navigate, http body lazy + status eager, big-integer no-uint-wrap sum, double+decimal error, type-unknown nav error + `as <type>` fix, sign+verify + tamper-fails.

- **20 independent additions** beyond the architect's matrix, including: registry shape-equivalence with renderer, `_raw` is private (verbatim passthrough invariant), `_raw` survives the courier path, mutation-then-serialize uses renderer, `LiftDataIfShaped` deletion + behaviour-gone two-prong, `http.response` deletion-by-absolute-name + Run-return-type, octet-stream `_raw` is `byte[]`, format-from-extension equals format-from-MIME, error-names-the-source, type-unknown error contains literal `as <type>`. Each enumerated in `v1/plan.md`.

- **Build**: `dotnet build PLang.Tests` → 0 errors, 175 warnings (no new warnings introduced by this contract; the increase from the type-kind-strict baseline of 347 reflects branch-level changes elsewhere on `lazy-deserialize`, not these test files).

- **Open items** for the coder, surfaced in `v1/plan.md`:
  1. `_raw` is private (vs protected).
  2. Type-unknown error wording — exact literal pinned, flip if a different phrasing wins.
  3. Narrowing-only-when-value-fits — pinned conservative; flip if aggressive narrowing wins.
  4. Cut 1's reader-probe-count needs a debug-only counter on `reader.@this`.
  5. `DoublePlusDecimal_Errors.test.goal` assumes `as decimal` modifier exists; demote to C#-only if not.

Stage-ordering note: the architect's "Stage 1 — no behavior change" rule means Batch 1's `TypeOwnedReadParityTests.cs` is the floor. The existing PLang `--test` suite + C# regression suites cover the broader floor; this contract adds the new pins on top.
