# Auditor v1 result — `data-serialize-cleanup`

**Date:** 2026-05-28
**Branch:** `data-serialize-cleanup`
**Range reviewed:** `b74bd6ce8..HEAD` (coder range — the 10 commits after the runtime2 merge).

## Summary

Three reviewers said PASS (codeanalyzer v2, tester v2, security v2). I walked the seams between them — the cross-file canonicalization story (`WireJsonConverter` ↔ `crypto/Default.cs` ↔ `plang.@this.OutboundOptions` ↔ `ContextLessFallback`), the `!` operator from parser through write-side gate, the Properties insertion / wire / navigation triangle, the `LiftDataIfShaped` heuristic, the depth bomb. The story holds.

**Verdict: PASS.** Two minor findings that endorse what the tester already flagged; no new critical or major.

## Previous reviews assessed

- **codeanalyzer v2** — *agree*. Each of F1–F11 lands at the cited line numbers; the ref-counted `Dictionary<@this, int>` swap (F2) composes correctly under nested same-Data hash; the `as`-with-fallback in crypto.Hash (F4) now returns a typed `SerializerMismatch` 500 when a non-canonical `application/plang` is registered; the `Stream.Length` guards (F1) cover all four call sites. The F9 (`EnsureSupportedValue` non-recursive) and F11 (`Unwrap` Context-mutation) deferrals are documented in code, not just in the report.

- **tester v2** — *agree*. The mutation-test claim (`OutboundOptions` → `JsonSerializerOptions.Default` makes three canonicalization tests fail) is the right load-bearing check for Stage 2. Their two weak-assertion notes (F1: `%archived!Type%` and F2: stale "pre-Stage-4" wire-shape comments) are real and worth fixing during a doc pass.

- **security v2** — *agree, including the v1 retraction*. Re-traced the depth bomb path: `LiftDataIfShaped` calls `JsonSerializer.Deserialize<@this>(element.GetRawText(), options)` — but `element` came from `JsonDocument.ParseValue(ref reader)` on the *parent* reader, which inherits `options.MaxDepth=64`. The sub-tree it can hand to the recursive call is structurally capped at MaxDepth deep, so the recursion ladder is bounded by `MaxDepth × constant`, not by payload depth. The `AsyncLocal<int> _readDepth` counter at `WireJsonConverter.cs:93–112` is correct defense-in-depth (covers a future caller that raises `options.MaxDepth`).

## Cross-file walks performed

### Canonicalization four-file chain

`crypto/Default.cs:48` resolves the registered `application/plang` serializer (or `ContextLessFallback`), calls `MarkOuterForHash(data)`, then `JsonSerializer.SerializeToUtf8Bytes(data, serializer.OutboundOptions)`. `OutboundOptions` (`plang/this.cs:92`) is the same options bag the `SerializeAsync` egress path uses → hash bytes ≡ wire bytes minus the outer Signature (suppressed by `IsHashOuter(data)` in `WireJsonConverter.Write`). Inner Datas still go through full sign-if-missing + four-field shape, so the outer signature transitively binds them. Holds.

`ContextLessFallback = new @this()` (`plang/this.cs:101`) — checked acyclic construction: parameterless ctor builds `_outbound`/`_inbound` with `WireJsonConverter` + `path.JsonConverter()` + `Transport.ForOutbound/Inbound` modifiers. None of these reach back into `plang.@this` statics. No cyclic-static-initialiser hazard.

### `!` operator end-to-end

`Variable.Resolve` (`Variable.cs:60-130`) flags four shapes as `IsMalformed`: `%x!!cost%` (multi-bang in head), `%x!a!b%` (multi-bang in head via tail), `%x.y!cost%` (bang after dot/bracket), `%!x!cost%` (negation + property). `variable/set.cs:53` checks `IsMalformed` first and returns `InvalidVariableReference` 400 before any property write. The catch at `set.cs:75` narrows to `ArgumentException` — matches the only thing `Properties.EnsureSupportedValue` throws. Composes.

