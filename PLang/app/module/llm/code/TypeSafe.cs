using app.actor.context;
using app.module.http;
using PlangHttpMethod = app.module.http.HttpMethod;

namespace app.module.llm.code;

/// <summary>
/// Decision provider over the typesafe "systemone" service: one state, many typed questions, one
/// exchange. The request goes out through the <c>http.request</c> action like every other outbound
/// call, so it inherits the actor's channel, timeouts and permission model rather than opening a
/// socket of its own.
/// </summary>
public sealed class TypeSafe : IDecider
{
    public string Name { get; init; } = "TypeSafe";
    public bool IsBuiltIn { get; set; }
    public string? Source { get; set; }

    public async Task<data.@this<global::app.type.item.dict.@this>> Decide(decider action)
    {
        var context = action.Context;

        // nothing asked (every step cached) — nothing to send: the answer is empty
        var questions = await action.Question.Value();
        if (questions == null || questions.CountRaw == 0)
            return context.Ok(new global::app.type.item.dict.@this());

        // the decider's own settings: the saved row, else the environment's, else the service's endpoint
        var setting = context.Setting.Of<global::app.module.llm.decider.setting>();
        var endpoint = setting.Endpoint.ToString();
        var apiKey = setting.Key.ToString();

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
            Timeout = new data.@this<global::app.type.item.duration.@this>("", new global::app.type.item.duration.@this(System.TimeSpan.FromMinutes(2))),
        };

        var result = await new global::app.goal.step.action.@this(http, context).Start(context);
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
}
