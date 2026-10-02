using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using app.actor.context;
using app.error;
using app.goal;
using app.module.setting;
using app.type.item.path;
using app.module.http;
using PlangHttpMethod = app.module.http.HttpMethod;
using number = global::app.type.item.number.@this;
using text = global::app.type.item.text.@this;
using item = global::app.type.item.@this;

namespace app.module.llm.code;

/// <summary>
/// OpenAI-compatible LLM provider. Owns the full lifecycle:
/// config, message formatting, HTTP calls (via http module), tool loop,
/// caching (via the app's store), streaming, validation, conversation continuity.
/// </summary>
public sealed class OpenAi : ILlm
{
    public string Name { get; init; } = "OpenAi";
    public bool IsBuiltIn { get; set; }
    public string? Source { get; set; }

    /// <summary>Fired before each LLM API call with resolved messages and the schema string (if any). Debug subscribes to this.</summary>
    /// Each subscriber is awaited before the request goes out.
    public event Func<List<LlmMessage>, string?, Task>? OnBeforeRequest;

    /// <summary>Fired with the raw LLM response string after each successful API call. Debug subscribes to this.
    /// Each subscriber is awaited before the result is built.</summary>
    public event Func<string, Task>? OnAfterResponse;

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
        var settings = app.store;
        var endpoint = await ResolveConfigAsync(settings, "llm.endpoint", "OPENAI_API_ENDPOINT",
            "https://api.openai.com/v1/chat/completions");
        var apiKey = await ResolveConfigAsync(settings, "llm.apiKey", "OPENAI_API_KEY", null);
        var model = ((action.Model == null ? null : await action.Model.Value())?.ToString()) is { Length: >0 } __m ? __m : null;
        model ??= await ResolveConfigAsync(settings, "llm.model", null, "gpt-5.4-nano");

        // --- Validate ---
        // HACK (minimal): Messages.Value can be NULL (not just empty) — the [IsNotNull]
        // guard checks the parameter's presence, not the lazily-resolved value. Seen in the
        // builder self-build: when on.error RETRIES QueryAndValidatePlan
        // (BuildGoal/Plan.goal:26), the parent-scope %messages% variable resolves to null on
        // the retry (it's fine on the first call), so `action.Messages.Value!.Count` NRE'd
        // and crashed the whole build with a bare NullReferenceException. Treat null like
        // empty → clean ValidationError instead of a crash.
        // TODO(coder): real fix is in the retry/scope handling — a sub-goal's access to a
        // parent-scope variable should survive an on.error retry (or pass %messages% as
        // a goal.call parameter to QueryAndValidatePlan so it re-binds each attempt). See
        // .bot/type-kind-strict/builder/v2/baseline-findings.md.
        // The .NET edge: the message list lowers ITSELF to the API's CLR shape.
        var rawMessages = (await action.Message.Value()).Clr<List<LlmMessage>>();
        if (rawMessages is not { Count: > 0 })
            return context.Error(new ActionError("Messages list is empty or null", "ValidationError", 400));

        // --- Build messages ---
        // %vars% in the message content (e.g. %goalForLlm%) are resolved when Messages materializes —
        // a template=plang container resolves its string leaves at .Value() (dict/list.Value).
        var messages = CloneMessages(rawMessages);
        string? schema;

        // Serialize Schema at the LLM boundary. Schema is `Data<object>?` because the
        // builder LLM may store it as a structured value (dict/list from a JSON
        // literal in .goal source) or as a free-form string (YAML/XML/prose). The LLM
        // expects text — JSON-serialize structured values, pass text through as-is.
        // An ABSENT/empty schema slot is "no schema" — asked via its truthiness
        // (an absent slot holds the null item, which is not truthy).
        async System.Threading.Tasks.Task<string?> SchemaOf(query a)
        {
            if (a.Schema == null || !await a.Schema.ToBooleanAsync()) return null;
            // The schema writes ITSELF through the Text serializer: a free-form json/yaml string
            // rides bare (as authored), a structured value (dict/list from a .goal json literal)
            // renders as json. NEVER STJ on the wrapper — that emitted the value's C# property bag
            // ({"Cacheable":…,"IsLeaf":…}) instead of the schema.
            using var ms = new System.IO.MemoryStream();
            await context.App.type.list["text"].kind.Encode(ms, a.Schema, context);
            return System.Text.Encoding.UTF8.GetString(ms.ToArray());
        }

