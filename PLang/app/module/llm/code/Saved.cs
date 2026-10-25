using Exchange = global::app.module.llm.type.exchange.@this;

namespace app.module.llm.code;

/// <summary>
/// One test's llm answers, kept in a file beside its goal (<c>LlmCache.test.goal</c> → <c>LlmCache.test.llm.json</c>)
/// and committed with it: a list of exchanges, each a request and its response, in the order first asked. A request
/// in the file is answered from it; one that isn't is asked of the answer behind it (OpenAI) once and added. A request
/// is the body as the API takes it, compared whole, so a changed prompt is a new question; no header is kept, so
/// neither is the key. A streamed ask always goes to the answer behind. A test registers it on its own app
/// (<c>test.setting.llm</c> saved); <see cref="Keep"/> saves the file holding the exchanges a passing run asked.
/// </summary>
public sealed class Saved : IAnswer
{
    public string Name => "saved";
    public bool IsBuiltIn { get; set; }
    public string? Source { get; set; }

    private readonly global::app.type.item.path.@this _file;
    private readonly IAnswer _live;
    // the file's exchanges, read at the first ask; the ones this run asked, in the file's order then the new ones
    private readonly SemaphoreSlim _gate = new(1, 1);
    private List<Exchange>? _exchanges;
    private readonly HashSet<Exchange> _asked = new(ReferenceEqualityComparer.Instance);
    // each exchange's request as the json writer writes it, written once: a request is compared by it, exactly
    private readonly Dictionary<Exchange, string> _written = new(ReferenceEqualityComparer.Instance);
    private bool _added;

    /// <summary>The answers of the test whose goal is at <paramref name="goal"/>; a question not in them goes to
    /// <paramref name="live"/>.</summary>
    public Saved(global::app.type.item.path.@this goal, IAnswer live)
    {
        _file = goal.Parent.Combine(goal.Stem + ".llm.json");
        _live = live;
    }

    public async Task<data.@this> Ask(data.@this body, query action)
    {
        var context = action.Context;
        if (await action.IsStreamed()) return await _live.Ask(body, action);

        await _gate.WaitAsync(context.CancellationToken);
        try
        {
            var read = await Read(context);
            if (!read.Success) return read;
            // the request matched exactly — its json as written, ordinal: a changed character (its case, a space) is a
            // new question, never another's answer
            var asking = await Written(body, context);
            if (!asking.Success) return asking;
            var question = (await asking.Value())!.ToString();
            foreach (var exchange in _exchanges!)
            {
                if (!_written.TryGetValue(exchange, out var request))
                {
                    var written = await Written(new data.@this("request", exchange.Request, context: context), context);
                    if (!written.Success) return written;
                    _written[exchange] = request = (await written.Value())!.ToString();
                }
                if (string.Equals(request, question, StringComparison.Ordinal))
                {
                    _asked.Add(exchange);
                    return context.Ok(exchange.Response);
                }
            }

            var answer = await _live.Ask(body, action);
            if (!answer.Success) return answer;
            var asked = new Exchange((await body.Value())!, (await answer.Value())!, context.App.type.list[typeof(Exchange)]);
            _exchanges!.Add(asked);
            _asked.Add(asked);
            _added = true;
            return answer;
        }
        finally { _gate.Release(); }
    }

    /// <summary>Saves the file holding the exchanges this run asked, in their order, when that is not what it holds:
    /// one added, or one no longer asked. A run that asked nothing leaves it as it is.</summary>
    public async Task<data.@this> Keep(global::app.actor.context.@this context)
    {
        await _gate.WaitAsync(context.CancellationToken);
        try
        {
            if (_exchanges == null || !_added && _asked.Count == _exchanges.Count) return context.Ok();
            var kept = new global::app.type.item.list.@this<Exchange>(_exchanges.Where(_asked.Contains).ToList(),
                context.App.type.list[typeof(global::app.type.item.list.@this<Exchange>)]);
            return await _file.Save(new data.@this("", kept, context: context), context);
        }
        finally { _gate.Release(); }
    }

    // the file's exchanges, read once; no file is none yet
    private async Task<data.@this> Read(global::app.actor.context.@this context)
    {
        if (_exchanges != null) return context.Ok();
        var exchanges = new List<Exchange>();
        if (await (await _file.Exists(context)).ToBooleanAsync())
        {
            var read = await _file.Read(context);
            if (!read.Success) return read;
            var file = await read.Value<global::app.type.item.list.@this<Exchange>>();
            if (!read.Success) return read;
            if (file == null)
                return context.Error(new global::app.error.@this($"The saved llm answers in {_file} are no list of exchanges", "SavedAnswersUnreadable", 500));
            foreach (var row in file.Items(context))
                if (await row.Value<Exchange>() is { } exchange) exchanges.Add(exchange);
                else return row.Success ? context.Error(new global::app.error.@this($"An entry in {_file} is no exchange", "SavedAnswersUnreadable", 500)) : row;
        }
        _exchanges = exchanges;
        return context.Ok();
    }

    // a request as the json writer writes it — the text two requests are the same question by, character for character
    private async Task<data.@this> Written(data.@this request, global::app.actor.context.@this context)
    {
        using var stream = new MemoryStream();
        var encoded = await context.App.type.list.Mime("application/json").Encode(stream, request, context);
        return encoded.Success
            ? context.Ok((global::app.type.item.text.@this)System.Text.Encoding.UTF8.GetString(stream.ToArray()))
            : encoded;
    }
}
