# security v1 — data-normalize — result

**Verdict: PASS.** No critical or high findings open. Two notes recorded
below (one design-choice footgun, one process-baseline match).

## Phase 1 — Blue team (defensive audit)

### `Data.Normalize` (`PLang/app/data/this.Normalize.cs`)

| Concern | Status |
|---|---|
| Depth cap | `MaxNormalizeDepth = 128`, raises `NormalizeException` past it. |
| Cycle detection | `HashSet<object>` keyed by `ReferenceEqualityComparer`, raises `CycleError`. |
| Leaf table | Aligned with `IsTreeLeafType`; primitives, BCL structs (`DateTime`/`DateTimeOffset`/`TimeSpan`/`Guid`), enums (by name), `byte[]`. |
| Delegate handling | Emitted as `null` leaf — avoids reflection-only members on `Method`/`Target` walks (StackOverflow surface defused at the leaf, not the recursion). |
| Nested `Data` | Observation-only — `Normalize` constructs a fresh `Data` rather than mutating the source, so signed inner Datas keep their original property identity. |
| Property-bag fan-out | Delegates the per-(type,mode) filter to `Tagged.PropertiesFor` (cached). Getter throws are wrapped as `NormalizeGetterThrew`. |
| `[Masked]` path | Never invokes the getter — value never crosses the boundary even via exception. |

Verdict: clean. The depth + cycle guards close the standing "recursive
methods need depth limits" rule; getter-throw wrapping avoids leaking the
caller's stack through an unrelated exception type.

### `Data.Reconstruct<T>` (`PLang/app/data/this.Reconstruct.cs`)

| Concern | Status |
|---|---|
| Walk recursion bound | Inherited from `Wire.Read.MaxReadDepth = 64` for wire-arriving trees. In-process callers can hand a deeper `Value` directly, but the trust boundary doesn't span there. |
| Type-confusion via wire | `targetType` always derives from the caller's generic `T`. Wire payloads can't redirect Walk into a different CLR type. |
| `FromNormalized` hook dispatch | Lookup is on `targetType`'s own metadata, not a wire-supplied name. Per-type cache. |
| Path hook | Reads child named `"relative"` (case-insensitive); calls `path.@this.Resolve(relative, ctx)`. AuthGate canonicalisation already closed in `purge-systemio-from-actions/v2` ([[pattern_authgate_canonicalization]]). |
| Dictionary key | `AppTypes.ConvertTo` failure raises `NormalizeException` rather than silent null insertion. |
| Caches | `_hookCache` / `_settablePropsCache` keyed by `System.Type` — process-wide, type metadata only, no actor-scoped data crosses. |

Verdict: clean for the wire boundary. Walk itself has no internal depth
counter — relying on `Wire.Read`'s 64-deep cap for untrusted input is OK
today and matches the architect's "Wire is the choke point" model. If a
future inbound path bypasses `Wire.Read` (raw STJ on a `Data` graph) the
cap would need to migrate down.

### `Wire.cs`

- `MaxReadDepth = 64`, cumulative across `LiftDataIfShaped` round-trips
  via `AsyncLocal<int>` (the F1 fix from the `data-serialize-cleanup`
  branch is preserved through the rename).
- `Properties` deserialisation routes through `ReadPropertyPrimitive`,
  which only accepts JSON primitives, arrays of primitives, and
  primitive-only nested dicts. No domain-type deserialisation reachable
  through the `properties` sidecar → no inner Data injection vector via
  that surface.
- Sign-if-missing only fires when `data.Context?.Actor != null` — bare-
  context fixtures stay unsigned. Hash-outer carve-out is ref-counted on
  the per-instance map.
- `View` is per-instance; the plang serializer builds three separate
  options bags (`_outbound`, `_inbound`, `_store`) so the view never
  drifts at call sites.

### `json.Writer`

- View threaded through the constructor; `EndRecord` consults `_view`
  before re-entering `NormalizeValue` for `Properties` entries
  (codeanalyzer v3 V1 — closed + fixture).
- Default leaf case **throws `NormalizeException`** rather than falling
  back to STJ reflection. Prevents the "unknown CLR type bypasses
  `[Out]`/`[Sensitive]` filter via STJ default object path" leak by
  failing closed.

### `Tagged` (filter)

- Per-(type, mode) cache; reflection fires once per type per mode per
  process.
- View-mode matrix is explicit (Out / Store / Debug) and `[Sensitive]`
  policy is intentionally inverted under `Store` so `Identity.PrivateKey`
  round-trips to sqlite.
- `[Masked]` honours wire-shape semantics: the getter never runs in the
  masked path, the literal `"****"` is emitted.

Note recorded below ("Transparent fallback") — design-intentional but
worth a memory entry.

### Attribute migrations

- `Identity` — `Name`/`PublicKey` get `[Out, Store]`; `PrivateKey` stays
  `[Sensitive]` and gains `[Store]` so sqlite round-trips. Out-view bytes
  do not contain `PrivateKey` (covered by `CanonicalizationTests`).
- `setting.value` — `[Out, Masked, Store]`. On the Out wire `value` is
  the literal `"****"`; on Store the real value travels. Covered by
  `MaskedSettingOnWire.test.goal` + `MaskedAttributeTests`.
