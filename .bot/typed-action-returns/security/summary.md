# security — typed-action-returns

## Version
v1

## What this is
Security review of the `typed-action-returns` branch. The branch makes a wide
structural change: action handlers' `Run()` methods now return `Task<Data<T>>`
with concrete `T` where the catalog declares a return shape, serializers wrap
their results in `Data<T>` with typed errors, the Ask flow carries the resolved
answer through the same record, and a compile-time `IClass.Build()` hook lets
handlers stamp downstream type inference.

Reviewing for: serializer trust-boundary discipline, HTTP size-cap regressions
from the duplicate-impl consolidation, signing inflow preservation through the
plang transport, Build()-at-validate exposure, and the new `Channel(name)`
no-op fallback semantics.

## What was done
1. Read codeanalyzer v3 + tester v2 reports for prior context.
2. Ran `scripts/semgrep-scan.sh` — **15 findings, matches baseline** (no
   regressions, no new exemptions).
3. Diff-walked all security-relevant production C# (132 files); deeply read:
   - `serializers/serializer/{Json,Text,plang/Data,plang/this}.cs`
   - `modules/http/code/Default.cs` (size cap + slow-loris consolidation)
   - `modules/output/ask.cs`, `data/ShouldExit.cs`, `IExitsGoal.cs`
   - `modules/IClass.cs`, `Generators/Emission/Action/this.cs`,
     `modules/file/read.cs`, `modules/llm/query.cs`, `modules/http/HttpBuildHelpers.cs`
   - `channels/this.cs`, `channels/channel/noop/this.cs`,
     `channels/channel/message/this.cs`, `channels/channel/stream/this.cs`
   - `Utils/PathHelper.cs`, `types/path/this.Authorize.cs`, `types/path/file/this.Operations.cs`
   - `modules/settings/Sqlite.cs`, `modules/mock/intercept.cs`, `mock/Mock/this.cs`
   - `types/this.cs`, `types/Registry.cs`, `Attributes/PlangTypeAttribute.cs`
4. Swept all standing-memory open findings — none regress on this branch.
5. Wrote `security-report.json`, `v1/report.md`, `v1/verdict.json`.

**Three new opens, all low/info, none merge-blocking:**

- **F1** — `Channels.Channel(name)` no-op fallback is silent-drop by design.
  Forward risk: if an audit/security/trace channel is introduced and a caller
  uses the convenience accessor instead of `Resolve+null-handling`, missing
  the channel registration would silently swallow events.
- **F2** — `Data.As(string typeName)` is a new public materializer keyed by
  runtime-supplied type name. Info-only; no current caller passes untrusted
  input. Hardened xmldoc + closed allowlist would prevent future drift.
- **F3** — `Channels.ReadAsT<T>` removed its outer catch-all in favour of
  forwarding the serializer's `Data<T>`. Serializer filters cover
  `JsonException`/`NotSupportedException`/`IOException` but not
  `OperationCanceledException` or `EndOfStreamException`. Defense-in-depth
  regression, not a bypass.

**Net improvement:** The HTTP `ReadLimitedBytesAsync`/`ReadLimitedStringAsync`
duplicate-implementation concern (previously on the security audit list as
"code duplication = security debt") is now **closed** — one owner of size cap
and slow-loris discipline.

## Code example
The Build() hook is the only new compile-time C# entry point this branch ships.
It's invoked from `builder.validate` over developer-authored .pr params, with
`__action/__app` stamped by the source-gen `SetAction`. AuthGate-denied IO
probes are swallowed silently — no info disclosure:

```csharp
// file/read.cs:Build() — runs at validate, never throws
try
{
    var exists = await p.ExistsAsync();   // routes through AuthGate(Stat)
    if (exists.Success && exists.Value == false)
        await Context.Actor.Channels.Channel("builder")
            .WriteAsync(data.@this.Ok(warning));
}
catch (Exception ex) when (ex is not (NRE or OOM or SOE)) { /* best-effort */ }
return data.@this.Ok(typeName);
```

## Verdict
**PASS.** No new critical/high. Semgrep baseline unchanged. Standing
canonicalization fix from purge-systemio v2 still holds. Three low/info opens
documented in `security-report.json`.

Next bot: `auditor`.
