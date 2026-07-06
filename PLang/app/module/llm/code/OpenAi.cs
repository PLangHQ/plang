using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using app.actor.context;
using app.error;
using app.goal;
using app.variable;
using app.module.setting;
using app.type.path;
using app.module.http;
using PlangHttpMethod = app.module.http.HttpMethod;
using number = global::app.type.number.@this;
using text = global::app.type.text.@this;
using item = global::app.type.item.@this;

namespace app.module.llm.code;

/// <summary>
/// OpenAI-compatible LLM provider. Owns the full lifecycle:
/// config, message formatting, HTTP calls (via http module), tool loop,
/// caching (via SettingsStore), streaming, validation, conversation continuity.
/// </summary>
public sealed class OpenAi : ILlm
{
    public string Name { get; init; } = "OpenAi";
    public bool IsDefault { get; set; }
    public bool IsBuiltIn { get; set; }
    public string? Source { get; set; }

    /// <summary>Fired before each LLM API call with resolved messages and the schema string (if any). Debug subscribes to this.</summary>
    public event Action<List<LlmMessage>, string?>? OnBeforeRequest;

    /// <summary>Fired with the raw LLM response string after each successful API call. Debug subscribes to this.</summary>
    public event Action<string>? OnAfterResponse;

    private const string ConversationKey = "__llm_conversation__";
    private const string SchemaKey = "__llm_schema__";
    private const string CacheTable = "LlmCache";

    // USD per 1M tokens. Longest matching prefix wins; missing model → null cost.
    // Prices as of 2026-05 — bump when OpenAI publishes a change.
    private static readonly (string prefix, decimal input, decimal cached, decimal output)[] Pricing = new[]
    {
        ("gpt-5.4-nano", 0.20m, 0.02m,   1.25m),
        ("gpt-5.4-mini", 0.75m, 0.075m,  4.50m),
        ("gpt-5.4",      2.50m, 0.25m,  15.00m),
    };

    private static (decimal input, decimal cached, decimal output)? PriceFor(string? model)
    {
        if (string.IsNullOrEmpty(model)) return null;
        (string prefix, decimal input, decimal cached, decimal output)? best = null;
        foreach (var row in Pricing)
            if (model.StartsWith(row.prefix, StringComparison.OrdinalIgnoreCase)
                && (best is null || row.prefix.Length > best.Value.prefix.Length))
                best = row;
        return best is null ? null : (best.Value.input, best.Value.cached, best.Value.output);
    }

