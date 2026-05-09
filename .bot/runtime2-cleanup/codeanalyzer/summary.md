# codeanalyzer — runtime2-cleanup

## Version

v3 — **cumulative final pass** before merge to `runtime2`. v1 covered stage 1, v2 covered stage 2; stages 3–27 had no per-stage codeanalyzer review, so v3 is the one-shot end-of-branch pass Ingi asked for after pulling all stages.

## What this is

Architect closed Tier 5 with 27 stages landed, both test suites green, and a 250-line self-audit (`architect/results.md`) comparing planned vs delivered. v3's job: independently verify, run the OBP smell checklist on the final shape, and look for things the architect's audit didn't catch.

## What was done

Three parallel Explore surveys + clean rebuild + both test suites:

1. App-spine OBP smell sweep on the 6 backbone files.
2. Tier 5 deep dive on stages 23–27 (never reviewed).
3. Cross-cutting cleanup verification: banned `Console.*` writes, stale stage-N comments, residual old type names (`IProvider`, `App.Build.`, `app.Variables`, `App.Utils.{Json,TypeConverter,…}`), orphan files, deleted-folder confirmation, TODO markers added during cleanup.

```
$ rm -rf */bin */obj && dotnet build PlangConsole       → green
$ dotnet run --project PLang.Tests                       → 2752/2752 ✅
$ cd Tests && ../PlangConsole/bin/Debug/net10.0/plang --test
                                                          → 199/199 ✅
```

(The `[Fail]` lines on `_fixtures_*` files are tester self-test fixtures asserting "a failing test reports as failed" — final summary line is `199 total, 199 pass`.)

## Findings (3, all non-blocker)

| # | Where | What | Severity |
|---|-------|------|----------|
| v3-1 | `Types/Registry.cs:34` | `public List<Assembly> Assemblies` — public mutable list, no Add/Remove discipline (OBP Q1). Zero external mutators in production today. | non-blocker |
| v3-2 | `modules/test/report.cs:38` | `Console.Out.Write(console.ToString())` bypasses Channels — the very rule the cleanup enforces. `Run()` is async with `Context.App` in scope; one-line fix. | non-blocker |
| v3-3 | `App/this.cs:347–355` | `Console.OpenStandardOutput/Error/Input()` in `WireDefaultConsoleChannels`. Stream acquisition (not a write); legitimate channel-bootstrap path. CLAUDE.md exception list could be tightened. | nit / docs |

**v3-2 is the most ironic one** — a cleanup branch built around channel discipline, and the Tester report module is one `await`-and-route-through-channels away from being clean. Worth taking before merge if Ingi agrees.

## Verdict: PASS

The branch is in shape to merge to `runtime2`. Architect's self-audit is accurate. Tier 5 judgment calls (Diagnostics-as-static, Conversion-public-static, http/Default static-eviction, Modules.App back-ref) all hold up under Rule C. Cross-cutting cleanup left no debris.

## Carryover from v1/v2 (status check)

| # | finding | status |
|---|---------|--------|
| v1-1 | Snapshot back-ref aliasing | Latent, untouched. Out of scope. |
| v1-2 | `// Stage 1:` migration-history comment at `Channels/this.cs:61` | **Still present** — same line v1 flagged. Trivial. |
| v1-3 | `AppThis_SerializersExists_PerActor` under-asserts distinctness | Out of scope. |

None re-introduced.

## Files

- `v1/` — stage 1 review (PASS, 3 findings).
- `v2/` — stage 2 review (PASS, 0 findings).
- `v3/plan.md` — review approach.
- `v3/report.md` — full findings + clean-areas inventory.
- `v3/verdict.json` — `{ status: "pass", findings: 3 non-blocker }`.
