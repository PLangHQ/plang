using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using App.Actor.Context;
using App.Errors;
using App.Goals.Goal;
using App.Variables;
using App.Settings;
using App.FileSystem;
using App.FileSystem.Default;
using App.modules.http;
using PlangHttpMethod = App.modules.http.HttpMethod;

namespace App.modules.llm.providers;

/// <summary>
/// OpenAI-compatible LLM provider. Owns the full lifecycle:
/// config, message formatting, HTTP calls (via http module), tool loop,
/// caching (via SettingsStore), streaming, validation, conversation continuity.
/// </summary>
public sealed class OpenAiProvider : ILlmProvider
{
    public string Name { get; init; } = "OpenAi";
    public bool IsDefault { get; set; }

    private const string ConversationKey = "__llm_conversation__";
    private const string SchemaKey = "__llm_schema__";
    private const string CacheTable = "LlmCache";

    public async Task<Data.@this> Query(query action)
    {
        var app = action.Context.App;
        var context = action.Context;
        var config = app.Config.For<http.Config>(context);

        // --- Config ---
        var settings = app.System.SettingsStore;
        var endpoint = await ResolveConfigAsync(settings, "llm.endpoint", "OPENAI_API_ENDPOINT",
            "https://api.openai.com/v1/chat/completions");
        var apiKey = await ResolveConfigAsync(settings, "llm.apiKey", "OPENAI_API_KEY", null);
        var model = action.Model
            ?? await ResolveConfigAsync(settings, "llm.model", null, "gpt-5.4-nano");

        // --- Validate ---
        if (action.Messages.Count == 0)
            return App.Data.@this.FromError(new ActionError("Messages list is empty", "ValidationError", 400));

        // --- Build messages ---
        var messages = CloneMessages(action.Messages);
        string? schema;

        if (action.ContinuePreviousConversation)
        {
            var prev = context.Get<List<LlmMessage>>(ConversationKey);
            if (prev != null)
                messages.InsertRange(0, prev);

            schema = action.Schema ?? context.Get<string>(SchemaKey);
        }
        else
        {
            schema = action.Schema;
            context.Set<List<LlmMessage>>(ConversationKey, null!);
            context.Set<string>(SchemaKey, null!);
        }

        // Snapshot originals BEFORE format mutation
        var originalMessages = CloneMessages(messages);

        // Append format/schema instruction
        var formatInstruction = BuildFormatInstruction(action.Format, schema);
        if (formatInstruction != null)
        {
            var systemMsg = messages.Find(m => m.Role == "system");
            if (systemMsg != null)
                systemMsg.Text += "\n" + formatInstruction;
            else
                messages.Insert(0, new LlmMessage { Role = "system", Text = formatInstruction });
        }

        // --- Cache check ---
        string? cacheKey = null;
        if (action.Cache && action.Tools == null)
        {
            cacheKey = ComputeCacheKey(messages, model, action.Temperature, schema, action.Format);
            var cached = await settings.Get(CacheTable, cacheKey);
            if (cached.Success && cached.Value != null)
            {
                return RestoreFromCache(cached);
            }
        }

        // --- Build tools for API ---
        List<object>? apiTools = null;
        if (action.Tools != null && action.Tools.Count > 0)
        {
            apiTools = action.Tools.Select(t => (object)new Dictionary<string, object>
            {
                ["type"] = "function",
                ["function"] = new Dictionary<string, object?>
                {
                    ["name"] = t.Name,
                    ["description"] = t.Description ?? "",
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
        double? totalCost = null;

        while (true)
        {
            // --- Build request body ---
            var body = new Dictionary<string, object?>
            {
                ["model"] = model,
                ["messages"] = ToApiMessages(messages, app.FileSystem),
                ["temperature"] = action.Temperature,
                ["max_completion_tokens"] = action.MaxTokens
            };
            if (apiTools != null)
                body["tools"] = apiTools;
            if (action.OnStream != null)
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
                Url = endpoint,
                Method = PlangHttpMethod.POST,
                Body = body,
                Headers = headers,
                Unsigned = true,
                TimeoutInSec = 120,
                OnStream = action.OnStream,
                StreamAs = action.OnStream != null ? StreamFormat.SSE : default
            };

            Data.@this httpResult = await app.RunAction(httpAction, context);
            if (action.OnStream != null)
            {
                // TODO: streaming tool call accumulation needs work
                // For now, streaming returns the accumulated result via the callback
                break;
            }

            if (!httpResult.Success)
                return httpResult;

            // --- Parse response ---
            var (responseJson, parseEx) = ParseApiResponse(httpResult.Value);
            if (responseJson == null)
            {
                if (parseEx != null)
                    return App.Data.@this.FromError(ActionError.FromException(parseEx, "ParseError", 500));
                return App.Data.@this.FromError(new ActionError("Failed to parse LLM API response", "ParseError", 500));
            }

            // Extract usage
            var usage = responseJson.Value.TryGetProperty("usage", out var usageProp) ? usageProp : (JsonElement?)null;
            if (usage != null)
            {
                totalPromptTokens += usage.Value.TryGetProperty("prompt_tokens", out var pt) ? pt.GetInt32() : 0;
                totalCompletionTokens += usage.Value.TryGetProperty("completion_tokens", out var ct) ? ct.GetInt32() : 0;
            }

            // Get first choice
            if (!responseJson.Value.TryGetProperty("choices", out var choices) || choices.GetArrayLength() == 0)
                return App.Data.@this.FromError(new ActionError("No choices in LLM response", "EmptyResponse", 500));

            var choice = choices[0];
            var message = choice.GetProperty("message");
            var content = message.TryGetProperty("content", out var contentProp) && contentProp.ValueKind != JsonValueKind.Null
                ? contentProp.GetString() : null;

            // --- Tool calls? ---
            if (message.TryGetProperty("tool_calls", out var toolCallsProp) && toolCallsProp.GetArrayLength() > 0)
            {
                if (toolCallCount >= action.MaxToolCalls)
                    break; // hit limit

                lastContent = content;
                var toolCalls = ParseToolCalls(toolCallsProp);

                // Slice to remaining budget — never execute more tools than the limit allows
                int remaining = action.MaxToolCalls - toolCallCount;
                if (toolCalls.Count > remaining)
                    toolCalls = toolCalls.Take(remaining).ToList();

                // Append assistant message with tool_calls to conversation
                messages.Add(new LlmMessage
                {
                    Role = "assistant",
                    Text = content,
                    ToolCalls = toolCalls
                });

                // Determine parallel execution
                bool allParallel = toolCalls.All(tc =>
                    action.Tools?.Find(t => t.Name == tc.Name)?.Parallel == true);

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
                        Text = results[i]
                    });
                    toolCallCount++;
                }

                continue; // re-query with tool results
            }

            // --- No tool calls — content response ---
            var rawResponse = content ?? "";

            // --- Format extraction ---
            var effectiveFormat = action.Format ?? (schema != null ? "json" : null);
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
                        return App.Data.@this.FromError(new ActionError(
                            "Response is not valid JSON", "JsonParseError", 400));

                    extracted = fromBlock!;
                }
            }