    public async Task<data.@this> Query(query action)
    {
        var app = action.Context.App;
        var context = action.Context;

        // --- Config ---
        var settings = await app.SettingsStore;
        var endpoint = await ResolveConfigAsync(settings, "llm.endpoint", "OPENAI_API_ENDPOINT",
            "https://api.openai.com/v1/chat/completions");
        var apiKey = await ResolveConfigAsync(settings, "llm.apiKey", "OPENAI_API_KEY", null);
        var model = ((action.Model == null ? null : await action.Model.Value())?.ToString()) is { Length: >0 } __m ? __m : null;
        model ??= await ResolveConfigAsync(settings, "llm.model", null, "gpt-5.4-nano");

        // --- Validate ---
        // HACK (minimal): Messages.Value can be NULL (not just empty) — the [IsNotNull]
        // guard checks the parameter's presence, not the lazily-resolved value. Seen in the
        // builder self-build: when error.handle RETRIES QueryAndValidatePlan
        // (BuildGoal/Plan.goal:26), the parent-scope %messages% variable resolves to null on
        // the retry (it's fine on the first call), so `action.Messages.Value!.Count` NRE'd
        // and crashed the whole build with a bare NullReferenceException. Treat null like
        // empty → clean ValidationError instead of a crash.
        // TODO(coder): real fix is in the retry/scope handling — a sub-goal's access to a
        // parent-scope variable should survive an error.handle retry (or pass %messages% as
        // a goal.call parameter to QueryAndValidatePlan so it re-binds each attempt). See
        // .bot/type-kind-strict/builder/v2/baseline-findings.md.
        // The .NET edge: the message list lowers ITSELF to the API's CLR shape.
        var rawMessages = global::app.type.item.@this.Lower<List<LlmMessage>>(await action.Messages.Value());
        if (rawMessages is not { Count: > 0 })
            return context.Error(new ActionError("Messages list is empty or null", "ValidationError", 400));

        // --- Build messages ---
        var messages = CloneMessages(rawMessages);
        string? schema;

        // Serialize Schema at the LLM boundary. Schema is `Data<object>?` because the
        // builder LLM may store it as a structured value (dict/list from a JSON
        // literal in .goal source) or as a free-form string (YAML/XML/prose). The LLM
        // expects text — JSON-serialize structured values, pass text through as-is.
        // An ABSENT/empty schema slot is "no schema" — asked via the binding's
        // IsEmpty (the value door hands the absent/null citizen, never C# null).
        async System.Threading.Tasks.Task<string?> SchemaOf(query a)
        {
            if (a.Schema == null || await a.Schema.IsEmpty()) return null;
            return (await a.Schema.Value()) switch
            {
                global::app.type.text.@this t => t.ToString(),
                { } v => System.Text.Json.JsonSerializer.Serialize(v, v.GetType()),
                _ => null,
            };
        }

        // Format slot — absent/empty is "no explicit format" (the door hands
        // the absent citizen, never C# null), so the schema-implies-json
        // fallback can fire.
        async System.Threading.Tasks.Task<string?> FormatOf(query a)
            => a.Format == null || await a.Format.IsEmpty() ? null : (await a.Format.Value())?.ToString();

        if (await action.ContinuePreviousConversation.ToBooleanAsync())
        {
            var prev = context.Get<List<LlmMessage>>(ConversationKey);
            if (prev != null)
                messages.InsertRange(0, prev);

            schema = await SchemaOf(action) ?? context.Get<string>(SchemaKey);
        }
        else
        {
            schema = await SchemaOf(action);
            context.Set<List<LlmMessage>>(ConversationKey, null!);
            context.Set<string>(SchemaKey, null!);
        }

        // Snapshot originals BEFORE format mutation
        var originalMessages = CloneMessages(messages);

        // Append format/schema instruction
        var formatInstruction = BuildFormatInstruction(await FormatOf(action), schema);
        if (formatInstruction != null)
        {
            var systemMsg = messages.Find(m => m.Role == "system");
            if (systemMsg != null)
                systemMsg.Content += "\n" + formatInstruction;
            else
                messages.Insert(0, new LlmMessage { Role = "system", Content = formatInstruction });
        }

        OnBeforeRequest?.Invoke(messages, schema);

        // --- Cache check ---
        // A build with caching disabled (--build={"cache":false}) bypasses the LLM
        // cache for EVERY query, not only the ones that thread cache=%!build.cache%.
        // The build-wide flag is authoritative; relying on each builder goal to pass
        // the per-call param is fragile (most don't), so the override lives here.
        // Gating cacheKey also skips the write below (guarded by cacheKey != null),
        // so cache:false is a full bypass: no read, no stale entry left behind.
        // TODO(build-mode-inversion): llm sniffs build mode from a foreign layer — invert
        // (build-born sets cache-off as config the llm reads via action.Cache) (plan §6.D).
        var build = app.Build;
        var buildCacheOff = build != null && !build.Cache;
        // The .NET edge: the tool list lowers itself once; reused below.
        List<GoalCall>? goalTools = action.Tools == null ? null
            : global::app.type.item.@this.Lower<List<GoalCall>>(await action.Tools.Value());
        string? cacheKey = null;
        if (await action.Cache.ToBooleanAsync() && goalTools == null && !buildCacheOff)
        {
            cacheKey = ComputeCacheKey(messages, model, (await action.Temperature.Value())!.ToDouble(), schema, await FormatOf(action));
            var cached = await settings.Get<global::app.type.item.@this>(CacheTable, cacheKey);
            // A missing key returns the null citizen (Ok(null) → Peek is null.this),
            // which is a real instance — test .IsNull, not a C# != null reference check.
            if (cached.Success && cached.Peek() is { IsNull: false })
            {
                return RestoreFromCache(cached);
            }
        }

        // --- Build tools for API ---
        List<object>? apiTools = null;
        if (goalTools is { Count: > 0 })
        {
            apiTools = goalTools.Select(t => (object)new Dictionary<string, object>
            {
                ["type"] = "function",
                ["function"] = new Dictionary<string, object?>
                {
                    ["name"] = t.Name,
                    ["description"] = "",
                    ["parameters"] = BuildParamSchema(t.Parameters)
                }
            }).ToList();
        }

        // --- Tracking ---
        int toolCallCount = 0;
        int validationRetries = 0;
        string? lastContent = null;
        int totalPromptTokens = 0;
        int totalCompletionTokens = 0;
        int totalCachedTokens = 0;
        decimal? totalCost = null;
        bool unknownModelLogged = false;

        while (true)
        {
            // --- Build request body ---
            var body = new Dictionary<string, object?>
            {
                ["model"] = model,
                ["messages"] = ToApiMessages(messages, app, context),
                ["temperature"] = (await action.Temperature.Value())!.ToDouble(),
                ["max_completion_tokens"] = (await action.MaxTokens.Value())!.ToInt64()
            };
            if ((action.TopP == null ? null : await action.TopP.Value()) != null)
                body["top_p"] = (await action.TopP.Value())!.ToDouble();
            if (apiTools != null)
                body["tools"] = apiTools;
            if ((action.OnStream == null ? null : await action.OnStream.Value()) != null)
                body["stream"] = true;

            // Remove null entries
            body = body.Where(kv => kv.Value != null).ToDictionary(kv => kv.Key, kv => kv.Value);

            // --- HTTP request via http module ---
            var headers = new Dictionary<string, object>();
            if (!string.IsNullOrEmpty(apiKey))
                headers["Authorization"] = $"Bearer {apiKey}";

            var httpAction = new request(context)
            {
                Url = new data.@this<global::app.type.text.@this>("", endpoint),
                Method = new data.@this<global::app.type.choice.@this<PlangHttpMethod>>("", PlangHttpMethod.POST),
                Body = new data.@this("", body, context: context),
                Headers = new data.@this<global::app.type.dict.@this>("", global::app.type.dict.@this.FromRaw(headers, context)),
                Unsigned = new data.@this<global::app.type.@bool.@this>("", true),
                TimeoutInSec = new data.@this<global::app.type.number.@this>("", 120),
                OnStream = action.OnStream,
                StreamAs = (action.OnStream == null ? null : await action.OnStream.Value()) != null ? new data.@this<global::app.type.choice.@this<StreamFormat>>("", StreamFormat.SSE) : default
            };

            data.@this httpResult = await app.Run(httpAction, context);
            if ((action.OnStream == null ? null : await action.OnStream.Value()) != null)
            {
                // TODO: streaming tool call accumulation needs work
                // For now, streaming returns the accumulated result via the callback
                break;
            }

            if (!httpResult.Success)
                return httpResult;

            // --- Parse response: navigate the channel-deserialized body (a dict);
            // no JsonElement. http returns plain Data whose lazy value materializes
            // (json → plang dict) through the reader; Value<dict> hands it typed.
            var dict = await httpResult.Value<dict>();
            if (dict == null)
                return context.Error(new ActionError("LLM response was not an object", "EmptyResponse", 500));

            // Usage / cost. (Token counters + cost math stay CLR/decimal here for now
            // — the class-wide native-plang-types migration is a tracked follow-up;
            // see .bot/compare-redesign/coder/native-plang-types-migration.md.)
            if (dict.Get<item>("usage") != null)
            {
                number callPrompt     = dict.Get<number>("usage.prompt_tokens") ?? 0;
                number callCompletion = dict.Get<number>("usage.completion_tokens") ?? 0;
                number callCached     = dict.Get<number>("usage.prompt_tokens_details.cached_tokens") ?? 0;

                totalPromptTokens     += callPrompt.ToInt32();
                totalCompletionTokens += callCompletion.ToInt32();
                totalCachedTokens     += callCached.ToInt32();

                // Cost: prompt_tokens includes the cached portion, so bill cached
                // separately and subtract it from the non-cached input bucket.
                var price = PriceFor(model);
                if (price is { } p)
                {
                    int nonCachedInput = Math.Max(0, callPrompt.ToInt32() - callCached.ToInt32());
                    totalCost = (totalCost ?? 0m)
                        + (decimal)nonCachedInput            * p.input  / 1_000_000m
                        + (decimal)callCached.ToInt32()      * p.cached / 1_000_000m
                        + (decimal)callCompletion.ToInt32()  * p.output / 1_000_000m;
                }
                else if (!unknownModelLogged)
                {
                    unknownModelLogged = true;
                    await (context.App.Debug?.Write($"llm.query: no pricing entry for model {model}, cost not computed") ?? Task.CompletedTask);
                }
            }

            // First choice
            if (dict.Get<item>("choices[0]") == null)
                return context.Error(new ActionError("No choices in LLM response", "EmptyResponse", 500));

            string? content = dict.Get<text>("choices[0].message.content")?.ToString();

            // Check finish_reason BEFORE parsing content — a truncated response
            // is not a JSON bug, it's the model running out of output budget. The
            // same goes for content_filter (model refused) and other non-"stop"
            // terminations. Surface these as dedicated errors so callers don't
            // waste time parsing incomplete JSON.
            var finishReason = dict.Get<text>("choices[0].finish_reason")?.ToString();
            var isTerminal = finishReason != null
                && finishReason != "stop"
                && finishReason != "tool_calls";
            if (isTerminal)
            {
                var key = finishReason switch
                {
                    "length" => "ResponseTruncated",
                    "content_filter" => "ResponseFiltered",
                    _ => "ResponseIncomplete"
                };
                var msg = finishReason == "length"
                    ? $"LLM output hit the max-tokens limit before finishing ({totalCompletionTokens} completion tokens). Raise MaxTokens or shorten the prompt."
                    : finishReason == "content_filter"
                    ? "LLM refused the request via content filter."
                    : $"LLM response ended abnormally (finish_reason={finishReason}).";
                return context.Error(new ActionError(msg, key, 400)
                {
                    Details = new Dictionary<string, object?>
                    {
                        ["FinishReason"] = finishReason,
                        ["RawResponse"] = content,
                        ["Model"] = model,
                        ["PromptTokens"] = totalPromptTokens,
                        ["CompletionTokens"] = totalCompletionTokens,
                        ["MaxTokens"] = (action.MaxTokens == null ? null : await action.MaxTokens.Value())
                    }
                });
            }

            // --- Tool calls? ---  just more of the dict.
            var toolCallsValue = dict.Get<global::app.type.list.@this>("choices[0].message.tool_calls");
            if (toolCallsValue != null && toolCallsValue.Count.ToInt32() > 0)
            {
                if (toolCallCount >= (await action.MaxToolCalls.Value())!.ToInt64())
                    break; // hit limit

                lastContent = content;
                var toolCalls = ParseToolCalls(dict);

                // Slice to remaining budget — never execute more tools than the limit allows
                int remaining = (await action.MaxToolCalls.Value())!.ToInt32() - toolCallCount;
                if (toolCalls.Count > remaining)
                    toolCalls = toolCalls.Take(remaining).ToList();

                // Append assistant message with tool_calls to conversation
                messages.Add(new LlmMessage
                {
                    Role = "assistant",
                    Content = content,
                    ToolCalls = toolCalls
                });

                // Determine parallel execution
                bool allParallel = toolCalls.All(tc =>
                    goalTools?.Find(t => t.Name == tc.Name)?.Parallel == true);

                // Execute tools
                List<string> results;
                if (allParallel && toolCalls.Count > 1)
                {
                    var tasks = toolCalls.Select(tc => ExecuteToolAsync(action, tc));
                    results = (await Task.WhenAll(tasks)).ToList();
                }
                else
                {
                    results = new List<string>();
                    foreach (var tc in toolCalls)
                    {
                        results.Add(await ExecuteToolAsync(action, tc));
                    }
                }

                // Append tool results
                for (int i = 0; i < toolCalls.Count; i++)
                {
                    messages.Add(new LlmMessage
                    {
                        Role = "tool",
                        ToolCallId = toolCalls[i].Id,
                        Content = results[i]
                    });
                    toolCallCount++;
                }

                continue; // re-query with tool results
            }

            // --- No tool calls — content response ---
            var rawResponse = content ?? "";

            // --- Format extraction ---
            var effectiveFormat = await FormatOf(action) ?? (schema != null ? "json" : null);
            var extracted = ExtractResponse(rawResponse, effectiveFormat);

            // --- JSON validation ---
            if (effectiveFormat == "json")
            {
                var parsed = TryParseJson(extracted);
                if (parsed == null)
                {
                    // Try extracting from code block
                    var fromBlock = ExtractJsonFromCodeBlock(rawResponse);
                    if (fromBlock != null)
                        parsed = TryParseJson(fromBlock);

                    if (parsed == null)
                        return context.Error(new ActionError(
                            "Response is not valid JSON", "JsonParseError", 400)
                        {
                            Details = new Dictionary<string, object?>
                            {
                                ["RawResponse"] = rawResponse,
                                ["Model"] = model,
                                ["Schema"] = schema
                            }
                        });

                    extracted = fromBlock!;
                }
            }

            // --- Custom validation ---
            if ((action.OnValidateResponse == null ? null : await action.OnValidateResponse.Value()) != null)
            {
                var validationCall = new GoalCall
                {
                    Name = ((await action.OnValidateResponse.Value()) as global::app.goal.GoalCall)!.Name,
                    PrPath = ((await action.OnValidateResponse.Value()) as global::app.goal.GoalCall)!.PrPath,
                    Parameters = new List<data.@this> { new data.@this("response", extracted, context: context) }
                };
                var validationResult = await app.RunGoalAsync(validationCall, context);

                if (!validationResult.Success)
                {
                    var validationError = validationResult.Error?.Message ?? "Unknown validation error";

                    if (validationRetries >= (await action.MaxValidationRetries.Value())!.ToInt64())
                    {
                        await app.CurrentActor.Channel.WriteTextAsync(global::app.channel.list.@this.Output,
                            $"  Validation failed (no retries left): {validationError}{Environment.NewLine}");
                        return context.Error(new ActionError(
                            $"LLM validation failed: {validationError}",
                            "ValidationFailed", 400));
                    }

                    validationRetries++;
                    await app.CurrentActor.Channel.WriteTextAsync(global::app.channel.list.@this.Output,
                        $"  Validation failed (retry {validationRetries}/{(await action.MaxValidationRetries.Value())}): {validationError}{Environment.NewLine}");
                    messages.Add(new LlmMessage
                    {
                        Role = "user",
                        Content = "Your response failed validation: "
                               + validationError
                               + "\nPlease fix and try again."
                    });
                    continue; // re-query
                }
            }

            // --- Store conversation for continuity (pre-mutation originals) ---
            originalMessages.Add(new LlmMessage { Role = "assistant", Content = rawResponse });
            context.Set(ConversationKey, originalMessages);
            context.Set(SchemaKey, schema);

            OnAfterResponse?.Invoke(rawResponse);

            // --- Build result ---
            object? resultValue = effectiveFormat == "json" ? TryParseJson(extracted) : (object?)extracted;
            var result = context.Ok(resultValue);

            // --- Cache store ---
            // Properties are [JsonIgnore] on Data, so store metadata as the value itself
            if (cacheKey != null)
            {
                var cacheEntry = new Dictionary<string, object?>
                {
                    // Store the UNWRAPPED native value (result.Value), not the raw
                    // JsonElement: a JsonElement does not survive the cache's disk
                    // serialization — Normalize reflects it to {"valuekind":"Object"},
                    // losing all content, so every cached JSON response would restore
                    // empty. Data.Ok already materialized resultValue into a native
                    // dict/list (or scalar) that serializes round-trip.
                    ["Value"] = result.Peek(),
                    ["RawResponse"] = rawResponse,
                    ["Model"] = model,
                    ["PromptTokens"] = totalPromptTokens,
                    ["CompletionTokens"] = totalCompletionTokens,
                    ["TotalTokens"] = totalPromptTokens + totalCompletionTokens,
                    ["CachedTokens"] = totalCachedTokens,
                    ["Cost"] = totalCost,
                    ["ToolCallCount"] = toolCallCount,
                    ["ValidationRetries"] = validationRetries,
                    ["Format"] = effectiveFormat,
                    ["Schema"] = schema
                };
                await settings.Set(CacheTable, cacheKey, new data.@this("cache", cacheEntry, context: context));
            }

            // --- Populate response properties ---
            SetProp(result, "RawResponse", rawResponse);
            SetProp(result, "Model", model);
            SetProp(result, "Messages", messages);
            SetProp(result, "Temperature", (await action.Temperature.Value()));
            SetProp(result, "MaxTokens", (await action.MaxTokens.Value()));
            SetProp(result, "Cached", false);
            SetProp(result, "PromptTokens", totalPromptTokens);
            SetProp(result, "CompletionTokens", totalCompletionTokens);
            SetProp(result, "TotalTokens", totalPromptTokens + totalCompletionTokens);
            SetProp(result, "CachedTokens", totalCachedTokens);
            SetProp(result, "Cost", totalCost);
            SetProp(result, "ToolCallCount", toolCallCount);
            SetProp(result, "ValidationRetries", validationRetries);
            SetProp(result, "Format", effectiveFormat);
            SetProp(result, "Schema", schema);

            return result;
        }

        // Loop exited via break (MaxToolCalls or streaming)
        var exitResult = context.Ok(lastContent);
        SetProp(exitResult, "Model", model);
        SetProp(exitResult, "ToolCallCount", toolCallCount);
        SetProp(exitResult, "PromptTokens", totalPromptTokens);
        SetProp(exitResult, "CompletionTokens", totalCompletionTokens);
        SetProp(exitResult, "TotalTokens", totalPromptTokens + totalCompletionTokens);
        SetProp(exitResult, "CachedTokens", totalCachedTokens);
        SetProp(exitResult, "Cost", totalCost);
        SetProp(exitResult, "Truncated", true);
        return exitResult;
    }

