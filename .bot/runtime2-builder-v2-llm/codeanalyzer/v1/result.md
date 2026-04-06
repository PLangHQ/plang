# Code Analysis v1 — LLM Module Results

## PLang/App/modules/llm/query.cs

### OBP Violations
None. Textbook handler → provider delegation: `Run() => await Llm.Query(this)`. The `[Provider]` attribute resolves `ILlmProvider` and the provider navigates the action record. This IS the target pattern.

### Simplifications
None.

### Readability
Clean. Well-documented properties with XML docs. 74 lines, single responsibility.

### Verdict: CLEAN
Action record follows the established handler → provider delegation pattern exactly.

---

## PLang/App/modules/llm/LlmMessage.cs

### OBP Violations
None.

### Simplifications
None.

### Readability
1. **Line 10-11**: `[Store, LlmBuilder]` vs no attribute on ToolCallId/ToolCalls — good separation of builder-visible vs internal fields. Clear.

### Verdict: CLEAN

---

## PLang/App/modules/llm/ToolCall.cs

### OBP Violations
None.

### Simplifications
None.

### Readability
Clean. 18 lines, three properties, all named clearly.

### Verdict: CLEAN

---

## PLang/App/modules/llm/providers/ILlmProvider.cs

### OBP Violations
None. `Task<Data> Query(query action)` — takes the full action record, lets the provider navigate.

### Simplifications
None.

### Readability
Clean.

### Verdict: CLEAN

---

## PLang/App/Engine/Goals/Goal/GoalCall.cs

### OBP Violations
None.

### Simplifications
None.

### Readability
Clean. `Description` and `Parallel` are well-placed — GoalCall now serves dual purpose as a goal reference and an LLM tool definition.

### Verdict: CLEAN

---

## PLang/App/Engine/Providers/this.cs

### OBP Violations
None.

### Simplifications
None.

### Readability
Clean. Two lines added: `ResolveType` mapping and `RegisterDefaults` call. Follows existing pattern exactly.

### Verdict: CLEAN

---

## PLang/App/modules/llm/providers/OpenAiProvider.cs

This is the 874-line provider that owns the full LLM lifecycle. Most findings are here.

### OBP Violations

1. **Line 366: `ExecuteToolAsync` receives decomposed parameters**
   - Current: `ExecuteToolAsync(EngineType engine, query action, ToolCall toolCall, PLangContext context)`
   - The method receives `engine` and `context` separately, but both are reachable via `action.Context.Engine` and `action.Context`.
   - OBP form: `ExecuteToolAsync(query action, ToolCall toolCall)` — let the method navigate `action.Context.Engine` and `action.Context` internally.
   - Why it matters: Passing `engine` alongside `action` (which has `.Context.Engine`) creates two paths to the same object. If someone later passes a different engine than `action.Context.Engine`, behavior diverges silently.
   - **Severity: Low** — private method, contained within the class.

2. **Line 479: `ToApiMessages` receives `engine` separately**
   - Current: `ToApiMessages(List<LlmMessage> messages, EngineType engine)`
   - Only uses `engine` for `ResolveImage` (file system access). Could take `IPLangFileSystem` or better, the full `EngineType` via a different path.
   - Same pattern as #1 — decomposed parameter.
   - **Severity: Low** — private static formatting helper.

### Simplifications

1. **Lines 139-169: Duplicate `httpAction` construction**
   - The non-streaming `httpAction` (line 139-148) is fully constructed, then immediately overwritten if `action.OnStream != null` (line 154-165). In the streaming path, the first construction is dead code.
   - Fix: Build once with conditional streaming properties.
   ```csharp
   var httpAction = new request
   {
       Context = context, Url = endpoint, Method = PlangHttpMethod.POST,
       Body = body, Headers = headers, Unsigned = true, TimeoutInSec = 120,
       OnStream = action.OnStream != null ? BuildStreamProxy(action, engine, context) : null,
       StreamAs = action.OnStream != null ? StreamFormat.SSE : default
   };
   ```

2. **Lines 867-873: `BuildStreamProxy` is a dead wrapper**
   - Current: `return action.OnStream;` — pure passthrough with a TODO comment.
   - This method exists for future expansion but currently adds indirection with zero behavior.
   - Recommendation: Inline for now, add the method when streaming accumulation is actually implemented. Dead wrappers confuse readers ("what's this method doing?").

### Readability

1. **`Query` method is ~330 lines (30-362)**
   - This is a long method. However, each section is clearly delineated with comments (`--- Config ---`, `--- Validate ---`, `--- Build messages ---`, etc.) and the logic is sequential pipeline.
   - The `while (true)` loop (line 116) has 5 exit paths: `break` at 169 (streaming), `break` at 205 (MaxToolCalls), `continue` at 252 (tool re-query), `continue` at 307 (validation retry), `return` at 357 (success).
   - **Not blocking, but worth noting**: If the method grows further (e.g., streaming accumulation), it should be split into named methods for each pipeline stage.

### Behavioral Reasoning

