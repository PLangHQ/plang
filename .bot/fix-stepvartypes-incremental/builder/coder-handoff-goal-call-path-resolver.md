# Coder handoff: `goal.call` prPath fails File.Exists at runtime

**From:** builder
**Branch:** `fix-stepvartypes-incremental`
**Severity:** medium — at least 2 tests fail with this shape (`App/CallStack/DepthIncreasesOnGoalCall`, `Channels/WriteToCustomChannel`); likely more across the suite. Reproducible.

## Symptom

```
[Fail] App/CallStack/DepthIncreasesOnGoalCall.test.goal (1ms)
  Error: File not found: /App/CallStack/.build/inner.pr
```

The test calls a sub-goal `Inner` (in `App/CallStack/Inner.goal`). The compiled `.pr` carries:

```json
{"name":"GoalName","value":{"name":"Inner","prPath":"/App/CallStack/.build/inner.pr"}}
```

That prPath is **correct** under the app-rooted convention (Tests/ is the app root; `/App/CallStack/.build/inner.pr` resolves to `/workspace/plang/Tests/App/CallStack/.build/inner.pr`, which exists). Yet at runtime the file-existence check fails and the test errors out before its first assertion.

## What I confirmed before handing this off

1. **The file exists.** `ls -la Tests/App/CallStack/.build/inner.pr` → present, ~1.5 KB.
2. **The prPath is right.** Top-level `prPath` on the test's own `.pr` is `/App/CallStack/.build/depthincreasesongoalcall.test.pr` (app-rooted, no `/Tests/` prefix), and the goal.call's GoalName prPath matches the same convention.
3. **Inner.pr is fresh and well-formed.** Just rebuilt it; own prPath is correctly `/App/CallStack/.build/inner.pr`.
4. **The LLM emitted clean output.** Compile trace shows `GoalName={name:"Inner"}` with no prPath — the prPath was added by builder.enrichResponse's `ResolveGoalCallPaths` (`PLang/app/modules/builder/code/Default.cs:934`), and that addition is also correct.

So inputs are all correct.

## What's weird

Same suite, same convention, inconsistent runtime behavior:

| Test | Own prPath | Sub-goal ref | Runtime |
|---|---|---|---|
| `DepthIncreasesOnGoalCall` | `/App/CallStack/…` ✓ | `/App/CallStack/.build/inner.pr` ✓ | **Fail** |
| `CrossFileChain` | `/App/CallStack/…` ✓ | `/Tests/App/CallStack/…` ✗ (stale prefix) | Pass |
| `IndirectGoalCycle` | `/Tests/App/CallStack/…` ✗ | n/a | Pass |
| `HandledFlagSet…` | `/App/CallStack/…` ✓ | n/a | Pass |

**The "correct" prPath fails. The malformed `/Tests/...` shape passes.** That should not be possible if the resolver is doing the right thing.

## Where the failure surfaces

`PLang/app/types/path/file/this.Operations.cs:59-60`:

```csharp
if (!System.IO.File.Exists(Absolute))
    return data.@this.FromError(new errors.ServiceError($"File not found: {Raw}", "NotFound", 404));
```

`Raw` is the prPath string handed in; `Absolute` is whatever the `Path` constructor computed. The error reports `Raw`, so we can't tell from the message what `Absolute` came out as — but `File.Exists(Absolute)` is returning false even though the file is present at the expected app-rooted resolution.

`Absolute` is set in the `Path` constructor at `PLang/app/types/path/this.cs:62` from the constructor's `absolutePath` arg. The factory that builds the `Path` from a raw prPath string is where the bug likely sits — handing it a `/App/CallStack/.build/inner.pr` prPath, it should resolve against `App.AbsolutePath` to get the filesystem path.

## Hypothesis

The runtime probably has TWO load paths for sub-goal calls:

1. **Name-based with cache** — `Goals.GetAsync("Inner", callingFolderPath: "App/CallStack")` walks the standard locations, hits the cache the second time. This path is robust and explains why CrossFileChain (which has a malformed prPath) works — the name resolution succeeds before the broken prPath is consulted.
2. **prPath direct-load** — when the GoalName carries a populated prPath, the runtime tries to load that file directly via a `Path` object. **This is the failing path.** The Path's `Absolute` is wrong for raw `/App/...` strings.

A test that misses the name cache and goes straight to the prPath dispatch is the one that fails. DepthIncreasesOnGoalCall is apparently that test.

## What we want

Either:

**A. Fix the `Path` constructor / factory so an app-rooted prPath resolves correctly to an absolute filesystem path.** This is the right fix — once correct, both load paths produce identical results.

**B. Stop populating `GoalName.prPath` from the builder.** Earlier on this branch we landed code that adds prPath during enrichment (`ResolveGoalCallPaths`). If the name-based load works reliably, the prPath is redundant. Removing it would unblock the failing tests but loses information that may be useful elsewhere.

Architect's call between A and B. **A is preferred** — having the canonical app-rooted shape work everywhere is what the rest of the codebase assumes.

## Verification

After the fix:

```bash
cd /workspace/plang/Tests
rm -rf App/CallStack/.build/{inner,depthincreasesongoalcall.test}.pr
/workspace/plang/PlangConsole/bin/Debug/net10.0/plang build \
  '--build={"files":"App/CallStack/Inner.goal","cache":false}'
/workspace/plang/PlangConsole/bin/Debug/net10.0/plang build \
  '--build={"files":"App/CallStack/DepthIncreasesOnGoalCall.test.goal","cache":false}'
/workspace/plang/PlangConsole/bin/Debug/net10.0/plang --test 2>&1 | grep DepthIncreasesOnGoalCall
```

Expect `[Pass]`. Then run the full suite and confirm the other `File not found` failure (`Channels/WriteToCustomChannel`) clears too.

## Out of scope

- Don't touch the builder enrichment that adds prPath; that's correct and useful. The fix is downstream at path resolution.
- Don't rebuild every test's `.pr` to normalize prPath shape — once the resolver is right, both shapes should work; we want the path resolver to be the single point of truth, not enforce convention via mass rebuild.
