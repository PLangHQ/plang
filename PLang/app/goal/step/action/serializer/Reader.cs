namespace app.goal.step.action.serializer;

/// <summary>
/// Typed (<see cref="app.type.reader.ITypeReader"/>) pull reader for <c>action</c> — the read-side
/// mirror of <see cref="app.goal.step.action.@this.Output"/>. Walks the handed
/// <see cref="app.type.format.IReader"/> in place (the channel already made the one reader and
/// positioned it): the action's bare shape <c>{module, name, property[], default?[], child?[]}</c>.
/// Each property row is read into a property — raw, no Data, no context. The action is made by its
/// module's catalog element (<c>Program</c>) — a clause (<c>on.error</c>, …) when that element is one.
/// <para>The reader is BORN with the step whose actions it reads, so every action it makes is born
/// holding that step — the same birth fact one level up. Having no parameterless constructor is what
/// keeps it out of the type-reader registry: the registry mints only readers that need no parent
/// (values and file roots), so there is no parentless way to make an action.</para>
/// </summary>
public sealed class Reader : global::app.type.reader.ITypeReader
{
    private readonly global::app.goal.step.@this _step;

    public Reader(global::app.goal.step.@this step) => _step = step;

    public string Kind => global::app.type.reader.@this.AnyKind;

    /// <summary>The one door. A null element is consumed and answered as the null citizen; the
    /// caller drops it.</summary>
    public global::app.type.item.@this Read<TReader>(ref TReader reader, string? kind,
        global::app.type.reader.ReadContext ctx)
        where TReader : global::app.type.format.IReader, allows ref struct
    {
        if (reader.Null()) return new global::app.type.item.@null.@this("action", kind);
        return Populate(ref reader, ctx);
    }

    // Reads one action off the handed reader. The wire carries the module and the name first; the action is made
    // then — by the module's catalog element, so a clause is born a clause — and everything after fills it.
    // The children it owns take their parent from the reader: an action held as a value the same step, child
    // steps that step's goal — the chain self-feeds.
    private global::app.goal.step.action.@this Populate<TReader>(ref TReader reader, global::app.type.reader.ReadContext ctx)
        where TReader : global::app.type.format.IReader, allows ref struct
    {
        global::app.module.@this? module = null;
        string? actionName = null;
        global::app.goal.step.action.@this? action = null;
        global::app.goal.step.action.@this Made()
            => action ??= module == null || actionName == null
                ? throw new global::app.error.PrFormatOutdatedException("an action's module and name come first")
                // Provenance at birth: an action READ is authored, not injected — so it is non-synthetic. A name
                // the module doesn't carry still reads (validation names it), as a plain action.
                : module[actionName]?.Program(_step)
                  ?? new global::app.goal.step.action.@this { Module = module, Name = actionName, Step = _step, Synthetic = false };

        reader.BeginObject();
        while (reader.NextName(out var name))
        {
            switch (name)
            {
                // The wire carries the module NAME; the action holds the element. Resolving here
                // means a .pr naming a module that no longer exists fails at LOAD instead of mid-execution.
                case "module":
                    var named = reader.String();
                    module = ctx.Context.App.module.Named(named)
                        ?? throw new System.Text.Json.JsonException($"module '{named}' isn't one of this app's modules.");
                    break;
                // The .pr's own keys are the only keys — the LLM answers in them too, so the answer
                // reads through the same door a built .pr does.
                case "name": actionName = reader.String(); break;
                case "property":
                    var made = Made();
                    reader.BeginArray();
                    while (reader.NextElement())
                        made.Property.Add(Property(reader.RawValue(), ctx));
                    reader.EndArray();
                    break;
                case "default":
                    var withDefault = Made();
                    reader.BeginArray();
                    while (reader.NextElement())
                        withDefault.Default.Add(Property(reader.RawValue(), ctx));
                    reader.EndArray();
                    break;
                // The old key: skipping it would load the action with no properties, silently.
                case "parameter":
                case "parameters":
                    throw new global::app.error.PrFormatOutdatedException($"action key '{name}' is now 'property'");
                case "child":
                    // chain self-feeds: a child step's goal is this action's step's goal
                    var childSteps = new global::app.goal.step.list.@this();
                    var stepReader = new global::app.goal.step.serializer.Reader(_step.Goal);
                    reader.BeginArray();
                    while (reader.NextElement())
                        if (stepReader.Read(ref reader, null, ctx) is global::app.goal.step.@this child)
                            childSteps.Add(child);
                    reader.EndArray();
                    Made().Child = childSteps;
                    break;
                // a key this format doesn't write is an older builder's: skipping it would load a
                // different action, silently
                default: throw new global::app.error.PrFormatOutdatedException($"action key '{name}' isn't in this .pr format");
            }
        }
        reader.EndObject();
        return Made();
    }