    // --- Tool execution ---

    private static async Task<string> ExecuteToolAsync(query action, ToolCall toolCall)
    {
        var app = action.Context.App;
        var context = action.Context;

        // OnToolCall — starting
        if ((action.OnToolCall == null ? null : await action.OnToolCall.Value()) != null)
        {
            var startCall = new GoalCall
            {
                Name = ((await action.OnToolCall.Value()) as global::app.goal.GoalCall)!.Name,
                PrPath = ((await action.OnToolCall.Value()) as global::app.goal.GoalCall)!.PrPath,
                Parameters = new List<data.@this>
                {
                    new data.@this("name", toolCall.Name, context: context),
                    new data.@this("arguments", toolCall.Arguments, context: context),
                    new data.@this("status", "starting", context: context)
                }
            };
            await app.RunGoalAsync(startCall, context);
        }

        string result;
        var goalCall = (action.Tools == null ? null
                : global::app.type.item.@this.Lower<List<GoalCall>>(await action.Tools.Value()))
            ?.Find(t => t.Name == toolCall.Name);
        if (goalCall == null)
        {
            result = $"Error: unknown tool '{toolCall.Name}'";
        }
        else
        {
            // Parse arguments and build GoalCall
            var parameters = ParseToolArguments(toolCall.Arguments, goalCall.Parameters, context);
            var parseError = parameters.Find(p => !p.Success);
            if (parseError != null)
            {
                result = "Error: " + (parseError.Error?.Message ?? "Failed to parse tool arguments");
            }
            else
            {
                var execCall = new GoalCall
                {
                    Name = goalCall.Name,
                    PrPath = goalCall.PrPath,
                    Parameters = parameters
                };
                var goalResult = await app.RunGoalAsync(execCall, context);

                if (goalResult.Success)
                    result = !goalResult.Peek().IsNull ? JsonSerializer.Serialize(goalResult.Peek()) : "";
                else
                    result = "Error: " + (goalResult.Error?.Message ?? "Unknown error");
            }
        }

        // OnToolCall — completed
        if ((action.OnToolCall == null ? null : await action.OnToolCall.Value()) != null)
        {
            var endCall = new GoalCall
            {
                Name = ((await action.OnToolCall.Value()) as global::app.goal.GoalCall)!.Name,
                PrPath = ((await action.OnToolCall.Value()) as global::app.goal.GoalCall)!.PrPath,
                Parameters = new List<data.@this>
                {
                    new data.@this("name", toolCall.Name, context: context),
                    new data.@this("arguments", toolCall.Arguments, context: context),
                    new data.@this("result", result, context: context),
                    new data.@this("status", "completed", context: context)
                }
            };
            await app.RunGoalAsync(endCall, context);
        }

        return result;
    }

