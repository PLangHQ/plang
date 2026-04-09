# Summary v1 — Code Analysis of system-goals-architecture

## What this is
Code quality analysis of the `system-goals-architecture` branch — a major rewrite that moves PLang from Runtime2 to the App namespace, introduces C#-driven execution (Goal.RunAsync → Steps.RunAsync → Step.RunAsync → Action.RunAsync), and restructures the module/handler system.

## What was done
Analyzed 18 core architecture files using the 5-pass method (OBP, simplification, readability, behavioral reasoning, deletion test). Also ran cross-cutting scans for bare catches, sync-over-async, System.IO violations, and Newtonsoft usage.

## Key findings

### Medium (3)
1. **Goal.PrPath backslash on Linux** (`Goal/this.cs:84`) — When Path has no separators, PrPath defaults to `\` as the directory separator. On Linux, this creates invalid paths like `dir.build\name.pr`. Fix: default to `/`.

2. **GoalCall parameter pollution** (`GoalCall.cs:95-97`) — Parameters are injected into context.Variables BEFORE the .pr file loads. If the load fails, the parameters remain on the caller's Variables, potentially shadowing existing variables. Fix: wrap in Save/Restore or inject after successful load.

3. **Newtonsoft in CommandLineParser** (`CommandLineParser.cs:1-2`) — Active `JToken.Parse` dependency. CLAUDE.md requires System.Text.Json in new code. The `Data.UnwrapJsonElement` method already does the exact same pattern with System.Text.Json.

### Minor (4)
- Sync-over-async in RemoveKeepAlive (deadlock risk in non-console contexts)
- Bare catch(Exception) in Step.RunAsync catches NullReferenceException
- 5 bare catches across App/ need narrowing
- Steps enumerator stamps Context but RunAsync doesn't (inconsistency)

### Low (3)
- GoalCall back-reference wiring incomplete for 3+ level sub-goals
- Path.Clone()/Identity.Clone() miss Returned/ReturnDepth (theoretical)
- Variable bracket resolution edge case with dots in resolved values

## Code example — PrPath backslash bug
```csharp
// Goal/this.cs:84 — current
return dir + ".build" + (sepIndex >= 0 ? Path[sepIndex].ToString() : "\\") + baseName.ToLowerInvariant() + ".pr";

// Fix — use forward slash (PLang paths are always forward-slash internally)
return dir + ".build" + (sepIndex >= 0 ? Path[sepIndex].ToString() : "/") + baseName.ToLowerInvariant() + ".pr";
```

## Verdict: NEEDS WORK
Recommend sending findings #1 (PrPath) and #2 (GoalCall params) to the coder. Finding #3 (Newtonsoft) can be bundled or deferred.