    // One property row — {name, type, value, properties?}. A property whose type is `action` or `list<action>`
    // holds program, not a value: its action(s) are read HERE, by the reader born with the step, so they are born
    // holding that step; the type-reader registry cannot mint one. Every other value is read by its type. Nothing
    // is loaded and no Data is made.
    private global::app.type.property.@this Property(byte[] raw, global::app.type.reader.ReadContext ctx)
    {
        var utf8 = new System.Text.Json.Utf8JsonReader(raw);
        utf8.Read();
        var row = new global::app.type.format.json.Reader(utf8, raw);

        var name = "";
        global::app.type.@this? type = null;
        global::app.type.item.@this? value = null;
        global::app.data.Properties? properties = null;
        // The value's bytes are held until the row closes: the "variable" list after it is what the
        // value is born with.
        byte[]? held = null;
        IReadOnlyList<global::app.type.item.variable.@this>? variables = null;
        row.BeginObject();
        while (row.NextName(out var key))
        {
            switch (key)
            {
                case "name": name = row.Null() ? "" : row.String(); break;
                case "type":
                    type = row.Null() ? null : ctx.Context.App.type.list.Reader.Reader("type", null, ctx.Context)
                        .Read(ref row, null, ctx) as global::app.type.@this;
                    break;
                case "value":
                    if (type is not { IsNull: false })
                        throw new global::app.data.reader.UntypedValueException(name, row.RawValue());
                    held = row.Slice();
                    break;
                case "variable": variables = new global::app.type.item.variable.serializer.Entry().Read(ref row, ctx); break;
                case "properties": properties = global::app.data.Properties.Read(ref row.Inner); break;
                default: throw new global::app.error.PrFormatOutdatedException($"property key '{key}' isn't in this .pr format");
            }
        }
        row.EndObject();
        if (held != null)
        {
            // A row marked a template names its variables; one without the list is an older .pr
            // (rebuild), never parsed on load.
            if (type!.Template != null && variables == null)
                throw new global::app.error.PrFormatOutdatedException($"property '{name}' is a template without its variable list");
            var bytes = new System.Text.Json.Utf8JsonReader(held);
            bytes.Read();
            var slot = new global::app.type.format.json.Reader(bytes, held);
            var born = ctx with { Variable = variables };
            value = type!.Name == "action" ? Read(ref slot, null, born)
                : type.Name == "list" && type.kind.Name == "action" ? Actions(ref slot, born)
                : type.Read(ref slot, born);
        }
        // No value slot — a typed absence under its declared type.
        if (value == null && type is { IsNull: false })
            value = new global::app.type.item.@null.@this(type);
        return new global::app.type.property.@this
        {
            Name = name,
            Type = type ?? ctx.Context.App.type.list["item"],
            Value = value,
            Properties = properties ?? new(),
        };
    }

    // A list<action> slot (on.error's Recovery): each action read by this reader, born holding the step.
    private global::app.goal.step.action.list.@this Actions<TReader>(ref TReader reader, global::app.type.reader.ReadContext ctx)
        where TReader : global::app.type.format.IReader, allows ref struct
    {
        var actions = new global::app.goal.step.action.list.@this();
        reader.BeginArray();
        while (reader.NextElement())
            if (Read(ref reader, null, ctx) is global::app.goal.step.action.@this action) actions.Add(action);
        reader.EndArray();
        return actions;
    }
}