    /// <summary>
    /// Parses the LLM's JSON arguments string into List&lt;Data&gt; matching the GoalCall's parameter definitions.
    /// </summary>
    private static List<data.@this> ParseToolArguments(string argumentsJson, List<data.@this>? parameterDefs, actor.context.@this context)
    {
        var result = new List<data.@this>();

        if (string.IsNullOrEmpty(argumentsJson))
            return result;

        try
        {
            using var doc = JsonDocument.Parse(argumentsJson);
            foreach (var prop in doc.RootElement.EnumerateObject())
            {
                var paramDef = parameterDefs?.Find(p => p.Name == prop.Name);
                object? value = prop.Value.ValueKind switch
                {
                    JsonValueKind.String => prop.Value.GetString(),
                    JsonValueKind.Number => prop.Value.TryGetInt64(out var l) ? l : prop.Value.GetDouble(),
                    JsonValueKind.True => true,
                    JsonValueKind.False => false,
                    JsonValueKind.Null => null,
                    _ => prop.Value.GetRawText()
                };
                result.Add(new data.@this(prop.Name, value, context: context));
            }
        }
        catch (JsonException ex)
        {
            // Return error Data so the caller sees the parse failure with full exception
            return new List<data.@this>
            {
                context.Error(ActionError.FromException(ex, "JsonParseError", 400))
            };
        }

        // Fill in defaults for parameters not provided by the LLM
        if (parameterDefs != null)
        {
            foreach (var def in parameterDefs)
            {
                if (!result.Any(r => r.Name == def.Name) && !def.Peek().IsNull)
                    result.Add(new data.@this(def.Name, def.Peek(), context: context));
            }
        }

        return result;
    }

