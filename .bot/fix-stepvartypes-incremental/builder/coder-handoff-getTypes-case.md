# Coder handoff: preserve original case in `goal.getTypes` snapshot

**From:** builder
**Branch:** `fix-stepvartypes-incremental`
**Severity:** low — produces spurious `missingVariable` warnings on every step that references a variable in non-lowercase form. Two prompt-only attempts to suppress them have failed; the LLM keeps comparing literal strings.

## The symptom

`Tests/TestModule/Run/TestRunEnforcesTimeout.test.goal` step 5 (`assert %hasTimeout% is true`) produces:

```
WARN missingVariable: "Variable %hasTimeout% is not present in the provided
scope snapshot (available: %tests%, %count%, %results%, %hastimeout%)."
```

The LLM lists `%hastimeout%` as available IN THE SAME SENTENCE and still flags `%hasTimeout%` as missing. Two prompt strengthenings (commit `bab2a39c3` and a subsequent CompileUser.llm "(case-insensitive)" header tweak) didn't suppress it. The LLM trusts what it sees in the snapshot over what the system prompt tells it.

## The root cause

`PLang/app/modules/goal/getTypes.cs` (the snapshot-building action):

```csharp
private static string Normalise(string raw)
    => raw.TrimStart('%').TrimEnd('%').ToLowerInvariant();
```

Every variable name written into `working` / `currentStepSnapshot` is lowercased. The snapshot rendered into the compile user prompt — via `CompileUser.llm`'s `{% for kv in stepVarTypes %}%{{ kv[0] }}%({{ kv[1] }}){% endfor %}` — iterates those keys, so the LLM sees `%hastimeout%` even though the step text and the original `variable.set Name=%hasTimeout%` used `hasTimeout`.

PLang is case-insensitive on variable names at runtime, so the lowercasing is correct for dict-lookup. It's only the *rendering* that loses information.

## The fix

Keep case-insensitive lookup, but preserve the first-seen original case for display.

Switch the dicts to `OrdinalIgnoreCase` so reassignments still overwrite the same slot:

```csharp
var working = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
var snapshot = new Dictionary<string, string>(working, StringComparer.OrdinalIgnoreCase);
```

Stop lowercasing in `Normalise`:

```csharp
private static string Normalise(string raw)
    => raw.TrimStart('%').TrimEnd('%');
```

Two subtle points worth knowing before you flip it:

1. **Case stability across reassignments.** With `OrdinalIgnoreCase`, `working["hasTimeout"] = "bool"` then `working["HASTIMEOUT"] = "bool"` both target the same slot — but `dict[secondKey] = value` does NOT update the stored *key*. The first-seen casing is retained. That's the behavior we want: snapshot shows the casing used the first time the variable was introduced (which is what the source author wrote in their `variable.set`).
2. **`Normalise` is also used to write the foreach `ItemName` key.** That path comes from `loop.foreach ItemName=...`'s parameter value, which is the source-text casing already. Preserving it is the same correctness call.

## Side cleanup (your call, not load-bearing)

Once the snapshot stops lying about casing, the corresponding "case-insensitive" paragraphs in `os/system/builder/llm/Compile.llm` (added in `bab2a39c3`) become belt-and-suspenders. Worth leaving — case-insensitive *behavior* at runtime is still useful for the LLM to know — but the specific "snapshot lowercases names" sentence can be deleted since it'll no longer be true.

## Verification

```bash
cd /workspace/plang/Tests
rm -rf TestModule/Run/.build/testrunenforcestimeout.test.pr
/workspace/plang/PlangConsole/bin/Debug/net10.0/plang build \
  '--build={"files":"TestModule/Run/TestRunEnforcesTimeout.test.goal","cache":false}'
```

Inspect the latest trace under `.build/traces/*/TestRunEnforcesTimeout.json`. Step 5's response should have `warnings: []` (currently 1 warning). User prompt's "Variables in scope" line should now read `%hasTimeout%(bool)` — original case preserved.

Other regression checks: snapshots from prior tests (`%items%`, `%message%`, `%hasMissingPr%`, `%hasFast%`, etc.) should all render in the case the .goal author wrote.

## Out of scope

- Don't touch the runtime variable lookup — it's case-insensitive everywhere via `IDictionary<string, …>` with `OrdinalIgnoreCase`. This change is purely about which *key string* is retained for display.
- Don't change `CompileUser.llm` — the `{% for kv in stepVarTypes %}` iteration just reads whatever keys the C# put in.
