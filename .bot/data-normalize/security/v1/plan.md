# security v1 — data-normalize

## Scope

Branch `data-normalize` lands structural normalization on `Data` so transport
becomes format-agnostic. New / changed attack surface:

- `Data.Normalize()` — reflection-at-boundary, recursion, cycle, depth (new file).
- `Data.Reconstruct<T>()` / `Walk` — tree walker (As<T> reverse direction, new file).
- `Wire.cs` (renamed from `WireJsonConverter.cs`) — gained `View` per-instance,
  unchanged read-depth cap from the previous branch.
- `app.channels.serializers.json.Writer` (new IWriter impl) — fails closed on
  unknown leaf types so unfiltered STJ reflection never re-enters.
- `app.channels.serializers.filters.Tagged` — per-(type,mode) wire-property
  filter; transparent fallback for untagged types.
- `[Masked]` attribute (new), `RawSignature` field deletion (canonicalised
  on `Signature`).
- Filtered domain types: `Identity`, `setting`, `Variable`, `path.@this`,
  `http.Response`.

Baselines from prior bots: codeanalyzer v3 PASS (all M / P / V findings closed),
tester v4 PASS (C# 3381/3381, PLang 233/233, BuilderSanity deterministic).
RawSignature deletion has its own reflection + string-scan regression suite.

## Approach

1. Semgrep baseline scan (15 known INFO hits; verify no new file enters the list).
2. Read `this.Normalize.cs` end-to-end — depth cap, cycle set, leaf table,
   property-bag dispatch, Masked-never-invokes-getter, getter-throws wrapping.
3. Read `this.Reconstruct.cs` end-to-end — leaf-shape skip, dictionary key
   conversion, positional-ctor path, FromNormalized hook, path Resolve hook
   (relies on AuthGate canonicalisation closed in `purge-systemio-from-actions`).
4. Read `Wire.cs` — confirm `MaxReadDepth` cap from prior branch still applied
   through `LiftDataIfShaped` round-trip; sign-if-missing gate; hash-outer
   carve-out; `Properties` read restricted to primitives only.
5. Read `json.Writer.cs` — view threaded through constructor (V1 from
   codeanalyzer closed), fail-closed default on unknown leaf type.
6. Read `Tagged.cs` — transparent-fallback semantics, Sensitive / Masked /
   JsonIgnore precedence per mode.
7. Read attribute migrations on the inventoried domain types and the
   `Sqlite.Set` → Store-view switch.

## Threat model relative to PLang's trust boundary

- Wire input is the only untrusted surface (post-signing-verify is trusted).
- Pre-auth deserialization at `Wire.Read` is the choke point — already
  depth-capped at 64 by prior branch.
- Reconstruct's `Walk` reflects on caller-supplied `T`, never on a wire-
  controlled type name → no polymorphic type-confusion vector.
- Outbound view (`Out`) defines what crosses the boundary; `Tagged.Compute`
  is the gate.

## Findings (preview)

Open: none critical/high. See `result.md` for full write-up.