    // --- Message formatting ---

    private static List<object> ToApiMessages(List<LlmMessage> messages, global::app.@this app, actor.context.@this context)
    {
        var result = new List<object>();
        foreach (var msg in messages)
        {
            if (msg.Role == "tool")
            {
                result.Add(new Dictionary<string, object?>
                {
                    ["role"] = "tool",
                    ["content"] = msg.Content ?? "",
                    ["tool_call_id"] = msg.ToolCallId
                });
                continue;
            }

            if (msg.ToolCalls != null && msg.ToolCalls.Count > 0)
            {
                // Assistant message with tool calls
                var apiMsg = new Dictionary<string, object?>
                {
                    ["role"] = "assistant",
                    ["content"] = msg.Content,
                    ["tool_calls"] = msg.ToolCalls.Select(tc => new Dictionary<string, object>
                    {
                        ["id"] = tc.Id,
                        ["type"] = "function",
                        ["function"] = new Dictionary<string, string>
                        {
                            ["name"] = tc.Name,
                            ["arguments"] = tc.Arguments
                        }
                    }).ToList()
                };
                result.Add(apiMsg);
                continue;
            }

            // Regular message — may have images
            if (msg.Images != null && msg.Images.Count > 0)
            {
                var contentParts = new List<object>();
                if (!string.IsNullOrEmpty(msg.Content))
                    contentParts.Add(new Dictionary<string, string> { ["type"] = "text", ["text"] = msg.Content });

                foreach (var image in msg.Images)
                {
                    var imageContent = ResolveImage(image, app, context);
                    contentParts.Add(imageContent);
                }

                result.Add(new Dictionary<string, object>
                {
                    ["role"] = msg.Role,
                    ["content"] = contentParts
                });
            }
            else
            {
                result.Add(new Dictionary<string, object?>
                {
                    ["role"] = msg.Role,
                    ["content"] = msg.Content
                });
            }
        }
        return result;
    }

