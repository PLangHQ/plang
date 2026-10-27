namespace app.goal;

// The goal IS a plang value (item), as an action is (step/action/this.Item.cs). The engine
// reads the typed internals (Name, Steps, Child, …) directly; the item faces are the boundary only.
// The goal is written by its tags, in declaration order (each member a plang value that writes itself — choice,
// the step/goal children); its serializer/Reader.cs reads itself back.
[global::app.Attributes.PlangType("goal")]
[global::app.Attributes.Format("", "application/plang-goal", ".pr", Store = true)]
public sealed partial class @this : global::app.type.item.@this, global::app.type.item.ICreate<@this>,
    global::app.type.item.IMatch<@this>, global::app.type.item.ICurrent<@this>, global::app.type.item.ILoad<@this>,
    global::app.type.item.IList<@this, global::app.goal.list.@this>, global::app.type.item.IEncode<@this>,
    global::app.type.item.IDecode<@this>, global::app.type.item.property.IOwner
{
    /// <summary>What a program adds to the goal — kept as long as the goal is loaded.</summary>
    [Store, global::app.LlmBuilder]
    public global::app.type.item.property.list.@this property { get; }

    /// <summary>
    /// The <c>.pr</c> form: the value written bare in plang's schema writer — a goal (or any program value:
    /// app.pr) writes its own structure, no Data around it, unsigned, in <paramref name="view"/> (its format's own is
    /// Store: a .pr is what plang keeps). Indented, every character as itself (a file is not a page), ending with a
    /// new line.
    /// </summary>
    public static async System.Threading.Tasks.Task<global::app.data.@this> Encode(System.IO.Stream stream,
        global::app.data.@this data, global::app.actor.context.@this context, global::app.View view,
        System.Text.Encoding? encoding, System.Threading.CancellationToken ct)
    {
        if (data.Peek() is not global::app.type.item.@this item)
            return context.Error(new global::app.error.@this($"%{data.Name}% holds no value to write as a .pr", "NothingToWrite", 400));
        await using (var utf8 = new System.Text.Json.Utf8JsonWriter(stream, new System.Text.Json.JsonWriterOptions
                     {
                         Indented = true,
                         // a .pr's line ends at LF on every OS, so one built on Windows is the same file
                         NewLine = "\n",
                         Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
                     }))
        {
            var writer = new global::app.type.item.wire.kind.plang.Writer(utf8, view, context);
            await item.Output(writer);
            await utf8.FlushAsync(ct);
        }
        stream.WriteByte((byte)'\n');
        return context.Ok();
    }

    /// <summary>
    /// The <c>.pr</c> form read back: its bytes are json, read through a json reader and walked by the goal's own
    /// reader into the goal — a goal written whole. A <c>.pr</c> that doesn't read (another format, broken json)
    /// is refused with its reason.
    /// </summary>
    public static System.Threading.Tasks.Task<global::app.data.@this> Decode(byte[] raw,
        global::app.actor.context.@this context, string name, global::app.type.item.path.@this? origin)
    {
        try
        {
            var utf8 = new System.Text.Json.Utf8JsonReader(raw);
            utf8.Read();
            var json = new global::app.type.item.kind.json.Reader(utf8, raw);
            // A goal's .pr is the build's own bytes: the one reader that grants their templates every variable
            // they list (the build, reading its own answer, is the other).
            var goal = new serializer.Reader().Read(ref json, null, new global::app.type.reader.ReadContext(context, Origin: origin, IsBuilt: true));
            return System.Threading.Tasks.Task.FromResult(new global::app.data.@this(name, goal, context: context));
        }
        catch (global::app.error.AppException refused)
        {
            return System.Threading.Tasks.Task.FromResult(context.Error(refused.Error));
        }
        catch (System.Exception broken) when (broken is System.Text.Json.JsonException or System.FormatException
                                                  or System.InvalidOperationException or System.NotSupportedException)
        {
            return System.Threading.Tasks.Task.FromResult(context.Error(new global::app.error.@this(
                $"failed to read {origin} as goal: {broken.Message}", "MaterializeFailed", 400) { Exception = broken }));
        }
    }

    /// <summary>A key names this goal by its address (<c>/system/error/show</c>), or one of its
    /// sub-goals by theirs (<c>/start#show</c>). Case is not the program's to get right.</summary>
    public System.Threading.Tasks.ValueTask<@this?> Match(string key)
    {
        if (string.Equals(Address, key, StringComparison.OrdinalIgnoreCase))
            return System.Threading.Tasks.ValueTask.FromResult<@this?>(this);
        return System.Threading.Tasks.ValueTask.FromResult(
            Child.Items().FirstOrDefault(c => string.Equals(c.Address, key, StringComparison.OrdinalIgnoreCase)));
    }

    /// <summary>A goal is selected by its name: one that exists, never made from a value.</summary>
    public static bool IsSelected => true;

    /// <summary>The goal a text names, as seen from the goal the asker is running — its own chain, its folder, then
    /// the app (<see cref="global::app.goal.list.@this.Find"/>); a goal not read yet loads. Any other value names
    /// none.</summary>
    public static async System.Threading.Tasks.ValueTask<global::app.data.@this<@this>?> Select(global::app.type.item.@this key,
        global::app.actor.context.@this asker)
        => key is global::app.type.item.text.@this name
            ? await asker.App.goal.list.Find(name.Clr<string>() ?? "", asker.call.Goal)
            : null;

    /// <summary>The goal running for the asker — what <c>%!goal%</c> answers.</summary>
    public static @this? Current(global::app.actor.context.@this context) => context.call.Goal;

    /// <summary>The app's goals: the list that reads them from their <c>.pr</c>.</summary>
    public static global::app.goal.list.@this List(global::app.@this app) => new(app);

    /// <summary>A goal passes through; anything else is declined. Program structure has one way in —
    /// its reader (<c>serializer/Reader.cs</c>) — and is never converted from a value.</summary>
    public static @this? Create(object? raw, global::app.type.@this? type, global::app.data.@this data)
    {
        if (raw is @this g) return g;
        // a name reaching the lift is one no goal answered to (Select already asked)
        if (raw is global::app.type.item.text.@this name)
        {
            data.Fail(new global::app.error.@this($"Goal '{name}' not found.", "GoalNotFound", 404));
            return null;
        }
        data.Fail(new global::app.error.@this(
            $"%{data.Name}% holds a {(raw as global::app.type.item.@this)?.Type.Name ?? raw?.GetType().Name ?? "null"} — " +
            "a goal is read from its .pr, never converted from a value.", "CreateItemDeclined", 400));
        return null;
    }

    /// <summary>A structure, never a single-token leaf.</summary>
    public override bool IsLeaf => false;

    /// <summary>A goal is part of a program, which is shared, never copied: a copy of it is it.</summary>
    protected internal override global::app.type.item.@this Clone() => this;
}
