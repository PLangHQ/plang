# tester v23 — part 1: baseline confirmation

Worktree `/workspace/plang-app-systems-tester`, branch `app-systems-tester` off
`origin/app-systems` 8918a214a. Fresh checkout needed a `dotnet restore`: a
never-restored worktree failed `./dev.sh build` with NETSDK1004 (the msbuild
calls ran without `-restore`). **Correction (architect, after the coder's fix
181468c11):** the `exit 0` I first reported was *my* pipeline — I ran
`./dev.sh build 2>&1 | tail -30`, so the shell reported tail's exit, not
dev.sh's; dev.sh itself exited 2. So the masking was my `| tail`, not the wrapper.
The real finding — a fresh worktree wasn't restored before build — is genuine and
is now fixed by 181468c11 (restore-if-never-restored before build). After a manual
`dotnet restore`, `./dev.sh build` was clean (121s).

## C# — matches baseline exactly

Full force sweep (`./dev.sh test --force`), every suite:

| suite | result |
|---|---|
| Generator | 0 fail / 188 |
| Types | 0 fail / 852 |
| Data | 0 fail / 849 |
| Modules | 0 fail / 1284 |
| Runtime | 0 fail / 1043 |
| Wire | **6 fail** / 534 |

The 6 Wire fails are all `SnapshotWireTests.*`, each tagged
`[Property("carried","test/plan/snapshot")]` (confirmed in the source). = the
baseline's 6 carried snapshot family. **C# baseline confirmed.**

## plang — baseline is the modal result, but non-deterministic

`cd test && ../PlangConsole/bin/Debug/net10.0/plang --test`, run three times from
the same clean tree:

| run | pass | fail | stale | skip |
|---|---|---|---|---|
| 1 | 245 | 16 | 12 | 3 |
| 2 | **244** | **17** | 12 | 3 |
| 3 | 245 | 16 | 12 | 3 |

Runs 1 and 3 reproduce the baseline's `245/16/12/3` and the 16 fail names match
`.bot/app-systems/builder/baseline.md` **exactly** (verified: sorted diff empty).
`git status` is **clean** after all three runs — no test leaves a file behind
(decision-718 hygiene holds).

## Finding T1 — `module/llm/LlmCache` is a live-network flake, un-owned (carry)

Run 2's 17th fail was `/module/llm/LlmCache.test.goal`:

```
[Fail] /module/llm/LlmCache.test.goal (55ms)
  Error: Network is unreachable (api.openai.com:443)
```

The whole `llm/*` family makes **real OpenAI calls** (durations 830–1295 ms when
they pass; `LlmQuery`, `LlmSchema`, `LlmContinue`, `LlmFormat` all hit the live
API). `llm.query`'s `Cache` default is `cache.use` (query.cs:94), so the first
call of a pair is always a cache miss → a live request; the second replays from
the `LlmCache` store. When the sandbox's route to `api.openai.com` blips (run 2),
the first call fails and the test reds. So `245/16` is only the modal outcome —
the suite is non-deterministic by exactly the llm tests.

This is **not owned** anywhere: `.bot/app-systems/{architect/next-branch,
architect/summary,builder/baseline,coder/summary}.md` mention the llm `Cache`
*flag-default* rename (decision 521) but nothing about the llm tests being
live-network / flaky. By the merge bar ("a red without an owner is a finding")
it is a finding, but a **carry** one: test-infra, not an app-systems regression,
and not a merge blocker on its own.

Two defects, both environment-independent of the flake:

1. **Non-hermetic.** Unlike `signing/*` (skips without Landlock) and `Duration`
   (skip-tag), the llm tests have no guard to skip when offline / no API key.
   They should either skip when the key/network is absent, or replay a recorded
   response, so the suite is deterministic.
2. **Weak assertion (false-green on the behaviour it names).** `LlmCache` is
   titled "Cache hit — verify second returns cached result" but asserts only
   `%first% is not null` / `%second% is not null`. It never checks the second
   call was served from cache (e.g. `%second%` equals `%first%`, or a cache-hit
   signal / `CachedTokens`). Caching could be broken and this test still passes
   whenever the network is up. The `.pr` builds the step correctly (cache rides
   the `cache.use` default; dropping the explicit `cache: true` is behaviourally
   a no-op), so this is a test-logic weakness, not a build miss.

**Recommendation:** guard the llm tests to skip without key/network (mirrors the
Landlock pattern), and strengthen `LlmCache` to assert `%second%` equals
`%first%` (or a cache-hit signal). Marked **carry**.