            // --- Custom validation ---
            if (action.OnValidateResponse != null)
            {
                var validationCall = new GoalCall
                {
                    Name = action.OnValidateResponse.Name,
                    PrPath = action.OnValidateResponse.PrPath,
                    Parameters = new List<Data.@this> { new Data.@this("response", extracted) }
                };
                var validationResult = await app.RunGoalAsync(validationCall, context);

                if (!validationResult.Success)
                {
                    var validationError = validationResult.Error?.Message ?? "Unknown validation error";
                    Console.WriteLine($"  LLM validation failed: {validationError}");

                    if (validationRetries >= action.MaxValidationRetries)
                        return App.Data.@this.FromError(new ActionError(
                            $"LLM validation failed: {validationError}",
                            "ValidationFailed", 400));

                    validationRetries++;
                    messages.Add(new LlmMessage
                    {
                        Role = "user",
                        Text = "Your response failed validation: "
                               + validationError
                               + "\nPlease fix and try again."
                    });
                    continue; // re-query
                }
            }

            // --- Store conversation for continuity (pre-mutation originals) ---
            originalMessages.Add(new LlmMessage { Role = "assistant", Text = rawResponse });
            context.Set(ConversationKey, originalMessages);
            context.Set(SchemaKey, schema);

            // --- Build result ---
            object? resultValue = effectiveFormat == "json" ? TryParseJson(extracted) : (object?)extracted;
            var result = App.Data.@this.Ok(resultValue);

