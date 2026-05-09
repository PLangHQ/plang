# auditor v1 result — runtime2-cleanup

## Verdict: FAIL (coder fix pass before merge)

Ingi elected to fix findings 1, 2, 3 on this branch rather than defer. Finding 4
is process-only. See "Recommendations for coder" at the bottom — those become the
fix list.

A 27-stage, 107-commit structural refactor. Three reviewers already cleared it; my
job was the seams. No critical or major findings. Four minor findings, all
pre-existing or low-impact, recorded for follow-up. Both test suites still green
(C# 2752/2752, PLang 199/199) on a clean rebuild from this commit.

## Previous reviewers' verdicts — assessed

| Reviewer | Verdict | My read |
|---|---|---|
| codeanalyzer v3 | PASS, 3 non-blockers | **Agree.** OBP smell sweep was thorough across the 6 backbone files + Tier 5 stages. v3-1 (Registry.Assemblies public list), v3-2 (Console.Out.Write in test/report), v3-3 (Console.Open* in WireDefaultConsoleChannels) all real. v3-2 elevated below — anti-thematic to the branch's own thesis. |
| security v1 | PASS, 2 low (both carry-over from v3) | **Agree.** Crypto, type resolution, deserialization, settings all preserve prior semantics. The two low findings are both restatements of codeanalyzer v3 — security correctly read them as hygiene, not attack surface. |
| tester | (no report) | **Gap.** Codeanalyzer ran the suites and confirmed counts, but no specialised tester pass. Notable because the cleanup added a compatibility facade (`TypeMappingTestFacade`) that ~111 test sites still use; whether tests exercise the new shape is non-trivial. See finding 4. |

## Findings

### 1. Minor — cross-file contract: three identical `CaseInsensitiveRead` bags, one of them in a test facade and not routed to production

**Files:**
- `PLang/App/Types/Conversion.cs:22` — `private static readonly _caseInsensitiveRead`
- `PLang/App/modules/http/code/Default.cs:55` — `private readonly _caseInsensitiveRead`
- `PLang.Tests/Support/TypeMappingTestFacade.cs:67` — `Json.CaseInsensitiveRead { get; }`

All three have **byte-identical** contents:

```csharp
PropertyNameCaseInsensitive = true,
Converters = { JsonStringEnumConverter(allowIntegerValues: true), EmptyStringToNullEnumConverterFactory(), TimeSpanIso8601() },
NumberHandling = JsonNumberHandling.AllowReadingFromString
```

**Missed by:** codeanalyzer (focused per-file), security (focused on trust boundaries).

**OBP smell #3:** "Same logical thing stored multiple times across types." The defence — "per-consumer ownership keeps Rule C closed without inventing a shared `Json` god-bag" — is internally consistent for the two production sites. But:

- The **test facade's bag is a fourth fork** that is *not* routed to either production home. Tests calling `App.Utils.Json.CaseInsensitiveRead` do not exercise the production options. If a converter is added to `Conversion._caseInsensitiveRead` (e.g. a new domain enum) and not also added to `http/Default._caseInsensitiveRead` and the test facade, drift goes undetected.
- The other facade properties (`CamelCaseIndented`, `PrWrite`, `DiagnosticOutput`) all route to production homes — only this one bag is locally constructed because there is no shared production origin to point at. The asymmetry is the tell.

**Suggestion (pre-merge):** route `TypeMappingTestFacade.Json.CaseInsensitiveRead` to one of the two production homes (e.g. expose it as an internal getter on `App.Types.Conversion` and forward) so the facade tests at least pin one of the two production options. Or: accept the trade-off in code (a comment that names the duplication explicitly so a future contributor adding a converter knows there are now **three** sites to update).

**Severity:** minor. No correctness issue today; failure mode is silent test drift on future converter additions.

---

### 2. Minor — `Diagnostics.@this` is a `public static class @this`, diluting the `@this` convention

**File:** `PLang/App/Diagnostics/this.cs:21`

**Missed by:** codeanalyzer (accepted as Rule C exception); architect's brief allowed it.

The `@this` convention in this codebase signals: *the folder's primary class, navigated via `parent.Folder`*. Every other folder I checked follows this — `App.Channels.@this` reachable as `app.Channels`, `App.Types.@this` as `app.Types`, etc. Routes from CLAUDE.md — "Every folder's primary class is `@this` in `this.cs`. Consumers use global aliases."

`Diagnostics.@this` is declared `public static class @this`. There is no `app.Diagnostics`. Callers reach it as `global::App.Diagnostics.@this.Format(...)`. The `@this` name is doing no work here — a normally-named static class (e.g. `App.Diagnostics.Formatter`, or just `App.Diagnostics.Output`) would communicate the shape correctly without claiming the folder-as-instance signal.

The coder's reasoning was practical (callers are in static contexts; threading `app` is friction). Fine — but then *don't* call it `@this`. A static helper class isn't a `@this` shape; using the same name for both blurs the convention's meaning.

**Suggestion:** rename `Diagnostics/this.cs` → `Diagnostics/Format.cs` (or similar) and the class to a normal name. The folder's `@this` slot stays available if a future instance form arises. Or, if instance is the intent eventually, keep the name but make a small `app.Diagnostics` mount now (one line in `App/this.cs`) so the convention is honoured.

**Severity:** minor / convention. Not functionally wrong; a small ding on the architectural-fit gauge that the branch otherwise scored very high on.

---

### 3. Minor (re-elevated from codeanalyzer v3-2 / security #2) — `Console.Out.Write` in `test/report.cs` is anti-thematic to the cleanup branch's own thesis

**File:** `PLang/App/modules/test/report.cs:38`
**Code:** `Console.Out.Write(console.ToString());`

**Status across reviewers:**
- codeanalyzer v3-2: "non-blocker, but worth taking before merge if Ingi agrees."
- security #2: "accepted-risk, internal hygiene."

**My read:** elevate to "fix before merge." The architect's plan repeatedly describes channel discipline as *the* organising principle of this cleanup. A handler that constructs a `StringBuilder` and then dumps it via `Console.Out.Write` — when `Run()` is `async`, has `Context.App` in scope, and `app.CurrentActor.Channels.WriteTextAsync(Output, ...)` is the documented path — is the exact thing the branch is meant to eliminate. Leaving it in says "we cleaned up everything except our own report module."

The two-reviewer chain accepted it as "diagnostic only / test harness only," which is true — the impact is hygiene, not security or correctness. But the branch is the cleanup branch. If not now, when?

**Suggestion:** `await Context.App.CurrentActor.Channels.WriteTextAsync(Output, console.ToString())` (mirroring the pattern in `output/write.cs`). Single-line change.

**Severity:** minor by impact, but symbolically important for the branch's own narrative.

---

### 4. Process — no tester review on a 465-file branch, ~111 test sites still go through the compatibility facade

**Files (representative):**
- `PLang.Tests/Support/TypeMappingTestFacade.cs` — the shim
- `~111 *.cs` files in `PLang.Tests/` retain `using App.Utils;` resolving to the facade

**What this means in practice:** most facade methods *do* route to production (`Types.@this.Get`, `Types.@this.GetTypeName`, `App.@this.CamelCaseIndented`, etc.) — those tests still exercise the production path. The single drift risk is finding 1 above (`CaseInsensitiveRead`).

What the absence of a tester review *did* miss is whether the **PLang test suite** (199 tests, including the new `Tests/Channels/*` cohort that came in via the runtime2-channels parent and is bundled in this merge) actually exercises the new channel discipline beyond happy paths. I sampled the Channels tests and they cover the headline cases (add basic / with config, set explicit / context actor / replace output, remove custom / role-channel-refused, write to custom / unknown, events on ask / before write). Coverage looks reasonable.

**Suggestion:** treat as advisory for next branch — when a cleanup spans this many stages, request an explicit tester pass even when the codeanalyzer ran the suites, because "tests pass" and "tests cover the new shape" are different questions.

**Severity:** process / advisory. Does not block this branch.

---

## Foundation ripple — confirmed safe

Spot-checked the high-blast-radius files outside what reviewers already covered:

- `PLang/App/this.cs` — 366-line change. The new `WireDefaultConsoleChannels` (lines 347–355) acquires streams via `Console.OpenStandardOutput/Error/Input()` (codeanalyzer v3-3). Stream acquisition, not writes, and is the legitimate bootstrap path before channels exist. Agree with codeanalyzer's read.
- `PLang/Executor.cs` — 31-line change, `App.@this` ctor signature shifted (now takes a root path). Caller updates verified across `PlangConsole/`, `TestFixtures/`, `PLang.Tests/`. Clean.
- `PLang/App/Types/this.cs` — 927-line keystone. Sampled the static-vs-instance split: state-touching methods are instance (`Register`, `Get`, `GetValidValues`), pure-logic helpers are `static` forwarders (`GetPrimitiveOrMime`, `IsScalarPlangType`). The architect's review of this stage notes this explicitly; I agree it's coherent.

## Verification

```
$ git rev-parse HEAD
fb8eda3bfeaadd53c6b02232bbcd998c85de5f22
$ rm -rf */bin */obj && dotnet build PlangConsole          → green
$ dotnet run --project PLang.Tests                          → 2752/2752
$ cd Tests && ../PlangConsole/bin/Debug/net10.0/plang --test → 199/199
```

Done as a sanity pin against my own findings — none of them required code changes to verify.

## What I did NOT do

- Re-run the OBP smell checklist on the 6 backbone files. codeanalyzer v3 covered this, and re-doing it would just produce the same list.
- Re-verify cryptographic primitives. security v1 covered, and the diff shows the EdDSA path untouched.
- Audit the architect's `results.md` (planned-vs-delivered). The architect already wrote a 250-line self-audit; reviewing it would be reviewing the reviewer's reviewer.

## Recommendations for coder (optional, non-blocking)

If a follow-up coder pass touches this branch before merge:

1. (Finding 3) One-line fix: `test/report.cs:38` → `await Context.App.CurrentActor.Channels.WriteTextAsync(Output, console.ToString())`. Aligns the branch with its own thesis.
2. (Finding 1) Either route `TypeMappingTestFacade.Json.CaseInsensitiveRead` to a production home, or add a comment block at all three sites naming the other two so future contributors don't drift.
3. (Finding 2) Consider renaming `Diagnostics/this.cs` → `Diagnostics/Format.cs` (class `App.Diagnostics.Format`). Clarifies the convention.

If Ingi prefers to merge as-is and address these in the next branch, that is also a defensible call — none of them block.
