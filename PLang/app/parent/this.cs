using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace app.parent;

/// <summary>
/// The app that started this plang — <c>%!app.parent%</c> (PlangOS's is the host plang on Windows). Its goals are
/// picked by name, <c>%!app.parent.goal["Claude"]%</c>, and called like any goal
/// (<c>call goal Claude in %!app.parent%, message=%text%</c>): the call and its arguments go up to the parent, the
/// parent runs its goal, and what comes back is the call's result. What links the two is set by what started them
/// (<see cref="Link"/>); with none, a call says there is no parent to call.
/// </summary>
public sealed class @this : global::app.type.item.@this
{
    private readonly ConcurrentDictionary<string, TaskCompletionSource<JsonElement>> waiting = new();

    /// <summary>How a message reaches the parent — set by what links them (PlangOS's screen sends it up beside its
    /// frames, and the parent's answer comes back down its input: <see cref="Answered"/>).</summary>
    internal Func<string, Task>? Link { get; set; }

    /// <summary>One step by dot: <c>goal</c> — the parent's goals, picked by name.</summary>
    public override System.Threading.Tasks.ValueTask<global::app.data.@this> Get(global::app.data.@this parent, string key)
        => string.Equals(key, "goal", StringComparison.OrdinalIgnoreCase)
            ? System.Threading.Tasks.ValueTask.FromResult(new global::app.data.@this(key, new goals(this), parent: parent))
            : base.Get(parent, key);

    /// <summary>Runs the parent's goal <paramref name="goal"/> with the call's own arguments (not its callers'): up to the parent,
    /// back with its result. A goal call that waits as long as a goal may (ten minutes).</summary>
    internal async Task<global::app.data.@this> Run(string goal, global::app.actor.context.@this context)
    {
        if (Link is not { } link)
            return context.Error(new global::app.error.Error(
                $"There is no parent app to call {goal} in: nothing that started this plang links to it", "NoParent", 404));
        var parameters = new JsonObject();
        if (context.Variable.Calls.Current is { } frame)
            foreach (var name in frame.Arguments)
                if (frame.TryGet(name, out var argument))
                {
                    // an argument that can't be written (a %variable% inside it that holds nothing) is the answer:
                    // half of it must not go up as if it were the whole
                    var (json, failed) = await Json(await argument.Follow(context), context);
                    if (failed != null) return failed;
                    parameters[name] = json;
                }
        var id = Guid.NewGuid().ToString("N")[..12];
        var answer = new TaskCompletionSource<JsonElement>(TaskCreationOptions.RunContinuationsAsynchronously);
        waiting[id] = answer;
        try
        {
            await link(new JsonObject { ["call"] = new JsonObject { ["id"] = id, ["goal"] = goal, ["parameters"] = parameters } }.ToJsonString());
            var reply = await answer.Task.WaitAsync(TimeSpan.FromMinutes(10), context.CancellationToken);
            if (reply.TryGetProperty("error", out var error) && error.ValueKind != JsonValueKind.Null)
                return context.Error(new global::app.error.Error($"{goal} in the parent app: {Text(error)}", "ParentGoalFailed", 500));
            return reply.TryGetProperty("result", out var result) && result.ValueKind != JsonValueKind.Null
                ? context.Ok(new global::app.type.item.serializer.json(context).Parse(result.Clone()))
                : context.Ok();
        }
        catch (TimeoutException)
        {
            return context.Error(new global::app.error.Error($"{goal} in the parent app didn't answer in ten minutes", "ParentGoalTimeout", 504));
        }
        finally { waiting.TryRemove(id, out _); }
    }

    /// <summary>The parent answered a call (<c>{"reply": {id, result | error}}</c>): the call waiting on it goes on.</summary>
    internal void Answered(JsonElement reply)
    {
        if (reply.TryGetProperty("id", out var id) && id.GetString() is { } key && waiting.TryRemove(key, out var answer))
            answer.TrySetResult(reply.Clone());
    }

    // a value as json, for the trip up: what it holds, written by plang's json format
    private static async Task<(JsonNode?, global::app.data.@this?)> Json(global::app.data.@this value, global::app.actor.context.@this context)
    {
        using var written = new MemoryStream();
        var encoded = await context.App.type.list.Mime("application/json").Encode(written, value, context);
        if (!encoded.Success) return (null, encoded);
        var text = System.Text.Encoding.UTF8.GetString(written.ToArray());
        try { return (JsonNode.Parse(text), null); }
        catch (JsonException) { return (JsonValue.Create(text), null); }
    }

    private static string Text(JsonElement e) => e.ValueKind == JsonValueKind.String ? e.GetString() ?? "" : e.ToString();

    /// <summary>The parent's goals: one picked by name is a goal that runs in the parent.</summary>
    private sealed class goals(@this parent) : global::app.type.item.@this
    {
        public override System.Threading.Tasks.ValueTask<global::app.data.@this> Get(global::app.data.@this data, string key, bool isIndex)
            => Get(data, key);

        public override System.Threading.Tasks.ValueTask<global::app.data.@this> Get(global::app.data.@this data, string key)
            => System.Threading.Tasks.ValueTask.FromResult(new global::app.data.@this(key,
                new global::app.goal.@this { Name = key, Elsewhere = parent }, parent: data));
    }
}
