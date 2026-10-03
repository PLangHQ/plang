using System.Text;
using System.Threading.Channels;
using app.Attributes;
using app.error;
using Call = app.goal.step.action.@this;
using Context = app.actor.context.@this;
using Data = app.data.@this;
using Text = app.type.item.text.@this;

namespace app.module.terminal.type.process;

/// <summary>
/// A program started with <c>terminal.open</c> that keeps running: lines it writes arrive through
/// OnOutput/OnError as <c>%!data%</c>, <c>terminal.send</c> writes a line to its stdin,
/// <c>terminal.wait</c> waits for it to exit, <c>terminal.stop</c> ends it.
/// </summary>
[PlangType("process")]
public sealed class @this : global::app.type.item.@this, global::app.type.item.ICreate<@this>
{
    /// <summary>The program's full path.</summary>
    [LlmBuilder, Out] public string Program { get; set; } = "";

    /// <summary>The operating system's process id.</summary>
    [LlmBuilder, Out] public int Id { get; set; }

    /// <summary>True until the program exits.</summary>
    [LlmBuilder, Out] public bool Running => Os is { HasExited: false };

    /// <summary>The operating system's process it is.</summary>
    internal code.child.@this? Os { get; set; }

    /// <summary>Its pipe pair (<c>terminal.open … with pipe</c>) as a channel: what is written goes to its fd 3, what it
    /// writes on its fd 4 is read — each message ended as the channel's end says (a newline unless set: DevTools'
    /// pipe ends each with NUL). Null when it was started without one.</summary>
    [LlmBuilder, Out] public global::app.channel.type.stream.@this? pipe { get; internal set; }

    /// <summary>It speaks plang's own format — a plang started with <c>--app.type.format=application/plang</c>: what it
    /// writes arrives as Data (an ask as an Ask), and what is sent to it goes as Data, signed.</summary>
    internal bool Plang { get; init; }

    private Task? _reading;

    /// <summary>The encoding its output is in (the terminal's Encoding setting): utf-8 unless set.</summary>
    internal string OutputEncoding { get; init; } = "utf-8";

    /// <summary>The actor that started it: its channels are that actor's, and nobody else's.</summary>
    internal global::app.actor.@this? StartedBy { get; init; }

    private global::app.channel.type.stream.@this? _input, _output, _error;

    /// <summary>Its stdin as a channel: what is sent to it is written there, one writer at a time; closed, it reads
    /// the end.</summary>
    internal global::app.channel.type.stream.@this input => _input ??=
        new("stdin", Os!.Input.BaseStream, global::app.channel.ChannelDirection.Output, ownsStream: true) { Actor = StartedBy! };

    /// <summary>Its stdout as a channel of lines, in its encoding (a last line with no newline is a line too).</summary>
    internal global::app.channel.type.stream.@this output => _output ??= Lines("stdout", Os!.Output.BaseStream);

    /// <summary>Its stderr as a channel of lines, in its encoding.</summary>
    internal global::app.channel.type.stream.@this error => _error ??= Lines("stderr", Os!.Error.BaseStream);

    private global::app.channel.type.stream.@this Lines(string name, Stream stream)
        => new(name, stream, global::app.channel.ChannelDirection.Input, ownsStream: true)
            { Framed = true, End = "\n", KeepsLast = true, Encoding = OutputEncoding, Actor = StartedBy! };

    /// <summary>The OS process, handed to the step that started it to run it to its end (<c>terminal.start</c>): that
    /// step owns it from here and disposes it; this item holds it no longer.</summary>
    internal code.child.@this Take()
    {
        var os = Os ?? throw new InvalidOperationException($"{Program} has no process to take");
        Os = null;
        return os;
    }

    // ---- what it is asked -------------------------------------------------------------------------

    /// <summary>Writes <paramref name="data"/> to its stdin as one line: to a plang that speaks plang's own format the
    /// Data whole, signed; to anything else its text (a dict or list its json) — <c>terminal.send</c>.</summary>
    internal async Task<Data> Send(Data data, Context context)
    {
        if (Os is not { HasExited: false } os)
            return context.Error(new ActionError($"The program isn't running: {this}", "ProgramNotRunning", 409));
        string line;
        if (Plang)
        {
            using var written = new MemoryStream();
            // what is sent is the value itself (what %answer% names), signed as it is — never the reference
            var encoded = await PlangFormat(context).Encode(written, context.Ok(await data.Value()), context);
            if (!encoded.Success) return encoded;
            line = Encoding.UTF8.GetString(written.ToArray()).TrimEnd('\r', '\n');
        }
        else
        {
            var value = await (await data.Follow(context)).Value();
            if (value is Text t) line = t.Clr<string>() ?? "";
            else
            {
                using var written = new MemoryStream();
                await Text.Encode(written, context.Ok(value), context, null, null, CancellationToken.None);
                line = Encoding.UTF8.GetString(written.ToArray()).ReplaceLineEndings(" ");
            }
        }
        var sent = await Write(line, context);
        return sent.Success ? context.Ok()
            : context.Error(new ActionError($"Could not write to {Program}: {sent.Error?.Message}", "ProgramNotRunning", 409));
    }

