using Data = global::app.data.@this;

namespace app.module.llm.type.exchange;

/// <summary>
/// PLang <c>exchange</c> value — one request to the llm and the response it got: <c>{request, response}</c>, the
/// request the body as the API takes it (no header, so never a key). What a test's saved answers hold, one per
/// question. The llm module's own.
/// </summary>
[global::app.Attributes.PlangType("exchange")]
public sealed class @this : global::app.type.item.@this, global::app.type.item.ICreate<@this>
{
    public static string Shape => "object";

    /// <summary>What a test's saved answers hold, never LLM vocabulary — kept out of the catalog.</summary>
    public static bool Internal => true;

    /// <summary>The request, the body as the API takes it.</summary>
    [Out, Store] public global::app.type.item.@this Request { get; }

    /// <summary>The response's body.</summary>
    [Out, Store] public global::app.type.item.@this Response { get; }

    /// <summary>An exchange born holding its type.</summary>
    public @this(global::app.type.item.@this request, global::app.type.item.@this response, global::app.type.@this type) : base(type)
    {
        Request = request;
        Response = response;
    }

    public override bool IsLeaf => false;

    /// <summary>An exchange is made from a dict of its two members, both there; anything else declines with why.</summary>
    public static @this? Create(object? raw, global::app.type.@this? type, Data data)
    {
        if (raw is @this exchange) return exchange;
        global::app.type.item.@this? request = null, response = null;
        if (raw is global::app.type.item.dict.@this dict)
            foreach (var entry in dict.Entries(data.Context!))
                switch (entry.Name.ToLowerInvariant())
                {
                    case "request": request = entry.Peek(); break;
                    case "response": response = entry.Peek(); break;
                }
        if (request == null || response == null)
        {
            data.Fail(new global::app.error.@this("an exchange is {request, response}, both there", "ExchangeInvalid", 400));
            return null;
        }
        return new(request, response, type ?? data.Context!.App.type.list[typeof(@this)]);
    }
}
