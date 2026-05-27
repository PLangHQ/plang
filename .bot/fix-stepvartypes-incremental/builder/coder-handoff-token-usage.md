# Coder handoff: cached-token extraction + cost computation in OpenAi.cs

**From:** builder
**Branch:** `fix-stepvartypes-incremental`
**Severity:** medium — the data exists in the API response but isn't being extracted; cost computation is plumbed but always returns null.

## What we want end-to-end

Surface per-LLM-call token usage and dollar cost in the web UI trace viewer so builders can see how much each build costs (prompt tokens, cached tokens, completion tokens, total tokens, USD cost) per step + per goal + per file. The web UI work is mine; this handoff covers only the C# pieces I need from coder.

## What exists today in `PLang/app/modules/llm/code/OpenAi.cs`

- Line ~139: `double? totalCost = null;` — declared, never assigned.
- Line ~200-205: extracts `prompt_tokens` and `completion_tokens` from `usage`. **Does not extract cached tokens.**
- Line ~422-425: writes `PromptTokens`, `CompletionTokens`, `TotalTokens`, `Cost` as properties on the Data result via `SetProp`.

So 60% of the plumbing exists; what's missing is (a) cached-token extraction and (b) cost computation.

## Change 1 — extract `cached_tokens`

OpenAI returns cached tokens inside `usage.prompt_tokens_details.cached_tokens`. Anthropic returns them at `usage.cache_read_input_tokens` and `usage.cache_creation_input_tokens` (two separate fields — reads are billed at the cached-rate; creations are billed at a higher write-cache rate). Both providers leave these absent when the response had no cache hit, so default to 0.

Add a new accumulator alongside the existing ones:

```csharp
int totalCachedTokens = 0;       // cache reads (billed at cached rate)
int totalCacheWriteTokens = 0;   // anthropic only — cache creation (billed at 1.25× input)
```

In the existing `if (usage != null)` block (line ~201):

```csharp
// OpenAI: usage.prompt_tokens_details.cached_tokens
if (usage.Value.TryGetProperty("prompt_tokens_details", out var ptd)
 && ptd.TryGetProperty("cached_tokens", out var ctok))
    totalCachedTokens += ctok.GetInt32();

// Anthropic: usage.cache_read_input_tokens / usage.cache_creation_input_tokens
if (usage.Value.TryGetProperty("cache_read_input_tokens", out var crt))
    totalCachedTokens += crt.GetInt32();
if (usage.Value.TryGetProperty("cache_creation_input_tokens", out var cct))
    totalCacheWriteTokens += cct.GetInt32();
```

Surface them via `SetProp` alongside the existing ones (line ~422-425):

```csharp
SetProp(result, "CachedTokens", totalCachedTokens);
SetProp(result, "CacheWriteTokens", totalCacheWriteTokens);
```

Include them in the `cacheEntry` dictionary (line ~398-411) the same way.

## Change 2 — compute `totalCost`

Need a per-model price table. Use **USD per 1 million tokens**, with three rates per model: input, cached, output. Anthropic also has a cache-write rate (typically 1.25× input).

Where to put it — pick whichever you prefer:
- **Inline in `OpenAi.cs`** as a `static readonly Dictionary<string, (decimal input, decimal cached, decimal output, decimal cacheWrite)>`. Simplest, but every price change is a rebuild.
- **Separate file** `PLang/app/modules/llm/code/ModelPricing.cs` exposing a `TryGetPrice(string model, out PriceRow row)`. Same shape, cleaner to find.

I have no preference on placement — pick the one that fits the file layout you've been using.

Starter table (current public list prices as of 2026-05; coder, please sanity-check these against the live pricing pages before shipping):

