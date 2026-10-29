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
            return context.Ok(new global::app.type.item.dict.@this(context.App.type.list[typeof(global::app.type.item.dict.@this)]));

        // the decider's own settings: the saved row, else the environment's, else the service's endpoint; its key as
        // asked — none, and no one gave one, sends nothing
        var setting = await context.Setting.Get<global::app.module.llm.decider.setting>();
        var key = await ((global::app.module.llm.setting.IKey)setting).Asked(context);
        if (!key.Success) return data.@this<global::app.type.item.dict.@this>.From(key);
        var endpoint = setting.Endpoint.ToString();

        var body = new Dictionary<string, object?>
        {
            ["state"] = await action.State.Value(),
            ["questions"] = questions,
            ["model"] = (await action.Model.Value()).ToString(),
        };

        var headers = new Dictionary<string, object> { ["Authorization"] = $"Bearer {await key.Value()}" };

        var http = new request(context)
        {
            Url = new data.@this<global::app.type.item.text.@this>("", endpoint, context: context),
            Method = new data.@this<global::app.type.item.choice.@this<PlangHttpMethod>>("", PlangHttpMethod.POST, context: context),
            Body = new data.@this("", body, context: context),
            Header = new data.@this<global::app.type.item.dict.@this>("",
                (global::app.type.item.dict.@this)global::app.type.item.@this.Create(headers, context), context: context),
            Unsigned = new data.@this<global::app.type.item.@bool.@this>("", true, context: context),
            Timeout = new data.@this<global::app.type.item.duration.@this>("", global::app.type.item.duration.@this.Create(System.TimeSpan.FromMinutes(2), context), context: context),
        };

        var result = await new global::app.goal.step.action.composed.@this(http, context).Start(context);
        if (!result.Success) return data.@this<global::app.type.item.dict.@this>.From(result);

        // The service answers {answers: {id: …}, usage: …}. The caller asked the questions, so the
        // caller gets the answers under the ids it chose; usage rides as a property rather than
        // mixing accounting into the value.
        var answers = await result.Get("answers");
        var dict = context.App.type.list[typeof(global::app.type.item.dict.@this)];
        var shaped = answers == null
            ? new global::app.type.item.dict.@this(dict)
            : await answers.Value<global::app.type.item.dict.@this>()
              ?? new global::app.type.item.dict.@this(dict);

        var answer = context.Ok(shaped).As<global::app.type.item.dict.@this>();
        if (await result.Get("usage") is { } usage) answer.Properties.Set("usage", await usage.Value());
        return answer;
    }
}
