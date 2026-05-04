# codeanalyzer — runtime2-callstack — v2

## What this is

Second pass on `runtime2-callstack`. Reviews 11 commits since v1 (`bdfa1ab7..0941d4e3`) —
coder's `Goal-pushes-Call-frame` follow-on plus the `runtime2-source-resolution` merge
(AsT carve-outs, JsonObject navigation, builder validateResponse, recovery Cause threading).

## What was done

- Verified status of all 5 v1 MINORs + the SerializableCallStack dead-code finding.
- Reviewed source-only changes (19 files, +770 / −101) for new issues.

## Findings

**v1 carry-forward:** all 5 MINORs still open. Coder did not address them this round.
Verbatim status in `result.md`.

**3 new MINORs:**

1. **`Goal.RunAsync` Push outside try/catch** (`Goals/Goal/this.cs:278`) — mirror of v1
   #3, new site. `CallStackOverflowException` from cycle detection escapes as raw CLR
   exception on recursive goals.
2. **`[ThreadStatic]` cycle-detection HashSets** (`Data/this.cs:533-534`,
   `Variables/this.cs:622-623`) — wrong primitive for async branches. Under
   `Task.WhenAll` two branches sharing a thread interleave on the same set → false
   `VariableResolutionCycle` errors and stale entries. Use `AsyncLocal<T>`.
3. **`AsCanonical` partial-match resolves `%!*%` infra refs at build**
   (`Data/this.cs:477`) — `Variables.Resolve(strVal)` missing `skipInfrastructure:true`.
   Same bug class `dd7bf37e` fixed in `SubstitutePrimitive`, asymmetric here.

**2 NITs:**

- `DictionaryNavigator` "Count" semantics inconsistent across its three arms — generic
  `IDictionary<string,object?>` shadows a user key named "Count", JsonObject doesn't.
- Goal frame's `Action.Step` pinned to `Steps[0]` forever — misleading provenance for
  any frame audited against `Caller.Action.Step` while later steps run.

## Verdict

**NEEDS WORK.** 5 v1 MINORs + 3 new MINORs. Send back to coder. Priority order:
`[ThreadStatic]` (intermittent, hard to debug) → AsCanonical infra-resolve (build-time
correctness regression risk) → Goal-Push-outside-try (crash on user error). Then catch
up the v1 backlog.

## Files written

- `.bot/runtime2-callstack/codeanalyzer/v2/result.md`
- `.bot/runtime2-callstack/codeanalyzer/v2/summary.md`
- `.bot/runtime2-callstack/codeanalyzer/v2/verdict.json`
