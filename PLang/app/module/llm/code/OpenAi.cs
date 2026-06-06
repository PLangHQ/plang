using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using app.actor.context;
using app.error;
using app.goal;
using app.variable;
using app.module.settings;
using app.type.path;
using app.module.http;
using PlangHttpMethod = app.module.http.HttpMethod;

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

    public async Task<data.@this<object>> Query(query action)
    {
        var app = action.Context.App;
        var context = action.Context;
        var config = app.Config.For<http.Config>(context);

        // --- Config ---
        var settings = app.SettingsStore;
        var endpoint = await ResolveConfigAsync(settings, "llm.endpoint", "OPENAI_API_ENDPOINT",
            "https://api.openai.com/v1/chat/completions");
        var apiKey = await ResolveConfigAsync(settings, "llm.apiKey", "OPENAI_API_KEY", null);
        var model = string.IsNullOrWhiteSpace(action.Model?.Value) ? null : action.Model.Value;
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
        if (action.Messages.Value is not { Count: > 0 })
            return global::app.data.@this<object>.FromError(new ActionError("Messages list is empty or null", "ValidationError", 400));

        // --- Build messages ---
        var messages = CloneMessages(action.Messages.Value!);
        string? schema;

        // Serialize Schema at the LLM boundary. Schema is `Data<object>?` because the
        // builder LLM may store it as a structured value (Dictionary/List from a JSON
        // literal in .goal source) or as a free-form string (YAML/XML/prose). The LLM
        // expects text — JSON-serialize structured values, pass strings through as-is.
        static string? SerializeSchema(object? raw)
        {
            return raw switch
            {
                null => null,
                string s => s,
                _ => System.Text.Json.JsonSerializer.Serialize(raw)
            };
        }

        if (action.ContinuePreviousConversation.Value)
        {
            var prev = context.Get<List<LlmMessage>>(ConversationKey);
            if (prev != null)
                messages.InsertRange(0, prev);

            schema = SerializeSchema(action.Schema?.Value) ?? context.Get<string>(SchemaKey);
        }
        else
        {
            schema = SerializeSchema(action.Schema?.Value);
            context.Set<List<LlmMessage>>(ConversationKey, null!);
            context.Set<string>(SchemaKey, null!);
        }

        // Snapshot originals BEFORE format mutation
        var originalMessages = CloneMessages(messages);

        // Append format/schema instruction
        var formatInstruction = BuildFormatInstruction(action.Format?.Value, schema);
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
        var buildCacheOff = app.Builder.IsEnabled && !app.Builder.Cache;
        string? cacheKey = null;
        if (action.Cache.Value && action.Tools?.Value == null && !buildCacheOff)
        {
            cacheKey = ComputeCacheKey(messages, model, action.Temperature.GetValue<double>(), schema, action.Format?.Value);
            var cached = await settings.Get(CacheTable, cacheKey);
            if (cached.Success && cached.Value != null)
            {
                return global::app.data.@this<object>.From(RestoreFromCache(cached));
            }
        }

        // --- Build tools for API ---
        List<object>? apiTools = null;
        if (action.Tools?.Value != null && action.Tools.Value.Count > 0)
        {
            apiTools = action.Tools.Value.Select(t => (object)new Dictionary<string, object>
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
                ["temperature"] = action.Temperature.GetValue<double>(),
                ["max_completion_tokens"] = action.MaxTokens.GetValue<long>()
            };
            if (action.TopP?.Value != null)
                body["top_p"] = action.TopP.GetValue<double>();
            if (apiTools != null)
                body["tools"] = apiTools;
            if (action.OnStream?.Value != null)
                body["stream"] = true;

            // Remove null entries
            body = body.Where(kv => kv.Value != null).ToDictionary(kv => kv.Key, kv => kv.Value);

            // --- HTTP request via http module ---
            var headers = new Dictionary<string, object>();
            if (!string.IsNullOrEmpty(apiKey))
                headers["Authorization"] = $"Bearer {apiKey}";

            var httpAction = new request
            {
                Context = context,
                Url = new data.@this<global::app.type.text.@this>("", endpoint),
                Method = new data.@this<PlangHttpMethod>("", PlangHttpMethod.POST),
                Body = new data.@this("", body),
                Headers = new data.@this<Dictionary<string, object>>("", headers),
                Unsigned = new data.@this<global::app.type.@bool.@this>("", true),
                TimeoutInSec = new data.@this<global::app.type.number.@this>("", 120),
                OnStream = action.OnStream,
                StreamAs = action.OnStream?.Value != null ? new data.@this<StreamFormat>("", StreamFormat.SSE) : default
            };

            data.@this httpResult = await app.RunAction(httpAction, context);
            if (action.OnStream?.Value != null)
            {
                // TODO: streaming tool call accumulation needs work
                // For now, streaming returns the accumulated result via the callback
                break;
            }

            if (!httpResult.Success)
                return global::app.data.@this<object>.From(httpResult);

            // --- Parse response ---
            // http.request returns plain Data with the body as its lazy value
            // (http.response dissolved). Touching .Value materializes the body
            // (json → object) through the reader.
            var responseBody = httpResult.Value;
            var (responseJson, parseEx) = ParseApiResponse(responseBody);
            if (responseJson == null)
            {
                if (parseEx != null)
                    return global::app.data.@this<object>.FromError(ActionError.FromException(parseEx, "ParseError", 500));
                return global::app.data.@this<object>.FromError(new ActionError("Failed to parse LLM API response", "ParseError", 500));
            }

            // Extract usage
            var usage = responseJson.Value.TryGetProperty("usage", out var usageProp) ? usageProp : (JsonElement?)null;
            if (usage != null)
            {
                int callPrompt     = usage.Value.TryGetProperty("prompt_tokens",     out var pt) ? pt.GetInt32() : 0;
                int callCompletion = usage.Value.TryGetProperty("completion_tokens", out var ct) ? ct.GetInt32() : 0;
                int callCached     = 0;
                if (usage.Value.TryGetProperty("prompt_tokens_details", out var ptd)
                 && ptd.TryGetProperty("cached_tokens", out var ctok)
                 && ctok.ValueKind == JsonValueKind.Number)
                    callCached = ctok.GetInt32();

                totalPromptTokens     += callPrompt;
                totalCompletionTokens += callCompletion;
                totalCachedTokens     += callCached;

                // Cost: prompt_tokens includes the cached portion, so bill cached
                // separately and subtract it from the non-cached input bucket.
                var price = PriceFor(model);
                if (price is { } p)
                {
                    int nonCachedInput = Math.Max(0, callPrompt - callCached);
                    totalCost = (totalCost ?? 0m)
                        + (decimal)nonCachedInput   * p.input  / 1_000_000m
                        + (decimal)callCached       * p.cached / 1_000_000m
                        + (decimal)callCompletion   * p.output / 1_000_000m;
                }
                else if (!unknownModelLogged)
                {
                    unknownModelLogged = true;
                    await context.App.Debug.Write($"llm.query: no pricing entry for model {model}, cost not computed");
                }
            }

            // Get first choice
            if (!responseJson.Value.TryGetProperty("choices", out var choices) || choices.GetArrayLength() == 0)
                return global::app.data.@this<object>.FromError(new ActionError("No choices in LLM response", "EmptyResponse", 500));

            var choice = choices[0];
            var message = choice.GetProperty("message");
            var content = message.TryGetProperty("content", out var contentProp) && contentProp.ValueKind != JsonValueKind.Null
                ? contentProp.GetString() : null;

            // Check finish_reason BEFORE parsing content — a truncated response
            // is not a JSON bug, it's the model running out of output budget. The
            // same goes for content_filter (model refused) and other non-"stop"
            // terminations. Surface these as dedicated errors so callers don't
            // waste time parsing incomplete JSON.
            var finishReason = choice.TryGetProperty("finish_reason", out var frProp)
                && frProp.ValueKind == JsonValueKind.String
                ? frProp.GetString() : null;
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
                return global::app.data.@this<object>.FromError(new ActionError(msg, key, 400)
                {
                    Details = new Dictionary<string, object?>
                    {
                        ["FinishReason"] = finishReason,
                        ["RawResponse"] = content,
                        ["Model"] = model,
                        ["PromptTokens"] = totalPromptTokens,
                        ["CompletionTokens"] = totalCompletionTokens,
                        ["MaxTokens"] = action.MaxTokens?.Value
                    }
                });
            }

            // --- Tool calls? ---
            if (message.TryGetProperty("tool_calls", out var toolCallsProp) && toolCallsProp.GetArrayLength() > 0)
            {
                if (toolCallCount >= action.MaxToolCalls.GetValue<long>())
                    break; // hit limit

                lastContent = content;
                var toolCalls = ParseToolCalls(toolCallsProp);

                // Slice to remaining budget — never execute more tools than the limit allows
                int remaining = action.MaxToolCalls.GetValue<int>() - toolCallCount;
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
                    action.Tools?.Value?.Find(t => t.Name == tc.Name)?.Parallel == true);

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
            var effectiveFormat = action.Format?.Value ?? (schema != null ? "json" : null);
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
                        return global::app.data.@this<object>.FromError(new ActionError(
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
            if (action.OnValidateResponse?.Value != null)
            {
                var validationCall = new GoalCall
                {
                    Name = action.OnValidateResponse.Value.Name,
                    PrPath = action.OnValidateResponse.Value.PrPath,
                    Parameters = new List<data.@this> { new data.@this("response", extracted) }
                };
                var validationResult = await app.RunGoalAsync(validationCall, context);

                if (!validationResult.Success)
                {
                    var validationError = validationResult.Error?.Message ?? "Unknown validation error";

                    if (validationRetries >= action.MaxValidationRetries.GetValue<long>())
                    {
                        await app.CurrentActor.Channel.WriteTextAsync(global::app.channel.list.@this.Output,
                            $"  Validation failed (no retries left): {validationError}{Environment.NewLine}");
                        return global::app.data.@this<object>.FromError(new ActionError(
                            $"LLM validation failed: {validationError}",
                            "ValidationFailed", 400));
                    }

                    validationRetries++;
                    await app.CurrentActor.Channel.WriteTextAsync(global::app.channel.list.@this.Output,
                        $"  Validation failed (retry {validationRetries}/{action.MaxValidationRetries.Value}): {validationError}{Environment.NewLine}");
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
            var result = global::app.data.@this.Ok(resultValue);

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
                    ["Value"] = result.Value,
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
                await settings.Set(CacheTable, cacheKey, new data.@this("cache", cacheEntry));
            }

            // --- Populate response properties ---
            SetProp(result, "RawResponse", rawResponse);
            SetProp(result, "Model", model);
            SetProp(result, "Messages", messages);
            SetProp(result, "Temperature", action.Temperature.Value);
            SetProp(result, "MaxTokens", action.MaxTokens.Value);
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

            return global::app.data.@this<object>.From(result);
        }

        // Loop exited via break (MaxToolCalls or streaming)
        var exitResult = global::app.data.@this.Ok(lastContent);
        SetProp(exitResult, "Model", model);
        SetProp(exitResult, "ToolCallCount", toolCallCount);
        SetProp(exitResult, "PromptTokens", totalPromptTokens);
        SetProp(exitResult, "CompletionTokens", totalCompletionTokens);
        SetProp(exitResult, "TotalTokens", totalPromptTokens + totalCompletionTokens);
        SetProp(exitResult, "CachedTokens", totalCachedTokens);
        SetProp(exitResult, "Cost", totalCost);
        SetProp(exitResult, "Truncated", true);
        return global::app.data.@this<object>.From(exitResult);
    }

    // --- Tool execution ---

    private static async Task<string> ExecuteToolAsync(query action, ToolCall toolCall)
    {
        var app = action.Context.App;
        var context = action.Context;

        // OnToolCall — starting
        if (action.OnToolCall?.Value != null)
        {
            var startCall = new GoalCall
            {
                Name = action.OnToolCall.Value.Name,
                PrPath = action.OnToolCall.Value.PrPath,
                Parameters = new List<data.@this>
                {
                    new data.@this("name", toolCall.Name),
                    new data.@this("arguments", toolCall.Arguments),
                    new data.@this("status", "starting")
                }
            };
            await app.RunGoalAsync(startCall, context);
        }

        string result;
        var goalCall = action.Tools?.Value?.Find(t => t.Name == toolCall.Name);
        if (goalCall == null)
        {
            result = $"Error: unknown tool '{toolCall.Name}'";
        }
        else
        {
            // Parse arguments and build GoalCall
            var parameters = ParseToolArguments(toolCall.Arguments, goalCall.Parameters);
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
                    result = goalResult.Value != null ? JsonSerializer.Serialize(goalResult.Value) : "";
                else
                    result = "Error: " + (goalResult.Error?.Message ?? "Unknown error");
            }
        }

        // OnToolCall — completed
        if (action.OnToolCall?.Value != null)
        {
            var endCall = new GoalCall
            {
                Name = action.OnToolCall.Value.Name,
                PrPath = action.OnToolCall.Value.PrPath,
                Parameters = new List<data.@this>
                {
                    new data.@this("name", toolCall.Name),
                    new data.@this("arguments", toolCall.Arguments),
                    new data.@this("result", result),
                    new data.@this("status", "completed")
                }
            };
            await app.RunGoalAsync(endCall, context);
        }

        return result;
    }

    /// <summary>
    /// Parses the LLM's JSON arguments string into List&lt;Data&gt; matching the GoalCall's parameter definitions.
    /// </summary>
    private static List<data.@this> ParseToolArguments(string argumentsJson, List<data.@this>? parameterDefs)
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
                result.Add(new data.@this(prop.Name, value));
            }
        }
        catch (JsonException ex)
        {
            // Return error Data so the caller sees the parse failure with full exception
            return new List<data.@this>
            {
                global::app.data.@this<object>.FromError(ActionError.FromException(ex, "JsonParseError", 400))
            };
        }

        // Fill in defaults for parameters not provided by the LLM
        if (parameterDefs != null)
        {
            foreach (var def in parameterDefs)
            {
                if (!result.Any(r => r.Name == def.Name) && def.Value != null)
                    result.Add(new data.@this(def.Name, def.Value));
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
            if (dataUri.Success && !string.IsNullOrEmpty(dataUri.Value))
            {
                return new Dictionary<string, object>
                {
                    ["type"] = "image_url",
                    ["image_url"] = new Dictionary<string, string>
                    {
                        ["url"] = dataUri.Value
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
            if (param.Value == null)
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
        // Try settings store
        var result = await settings.Get("LlmConfig", settingKey);
        if (result.Success && result.Value != null)
        {
            var val = result.Value is data.@this d ? d.Value?.ToString() : result.Value.ToString();
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

    private static (JsonElement? Result, Exception? Error) ParseApiResponse(object? value)
    {
        if (value == null) return (null, null);

        if (value is JsonElement je)
            return (je, null);

        try
        {
            var json = JsonSerializer.Serialize(value);
            using var doc = JsonDocument.Parse(json);
            return (doc.RootElement.Clone(), null);
        }
        catch (JsonException ex)
        {
            return (null, ex);
        }
    }

    private static List<ToolCall> ParseToolCalls(JsonElement toolCallsElement)
    {
        var result = new List<ToolCall>();
        foreach (var tc in toolCallsElement.EnumerateArray())
        {
            var id = tc.TryGetProperty("id", out var idProp) ? idProp.GetString() ?? "" : "";
            var func = tc.GetProperty("function");
            var name = func.TryGetProperty("name", out var nameProp) ? nameProp.GetString() ?? "" : "";
            var args = func.TryGetProperty("arguments", out var argsProp) ? argsProp.GetString() ?? "" : "";

            result.Add(new ToolCall { Id = id, Name = name, Arguments = args });
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
        // The cached value is a dictionary with Value + metadata. A json cache
        // entry now materializes to the native dict value type — unwrap it to raw
        // so the Dictionary branch below reads Value + the metadata props.
        var cachedValue = cached.Value is app.type.dict.@this nativeDict
            ? nativeDict.ToRaw()
            : cached.Value;
        object? resultValue = null;
        var props = new Dictionary<string, object?>();

        if (cachedValue is JsonElement je && je.ValueKind == JsonValueKind.Object)
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
        else if (cachedValue is Dictionary<string, object?> dict)
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
        if (props.TryGetValue("RawResponse", out var rawObj) && rawObj is string rawResp
            && !string.IsNullOrEmpty(rawResp))
        {
            var fmt = props.TryGetValue("Format", out var fmtObj) ? fmtObj as string : null;
            resultValue = ParseResultValue(rawResp, fmt) ?? resultValue;
        }

        var result = global::app.data.@this.Ok(resultValue);
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