            // --- Cache store ---
            // Properties are [JsonIgnore] on Data, so store metadata as the value itself
            if (cacheKey != null)
            {
                var cacheEntry = new Dictionary<string, object?>
                {
                    ["Value"] = resultValue,
                    ["RawResponse"] = rawResponse,
                    ["Model"] = model,
                    ["PromptTokens"] = totalPromptTokens,
                    ["CompletionTokens"] = totalCompletionTokens,
                    ["TotalTokens"] = totalPromptTokens + totalCompletionTokens,
                    ["Cost"] = totalCost,
                    ["ToolCallCount"] = toolCallCount,
                    ["ValidationRetries"] = validationRetries,
                    ["Format"] = effectiveFormat,
                    ["Schema"] = schema
                };
                await settings.Set(CacheTable, cacheKey, new Data.@this("cache", cacheEntry));
            }

            // --- Populate response properties ---
            SetProp(result, "RawResponse", rawResponse);
            SetProp(result, "Model", model);
            SetProp(result, "Messages", messages);
            SetProp(result, "Temperature", action.Temperature);
            SetProp(result, "MaxTokens", action.MaxTokens);
            SetProp(result, "Cached", false);
            SetProp(result, "PromptTokens", totalPromptTokens);
            SetProp(result, "CompletionTokens", totalCompletionTokens);
            SetProp(result, "TotalTokens", totalPromptTokens + totalCompletionTokens);
            SetProp(result, "Cost", totalCost);
            SetProp(result, "ToolCallCount", toolCallCount);
            SetProp(result, "ValidationRetries", validationRetries);
            SetProp(result, "Format", effectiveFormat);
            SetProp(result, "Schema", schema);