    // internal so OpenAiImageDenialTests can invoke the handler directly
    // (the public Query path requires a real OpenAI HTTP setup).
    internal static object ResolveImage(string image, global::app.@this app, actor.context.@this context)
    {
        if (image.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
            || image.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            return new Dictionary<string, object>
            {
                ["type"] = "image_url",
                ["image_url"] = new Dictionary<string, string> { ["url"] = image }
            };
        }

        // Try file path — gated through path.ReadAsDataUri (D9a content-shape
        // verb). AuthGate(Read) fires inside; in-root fast-passes, out-of-root
        // surfaces as a permission prompt or denial. Sync-wait: message
        // formatting is sync and the bytes-then-encode pipeline is cheap.
        try
        {
            var imgPath = global::app.type.path.@this.Resolve(image, context);
            var dataUri = imgPath.ReadAsDataUri().GetAwaiter().GetResult();
            if (dataUri.Success && !string.IsNullOrEmpty(dataUri.Peek()?.ToString()))
            {
                return new Dictionary<string, object>
                {
                    ["type"] = "image_url",
                    ["image_url"] = new Dictionary<string, string>
                    {
                        ["url"] = dataUri.Peek()?.ToString()
                    }
                };
            }
        }
        catch (Exception ex) when (ex is not (NullReferenceException or OutOfMemoryException or StackOverflowException))
        {
            // Fall through to base64 assumption
        }

        // Assume base64
        return new Dictionary<string, object>
        {
            ["type"] = "image_url",
            ["image_url"] = new Dictionary<string, string>
            {
                ["url"] = image.StartsWith("data:") ? image : $"data:image/png;base64,{image}"
            }
        };
    }