        // Format slot — absent/empty is "no explicit format" (not truthy), so the
        // schema-implies-json fallback can fire.
        async System.Threading.Tasks.Task<string?> FormatOf(query a)
            => a.Format == null || !await a.Format.ToBooleanAsync() ? null : (await a.Format.Value())?.ToString();

        // a record that refuses what the step wrote answers why
        if (await action.Conversation.Value() is not { } conversation) return context.Error(action.Conversation.Error!);
        if (await action.Limit.Value() is not { } limit) return context.Error(action.Limit.Error!);
        // the conversation continued rides on the response it continues: its messages, and its schema when this
        // query gives none
        schema = await SchemaOf(action);
        if (conversation.Continue is { } continued)
        {
            // a conversation continues an llm answer — the response carries its messages; a value that carries none
            // continues nothing
            var previous = await continued.Follow(context);
            if (await previous.Properties.Value("Messages") is not global::app.type.item.@this history)
                return context.Error(new global::app.error.Error(
                    $"a conversation continues an llm answer; %{previous.Name}% isn't one", "ConversationInvalid", 400));
            messages.InsertRange(0, history.Clr<List<LlmMessage>>() ?? new List<LlmMessage>());
            schema ??= (await previous.Properties.Value("Schema"))?.ToString();
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

        if (OnBeforeRequest is { } before)
            foreach (Func<List<LlmMessage>, string?, Task> hook in before.GetInvocationList())
                await hook(messages, schema);

        // --- Cache check ---
        // The cache decision reads only action.Cache — no build-mode sniff: a build that skips the cache says so in
        // its own setting, which its steps hand to the query. Gating cacheKey also skips the write below (guarded by
        // cacheKey != null), so skip is a full bypass: no read, no stale entry left behind.
        List<Tool>? goalTools = await ToolsOf(action);
        string? cacheKey = null;
        if ((await action.Cache.Value())?.Value == global::app.module.cache.type.cache.use && goalTools == null)
        {
            cacheKey = ComputeCacheKey(messages, model, (await action.Temperature.Value())!.ToDouble(), schema, await FormatOf(action));
            var cached = await settings.Get<global::app.type.item.@this>(CacheTable, cacheKey);
            // A missing key returns the null citizen (Ok(null) → Peek is null.this),
            // which is a real instance — test .IsNull, not a C# != null reference check.
            if (cached.Success && cached.Peek() is { IsNull: false })
            {
                return await RestoreFromCache(cached);
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
                    ["parameters"] = BuildParamSchema(t.Declared)
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
                ["messages"] = await ToApiMessages(messages, app, context),
                ["temperature"] = (await action.Temperature.Value())!.ToDouble(),
                ["max_completion_tokens"] = limit.Token.ToInt64()
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
                Url = new data.@this<global::app.type.item.text.@this>("", endpoint),
                Method = new data.@this<global::app.type.item.choice.@this<PlangHttpMethod>>("", PlangHttpMethod.POST),
                Body = new data.@this("", body, context: context),
                Header = new data.@this<global::app.type.item.dict.@this>("", (global::app.type.item.dict.@this)global::app.type.item.@this.Create(headers, context)),
                Unsigned = new data.@this<global::app.type.item.@bool.@this>("", true),
                Timeout = new data.@this<global::app.type.item.duration.@this>("", new global::app.type.item.duration.@this(System.TimeSpan.FromMinutes(2))),
                OnStream = action.OnStream,
                StreamAs = (action.OnStream == null ? null : await action.OnStream.Value()) != null ? new data.@this<global::app.type.item.choice.@this<StreamFormat>>("", StreamFormat.SSE) : default
            };

            data.@this httpResult = await new global::app.goal.step.action.@this(httpAction, context).Start(context);
            if ((action.OnStream == null ? null : await action.OnStream.Value()) != null)
            {
                // TODO: streaming tool call accumulation needs work
                // For now, streaming returns the accumulated result via the callback
                break;
            }

            if (!httpResult.Success)
                return httpResult;

            // --- Parse response: the body materializes as a clr(json) and is NAVIGATED by path
            // through its own kind (never lowered to a dict). OpenAi's loop is async, so each hop
            // is the async Get door; no Value<dict>, no reflection.
            var response = httpResult;

            // Usage / cost. (Token counters + cost math stay CLR/decimal here for now
            // — the class-wide native-plang-types migration is a tracked follow-up;
            // see .bot/compare-redesign/coder/native-plang-types-migration.md.)
            if (await Nav<item>(response, "usage") != null)
            {
                number callPrompt     = await Nav<number>(response, "usage.prompt_tokens") ?? 0;
                number callCompletion = await Nav<number>(response, "usage.completion_tokens") ?? 0;
                number callCached     = await Nav<number>(response, "usage.prompt_tokens_details.cached_tokens") ?? 0;

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
            if (await Nav<item>(response, "choices[0]") == null)
                return context.Error(new ActionError("No choices in LLM response", "EmptyResponse", 500));

            string? content = (await Nav<text>(response, "choices[0].message.content"))?.ToString();

            // Check finish_reason BEFORE parsing content — a truncated response
            // is not a JSON bug, it's the model running out of output budget. The
            // same goes for content_filter (model refused) and other non-"stop"
            // terminations. Surface these as dedicated errors so callers don't
            // waste time parsing incomplete JSON.
            var finishReason = (await Nav<text>(response, "choices[0].finish_reason"))?.ToString();
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
                    ? $"LLM output hit the max-tokens limit before finishing ({totalCompletionTokens} completion tokens). Raise limit.token or shorten the prompt."
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
                        ["Limit"] = limit
                    }
                });
            }

            // --- Tool calls? ---  navigate them off the clr(json) response (no dict lowering).
            var toolCalls = await ParseToolCalls(response);
            if (toolCalls.Count > 0)
            {
                if (toolCallCount >= limit.Tool.ToInt64())
                    break; // hit limit

                lastContent = content;

                // Slice to remaining budget — never execute more tools than the limit allows
                int remaining = limit.Tool.ToInt32() - toolCallCount;
                if (toolCalls.Count > remaining)
                    toolCalls = toolCalls.Take(remaining).ToList();

                // Append assistant message with tool_calls to conversation
                messages.Add(new LlmMessage
                {
                    Role = "assistant",
                    Content = content,
                    ToolCalls = toolCalls
                });

                // The tools are called in order, each as its goal.call says: a plain one runs to its end before
                // the next is called; a Parallel one answers a task at once and runs on. Each result is made when
                // its call has ended, and they go back in call order.
                // a Parallel tool's call answers its task: the result is made once the task has ended
                async Task<string> Ended(ToolCall tc, data.@this called)
                    => await Result(action, tc, await called.Use<global::app.task.@this>(task => task.Wait()));

                var ending = new List<Task<string>>();
                foreach (var tc in toolCalls)
                {
                    var called = await Call(action, tc, goalTools);
                    var parallel = called.Success && goalTools?.Find(t => t.Name == tc.Name) is { } tool
                        && await tool.Call.Parallel.ToBooleanAsync();
                    ending.Add(parallel ? Ended(tc, called) : Task.FromResult(await Result(action, tc, called)));
                }
                var results = new List<string>();
                foreach (var end in ending) results.Add(await end);

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
            var result = await Answer(rawResponse, effectiveFormat, context);

            // --- JSON validation --- a json answer is read now: one that isn't json is the query's error
            if (effectiveFormat == "json")
            {
                await result.Value();
                if (!result.Success)
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
            }

            // --- Custom validation ---
            if ((action.OnValidateResponse == null ? null : await action.OnValidateResponse.Value()) is { } validator)
            {
                // The answer is the validator's %response%, in a frame for it — never the caller's own variables.
                data.@this validationResult;
                await using (context.call.Push([new data.@this("response", extracted, context: context)], validator))
                    validationResult = await validator.Start(context);

                if (!validationResult.Success)
                {
                    var validationError = validationResult.Error?.Message ?? "Unknown validation error";

                    if (validationRetries >= limit.Retry.ToInt64())
                    {
                        await context.Actor.Channel[global::app.channel.list.@this.Output].WriteText(
                            $"  Validation failed (no retries left): {validationError}");
                        return context.Error(new ActionError(
                            $"LLM validation failed: {validationError}",
                            "ValidationFailed", 400));
                    }

                    validationRetries++;
                    await context.Actor.Channel[global::app.channel.list.@this.Output].WriteText(
                        $"  Validation failed (retry {validationRetries}/{limit.Retry}): {validationError}");
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

            // --- The conversation as sent (pre-mutation originals) and the answer: what a later query continues ---
            originalMessages.Add(new LlmMessage { Role = "assistant", Content = rawResponse });
            var conversed = global::app.type.item.@this.Create(originalMessages, context);

            if (OnAfterResponse is { } after)
                foreach (Func<string, Task> hook in after.GetInvocationList())
                    await hook(rawResponse);

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
                    // empty. The answer is read here (the cache keeps the value, not its unread bytes),
                    // so it rides as a native dict/list (or scalar) that serializes round-trip.
                    ["Value"] = await result.Value(),
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
                    ["Schema"] = schema,
                    ["Messages"] = conversed
                };
                await settings.Set(CacheTable, cacheKey, new data.@this("cache", cacheEntry, context: context));
            }

            // --- Populate response properties ---
            SetProp(result, "RawResponse", rawResponse);
            SetProp(result, "Model", model);
            SetProp(result, "Messages", conversed);
            SetProp(result, "Temperature", (await action.Temperature.Value()));
            SetProp(result, "Limit", limit);
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

        // Loop exited via break (limit.tool or streaming)
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

    // The tool call made: OnToolCall "starting", then the held goal.call with the model's arguments — its answer (a
    // Parallel call's is its task), or why it couldn't be made (an unknown tool, arguments that don't read).
    private static async Task<data.@this> Call(query action, ToolCall toolCall, List<Tool>? tools)
    {
        var context = action.Context;

        // OnToolCall — starting. The run-state binds in a frame for the held call (never the caller's own
        // variables: a user's %name% stays theirs); the held call runs as itself.
        if (action.OnToolCall != null && await action.OnToolCall.Value() is { } onToolCall)
            await using (context.call.Push(toolCall.State("starting", null, context), onToolCall))
                await onToolCall.Start(context);

        if (tools?.Find(t => t.Name == toolCall.Name) is not { } tool)
            return context.Error(new ServiceError($"unknown tool '{toolCall.Name}'", "UnknownTool", 404));

        // The model's arguments are what this invocation supplies: the held call runs inside a
        // frame born with them, FOR it — its declaration rows bind nothing, its valued rows are
        // defaults that yield to a supplied name, and a parallel sibling sees its own frame (a task
        // keeps the frame it was started in).
        var parameters = tool.Arguments(toolCall.Arguments, context);
        if (parameters.Find(p => !p.Success) is { } unread) return unread;
        await using (context.call.Isolate(parameters, tool.Held))
            return await tool.Held.Start(context);
    }

    // What goes back to the model for an ended tool call: its result as json ("" for none), or "Error: " and why;
    // then OnToolCall "completed".
    private static async Task<string> Result(query action, ToolCall toolCall, data.@this ended)
    {
        var context = action.Context;
        string result;
        if (!ended.Success)
            result = "Error: " + (ended.Error?.Message ?? "Unknown error");
        else if (ended.Peek().IsNull)
            result = "";
        else
        {
            // The tool result writes ITSELF as json — never STJ on the item (property bag).
            using var ms = new System.IO.MemoryStream();
            await context.App.type.list["item"].kind["json"]!.Encode(ms, ended, context);
            result = System.Text.Encoding.UTF8.GetString(ms.ToArray());
        }

        // OnToolCall — completed
        if (action.OnToolCall != null && await action.OnToolCall.Value() is { } onToolCall)
            await using (context.call.Push(toolCall.State("completed", result, context), onToolCall))
                await onToolCall.Start(context);

        return result;
    }

    // --- Message formatting ---

    private static async Task<List<object>> ToApiMessages(List<LlmMessage> messages, global::app.@this app, actor.context.@this context)
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
                    var imageContent = await ResolveImage(image, app, context);
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
    internal static async Task<object> ResolveImage(string image, global::app.@this app, actor.context.@this context)
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

        // Try file path — its reference's raw content, gated: in-root fast-passes, out-of-root
        // surfaces as a permission prompt or denial.
        // The probe is whether the image names a file: a string that can't be a path at all (a base64 payload
        // too long or with characters no path takes) is no file, and neither is a path to nothing (404). Any
        // other failure — a denied read, an IO error — is the query's, not a guess at base64.
        global::app.type.item.path.@this? imgPath = null;
        try { imgPath = global::app.type.item.path.@this.Resolve(image, context); }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException) { }
        if (imgPath != null)
        {
            var content = await imgPath.Bytes(context);
            // OpenAI takes an attached image as a data URI — composed here, at its boundary.
            if (content.Success && content.Peek() is global::app.type.item.binary.@this { Value.Length: > 0 } bytes)
            {
                var mime = imgPath.MimeType(context);
                return new Dictionary<string, object>
                {
                    ["type"] = "image_url",
                    ["image_url"] = new Dictionary<string, string>
                    {
                        ["url"] = $"data:{mime};base64,{Convert.ToBase64String(bytes.Value)}"
                    }
                };
            }
            if (!content.Success && content.Error is { } unread && !unread.Status.Equals((global::app.type.item.status.@this)404))
                throw new global::app.error.AppException(unread);
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

    // The part of the model's answer its format asks for: the answer as it is when no format was asked; the
    // content of a code block fenced as the format (or any code block) — a json answer fenced as ```json too.
    private static string ExtractResponse(string content, string? format)
    {
        if (format == null)
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
    /// The model's answer as a value: the part its <paramref name="format"/> asks for, decoded by that
    /// format's kind (json → json, md → text/md; no format, or one no type reads, is text) — content from
    /// outside, so a birth through its type, left unread until touched. The live answer and the cache replay
    /// both read it here, so a replayed answer is the same value as a fresh one (the cache keeps the plain
    /// response text, never a parsed object).
    /// </summary>
    private async Task<data.@this> Answer(string rawResponse, string? format, actor.context.@this context)
    {
        var types = context.App.type.list;
        var kind = format != null && types.Kind(format) is { Owner: not null } read ? read : types.Mime("text/plain");
        return await kind.Decode(Encoding.UTF8.GetBytes(ExtractResponse(rawResponse, format)), context);
    }

    // --- Parameter schema ---

    private static Dictionary<string, object> BuildParamSchema(IReadOnlyList<data.@this> parameters)
    {
        if (parameters.Count == 0)
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
                ["type"] = MapPlangTypeToJsonSchema(param.Type?.Name, param.Type?.kind is { IsEmpty: false } kind ? kind.Name : null)
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

    private static async Task<string> ResolveConfigAsync(global::app.store.@this settings, string settingKey,
        string? envVar, string? defaultValue)
    {
        // Try settings store. A missing key returns the null citizen (Peek is
        // null.this, not C# null) — test .IsNull, or a missing setting reads as the
        // literal string "null" and (e.g.) the endpoint becomes "null:443".
        var result = await settings.Get<global::app.type.item.@this>("LlmConfig", settingKey);
        if (result.Success && result.Peek() is { IsNull: false })
        {
            // A clr never wraps a Data (the ctor forbids it), so read the value directly.
            var val = (await result.Value())?.ToString();
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

    // Tool calls are just part of the response — navigate them off the clr(json) through its own
    // kind (enumerate the array, navigate each element), never lowering json to a dict/list.
    private static async System.Threading.Tasks.ValueTask<List<ToolCall>> ParseToolCalls(data.@this response)
    {
        var result = new List<ToolCall>();
        var arr = await response.Get("choices[0].message.tool_calls");
        if (!arr.IsInitialized) return result;

        foreach (var (_, el) in await arr.EnumerateItems())
            result.Add(new ToolCall
            {
                Id        = (await Nav<text>(el, "id"))?.ToString() ?? "",
                Name      = (await Nav<text>(el, "function.name"))?.ToString() ?? "",
                Arguments = (await Nav<text>(el, "function.arguments"))?.ToString() ?? ""
            });
        return result;
    }

    // Navigate a value by path (async, through its own kind) and hand it typed — null on a miss.
    // Keeps a json response as clr: no Value<dict> lowering, just the value's own kind navigation.
    private static async System.Threading.Tasks.ValueTask<T?> Nav<T>(data.@this value, string path)
        where T : global::app.type.item.@this
    {
        var d = await value.Get(path);
        if (!d.IsInitialized) return null;
        return (await d.Value()) as T;
    }

    // --- Helpers ---

    /// <summary>
    /// Restores a cached result from the app's store.
    /// The cache stores metadata as a dictionary since Data.Properties is [JsonIgnore].
    /// </summary>
    private async Task<data.@this> RestoreFromCache(data.@this cached)
    {
        // Materialize the stored entry — a settings-stored dict round-trips as a lazy wire, so
        // Peek() alone would hand back the still-encoded slice (neither dict nor clr), and the
        // Value-unwrap below would silently miss, riding the whole {Value, RawResponse, …} cache
        // entry back as the result. Value() decodes the wire to its native dict first.
        var cachedValue = await cached.Value();
        object? resultValue = cachedValue;   // non-container fallback; overridden below by RawResponse
        var props = new Dictionary<string, object?>();

        // The cache entry round-trips as a native dict (in-memory) or a clr(json) (through the
        // json reader) — both enumerate uniformly. The old per-shape reconstruction
        // (dict / Clr{JsonElement} / Clr{Dictionary}) existed ONLY because a JsonElement
        // couldn't survive the cache; a clr(json) round-trips as raw json now.
        System.Collections.Generic.IEnumerable<data.@this>? entries = cachedValue switch
        {
            global::app.type.item.dict.@this d => d.Entries(cached.Context),
            global::app.type.clr.@this c => c.Enumerate(cached.Context),
            _ => null
        };
        if (entries != null)
        {
            resultValue = null;
            foreach (var entry in entries)
            {
                if (entry.Name == "Value") { resultValue = entry.Peek(); continue; }
                // each property read through its Data, so a stored one comes back as what it is, not its wire
                props[entry.Name] = await entry.Value();
            }
        }

        // Authoritative reconstruction: re-parse the round-tripped RawResponse
        // string with the same logic the live path uses. The plain string
        // survives disk serialization losslessly, whereas a parsed JsonElement /
        // native value does not always round-trip its element shape — so trusting
        // the stored "Value" can yield a list/dict the consumer can't convert.
        // A dict-navigated prop rides as the native text value — read its backing
        // string; a legacy raw prop is already a string.
        // An LLM's answer is data from outside, never a template: the store hands its values back
        // unopened (a %name% in them reads as a template's variable there), so the raw response is
        // taken as the text it is — its raw content, never opened through a door that renders it.
        static string? AsText(object? v) => v switch
        {
            string s => s,
            global::app.type.item.wire.@this => null,   // still encoded: the stored Value stands
            global::app.type.item.source { RawText: { } raw } => raw,
            global::app.type.item.text.@this t => t.Clr<string>(),
            _ => null,
        };
        string? rawResp = AsText(props.GetValueOrDefault("RawResponse"));
        var result = !string.IsNullOrEmpty(rawResp)
            ? await Answer(rawResp, AsText(props.GetValueOrDefault("Format")), cached.Context)
            : cached.Context.Ok(resultValue);
        SetProp(result, "Cached", true);
        foreach (var kvp in props)
            SetProp(result, kvp.Key, kvp.Value);
        return result;
    }

    private static void SetProp(data.@this data, string name, object? value)
    {
        data.Properties[name] = value;
    }

    // A tool as the loop needs it: the held goal.call (run as itself) and its bound handler, whose own
    // typed properties answer the goal, the declared parameters and whether it may run in parallel.
    // The function name the model sees is the goal's leaf name — a model function name cannot carry
    // the '/' of an app-absolute address.
    private sealed record Tool(global::app.goal.step.action.@this Held, global::app.module.goal.Call Call, string Name)
    {
        public IReadOnlyList<data.@this> Declared
            => (Call.Parameter?.Peek() as global::app.type.item.list.@this)?.Items(Call.Context).ToList()
               ?? (IReadOnlyList<data.@this>)System.Array.Empty<data.@this>();

        /// <summary>
        /// The model's JSON arguments as this tool takes them — only the names it declares. A name the tool
        /// doesn't declare is never bound (the frame is read before the caller's memory, so an undeclared
        /// name would shadow the caller's own variable): it answers the model as an error, like invalid JSON.
        /// A declared row with a value is a default the held call binds itself where the model was silent.
        /// The arguments are json, opened by the json kind: each member is born the plang value it is (text, a
        /// number, a bool, null, or a json value navigated by its keys); json that doesn't read, or a value that
        /// isn't an object of named members, answers the model as an error.
        /// </summary>
        public List<data.@this> Arguments(string json, actor.context.@this context)
        {
            var arguments = new List<data.@this>();
            if (string.IsNullOrEmpty(json)) return arguments;
            var opened = context.App.type.list["item"].kind["json"]!.Open(json, context)!;
            if (!opened.Success) return [opened];
            if (opened.Peek() is not { IsSequence: false, IsLeaf: false } members)
                return [context.Error(new ServiceError($"the arguments of {Name} are not an object of named arguments", "ArgumentsNotAnObject", 400))];

            var declared = Declared.Select(d => d.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
            foreach (var (_, argument) in members.EnumerateItems(context))
            {
                if (!declared.Contains(argument.Name))
                    return [context.Error(new ServiceError(
                        $"'{argument.Name}' is not an argument of {Name}" + (declared.Count == 0 ? " — it takes none" : $" — it takes {string.Join(", ", declared)}"),
                        "UnknownArgument", 400))];
                arguments.Add(argument);
            }
            return arguments;
        }
    }

    // The tools ride as a plang list of held goal.call actions. Null when no tools were passed.
    private static async Task<List<Tool>?> ToolsOf(query action)
    {
        if (action.Tool == null || !await action.Tool.ToBooleanAsync()) return null;
        if (await action.Tool.Value() is not global::app.type.item.list.@this list) return null;
        var tools = new List<Tool>();
        foreach (var row in list.Items(action.Context))
        {
            if (row.Peek() is not global::app.goal.step.action.@this held) continue;
            if ((await held.Bind(action.Context)).Handler is not global::app.module.goal.Call call) continue;
            // a tool goes by its goal's name as written, the leaf of an address
            var goal = held["Name"]?.Value?.RawText ?? "";
            tools.Add(new Tool(held, call, goal[(goal.LastIndexOf('/') + 1)..]));
        }
        return tools;
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
