using app.variable;
using app.actor.context;
using app.module.action.http;
using PlangHttpMethod = app.module.action.http.HttpMethod;

namespace app.module.action.llm.code;

/// <summary>
/// Decision provider over the typesafe "systemone" service: one state, many typed questions, one
/// exchange. The request goes out through the <c>http.request</c> action like every other outbound
/// call, so it inherits the actor's channel, timeouts and permission model rather than opening a
/// socket of its own.
/// </summary>
public sealed class TypeSafe : IDecider
{
    public string Name { get; init; } = "TypeSafe";
    public bool IsDefault { get; set; }
    public bool IsBuiltIn { get; set; }
    public string? Source { get; set; }

    public async Task<data.@this<global::app.type.item.dict.@this>> Decide(decider action)
    {
        var context = action.Context;
        var app = context.App;

        // nothing asked (every step cached) — nothing to send: the answer is empty
        var questions = await action.Question.Value();
        if (questions == null || questions.CountRaw == 0)
            return context.Ok(new global::app.type.item.dict.@this());

        var settings = await app.SettingsStore;

        var endpoint = await Config(settings, "decider.endpoint", "TYPESAFE_ENDPOINT",
            "https://api.typesafe.ai/v1/systemone");
        var apiKey = await Config(settings, "decider.apiKey", "TYPESAFE_API_KEY", null);

        var body = new Dictionary<string, object?>
        {
            ["state"] = await action.State.Value(),
            ["questions"] = questions,
            ["model"] = (await action.Model.Value()).ToString(),
        };

        var headers = new Dictionary<string, object>();
        if (!string.IsNullOrEmpty(apiKey)) headers["Authorization"] = $"Bearer {apiKey}";

        var http = new request(context)
        {
            Url = new data.@this<global::app.type.item.text.@this>("", endpoint),
            Method = new data.@this<global::app.type.item.choice.@this<PlangHttpMethod>>("", PlangHttpMethod.POST),
            Body = new data.@this("", body, context: context),
            Header = new data.@this<global::app.type.item.dict.@this>("",
                (global::app.type.item.dict.@this)global::app.type.item.@this.Create(headers, context)),
            Unsigned = new data.@this<global::app.type.item.@bool.@this>("", true),
            TimeoutInSec = new data.@this<global::app.type.item.number.@this>("", 120),
        };

        var result = await app.Run(http, context);
        if (!result.Success) return data.@this<global::app.type.item.dict.@this>.From(result);

        // The service answers {answers: {id: …}, usage: …}. The caller asked the questions, so the
        // caller gets the answers under the ids it chose; usage rides as a property rather than
        // mixing accounting into the value.
        var answers = await result.Get("answers");
        var shaped = answers == null
            ? new global::app.type.item.dict.@this()
            : await answers.Value<global::app.type.item.dict.@this>()
              ?? new global::app.type.item.dict.@this();

        var answer = context.Ok(shaped).As<global::app.type.item.dict.@this>();
        if (await result.Get("usage") is { } usage) answer.Properties.Set("usage", await usage.Value());
        return answer;
    }

    /// <summary>Settings first, then environment, then the built-in default — the same order the
    /// llm provider resolves its own endpoint and key in.</summary>
    private static async Task<string?> Config(
        global::app.module.action.setting.IStore settings, string key, string? envVar, string? fallback)
    {
        // A missing key returns the null citizen, not C# null — test IsNull, or the endpoint reads
        // as the literal string "null".
        var stored = await settings.Get<global::app.type.item.@this>("DeciderConfig", key);
        if (stored.Success && stored.Peek() is { IsNull: false }
            && (await stored.Value())?.ToString() is { Length: > 0 } s) return s;
        if (envVar != null && System.Environment.GetEnvironmentVariable(envVar) is { Length: > 0 } e) return e;
        return fallback;
    }
}