1. **Line 587: Bare `catch` in `ResolveImage` — masks programming errors**
   - Current:
     ```csharp
     catch
     {
         // Fall through to base64 assumption
     }
     ```
   - This catches `NullReferenceException`, `StackOverflowException`, `OutOfMemoryException` — all of which should propagate. A bug in the filesystem abstraction would silently produce garbage base64 data sent to the LLM.
   - Fix: Use the established negative catch filter pattern:
     ```csharp
     catch (Exception ex) when (ex is not (NullReferenceException or OutOfMemoryException or StackOverflowException))
     ```
   - **Severity: Medium** — silent data corruption on programming errors.

2. **Line 765-768: Bare `catch` in `ParseApiResponse` — same issue**
   - Current:
     ```csharp
     catch
     {
         return null;
     }
     ```
   - If `JsonSerializer.Serialize(value)` throws for any reason beyond serialization failure (OOM, stack overflow), the method silently returns null → "Failed to parse LLM API response" error masks the real cause.
   - Fix: `catch (JsonException)` or the negative filter.
   - **Severity: Medium** — obscures the real error when things go wrong.

3. **Line 734: Sync-over-async in `ResolveConfig`**
   - Current: `settings.Get("LlmConfig", settingKey).GetAwaiter().GetResult()`
   - This is called from the async `Query` method. If `ISettingsStore.Get` uses a synchronization context, this deadlocks. Even without a sync context, `.GetAwaiter().GetResult()` blocks the thread pool thread.
   - Fix: Make `ResolveConfig` async and `await` it:
     ```csharp
     private static async Task<string> ResolveConfigAsync(ISettingsStore settings, ...)
     ```
   - **Severity: Medium** — potential deadlock, guaranteed thread-pool thread blocking.

### Deletion Test

1. **Lines 465-472: `ParseToolArguments` default fill-in — untested**
   ```csharp
   if (parameterDefs != null)
   {
       foreach (var def in parameterDefs)
       {
           if (!result.Any(r => r.Name == def.Name) && def.Value != null)
               result.Add(new Data(def.Name, def.Value));
       }
   }
   ```
   - Could delete these 8 lines and no test would fail. All tool call tests provide complete arguments. No test exercises the case where the LLM omits a parameter that has a default value defined in the GoalCall.
   - **Recommendation**: Add test: tool with `new Data("units", "metric")` default, LLM response omits "units", verify "metric" is used.

2. **Lines 693-704: `MapPlangTypeToJsonSchema` individual type mappings — partially untested**
   - Only `string` mapping is exercised (via `Type.String` in tool tests). The `int→integer`, `bool→boolean`, `list→array`, `object→object` mappings are never hit.
   - Could change `"bool" => "boolean"` to `"bool" => "string"` and no test would fail.
   - **Recommendation**: Add test with mixed parameter types (string, int, bool) and verify the schema JSON.

3. **Lines 818-826: `RestoreFromCache` Dictionary branch — dead defensive code**
   ```csharp
   else if (cached.Value is Dictionary<string, object?> dict)
   ```
   - Cache values go through SQLite JSON round-trip and always come back as `JsonElement`. This branch handles an in-memory case that never occurs in practice. Could delete and no test fails.
   - **Severity: Low** — defensive code, not harmful, but worth a comment explaining when this path would be hit.

4. **Lines 867-873: `BuildStreamProxy` — dead wrapper (also in Simplifications above)**
   - Could delete entirely and replace line 163 with `OnStream = action.OnStream`. No test fails.

---

## Summary

| File | Verdict |
|------|---------|
| `query.cs` | CLEAN |
| `LlmMessage.cs` | CLEAN |
| `ToolCall.cs` | CLEAN |
| `ILlmProvider.cs` | CLEAN |
| `GoalCall.cs` | CLEAN |
| `Providers/this.cs` | CLEAN |
| `OpenAiProvider.cs` | **NEEDS WORK** |

### Critical Findings (must fix)

1. **Bare catches** (OpenAiProvider:587, 765) — use scoped `catch (JsonException)` or negative filter `catch (Exception ex) when (ex is not (...))`. These silently mask programming errors and produce misleading error messages.

2. **Sync-over-async** (OpenAiProvider:734) — `ResolveConfig` uses `.GetAwaiter().GetResult()` inside an async method. Make it async to prevent deadlocks and thread-pool starvation.

### Moderate Findings (should fix)

3. **Untested default fill-in** (OpenAiProvider:465-472) — `ParseToolArguments` default parameter substitution has zero test coverage. Add a test.

4. **Untested type mappings** (OpenAiProvider:693-704) — Only `string` type mapping is exercised. Add a test with mixed types.

### Minor Findings (nice to fix)

5. **Duplicate httpAction construction** (OpenAiProvider:139-169) — Build once with conditional properties.

6. **Dead `BuildStreamProxy` wrapper** (OpenAiProvider:867-873) — Inline until streaming accumulation is implemented.

7. **Decomposed parameters in `ExecuteToolAsync`** (OpenAiProvider:366) — Pass `action` instead of `engine + action + context`.

### Overall Verdict: NEEDS WORK

Two bare catches and a sync-over-async are the blocking issues. The code structure is solid — the handler → provider delegation is textbook OBP, the tool loop is well-designed, and the conversation continuity mechanism correctly avoids format instruction compounding. The issues are localized to `OpenAiProvider.cs` internal methods.
