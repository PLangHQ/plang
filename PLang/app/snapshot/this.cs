namespace app.snapshot;

/// <summary>
/// A run's state: the app written in the snapshot view, and read back the same way. Taken, it holds the app, its one
/// member: the root is where the app lives, so it is written whole there, each object writing its own tagged members
/// (an actor under the app's actors, its context, its variables and its calls), and anything that points at an object
/// writes what finds it (<see cref="global::app.type.item.@this.Key"/>). Read back, it holds what the app wrote, until a
/// resume reads it onto the app it goes on in, each object reading itself back. A template comes back only from bytes the
/// gate trusts (<see cref="global::app.type.item.source"/>), so a template inside it comes back literal: the bytes' origin
/// is not known below the snapshot's reader.
/// </summary>
[global::app.Attributes.PlangType("snapshot")]
public sealed partial class @this : global::app.type.item.@this, global::app.type.item.ICreate<@this>, IView<@this>
{
    /// <summary>A snapshot is written and read in the snapshot view: a run's state at rest.</summary>
    public static global::app.View View => global::app.View.Snapshot;

    /// <summary>The actor context this snapshot is born in: what it holds answers as it.</summary>
    public global::app.actor.context.@this Context { get; }

    // The app: the live app when taken, what it wrote when read back (its bytes until a navigation opens them); null
    // when the snapshot holds nothing.
    private global::app.type.item.@this? _app;

    /// <summary>A snapshot of <paramref name="app"/>, born in <paramref name="context"/>: the live app taken, or what an
    /// app wrote, read back.</summary>
    internal @this(global::app.type.item.@this? app, global::app.actor.context.@this context) : base(context.App.type.list[typeof(@this)])
    {
        Context = context;
        _app = app;
    }

    /// <summary>Reads what the app wrote onto <paramref name="app"/>, the app the run goes on in: each object it holds
    /// reads itself back (<see cref="global::app.type.item.kind.reflection.@this"/>'s read onto a host). What can't come
    /// back is the answer, its error whole.</summary>
    internal async System.Threading.Tasks.Task<global::app.data.@this> Read(global::app.@this app, global::app.actor.context.@this context)
    {
        // a snapshot that holds nothing leaves the app as it is
        if (_app is not { } value) return context.Ok();
        // what the app wrote names locations (a frame's goal): each is loaded into the app the run goes on in first, so
        // the read that follows finds what it names held. One opened and edited (a navigation) is written again first, and
        // read back from what it wrote: each object made again from the structure as it is now, never the objects the
        // navigation made (a frame born with the step its place named before the edit).
        try
        {
            // the app's name, read now: what the snapshot names its app by is checked against it while the read can't wait
            await app.Name(context);
            var written = value as global::app.type.item.wire.@this ?? await global::app.type.item.wire.@this.Of(value, View, context);
            if (written is { Raw: string raw })
            {
                var loaded = await View.Load(app.type.list[typeof(global::app.@this)], raw, context);
                if (!loaded.Success) return loaded;
            }
            if (View.Make(context.App.type.list[typeof(global::app.type.item.dict.@this)], written, context) is not global::app.type.item.dict.@this dict)
                return context.Error(new global::app.error.@this(
                    $"the snapshot's app reads as a {value.Type.Name}, not what an app writes", "SnapshotRowUnread", 400));
            ((global::app.type.item.kind.reflection.@this)context.App.type.list["item"].kind["*"]!).Read(dict, app, context, View);
            return context.Ok();
        }
        catch (global::app.error.AppException ex) { return context.Error(ex.Error); }
    }

    /// <summary>A copy holds a copy of its app, born in the same context: a live app is its own copy.</summary>
    protected internal override global::app.type.item.@this Clone() => new @this(_app?.Clone(), Context);

    /// <summary>A snapshot that holds nothing is empty.</summary>
    public override bool IsTruthy() => _app?.IsTruthy() == true;

    /// <summary>A snapshot writes its app under <c>app</c>, in the snapshot's own view whatever view writes it (a .plang
    /// file, the wire): a live app its members, what an app wrote as it was read (its bytes relayed as they are while
    /// nothing changed them), one opened and edited written again as the snapshot writes it.</summary>
    protected internal override async System.Threading.Tasks.ValueTask Write(global::app.type.format.IWriter writer)
    {
        writer.BeginObject();
        if (_app is { } app)
        {
            writer.Name("app");
            // what a navigation opened is written again as the snapshot writes it; the live app and the bytes it took
            // write themselves, as they did
            await writer.Write(app is global::app.type.item.dict.@this opened ? await global::app.type.item.wire.@this.Of(opened, View, writer.Context) : app);
        }
        writer.EndObject();
    }
}
