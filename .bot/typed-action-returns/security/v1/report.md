# Security Review v1 — typed-action-returns

**Branch base:** `origin/runtime2` (`e41d650ae`) → HEAD (`ead910d1a`)
**Diff:** ~190 files, ~11.5kLOC. Production C#: 132 files.
**Posture going in:** PASSed codeanalyzer v3 + tester v2 (mutation-validated).

## Phase 1 — Blue Team (attack surface walk)

| Area | What changed | Trust boundary | Existing mitigations | Gaps |
|---|---|---|---|---|
| **Stream/Json/Plang serializers** | Methods now return `Data<T>` carrying typed errors instead of raw `T?` + thrown exceptions | wire ingress (channels) + sqlite settings store | catch narrowed to `JsonException / NotSupportedException / IOException`; `application/plang` path still routes through `JsonSerializer.Deserialize<data.@this>` with `_transportInOptions` (signature inflow preserved) | catch list is exhaustive for STJ — `ArgumentException` from a malformed type-discriminator could in theory escape, but STJ wraps those as `JsonException` |
| **plang/Data wire envelope** | `FromEnvelope` unchanged: sets `Signature` from wire, defers verification | wire ingress | `EnsureSigned` on egress; `VerifyAsync` is the trust gate on the populated `Data` | unchanged from baseline (`Envelope.Signature` keyless-by-design is documented `// nosemgrep` and accepted via downstream verify) |
| **HTTP size cap / slow-loris** | Two duplicate impls of size-capped read collapsed into `ReadLimitedBytesAsync`; `ReadLimitedStringAsync` wraps it | external HTTP server | size cap (`MaxResponseSize`, default 100MB), throughput floor 1KB/s over 30s window, typed errors `ResponseTooLarge`/`SlowResponse` at the leaf, error body capped at 4KB | none — code duplication that previously concerned us is now closed (was on the standing audit list) |
| **HTTP body content-type dispatch** | New `Response { Status, Headers, Body, Duration }` record; body shape chosen by serializer registry then `TextFallback` | external HTTP server | size cap fires before deserialize; deserializer failures fall back to UTF-8 string; binary stays bytes | `Encoding.UTF8.GetString(bytes)` over unbounded-but-capped bytes — safe at the cap (100MB) |
| **Ask/`IExitsGoal` shape** | `Ask` now carries `Answer`; `ShouldExit` returns false when bound; `Ask.ToString()` emits Answer | end-user interactive reply | `[Sensitive]` not on `Answer`; routing meant to be through Output channel | codeanalyzer-v3 F7 already documented: `ToString()` leaks the answer; reviewers warned in xmldoc. Not a new finding. |
| **Build() compile-time hook** | New `IClass.Build()` invoked by builder.validate; source-gen `SetAction` stamps `__action/__app` so lazy property getters resolve | builder reads developer-authored .goal/.pr | `file/read.cs:Build()` does an `ExistsAsync()` probe; wrapped in catch-all (silent best-effort warning). Path resolves through normal AuthGate chain. | at build time the actor's AuthGate prompts are the safety net; Build() respects them by failing into the swallow-catch. No leak. |
| **Channel(name) no-op fallback** | New `Channels.Channel(name)` returns process-wide `__noop__` channel on miss instead of null | n/a (in-process) | scoped to specific call sites: builder warnings, advisory writes | **forward risk** (see F1) — if "audit"/"security" channels are ever introduced and a caller writes opportunistically through `Channel("audit")`, silent NoOp would swallow audit events |
| **Data.As(string typeName, ctx)** | New public materializer keyed by runtime-supplied type name | callable from any C# | `Types.Clr(typeName)` / `AppTypes.GetPrimitiveOrMime` look up registered types only; unknown name fails fast `UnknownType 400` | type-confusion if caller passes attacker-controlled `typeName` (low — caller must already have a `Data` reference, no boundary crossing today) |
| **path canonicalization** | `PathHelper.GetExtension` now trims leading dot; no behavioral change to root canonicalization | wire/CLI path strings | `FilePath` ctor still canonicalizes via `PathHelper.GetFullPath`; AuthGate posture unchanged | none — purge-systemio v2 `..` traversal fix still holds; regression suite at `PLang.Tests/App/Types/PathTests/DotDotTraversalRegressionTests.cs` intact |
| **Mock intercept (rename)** | Old `MockHandle` → `Mock.@this`; new `intercept.cs` is a re-author with regex-based pattern matching | test author (trusted) | test-time only; `Cacheable=false`; regex constructed from user pattern | regex compiled without timeout (`Regex.IsMatch` default), but test author authors both pattern and target → no external trust crossing |
| **Settings/Sqlite Deserialize wrapping** | Three `Deserialize<T>` callsites now unwrap `Data<T>` | local sqlite | `SanitizeTableName` whitelist on table-name interpolation (pre-existing, unchanged) | none |

### Semgrep
`scripts/semgrep-scan.sh` → **15 findings, 5 rules, 376 targets** — matches baseline (no regressions, no new exemptions).

---

## Phase 2 — Red Team (findings)

### F1 — `Channels.Channel(name)` no-op fallback is silent-drop by design (LOW — forward risk only)

**Vector.** Any caller writing to a named channel via the new convenience accessor `Channels.Channel(name)` and assuming the write was observed by a registered listener gets an unconditional `Data.Ok()` from a process-wide `__noop__` sink when the channel is missing. Distinct from `Resolve(name)` which returns null and forces the caller to handle the miss explicitly.