| Model match (prefix) | input | cached | output | cache-write (anthropic) |
|---|---|---|---|---|
| `gpt-5` | 1.25 | 0.125 | 10.00 | — |
| `gpt-4.1` | 2.00 | 0.50 | 8.00 | — |
| `gpt-4o-mini` | 0.15 | 0.075 | 0.60 | — |
| `gpt-4o` | 2.50 | 1.25 | 10.00 | — |
| `o1` | 15.00 | 7.50 | 60.00 | — |
| `o3` | 2.00 | 0.50 | 8.00 | — |
| `o3-mini` | 1.10 | 0.55 | 4.40 | — |
| `claude-opus-4` | 15.00 | 1.50 | 75.00 | 18.75 |
| `claude-sonnet-4` | 3.00 | 0.30 | 15.00 | 3.75 |
| `claude-haiku-4` | 1.00 | 0.10 | 5.00 | 1.25 |

Match the longest prefix that fits the model id (e.g. `gpt-4o-mini-2024-07-18` matches `gpt-4o-mini`, not `gpt-4o`). If no prefix matches, leave `totalCost = null` (the web UI will show "—" rather than a misleading $0.00).

Cost formula (per call, accumulate across retries the same way the token counts already do):

```csharp
// Non-cached input tokens are the prompt tokens MINUS cached read tokens.
// Cached tokens are billed at the cached rate; cache-write tokens at the
// write-cache rate (anthropic). Completion is straight output rate.
int nonCachedInput = Math.Max(0, totalPromptTokens - totalCachedTokens - totalCacheWriteTokens);
totalCost = (totalCost ?? 0m)
  + (decimal)nonCachedInput      * row.input      / 1_000_000m
  + (decimal)totalCachedTokens   * row.cached     / 1_000_000m
  + (decimal)totalCacheWriteTokens * row.cacheWrite / 1_000_000m
  + (decimal)totalCompletionTokens * row.output    / 1_000_000m;
```

Three notes:
- Keep `totalCost` as `decimal?` instead of `double?` — money math; existing field type is `double?`, change it to `decimal?` and update the `SetProp` write at line ~425 accordingly. The web UI will format on the JS side; rendering `0.00012345m` is fine.
- The formula assumes prompt_tokens **includes** the cached portion (true for OpenAI's accounting; verify Anthropic uses the same convention before shipping — if Anthropic reports `prompt_tokens` exclusive of cache, drop the `- totalCachedTokens` subtraction).
- For unknown models: don't fabricate. Leave `totalCost` null. Log a one-liner via `await context.App.Debug.Write($"llm.query: no pricing entry for model {model}, cost not computed")` so we know what to add to the table.

## Verification

After coder ships, I'll wire the PLang trace capture (`BuildGoal/Plan.goal` + `BuildStep/Start.goal`) to include these properties under a new `usage` field on `response`, then add per-step / per-goal / per-file token+cost rows to the web UI.

Smoke recipe coder can run independently to confirm the C# work:

```bash
cd /workspace/plang/Tests
rm -rf Simple/.build
/workspace/plang/PlangConsole/bin/Debug/net10.0/plang build \
  '--build={"files":"Simple/Start.goal","cache":false}' \
  '--debug={"llm":{"response":true},"maxLength":50000}' 2>&1 | grep -E "usage|Cost|Cached"
```

The raw `usage` block from each LLM call should appear (with `cached_tokens` if the model returned it), and the `Data` result should carry `PromptTokens`, `CompletionTokens`, `TotalTokens`, `CachedTokens`, `CacheWriteTokens`, `Cost`.

## Out of scope

- Don't touch BuildStep/Start.goal or BuildGoal/Plan.goal — the PLang-side trace capture is my next pass once the C# data is available.
- Don't add a UI for this. I'll wire web/index.html after.
- Don't rename existing properties (`PromptTokens`, etc.) — the web UI work depends on the current names.
- Don't try to track cost across the whole build run from C# — it's per-call. Per-goal and per-file totals are an aggregation step in PLang (foreach over stepPasses) or the web UI; pick wherever's cleanest.