### Properties / wire / navigation triangle

- Insertion gate (`Properties.cs:117`) rejects raw `Data` instances; structural pass on `IDictionary`/`IEnumerable`. F9/security-F3 already cover the asymmetric-round-trip risk for `List<Data>` smuggled through a container; docs note it.
- Wire write (`WireJsonConverter.cs:297-307`) emits `properties` only when `Count > 0`; each value goes through the same options bag, so a `Data` inside a container would fire sign-if-missing on egress but `ReadPropertyPrimitive` (`:203-236`) parses only primitive/list/dict shapes on ingress — never invokes `LiftDataIfShaped`. The signed inner attestation rides on the bytes but is unreachable post-deserialise. Architect/security accepted; producers carry the discipline.
- `LiftDataIfShaped` heuristic (`:244-260`) fires only inside a Data's `value` slot, not inside `properties`. A user dict with both `"name"` and `"value"` keys placed in `Data.Value` *would* lift back as a Data on round-trip — but this is the by-design heuristic the converter doc calls out. The `properties` scope is immune.

### Foundation ripple

`Data` ↔ `Properties` is foundation; the changes here are additive (new wire scope, new insertion gate) rather than re-shaping existing call sites. Walked the consumers: `condition.if.branchIndex`, `test.report.summaryFail`, and other metadata writes all still ride through `Properties[...] =` and continue to round-trip as primitives (the legacy shape Stage 4 preserved). No latent breakage.

## Findings

### F1 (minor) — `%archived!Type%` test passes via reflection fallback, not the Properties scope (endorses tester F1)

**File:** `Tests/Serialization/CompressRoundTrip.test.goal:6`
**Status:** Tester flagged; I'm confirming as the right call.

`compress` sets `Data.Type = type.FromName("archived")` (`this.Transport.cs:127`), not `Properties["Type"]`. `this.Navigation.cs:318` makes `!key` fall through to `GetProperty(key)` on the Data infrastructure when the Properties bag misses — so `%archived!Type%` reads `Data.Type` via reflection and the assertion passes for the wrong reason. The test reads like it's exercising the Stage 4 Properties scope but is actually exercising the fallback path.

Two clean ways to tighten:
- Use `%archived.Type.Value%` (explicit Data infrastructure) — keeps the assertion's intent obvious.
- Or add a real Properties scope assertion on a separate Compress test, leaving this one alone.

Not blocking — round-trip semantics ARE correct; the assertion just doesn't gate the path the file name suggests.

### F2 (info) — `crypto/hash.cs:18` declares `async` with no `await`

**File:** `PLang/app/modules/crypto/hash.cs:18`
```csharp
public async Task<data.@this<byte[]>> Run() => Crypto.Hash(this);
```

The `async` keyword wraps a synchronous return into a `Task<T>` — semantically fine, but the compiler should be emitting CS1998 ("async method lacks await"). I don't see it in the current build output (likely suppressed at project level). Dropping `async` to `public Task<data.@this<byte[]>> Run() => Task.FromResult(Crypto.Hash(this));` is a one-line cleanup. Cosmetic, not blocking.

The deeper question — *why* is `Crypto.Hash` sync at all when sign-if-missing inside `JsonSerializer.SerializeToUtf8Bytes` is going to do `GetAwaiter().GetResult()` anyway? — is the same sync-over-async F3 carve-out codeanalyzer documented. The right fix is a pre-walk async pass through the Data graph that calls `EnsureSigned` on every reachable Data before the sync serialise. Outside this branch's scope; tracked.

## Process note

`.bot/data-serialize-cleanup/coder/` doesn't exist — no `summary.md`, no `v<N>/plan.md`, no coder session in `report.json`. Tester already flagged this. Not for me to escalate further; the work landed correctly and the other bots' reports cover the same ground. Surfacing once for the docs/marketing handoff in case it matters.

## Verdict

**PASS.** Branch is merge-ready. Findings above are post-merge polish, not blockers.