**Today's reachability.** Three call sites in this diff write through `Channel("builder")`:
- `file/read.cs:65` — missing-file BuildWarning
- (via Build() helpers in other actions, same pattern)
- Coder Build() implementations across Stages 1-4

All three are advisory build-time writes whose silent-drop semantics are the *intent* (Build() must work outside an active build). The XML doc on `Channels.Channel(name)` explicitly contrasts this with `Resolve`.

**Latent risk.** If a future "audit", "security", or "trace" channel is introduced and any caller uses the convenience accessor, missing the channel registration would silently swallow audit events. The fallback is the wrong default for security-relevant channels.

**Affected.** `PLang/app/channels/this.cs:106-115`, `PLang/app/channels/channel/noop/this.cs`.

**Proposed fix (forward-looking, do **not** require for merge).** When the next channel with security semantics lands, route its writers through `Resolve(name)` (null + explicit handling) — *not* `Channel(name)`. Add a comment to the `Channel(name)` xmldoc warning: "do not use for channels whose absence is a security signal — use Resolve and handle null."

**Status.** Open, accepted-risk for this branch. Flagged for the merge committee + memorialised below.

---

### F2 — `Data.As(string typeName)` opens runtime type-confusion if `typeName` is ever caller-controlled (INFO)

**Vector.** New public method `data.@this.As(string typeName, actor.context.@this? context = null)` resolves an arbitrary name through the type registry and coerces the value to the resulting CLR type. A caller that passes an attacker-influenced `typeName` could downcast a value into a wrong shape; downstream code assuming a specific PLang type would see a different runtime object.

**Today's reachability.** Only known consumer is `todos.md` "file.save cross-type coercion" — passing the destination extension. Destination is the user's own path. No wire ingress today.

**Mitigation already in place.**
- Unknown type name → fast-fail `UnknownType` (400) ServiceError, no nullref.
- Empty/whitespace name → fast-fail `InvalidTypeName`.
- `AppTypes.TryConvertTo` (string source) / `ConvertTo` (object source) — both bounded converters; no `Activator.CreateInstance` on arbitrary types, no `Convert.ChangeType` to attacker-named primitives without prior registry lookup.

**Affected.** `PLang/app/data/this.cs:478-501`.

**Proposed fix.** Inline doc on `Data.As(string)` already warns "Used at call sites where the caller knows the target shape at runtime." Strengthen to: "Caller MUST own the `typeName` value — never thread an externally-sourced string through here without validating against a closed allowlist."

**Status.** Open, info-only. No active vector.

---

### F3 — Channel.ReadAsT removed its outer catch-all; serializer cancellations/EOF now propagate (LOW)

**Vector.** Previously `channels/this.cs:ReadAsT<T>` wrapped the serializer call in a `try/catch (Exception ex) when (not NRE/OOM/SO)` that surfaced any failure as `Data.Error("ReadError")`. The new flow returns the serializer's `Data<T>` directly because serializers internally handle JSON/NotSupported/IOException. But the inner serializer catches do **not** include:

- `OperationCanceledException` from CT firing during `ReadAsync`
- `EndOfStreamException` from a truncated stream not reported as IOException

These now bubble up to the caller of `ReadAsT` as raw exceptions rather than typed `ReadError`.

**Affected.** `PLang/app/channels/this.cs:184-195`.

**Severity.** Low — these exceptions are recoverable at the action level (action `ExecuteAsync` has its own outer catch in the source-gen emission), so the user-facing error is still typed. The regression is one of *layered containment*, not bypass.

**Proposed fix.** Either restore the outer `try/catch` (preferred — defense in depth costs nothing here) **or** add `OperationCanceledException` to each serializer's exception-filter list. The XML doc on `ReadAsT` says "no extra try/catch needed because parse failures travel through Data.Error now" — which is true for parse failures but not for cancellation.

**Status.** Open, low. Not a merge blocker.

---

## Standing findings sweep (carry-overs from memory)

| Standing item | Touched this branch? | Status |
|---|---|---|
| `Variables.Snapshot()` not honoring `[Sensitive]` in test module | No | Still open. Test/run.cs only renamed types. |
| Image `ReadAllBytes` no size limit (OpenAI provider) | No (renamed Response.Body unwrap only) | Still open. |
| `MigrationEnvelope.Signature` keyless integrity hash | No | Still open. |
| `Channel.Stream.ReadAllBytesAsync` unbounded | No | Still open. |
| `callback.run` skips signing.verify when RawSignature null | No | Still open. |
| `Data.Clone()` shallow Properties | No | Still open. |
| AuthGate `..` traversal | No (PathHelper trim is leaf-only) | **CLOSED** (purge-systemio v2 `064724fda`); regression suite intact. |

No new opens against any of the above on this branch.

---

## Summary

Wide structural diff (typed action returns, serializer Data<T> wrapping, Mock rename, Schema relocation), no new critical/high. The HTTP size-cap consolidation (a previous *audit candidate* item) **closes** the duplicate-implementation concern by making `ReadLimitedBytesAsync` the single owner of size and slow-loris discipline — net improvement.

Three open items (F1 forward-risk on no-op channel, F2 info on `Data.As(string)`, F3 low containment regression on serializer exceptions outside the JSON/IO set). None of them block merge.

Semgrep baseline unchanged. AuthGate canonicalization fix from purge-systemio still holds. Build() hook respects existing AuthGate posture and swallows denials silently (no info-disclosure).

**Verdict: PASS.**