    /// <summary>Waits for it to exit, its last lines delivered first; the value is its exit code — <c>terminal.wait</c>.</summary>
    internal async Task<global::app.data.@this<global::app.type.item.number.@this>> Wait(Context context)
    {
        if (Os is not { } os)
            return global::app.data.@this<global::app.type.item.number.@this>.From(context.Error(new ActionError("No running program to wait for.", "ProgramNotRunning", 409)));
        await os.WaitForExitAsync();
        if (_reading != null) await _reading;
        return context.Ok<global::app.type.item.number.@this>(os.ExitCode);
    }

    /// <summary>Ends it and everything it started — <c>terminal.stop</c>.</summary>
    internal Data Stop(Context context)
    {
        Kill();
        return context.Ok();
    }

    // ---- for C# that holds a program (the browser holds Chromium) ---------------------------------

    /// <summary>Ends it and everything it started, when it still runs.</summary>
    internal void Kill()
    {
        if (Os is { HasExited: false } os) os.Kill();
    }

    /// <summary>Done when it has exited.</summary>
    internal Task Exited => Os?.WaitForExitAsync() ?? Task.CompletedTask;

    /// <summary>Its exit code, once it has exited.</summary>
    internal int ExitCode => Os?.ExitCode ?? -1;

    // its last words on stderr — what it said before it stopped, if it stops
    private readonly Queue<string> _said = new();

    /// <summary>A line it wrote on stderr: the last 40 are kept.</summary>
    internal void Heard(string line)
    {
        lock (_said)
        {
            _said.Enqueue(line);
            while (_said.Count > 40) _said.Dequeue();
        }
    }

    /// <summary>What it said last on stderr (up to 40 lines), oldest first.</summary>
    internal string Said
    {
        get { lock (_said) return string.Join("\n", _said); }
    }

    // ---- what it says -----------------------------------------------------------------------------

    /// <summary>Its lines, delivered as they come, one goal call at a time per app (on.code.Gate): stdout to
    /// <paramref name="onOutput"/>, stderr to <paramref name="onError"/>, its last stderr lines kept.</summary>
    internal void Read(Call? onOutput, Call? onError, Context context)
    {
        var lines = Lines();
        _reading = Task.Run(async () =>
        {
            try
            {
                await foreach (var (isError, line) in lines.ReadAllAsync())
                {
                    if (isError) Heard(line);
                    var call = isError ? onError : onOutput;
                    if (call != null) await global::app.module.on.code.Gate.Call(call, await Line(line, context), context);
                }
            }
            // what stops the reading is said — a program whose lines stop arriving without a word is a mystery
            catch (Exception ex) when (ex is not (OutOfMemoryException or StackOverflowException))
            {
                await Unread(new ServiceError($"Reading {Program} stopped: {ex.Message}", "ReadingStopped") { Exception = ex }, context);
            }
        });
    }

    /// <summary>Its stdout as binary messages ([u32 length, little-endian][bytes]): each, as it arrives, straight to
    /// <paramref name="screen"/> (no goal per message) and to <paramref name="onOutput"/> as binary; its stderr is text,
    /// each line to <paramref name="onError"/> and its last lines kept. A plang behind it takes calls to its goals
    /// (<c>%container.goal["Question"]%</c>): a call goes down its input as a line, its answer comes back as a message of
    /// its own (kind 9, <c>{"reply": …}</c>).</summary>
    internal void ReadMessages(Call? onOutput, Call? onError, global::app.module.screen.type.screen.@this? screen, Context context)
    {
        var os = Os!;
        var errors = error;
        var said = Task.Run(async () =>
        {
            while (await errors.Read() is { Success: true } line)
            {
                var text = (await line.Value())?.ToString() ?? "";
                Heard(text);
                if (onError != null) await global::app.module.on.code.Gate.Call(onError, Written(text, context), context);
            }
        });
        Remote.Link = async line =>
        {
            var sent = await Write(line, context);
            if (!sent.Success) throw new IOException(sent.Error?.Message);
        };
        _reading = Task.Run(async () =>
        {
            // binary is framed by its own lengths, not a channel's end: read off the stream itself
            await foreach (var message in Messages(os.Output.BaseStream))
            {
                if (Reply(message) is { } reply)
                {
                    Remote.Answered(reply);
                    continue;
                }
                screen?.Show(message);
                if (onOutput != null)
                    await global::app.module.on.code.Gate.Call(onOutput,
                        new Data("!data", new global::app.type.item.binary.@this(message), context.App.type.list["binary"], context: context), context);
            }
            await said;   // its last stderr lines are delivered too
        });
    }

