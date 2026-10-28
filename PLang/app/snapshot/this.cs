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

    // What the app wrote, as it was read back; null for one taken from a live app.
    private readonly global::app.type.item.dict.@this? _written;

    /// <summary>The app whose run this is, taken in <paramref name="context"/>.</summary>
    internal @this(global::app.@this app, global::app.actor.context.@this context) : base(context.App.type.list[typeof(@this)])
    {
        Context = context;
        _app = app;
    }

    // The app taken; null for one read back.
    private readonly global::app.@this? _app;

    /// <summary>What a snapshot's text held (<paramref name="written"/>), read back in <paramref name="context"/>.</summary>
    internal @this(global::app.type.item.dict.@this written, global::app.actor.context.@this context) : base(context.App.type.list[typeof(@this)])
    {
        Context = context;
        _written = written;
    }

    /// <summary>Reads what the app wrote onto <paramref name="into"/>, the app the run goes on in: each object it holds
    /// reads itself back (<see cref="global::app.type.item.kind.reflection.@this"/>'s read onto a host). What can't come
    /// back is the answer, its error whole.</summary>
    internal async System.Threading.Tasks.Task<global::app.data.@this> Read(global::app.@this into, global::app.actor.context.@this context)
    {
        // a snapshot that holds nothing leaves the app as it is
        if (_written?.Get("app", context) is not { } held) return context.Ok();
        // what the app wrote names locations (a frame's goal): each is loaded into the app the run goes on in first, so
        // the read that follows finds what it names held. One opened and edited (a navigation) is written again first.
        if ((held.Peek() as global::app.type.item.wire.@this ?? await global::app.type.item.wire.@this.Of(held.Peek()!, View, context))
            is { Raw: string raw })
        {
            var loaded = await View.Load(into.type.list[typeof(global::app.@this)], raw, context);
            if (!loaded.Success) return loaded;
        }
        try
        {
            var dict = context.App.type.list[typeof(global::app.type.item.dict.@this)];
            if ((held.Peek() as global::app.type.item.dict.@this ?? View.Make(dict, held.Peek(), context)) is not global::app.type.item.dict.@this written)
                return context.Error(new global::app.error.@this(
                    $"the snapshot's app reads as a {held.Peek()?.Type.Name ?? "nothing"}, not what an app writes", "SnapshotRowUnread", 400));
            ((global::app.type.item.kind.reflection.@this)context.App.type.list["item"].kind["*"]!).Read(written, into, context, View);
            return context.Ok();
        }
        catch (global::app.error.AppException refused) { return context.Error(refused.Error); }
    }

    /// <summary>A copy of a read snapshot holds a copy of what it read, born in the same context; a taken one holds the
    /// app, which is never copied.</summary>
    protected internal override global::app.type.item.@this Clone()
        => _written is { } written ? new @this((global::app.type.item.dict.@this)written.Clone(), Context) : this;

    /// <summary>A snapshot that holds nothing is empty.</summary>
    public override bool IsTruthy() => _app != null || _written?.IsTruthy() == true;

    /// <summary>A snapshot writes what it holds: the app whole, under <c>app</c> — the root is where the app lives — or
    /// what the app wrote, as it was read.</summary>
    protected internal override async System.Threading.Tasks.ValueTask Write(global::app.type.format.IWriter writer)
    {
        writer.BeginObject();
        if (_written is { } written)
            // each as it was read: what the app wrote, its bytes relayed as they are while nothing changed them
            foreach (var entry in written.Rows(Context))
            {
                writer.Name(entry.Name);
                if (entry.Peek() is { } value) await value.Output(writer);
                else writer.Null();
            }
        else
        {
            writer.Name("app");
            await _app!.Members(writer);
        }
        writer.EndObject();
    }
}
