using System.Text;

namespace app.module.screen;

/// <summary>
/// The screen takes this app's input itself: each line that arrives (an event from the host:
/// <c>{"mouse":…}</c>, <c>{"key":…}</c>, …) goes straight to it — no goal per line, and nothing
/// waits behind a goal: a goal that waits for the person (a question on the screen) can't hold up
/// the click that answers it. Returns when the input ends (the host closed it). For PlangOS, whose
/// input is the pipe from host plang. The input is read through a handle of its own (the process's
/// stdin, opened anew): the app's input channel may be something else meanwhile (PlangOS asks the
/// person through a goal) without taking the host's events with it.
/// </summary>
[Action("listen", Cacheable = false)]
public partial class listen : IContext
{
    /// <summary>The screen, from <c>screen.open</c>.</summary>
    public partial data.@this<Screen> Screen { get; init; }

    public async Task<data.@this> Start()
    {
        var screen = await Screen.Value();
        if (screen?.Display is not { } display)
            return Context.Error(new global::app.error.ActionError("This screen takes no input (it isn't PlangOS's display).", "NotSupported", 400));
        var lines = 0;
        using var reader = new StreamReader(Console.OpenStandardInput(), new UTF8Encoding(false), false, 1 << 16);
        while (await reader.ReadLineAsync() is { } line)
        {
            lines++;
            // the host answering a call to one of its goals ({"reply": …}) goes to the call waiting on it
            if (line.StartsWith("{\"reply\"", StringComparison.Ordinal) && Member(line, "reply") is { } reply)
            {
                Context.App.parent.Answered(reply);
                continue;
            }
            // the host calling one of this shell's goals ({"call": {id, goal, parameters}} — %container.goal["Question"]%
            // there): it runs here, one at a time with the other callbacks, and its answer goes up beside the frames
            if (line.StartsWith("{\"call\"", StringComparison.Ordinal) && Member(line, "call") is { } call)
            {
                _ = Called(call, display);
                continue;
            }
            display.Input(line);
        }
        return Context.Ok<global::app.type.item.number.@this>(lines);
    }

    /// <summary>Runs the shell's goal the host called, its parameters bound by name, and answers it:
    /// <c>{"reply": {id, result}}</c>, or <c>{"reply": {id, error}}</c>. Only the shell's own goals — a bare name,
    /// as seen from the goal that listens (Screen.goal and its folder) — never a path elsewhere.</summary>
    private async Task Called(System.Text.Json.JsonElement call, code.wayland.Display display)
    {
        var id = call.TryGetProperty("id", out var i) ? i.GetString() ?? "" : "";
        var name = call.TryGetProperty("goal", out var g) ? g.GetString() ?? "" : "";
        var context = Context;
        async Task Answer(System.Text.Json.Nodes.JsonObject reply)
        {
            reply["id"] = id;
            display.Up(new System.Text.Json.Nodes.JsonObject { ["reply"] = reply }.ToJsonString());
            await Task.CompletedTask;
        }
        if (name.Length == 0 || name.Contains('/') || name.Contains('\\'))
        {
            await Answer(new() { ["error"] = $"PlangOS runs only its shell's own goals, by name: not '{name}'" });
            return;
        }
        await global::app.module.on.code.Gate.Run(async () =>
        {
            var goal = await context.App.goal.list.Find(name, context.CallStack.Goal);
            if (goal == null)
            {
                await Answer(new() { ["error"] = $"PlangOS's shell has no goal {name}" });
                return;
            }
            var bound = new List<global::app.data.@this>();
            if (call.TryGetProperty("parameters", out var parameters) && parameters.ValueKind == System.Text.Json.JsonValueKind.Object)
                foreach (var p in parameters.EnumerateObject())
                    bound.Add(new global::app.data.@this(p.Name, new global::app.type.item.serializer.json(context).Parse(p.Value.Clone()), context: context));
            global::app.data.@this ran;
            await using (context.Variable.Calls.Push(bound)) ran = await goal.Start(context);
            if (!ran.Success)
            {
                await Answer(new() { ["error"] = ran.Error?.Message ?? "failed" });
                return;
            }
            using var written = new MemoryStream();
            var encoded = await context.App.type.list.Mime("application/json").Encode(written, ran, context);
            if (!encoded.Success) { await Answer(new() { ["error"] = encoded.Error?.Message ?? "its answer couldn't be written" }); return; }
            var text = Encoding.UTF8.GetString(written.ToArray());
            System.Text.Json.Nodes.JsonNode? result;
            try { result = System.Text.Json.Nodes.JsonNode.Parse(text); }
            catch (System.Text.Json.JsonException) { result = System.Text.Json.Nodes.JsonValue.Create(text); }
            await Answer(new() { ["result"] = result });
        }, context);
    }

    private static System.Text.Json.JsonElement? Member(string line, string name)
    {
        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(line);
            return doc.RootElement.TryGetProperty(name, out var member) ? member.Clone() : null;
        }
        catch (System.Text.Json.JsonException) { return null; }
    }
}