    // --- Format handling ---

    private static string? BuildFormatInstruction(string? format, string? schema)
    {
        var effectiveFormat = format ?? (schema != null ? "json" : null);

        if (effectiveFormat == null)
            return null;

        if (effectiveFormat == "json" && schema != null)
            return $"You MUST respond in JSON, schema: {schema}";
        if (effectiveFormat == "json")
            return "You MUST respond in JSON";

        return $"You MUST respond in ```{effectiveFormat}``` code block";
    }

    private static string ExtractResponse(string content, string? format)
    {
        if (format == null || format == "json")
            return content;

        // Try format-specific code block
        var pattern = $"```{Regex.Escape(format)}\\n(.*?)\\n```";
        var match = Regex.Match(content, pattern, RegexOptions.Singleline);
        if (match.Success)
            return match.Groups[1].Value;

        // Fallback: any code block
        match = Regex.Match(content, "```\\n?(.*?)\\n?```", RegexOptions.Singleline);
        if (match.Success)
            return match.Groups[1].Value;

        return content;
    }

    /// <summary>
    /// Reproduces the live path's "raw response → result value" parse: strips a
    /// ```json/```fmt code fence when present and parses JSON to the element the
    /// caller wraps via <c>Data.Ok</c> (which materializes it to a native value).
    /// Shared by the live build and cache-restore so a restored result is
    /// byte-identical to a fresh one — the cache round-trips the plain
    /// <c>RawResponse</c> string, never a fragile parsed object.
    /// </summary>
    internal static object? ParseResultValue(string rawResponse, string? effectiveFormat)
    {
        var extracted = ExtractResponse(rawResponse, effectiveFormat);
        if (effectiveFormat != "json")
            return extracted;

        var parsed = TryParseJson(extracted);
        if (parsed == null)
        {
            var fromBlock = ExtractJsonFromCodeBlock(rawResponse);
            if (fromBlock != null)
                parsed = TryParseJson(fromBlock);
        }
        return parsed;
    }