            return result;
        }

        // Loop exited via break (MaxToolCalls or streaming)
        var exitResult = App.Data.@this.Ok(lastContent);
        SetProp(exitResult, "Model", model);
        SetProp(exitResult, "ToolCallCount", toolCallCount);
        SetProp(exitResult, "PromptTokens", totalPromptTokens);
        SetProp(exitResult, "CompletionTokens", totalCompletionTokens);
        SetProp(exitResult, "TotalTokens", totalPromptTokens + totalCompletionTokens);
        SetProp(exitResult, "Truncated", true);
        return exitResult;
    }

    // --- Tool execution ---

    private static async Task<string> ExecuteToolAsync(query action, ToolCall toolCall)
    {
        var app = action.Context.App;
        var context = action.Context;

        // OnToolCall — starting
        if (action.OnToolCall != null)
        {
            var startCall = new GoalCall
            {
                Name = action.OnToolCall.Name,
                PrPath = action.OnToolCall.PrPath,
                Parameters = new List<Data.@this>
                {
                    new Data.@this("name", toolCall.Name),
                    new Data.@this("arguments", toolCall.Arguments),
                    new Data.@this("status", "starting")
                }
            };
            await app.RunGoalAsync(startCall, context);
        }

        string result;
        var goalCall = action.Tools?.Find(t => t.Name == toolCall.Name);
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
        if (action.OnToolCall != null)
        {
            var endCall = new GoalCall
            {
                Name = action.OnToolCall.Name,
                PrPath = action.OnToolCall.PrPath,
                Parameters = new List<Data.@this>
                {
                    new Data.@this("name", toolCall.Name),
                    new Data.@this("arguments", toolCall.Arguments),
                    new Data.@this("result", result),
                    new Data.@this("status", "completed")
                }
            };
            await app.RunGoalAsync(endCall, context);
        }

        return result;
    }

    /// <summary>
    /// Parses the LLM's JSON arguments string into List&lt;Data&gt; matching the GoalCall's parameter definitions.
    /// </summary>
    private static List<Data.@this> ParseToolArguments(string argumentsJson, List<Data.@this>? parameterDefs)
    {
        var result = new List<Data.@this>();

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
                result.Add(new Data.@this(prop.Name, value));
            }
        }
        catch (JsonException ex)
        {
            // Return error Data so the caller sees the parse failure with full exception
            return new List<Data.@this>
            {
                App.Data.@this.FromError(ActionError.FromException(ex, "JsonParseError", 400))
            };
        }

        // Fill in defaults for parameters not provided by the LLM
        if (parameterDefs != null)
        {
            foreach (var def in parameterDefs)
            {
                if (!result.Any(r => r.Name == def.Name) && def.Value != null)
                    result.Add(new Data.@this(def.Name, def.Value));
            }
        }

        return result;
    }

    // --- Message formatting ---

    private static List<object> ToApiMessages(List<LlmMessage> messages, IPLangFileSystem fileSystem)
    {
        var result = new List<object>();
        foreach (var msg in messages)
        {
            if (msg.Role == "tool")
            {
                result.Add(new Dictionary<string, object?>
                {
                    ["role"] = "tool",
                    ["content"] = msg.Text ?? "",
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
                    ["content"] = msg.Text,
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
                if (!string.IsNullOrEmpty(msg.Text))
                    contentParts.Add(new Dictionary<string, string> { ["type"] = "text", ["text"] = msg.Text });

                foreach (var image in msg.Images)
                {
                    var imageContent = ResolveImage(image, fileSystem);
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
                    ["content"] = msg.Text
                });
            }
        }
        return result;
    }

    private static object ResolveImage(string image, IPLangFileSystem fileSystem)
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

        // Try file path
        try
        {
            if (fileSystem.File.Exists(image))
            {
                var bytes = fileSystem.File.ReadAllBytes(image);
                var base64 = Convert.ToBase64String(bytes);
                var extension = fileSystem.Path.GetExtension(image).TrimStart('.').ToLowerInvariant();
                var mimeType = extension switch
                {
                    "jpg" or "jpeg" => "image/jpeg",
                    "png" => "image/png",
                    "gif" => "image/gif",
                    "webp" => "image/webp",
                    _ => "image/png"
                };
                return new Dictionary<string, object>
                {
                    ["type"] = "image_url",
                    ["image_url"] = new Dictionary<string, string>
                    {
                        ["url"] = $"data:{mimeType};base64,{base64}"
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

    private static Dictionary<string, object> BuildParamSchema(List<Data.@this>? parameters)
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
                ["type"] = MapPlangTypeToJsonSchema(param.Type?.Value)
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

    private static string MapPlangTypeToJsonSchema(string? typeName)
    {
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
            sb.Append(msg.Role).Append(':').Append(msg.Text ?? "").Append('|');
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

    private static async Task<string> ResolveConfigAsync(ISettingsStore settings, string settingKey,
        string? envVar, string? defaultValue)
    {
        // Try settings store
        var result = await settings.Get("LlmConfig", settingKey);
        if (result.Success && result.Value != null)
        {
            var val = result.Value is Data.@this d ? d.Value?.ToString() : result.Value.ToString();
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
    private static Data.@this RestoreFromCache(Data.@this cached)
    {
        // The cached value is a dictionary with Value + metadata
        object? resultValue = null;
        var props = new Dictionary<string, object?>();

        if (cached.Value is JsonElement je && je.ValueKind == JsonValueKind.Object)
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
        else if (cached.Value is Dictionary<string, object?> dict)
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
            resultValue = cached.Value;
        }

        var result = App.Data.@this.Ok(resultValue);
        SetProp(result, "Cached", true);
        foreach (var kvp in props)
            SetProp(result, kvp.Key, kvp.Value);
        return result;
    }

    private static void SetProp(Data.@this data, string name, object? value)
    {
        data.Properties[name] = new Data.@this(name, value);
    }

    private static List<LlmMessage> CloneMessages(List<LlmMessage> messages)
    {
        return messages.Select(m => new LlmMessage
        {
            Role = m.Role,
            Text = m.Text,
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
