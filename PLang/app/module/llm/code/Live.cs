using app.module.http;
using PlangHttpMethod = app.module.http.HttpMethod;

namespace app.module.llm.code;

/// <summary>
/// OpenAI itself: a request sent to the llm setting's endpoint with its key, through http.request. The key is asked
/// here, when a request goes out, never before. When providers other than OpenAI land, each owns its own send, and
/// <see cref="Saved"/> stands in front of any of them unchanged.
/// </summary>
public sealed class Live : IAnswer
{
    public string Name => "live";
    public bool IsBuiltIn { get; set; }
    public string? Source { get; set; }

    public async Task<data.@this> Ask(data.@this body, query action)
    {
        var context = action.Context;
        var llm = await context.Setting.Get<global::app.module.llm.setting.@this>();
        var key = await ((global::app.module.llm.setting.IKey)llm).Asked(context);
        if (!key.Success) return key;

        var headers = new Dictionary<string, object> { ["Authorization"] = $"Bearer {await key.Value()}" };
        var streamed = await action.IsStreamed();

        var send = new request(context)
        {
            Url = new data.@this<global::app.type.item.text.@this>("", llm.Endpoint, context: context),
            Method = new data.@this<global::app.type.item.choice.@this<PlangHttpMethod>>("", PlangHttpMethod.POST, context: context),
            Body = body,
            Header = new data.@this<global::app.type.item.dict.@this>("", (global::app.type.item.dict.@this)global::app.type.item.@this.Create(headers, context), context: context),
            Unsigned = new data.@this<global::app.type.item.@bool.@this>("", true, context: context),
            Timeout = new data.@this<global::app.type.item.duration.@this>("", global::app.type.item.duration.@this.Create(System.TimeSpan.FromMinutes(2), context), context: context),
            OnStream = action.OnStream,
            StreamAs = streamed ? new data.@this<global::app.type.item.choice.@this<StreamFormat>>("", StreamFormat.SSE, context: context) : default
        };
        return await new global::app.goal.step.action.composed.@this(send, context).Start(context);
    }

    /// <summary>OpenAI keeps nothing of a run.</summary>
    public Task<data.@this> Keep(global::app.actor.context.@this context) => Task.FromResult(context.Ok());
}