    /// <summary>One line on its stdin, through its input channel (one writer at a time).</summary>
    internal Task<Data> Write(string line, Context context)
        => input.Write(context.Ok((Text)(line + System.Environment.NewLine)));

    /// <summary>Its stdout and stderr as one queue of lines, completed when both channels end.</summary>
    internal ChannelReader<(bool error, string line)> Lines()
    {
        var lines = System.Threading.Channels.Channel.CreateUnbounded<(bool error, string line)>();
        // a read that fails (not the channel's end) ends the queue with why, so whoever reads it says so
        _ = Task.WhenAll(Pump(output, false, lines.Writer), Pump(error, true, lines.Writer))
            .ContinueWith(pumped => lines.Writer.Complete(pumped.Exception?.InnerException), TaskScheduler.Default);
        return lines.Reader;
    }

    // a channel's lines into the queue, until it ends
    private static async Task Pump(global::app.channel.type.stream.@this channel, bool isError, ChannelWriter<(bool, string)> writer)
    {
        while (true)
        {
            var line = await channel.Read();
            if (!line.Success)
            {
                if (line.Error?.Key == "ChannelEnded") return;
                throw new IOException($"{channel.Name}: {line.Error?.Message}");
            }
            await writer.WriteAsync((isError, (await line.Value())?.ToString() ?? ""));
        }
    }

    /// <summary>Length-prefixed binary messages: [u32 length, little-endian][that many bytes].</summary>
    private static async IAsyncEnumerable<byte[]> Messages(Stream stream)
    {
        var header = new byte[4];
        while (true)
        {
            try { await stream.ReadExactlyAsync(header); }
            catch (EndOfStreamException) { yield break; }
            var length = System.Buffers.Binary.BinaryPrimitives.ReadInt32LittleEndian(header);
            if (length is < 0 or > 256 * 1024 * 1024) yield break;   // not our framing: stop, don't allocate garbage
            var message = new byte[length];
            try { await stream.ReadExactlyAsync(message); }
            catch (EndOfStreamException) { yield break; }
            yield return message;
        }
    }

    /// <summary>A message up from a plang behind it that answers a call to one of its goals: kind 9, <c>{"reply": …}</c>.</summary>
    private static System.Text.Json.JsonElement? Reply(byte[] message)
    {
        if (message.Length < 10 || message[0] != 9 || message[1] != (byte)'{') return null;
        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(message.AsMemory(1));
            return doc.RootElement.TryGetProperty("reply", out var reply) ? reply.Clone() : null;
        }
        catch (System.Text.Json.JsonException) { return null; }
    }

    // What it said, as %!data%: from a plang that speaks plang's own format, a line that is a Data is that Data whole
    // (an ask is an Ask — `if %!data.type% == "ask"`); any other line is its text.
    private async Task<Data> Line(string line, Context context)
    {
        if (!Plang || !line.TrimStart().StartsWith('{')) return Written(line, context);
        try
        {
            var data = await PlangFormat(context).Decode(Encoding.UTF8.GetBytes(line), context, "!data");
            // read now, so the goal reading it sees the value itself — an ask from the program is its question, not
            // this goal's own ask waiting
            if (data.Success) { await data.Value(); data.Name = "!data"; return data; }
            await Unread(data.Error!, context);
        }
        catch (Exception ex) when (ex is not (OutOfMemoryException or StackOverflowException))
        {
            await Unread(new ServiceError($"plang's own format didn't read: {ex.Message}", "PlangUnread") { Exception = ex }, context);
        }
        return Written(line, context);   // what couldn't be read as Data still arrives, as its text
    }

    /// <summary>A line as <c>%!data%</c>, text.</summary>
    internal static Data Written(string line, Context context)
        => new("!data", line, context.App.type.list["text"], context: context);

    // A line from a plang that didn't read as Data: said on the error channel, never swallowed.
    private static Task Unread(Error why, Context context) => context.Actor.Channel.Report(context.Error(why));

    /// <summary>plang's own format — the one a plang started with --app.type.format=application/plang speaks.</summary>
    internal static global::app.type.kind.@this PlangFormat(Context context) => context.App.type.list.Mime("application/plang");

    /// <summary>The app this program runs, when it is a plang that takes calls on its input (PlangOS): its goals are
    /// called like this app's — <c>call goal Question in %container%</c>, <c>%container.goal["Question"]%</c> — the call
    /// going down its input, the answer coming back beside its frames. The same link as <c>%!app.parent%</c>, the
    /// other way.</summary>
    internal global::app.parent.@this Remote { get; } = new();

    /// <summary>One step by dot: <c>goal</c> — the goals of the app it runs; any other member as every item's.</summary>
    public override ValueTask<Data> Get(Data parent, string key)
        => string.Equals(key, "goal", StringComparison.OrdinalIgnoreCase) ? Remote.Get(parent, key) : base.Get(parent, key);

    public override string ToString() => $"{Program} (pid {Id}{(Running ? "" : ", exited")})";
}