- `Variable.Name` — `[Out]`; `RawValue` / `WasPercentWrapped` do not
  ship in Out view. RawValue is the only place a `%password%` raw string
  could land; excluding it is the right default.
- `path.@this` — `Scheme` / `Relative` ship; `Absolute` does not.
  Reconstruction rebuilds via `path.Resolve` → AuthGate.
- `http.Response` — `Status` / `Headers` / `Body` ship; `Duration`
  doesn't. (`Duration` isn't sensitive — just not visible to receivers.)

### Sqlite migration

`Sqlite.Set` now uses `_serializer.Store(data)` (Store view); `Get` uses
`Load` (Store view inbound). Identity's `[Sensitive, Store] PrivateKey`
round-trips correctly. `Get`/`GetAll`/`Set` symmetric.

## Phase 2 — Red team

| Attack | Reach |
|---|---|
| Deeply-nested wire payload → StackOverflow during `Read` | Bounded at 64 (carried from prior branch). |
| Deeply-nested wire payload → StackOverflow during `Normalize` | Bounded at 128 (this branch). |
| Reference cycle through normalised tree | Detected by `ReferenceEqualityComparer` `HashSet` in `Normalize`. |
| Wire-driven type confusion via `Reconstruct<T>` | Not reachable — `T` is caller-controlled. |
| Reflection leak of untagged domain type's properties | Closed by `json.Writer` fail-closed default; opened in transparent-fallback if a type lacks all four tags (see note below). |
| `[Sensitive]` leak on `Store` path | Intentional — local persistence requires round-trip. |
| Path traversal via reconstructed `path` (`..`) | Closed in `purge-systemio-from-actions/v2`; AuthGate canonicalises before the lexical prefix check. |
| Properties sidecar carries hidden inner `Data` | Not reachable — `ReadPropertyPrimitive` is primitives-only. |
| Pre-auth deserialise → arbitrary code path / typed-deserialise abuse | Bounded by Wire's depth cap and the converter's fixed shape; the `signature` field deserialises into `app.modules.signing.Signature` (a record with three fields, no polymorphism). |

## Notes (not findings)

### N1 — `Tagged.Compute` transparent fallback

`PLang/app/channels/serializers/filters/Tagged.cs:48-71`

```csharp
bool typeIsTagAware = false;
foreach (var p in props) {
    if (p.IsDefined(typeof(OutAttribute), inherit: false)
        || p.IsDefined(typeof(StoreAttribute), inherit: false)
        || p.IsDefined(typeof(SensitiveAttribute), inherit: false)
        || p.IsDefined(typeof(MaskedAttribute), inherit: false)) {
        typeIsTagAware = true; break;
    }
}
```

A type that carries **none** of `[Out]/[Store]/[Sensitive]/[Masked]`
ships **every** public property in Out **and** Store mode. The architect
plan explicitly chose this so nested helper records (verb sub-records,
small DTOs) round-trip without per-property bookkeeping.

The trade-off: any future domain type added to the value graph that
holds a secret and forgets `[Sensitive]` leaks it. The standing
mitigation today is the inventoried list in
`OutAttributeInventoryTests` plus reviewer discipline.

Posture: **accepted-design**. Not a finding. Captured to memory as a
standing rule so the next branch that adds a domain type into a Data
value graph remembers to opt in.

### N2 — `BuilderPlannerFailed` plan dump uses default `JsonSerializer.Serialize`

`PLang/app/modules/builder/code/Default.cs:321`

The new error-message dump for builder planner failures round-trips the
LLM's `planValue` through `JsonSerializer.Serialize` with no options
object. Semgrep flags it under the `plang-serializer-default-options`
rule — same baseline rule the existing 14 sites already match.

`planValue` is a JSON-shaped LLM response (`JsonElement` / `JsonNode` /
anonymous record). No `path.@this`, no `[Sensitive]` properties, no
runtime-graph cycle is reachable from it. The risk profile matches the
baseline; the count stays at 15.

Posture: **baseline-consistent**, no separate finding.

## Cross-bot acknowledgements

- codeanalyzer v3 V1 (`json.Writer.EndRecord` hard-coded `View.Out`) —
  closed with the constructor-threaded `_view` + fixture
  (`CanonicalizationTests.cs:101-136`). Re-verified by reading.
- tester v4 PASS — relied on for baseline test counts and BuilderSanity
  determinism.

## Summary

The branch lands a clean, format-agnostic normalization layer. The two
recursive new files (`Normalize` and `Reconstruct.Walk`) both inherit a
bounded trust boundary: Normalize has its own 128-depth cap + cycle
detection; Reconstruct relies on `Wire.Read`'s 64-depth cap from the
prior branch (correct given the architect's "Wire is the choke point"
model). The `json.Writer` fail-closed default is the right posture for
the OBP-violation it forecloses (STJ reflection bypassing the tagged
filter). Attribute migrations are surgical — `Identity`, `setting`,
`Variable`, `path`, `http.Response` all carry the right inventory and
the sqlite path correctly switched to the `Store` view.

No critical or high findings open. Two design-aware notes recorded for
memory. Verdict PASS.
