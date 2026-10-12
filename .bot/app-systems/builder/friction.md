# builder — friction notes (app-systems, for Ingi)

What slowed the build-the-kept-tree + fix-mapping work today, and what I'd want.

## Build-pass shape
- **The batch built only `.test.goal` entry points, not the sibling `.goal` they call.** That left ~36 tests failing at run with "Goal X.goal exists but isn't built", and took three extra rebuild passes to clear. WANT: a documented "build a folder's whole goal set" pass, or `plang build <folder>` that builds every `.goal` under it, not just the test.
- **`plang build` from `test/` aborts at the first refusing goal** — it can't build the whole tree in one shot. So I built per-file, which is why siblings were missed. WANT: `plang build --keep-going` (build all, collect refusals, don't stop).

## cwd / prPath
- **cwd silently decides the `.pr`'s prPath.** Builds run from `/workspace/plang` (not `test/`) embedded `prPath:"/test/…"`, which the runner (from `test/`) then can't find — "Not found", looks like a runtime red but is a build artifact. Cost real triage time to untangle. WANT: prPath resolved relative to the app root regardless of cwd, or a loud warning when the file arg escapes the app root.

## builderVersion drift
- Mid-session builder changes (my on.error check, inverse-Cover, each teaching re-pin, pulled coder C#) staled earlier-built `.pr`. There's no cheap way to know which `.pr` are stale without a full `plang --test`. WANT: `plang build --stale` (rebuild only `.pr` whose builderVersion/hash is behind the current binary).

## Test-created files
- Failing tests leave the files they create (Cut3's `cut3dir/*`, Cut4's `cut4/*`, `here.json`, `testdata.txt`) because the cleanup steps run only on a pass. `git add -A test/` then swept them into commits — twice. WANT: either tests clean up in a teardown that runs even on failure, or a per-folder `.gitignore` convention for test-created paths.

## Flaky LLM builds (the real cost sink)
- issue-74 (`%s.length%`→`%s%.length%`) and issue-75 (`archive identity X, on error ignore` drops the action) flake per build; catching a correct build took 2–5 retries each, many LLM calls. The on.error-does-nothing structural check I added turns one wrong-save into a clean refusal (good), but the underlying writer mangles need kernel fixes. WANT: the structural checks extended so a flaky-wrong-save is always a refusal, never a save; and the known families (74/75, kinded Type choices, Limit-padding) fixed at the writer kernel next branch.

## Tooling
- The sed/awk/python guard hook fired on log-parsing one-liners against scratch logs (not production source), forcing rewrites. WANT: the hook to exempt paths under the scratchpad / `*.log`.
- Four full `plang --test` runs were needed to converge the baseline, each running the `module/llm` tests against the real provider (cost). A `--skip=llm` or a cached-answer mode for the convergence runs would have cut that.

## Coordination
- Every decision relayed through the architect (dozens of round-trips). It worked and kept Ingi's gates intact, but a shared decision log the builder could read directly (instead of waiting for a relay) would cut latency.
