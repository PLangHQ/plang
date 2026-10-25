using System.Text.Json;
using System.Text.Json.Nodes;

namespace app.module.llm.code;

/// <summary>
/// One test's llm answers, kept in a file beside its goal (<c>LlmCache.test.goal</c> → <c>LlmCache.test.llm.json</c>)
/// and committed with it: a list of {request, response}, in the order first asked. A request in the file is answered
/// from it; one that isn't is asked of the answer behind it (OpenAI) once and added. The request is the body as the
/// API takes it, compared whole, so a changed prompt is a new question; no header is kept, so neither is the key. A
/// streamed ask always goes to the answer behind. A test registers it on its own app (<c>test.setting.llm</c> saved);
/// <see cref="Keep"/> writes the file back holding the answers the run used.
/// </summary>
public sealed class Saved : IAnswer
{
    public string Name => "saved";
    public bool IsBuiltIn { get; set; }
    public string? Source { get; set; }

    // the file as a reviewer reads it: indented, a prompt's quotes and < > as they are
    private readonly JsonSerializerOptions _readable = new()
    {
        WriteIndented = true,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    private readonly global::app.type.item.path.file.@this _file;
    private readonly IAnswer _live;
    // the file's entries, read at the first ask; each marked when the run asks it
    private readonly SemaphoreSlim _gate = new(1, 1);
    private List<Entry>? _entries;
    private bool _added;

    private sealed class Entry
    {
        internal required string Request { get; init; }
        internal required JsonNode Response { get; init; }
        internal bool Used { get; set; }
    }

    /// <summary>The answers of the test whose goal is at <paramref name="goal"/>; a question not in them goes to
    /// <paramref name="live"/>.</summary>
    public Saved(global::app.type.item.path.@this goal, IAnswer live, global::app.actor.context.@this context)
    {
        var location = goal.ToString()!;
        _file = global::app.type.item.path.file.@this.Resolve(location[..^goal.Name.ToString().Length] + goal.Stem + ".llm.json", context);
        _live = live;
    }

    public async Task<data.@this> Ask(data.@this body, query action)
    {
        var context = action.Context;
        if ((action.OnStream == null ? null : await action.OnStream.Value()) != null) return await _live.Ask(body, action);

        var request = await Json(body, context);
        if (!request.Success) return request;
        var question = Compact(JsonNode.Parse((await request.Value())!.ToString()!));

        await _gate.WaitAsync(context.CancellationToken);
        try
        {
            var read = await Read(context);
            if (!read.Success) return read;
            if (_entries!.FirstOrDefault(entry => entry.Request == question) is { } saved)
            {
                saved.Used = true;
                return await context.App.type.list.Mime("application/json")
                    .Decode(System.Text.Encoding.UTF8.GetBytes(saved.Response.ToJsonString()), context, "response");
            }

            var answer = await _live.Ask(body, action);
            if (!answer.Success) return answer;
            var response = await Json(answer, context);
            if (!response.Success) return response;
            _entries!.Add(new Entry { Request = question, Response = JsonNode.Parse((await response.Value())!.ToString()!)!, Used = true });
            _added = true;
            return answer;
        }
        finally { _gate.Release(); }
    }

    /// <summary>Writes the file back holding the answers this run used, in their order, when that is not what it
    /// holds: one added, or one no longer asked. A run that asked nothing leaves it as it is.</summary>
    public async Task<data.@this> Keep(global::app.actor.context.@this context)
    {
        await _gate.WaitAsync(context.CancellationToken);
        try
        {
            if (_entries == null || !_added && _entries.All(entry => entry.Used)) return context.Ok();
            var kept = new JsonArray(_entries.Where(entry => entry.Used)
                .Select(entry => (JsonNode)new JsonObject
                {
                    ["request"] = JsonNode.Parse(entry.Request),
                    ["response"] = entry.Response.DeepClone(),
                }).ToArray());
            return await _file.WriteText(kept.ToJsonString(_readable) + "\n", context);
        }
        finally { _gate.Release(); }
    }

    // the file's entries, read once; no file is none yet
    private async Task<data.@this> Read(global::app.actor.context.@this context)
    {
        if (_entries != null) return context.Ok();
        var entries = new List<Entry>();
        if (await (await _file.Exists(context)).ToBooleanAsync())
        {
            var read = await _file.Read(context);
            if (!read.Success) return read;
            // the reference as it landed, its bytes as they are: the file is read as the json it is, not as a value
            if (read.Peek() is not global::app.type.item.content.@this reference) return context.Error(Unreadable("it is no file"));
            var content = await reference.Content(context);
            if (!content.Success) return content;
            JsonNode? file;
            try { file = JsonNode.Parse((await content.Value())!.Value); }
            catch (JsonException ex)
            {
                var error = Unreadable(ex.Message);
                error.list.Add(global::app.error.@this.FromException(ex));
                return context.Error(error);
            }
            if (file is not JsonArray list) return context.Error(Unreadable("it holds no list"));
            foreach (var node in list)
            {
                if (node is not JsonObject { } entry || entry["request"] is not { } request || entry["response"] is not { } response)
                    return context.Error(Unreadable("an entry holds no request and response"));
                entries.Add(new Entry { Request = Compact(request), Response = response.DeepClone() });
            }
        }
        _entries = entries;
        return context.Ok();
    }

    private global::app.error.@this Unreadable(string why)
        => new($"The saved llm answers in {_file} can't be read: {why}", "SavedAnswersUnreadable", 500);

    // a value as the json the API takes
    private async Task<data.@this> Json(data.@this value, global::app.actor.context.@this context)
    {
        using var stream = new MemoryStream();
        var written = await context.App.type.list.Mime("application/json").Encode(stream, value, context);
        return written.Success ? context.Ok((global::app.type.item.text.@this)System.Text.Encoding.UTF8.GetString(stream.ToArray())) : written;
    }

    // one request's json, the same text however it was laid out
    private string Compact(JsonNode? node) => node?.ToJsonString() ?? "null";
}