    private static JsonElement? TryParseJson(string text)
    {
        try
        {
            using var doc = JsonDocument.Parse(text);
            return doc.RootElement.Clone();
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string? ExtractJsonFromCodeBlock(string content)
    {
        var match = Regex.Match(content, "```(?:json)?\\n?(.*?)\\n?```", RegexOptions.Singleline);
        return match.Success ? match.Groups[1].Value : null;
    }

    // --- Parameter schema ---

    private static Dictionary<string, object> BuildParamSchema(List<data.@this>? parameters)
    {
        if (parameters == null || parameters.Count == 0)
            return new Dictionary<string, object>
            {
                ["type"] = "object",
                ["properties"] = new Dictionary<string, object>()
            };

        var props = new Dictionary<string, object>();
        var required = new List<string>();

        foreach (var param in parameters)
        {
            props[param.Name] = new Dictionary<string, string>
            {
                ["type"] = MapPlangTypeToJsonSchema(param.Type?.Name, param.Type?.Kind)
            };
            if (param.Peek().IsNull)
                required.Add(param.Name);
        }

        var result = new Dictionary<string, object>
        {
            ["type"] = "object",
            ["properties"] = props
        };
        if (required.Count > 0)
            result["required"] = required;

        return result;
    }

    private static string MapPlangTypeToJsonSchema(string? typeName, string? kind = null)
    {
        // Post-Stage-2: typeName "number" with kind discriminates precision.
        // int/long → integer; decimal/double/float → number.
        if (string.Equals(typeName, "number", System.StringComparison.OrdinalIgnoreCase))
        {
            return kind?.ToLowerInvariant() switch
            {
                "int" or "long" => "integer",
                _ => "number"
            };
        }
        return typeName?.ToLowerInvariant() switch
        {
            "int" or "long" or "int32" or "int64" => "integer",
            "double" or "float" or "decimal" => "number",
            "bool" or "boolean" => "boolean",
            "list" or "array" => "array",
            "object" or "dictionary" => "object",
            _ => "string"
        };
    }

    // --- Caching ---

    private static string ComputeCacheKey(List<LlmMessage> messages, string model,
        double temperature, string? schema, string? format)
    {
        var sb = new StringBuilder();
        foreach (var msg in messages)
        {
            sb.Append(msg.Role).Append(':').Append(msg.Content ?? "").Append('|');
            if (msg.Images != null)
                foreach (var img in msg.Images)
                    sb.Append("img:").Append(img).Append('|');
        }
        sb.Append("model:").Append(model);
        sb.Append("temp:").Append(temperature);
        if (schema != null) sb.Append("schema:").Append(schema);
        if (format != null) sb.Append("format:").Append(format);

        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(sb.ToString()));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    // --- Config resolution ---

    private static async Task<string> ResolveConfigAsync(IStore settings, string settingKey,
        string? envVar, string? defaultValue)
    {
        // Try settings store. A missing key returns the null citizen (Peek is
        // null.this, not C# null) — test .IsNull, or a missing setting reads as the
        // literal string "null" and (e.g.) the endpoint becomes "null:443".
        var result = await settings.Get<global::app.type.item.@this>("LlmConfig", settingKey);
        if (result.Success && result.Peek() is { IsNull: false })
        {
            var val = (await result.Value()) is Clr { Value: data.@this d }
                ? (await d.Value())?.ToString()
                : (await result.Value())?.ToString();
            if (!string.IsNullOrEmpty(val)) return val;
        }

        // Try environment variable
        if (!string.IsNullOrEmpty(envVar))
        {
            var envVal = Environment.GetEnvironmentVariable(envVar);
            if (!string.IsNullOrEmpty(envVal)) return envVal;
        }

        return defaultValue ?? "";
    }

    // --- Response parsing ---

    // Tool calls are just part of the response dict — navigate them.
    private static List<ToolCall> ParseToolCalls(dict response)
    {
        var result = new List<ToolCall>();
        var arr = response.Get<global::app.type.list.@this>("choices[0].message.tool_calls");
        if (arr == null) return result;

        int count = arr.Count.ToInt32();
        for (int i = 0; i < count; i++)
        {
            var b = $"choices[0].message.tool_calls[{i}]";
            result.Add(new ToolCall
            {
                Id        = response.Get<text>($"{b}.id")?.ToString() ?? "",
                Name      = response.Get<text>($"{b}.function.name")?.ToString() ?? "",
                Arguments = response.Get<text>($"{b}.function.arguments")?.ToString() ?? ""
            });
        }
        return result;
    }

    // --- Helpers ---

    /// <summary>
    /// Restores a cached result from the SettingsStore.
    /// The cache stores metadata as a dictionary since Data.Properties is [JsonIgnore].
    /// </summary>
    private static data.@this RestoreFromCache(data.@this cached)
    {
        var cachedValue = cached.Peek();
        object? resultValue = null;
        var props = new Dictionary<string, object?>();

        // A json cache entry materializes to the native dict — navigate its
        // entries directly (Value + the metadata props), no raw copy.
        if (cachedValue is app.type.dict.@this nativeDict)
        {
            resultValue = nativeDict.Get("Value")?.Peek();
            foreach (var entry in nativeDict.Entries)
            {
                if (entry.Name == "Value") continue;
                props[entry.Name] = entry.Peek();
            }
        }
        else if (cachedValue is Clr { Value: JsonElement je } && je.ValueKind == JsonValueKind.Object)
        {
            if (je.TryGetProperty("Value", out var valProp))
                resultValue = valProp.ValueKind == JsonValueKind.Null ? null : valProp.Clone();

            foreach (var prop in je.EnumerateObject())
            {
                if (prop.Name == "Value") continue;
                props[prop.Name] = prop.Value.ValueKind switch
                {
                    JsonValueKind.String => prop.Value.GetString(),
                    JsonValueKind.Number => prop.Value.TryGetInt64(out var l) ? (object)l : prop.Value.GetDouble(),
                    JsonValueKind.True => true,
                    JsonValueKind.False => false,
                    JsonValueKind.Null => null,
                    _ => prop.Value.GetRawText()
                };
            }
        }
        else if (cachedValue is Clr { Value: Dictionary<string, object?> dict })
        {
            resultValue = dict.GetValueOrDefault("Value");
            foreach (var kvp in dict)
            {
                if (kvp.Key == "Value") continue;
                props[kvp.Key] = kvp.Value;
            }
        }
        else
        {
            resultValue = cachedValue;
        }

        // Authoritative reconstruction: re-parse the round-tripped RawResponse
        // string with the same logic the live path uses. The plain string
        // survives disk serialization losslessly, whereas a parsed JsonElement /
        // native value does not always round-trip its element shape — so trusting
        // the stored "Value" can yield a list/dict the consumer can't convert.
        // A dict-navigated prop rides as the native text value — read its backing
        // string; a legacy raw prop is already a string.
        static string? AsText(object? v) => v switch
        {
            global::app.type.text.@this t => t.Clr<string>(),
            string s => s,
            _ => null,
        };
        string? rawResp = AsText(props.GetValueOrDefault("RawResponse"));
        if (!string.IsNullOrEmpty(rawResp))
        {
            string? fmt = AsText(props.GetValueOrDefault("Format"));
            resultValue = ParseResultValue(rawResp, fmt) ?? resultValue;
        }

        var result = cached.Context.Ok(resultValue);
        SetProp(result, "Cached", true);
        foreach (var kvp in props)
            SetProp(result, kvp.Key, kvp.Value);
        return result;
    }

    private static void SetProp(data.@this data, string name, object? value)
    {
        data.Properties[name] = value;
    }

    private static List<LlmMessage> CloneMessages(List<LlmMessage> messages)
    {
        return messages.Select(m => new LlmMessage
        {
            Role = m.Role,
            Content = m.Content,
            Images = m.Images != null ? new List<string>(m.Images) : null,
            ToolCallId = m.ToolCallId,
            ToolCalls = m.ToolCalls?.Select(tc => new ToolCall
            {
                Id = tc.Id,
                Name = tc.Name,
                Arguments = tc.Arguments
            }).ToList()
        }).ToList();
    }

}
