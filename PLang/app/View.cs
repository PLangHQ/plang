namespace app;

/// <summary>
/// What a write shows — chosen where a write starts and held by its writer. A view holds nothing: it answers which of a
/// value's members show (by their tags), and whether a value that holds variables is kept as written or used.
/// <list type="bullet">
///   <item><b>Store</b> — what plang keeps (a <c>.pr</c>, its store): the <c>[Store]</c> members, nothing masked; a value
///         kept as written (<c>%name%</c>).</item>
///   <item><b>Out</b> — the wire, what others see: the <c>[Out]</c> members, <c>[Sensitive]</c> left out,
///         <c>[Masked]</c> as <c>****</c>; a value used (rendered, resolved).</item>
///   <item><b>Debug</b> — a dump: every member, sensitive and masked ones as <c>****</c>; a value as written beside the
///         value it resolves to, with its type, or <c>not set</c>.</item>
///   <item><b>Snapshot</b> — a run's state at rest: what Store keeps, with <c>[Sensitive]</c> members left out and a
///         sensitive value written as null.</item>
/// </list>
/// A type with no Out/Store/Sensitive/Masked tag anywhere is transparent: every public member shows in Store and Out.
/// </summary>
public abstract class View
{
    /// <summary>What plang keeps.</summary>
    public static readonly View Store = new store();

    /// <summary>What crosses to others.</summary>
    public static readonly View Out = new @out();

    /// <summary>A dump.</summary>
    public static readonly View Debug = new debug();

    /// <summary>A run's state at rest.</summary>
    public static readonly View Snapshot = new snapshot();

    private View() { }

    /// <summary>Whether <paramref name="property"/> shows, on a type that is tag-aware (<paramref name="tagged"/>) or not.</summary>
    internal abstract bool Shows(System.Reflection.PropertyInfo property, bool tagged);

    /// <summary>Whether a shown <paramref name="property"/> writes <c>****</c> in place of its value.</summary>
    internal abstract bool Masks(System.Reflection.PropertyInfo property);

    /// <summary>Characters nobody should see (a key, a password), written as this view shows them: <c>****</c>.</summary>
    internal virtual void Sensitive(string characters, global::app.type.format.IWriter writer) => writer.String("****");

    /// <summary>The name <paramref name="data"/> shows under in this view, or none: a Data's name is plang's own store's
    /// (a server's binding label is not API surface).</summary>
    internal virtual string? Name(global::app.data.@this data) => null;

    /// <summary>Whether <paramref name="type"/> declares what it shows in this view: any wire tag, or (in a dump) a
    /// <c>[Debug]</c> member.</summary>
    internal virtual bool Declares(System.Type type) => Tagged(type);

    /// <summary>A value of plang's own assembly that declares nothing to show: on the wire and in the store it is not
    /// meant to be written, and refuses, naming it.</summary>
    internal virtual void Undeclared(object value, global::app.type.format.IWriter writer)
        => throw new global::app.data.OutputException(
            $"'{value.GetType().FullName}' has no wire contract — it declares no [Out]/[Store] face and is "
            + "not meant to cross the wire. Write what you actually meant to write, or tag the type.",
            "NoWireContract");

    /// <summary>A shown member whose getter threw: the write fails, naming it.</summary>
    internal virtual System.Threading.Tasks.ValueTask Unread(global::app.type.item.kind.reflection.Tagged.Entry member,
        System.Exception thrown, global::app.type.format.IWriter writer)
        => throw new global::app.data.OutputException(
            $"Output failed reading {member.Property.DeclaringType?.Name}.{member.Property.Name}: {thrown.Message}", "OutputGetterThrew", thrown);

    /// <summary>A Data written in this view: its record around its value (its error, when it failed). One whose value holds
    /// variables (a template, a reference) is this view's to write — kept as written, or used. <paramref name="layer"/>:
    /// the Data begins a layer of plang's own format.</summary>
    internal async System.Threading.Tasks.ValueTask Write(global::app.data.@this data, global::app.type.format.IWriter writer, bool layer)
    {
        if (data.Error == null && data.HasVariable)
        {
            await Template(data, writer, layer);
            return;
        }
        await writer.BeginRecord(data, layer);
        await ((global::app.type.item.@this?)data.Error ?? data.Peek()).Output(writer);
        await writer.EndRecord(data);
    }

    /// <summary>A value written in this view, with no Data around it (a property row's value); one that holds variables
    /// is this view's to write — kept as written, or used.</summary>
    internal System.Threading.Tasks.ValueTask Write(global::app.type.item.@this value, global::app.type.format.IWriter writer)
        => value.HasVariable ? Template(value, writer) : value.Output(writer);

    /// <summary>A Data whose value holds variables, in this view: its record around its value as this view takes it.</summary>
    private protected virtual async System.Threading.Tasks.ValueTask Template(global::app.data.@this data, global::app.type.format.IWriter writer, bool layer)
    {
        await writer.BeginRecord(data, layer);
        await Template(data.Peek(), writer);
        await writer.EndRecord(data);
    }

    /// <summary>A value that holds variables, in this view: kept as written, used, or both in a dump.</summary>
    private protected abstract System.Threading.Tasks.ValueTask Template(global::app.type.item.@this value, global::app.type.format.IWriter writer);

    /// <summary>The variables a value kept as written names, as a member of the object its row is closing: plang's own
    /// store writes them (a <c>.pr</c> row's <c>variable</c>, parsed once at build); no other view shows them.</summary>
    internal virtual System.Threading.Tasks.ValueTask Variables(global::app.type.item.@this value, global::app.type.format.IWriter writer)
        => default;


    /// <summary>The Data as this view takes it: what it writes, settled once — a reference a copy of the Data it names,
    /// a template its render, a name that reads nothing the error. A Data that is a failure is taken as it is: the
    /// failure is what it carries. plang's own store keeps it as written.</summary>
    internal virtual async System.Threading.Tasks.ValueTask<global::app.data.@this> Settle(global::app.data.@this data)
    {
        if (!data.Success) return data;
        // what the Data settles to — a reference the Data it names, a template its render; not initialized when a
        // name it names holds nothing, which is the write's error
        var answer = await data.Settle();
        if (!answer.IsInitialized || !answer.Success)
            throw new global::app.error.AppException(answer.Error ?? new global::app.error.VariableNotFoundException(data.Name).Error);
        return answer;
    }

    /// <summary>Whether <paramref name="type"/> carries any wire tag (Out, Store, Snapshot, Sensitive, Masked): a
    /// deliberate host whose members are a contract. A type with none is transparent.</summary>
    internal bool Tagged(System.Type type)
        => type.GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance)
            .Any(p => Is<OutAttribute>(p) || Is<StoreAttribute>(p) || Is<SnapshotAttribute>(p) || Is<SensitiveAttribute>(p) || Is<MaskedAttribute>(p));

    /// <summary>The reader a value of <paramref name="type"/> (its kind <paramref name="kind"/>) is read by in this view:
    /// the one its type registers for this view (a goal's in the snapshot), else the type's own.</summary>
    internal global::app.type.reader.ITypeReader Reader(global::app.type.@this type, string? kind,
        global::app.actor.context.@this context)
        => context.App.type.list.Reader.Typed(type.Name, kind, this) ?? context.App.type.list.Reader.Reader(type.Name, kind, context);

    /// <summary>A member of <paramref name="type"/> read from <paramref name="slot"/>, the value a record holds for it, in
    /// this view: by the reader this view reads its type by (a goal in the snapshot from its key; an error, a record's
    /// properties, by their own), as a <c>.pr</c> slot is read. A record with no value for it gives none.</summary>
    internal global::app.type.item.@this? Make(global::app.type.@this type, global::app.type.item.@this? slot,
        global::app.actor.context.@this context)
    {
        var kind = type.kind.IsEmpty ? null : type.kind.Name;
        return slot?.Read(Reader(type, kind, context), kind, context, this) as global::app.type.item.@this;
    }

    /// <summary>What a value of <paramref name="type"/>, as <paramref name="raw"/> holds it, needs loaded before it is
    /// read in this view — the async half a sync reader can't do. By default nothing.</summary>
    internal virtual System.Threading.Tasks.Task<global::app.data.@this> Load(global::app.type.@this type, string raw,
        global::app.actor.context.@this context)
        => System.Threading.Tasks.Task.FromResult(context.Ok());

    /// <summary><paramref name="value"/> written in this view — every value's write comes through here
    /// (<c>item.Output</c>): by default the value writes itself, its own look for the writer.</summary>
    internal virtual System.Threading.Tasks.ValueTask Output(global::app.type.item.@this value, global::app.type.format.IWriter writer)
        => writer.Write(value);

    private protected bool Is<TAttribute>(System.Reflection.PropertyInfo property) where TAttribute : System.Attribute
        => property.IsDefined(typeof(TAttribute), inherit: false);

    // [JsonIgnore] keeps a runtime-graph member (a path's goal call, a context) out of a store and a dump unless an
    // explicit [Out] or [Store] opts it back in.
    private protected bool IsIgnored(System.Reflection.PropertyInfo property)
        => Is<System.Text.Json.Serialization.JsonIgnoreAttribute>(property) && !Is<OutAttribute>(property) && !Is<StoreAttribute>(property);

    private class store : View
    {
        internal override bool Shows(System.Reflection.PropertyInfo property, bool tagged)
            => !IsIgnored(property) && (!tagged || Is<StoreAttribute>(property));

        // nothing is hidden from plang's own store
        internal override bool Masks(System.Reflection.PropertyInfo property) => false;

        internal override void Sensitive(string characters, global::app.type.format.IWriter writer) => writer.String(characters);

        internal override string? Name(global::app.data.@this data) => data.Name;

        private protected override System.Threading.Tasks.ValueTask Template(global::app.type.item.@this value, global::app.type.format.IWriter writer)
            => value.Output(writer);   // the value as written: a template's text, a reference's name, as the step gave them

        // plang's own store keeps a Data as written: its references and templates with the variables they name
        internal override System.Threading.Tasks.ValueTask<global::app.data.@this> Settle(global::app.data.@this data) => new(data);

        // a value kept as written names its variables beside it, so reading it back never parses
        internal override async System.Threading.Tasks.ValueTask Variables(global::app.type.item.@this value, global::app.type.format.IWriter writer)
        {
            if (!value.HasVariable) return;
            writer.Name("variable");
            await new global::app.type.item.variable.serializer.Entry().Write(writer, value.Variable);
        }

        public override string ToString() => "Store";
    }

    // what store keeps, but never a secret: a [Sensitive] member is left out and a sensitive value is null, so a resumed
    // run asks again rather than going on with a fake value
    private sealed class snapshot : store
    {
        // a type found elsewhere (it tags what finds it, [Snapshot]) shows only that; any other shows what store keeps,
        // never a secret
        internal override bool Shows(System.Reflection.PropertyInfo property, bool tagged)
            => Found(property.ReflectedType!) ? Is<SnapshotAttribute>(property)
                : !Is<SensitiveAttribute>(property) && base.Shows(property, tagged);

        // a value found elsewhere writes what finds it, whatever its own look; any other writes itself
        internal override System.Threading.Tasks.ValueTask Output(global::app.type.item.@this value, global::app.type.format.IWriter writer)
            => Found(value.GetType()) ? value.Members(writer) : writer.Write(value);

        // a value found elsewhere is read from its key, which may name a location (a goal's): each location a key
        // names is loaded by its owner's type first, so the key's own read finds what it names without waiting. A
        // record that is not one (a frame) holds its found members the same way.
        internal override System.Threading.Tasks.Task<global::app.data.@this> Load(global::app.type.@this type, string raw,
            global::app.actor.context.@this context)
            => type.ClrType is { } clr ? Load(clr, Key(raw, context), context) : base.Load(type, raw, context);

        // a key as the record it is written as, its members as the general read makes them
        private static global::app.type.item.dict.@this? Key(string raw, global::app.actor.context.@this context)
        {
            var bytes = System.Text.Encoding.UTF8.GetBytes(raw);
            var utf8 = new System.Text.Json.Utf8JsonReader(bytes);
            utf8.Read();
            var json = new global::app.type.item.kind.json.Reader(utf8, bytes);
            return json.Peek() == global::app.type.format.TokenKind.Object
                ? new global::app.type.item.dict.serializer.Reader().Read(ref json, null, new global::app.type.reader.ReadContext(context)) as global::app.type.item.dict.@this
                : null;
        }

        // the walk: a member found elsewhere in turn (a step's goal), a location loaded by the type that owns it
        private async System.Threading.Tasks.Task<global::app.data.@this> Load(System.Type clr, global::app.type.item.dict.@this? key,
            global::app.actor.context.@this context)
        {
            if (key == null) return context.Ok();
            var types = context.App.type.list;
            foreach (var entry in global::app.type.item.kind.reflection.Tagged.PropertiesFor(clr, this))
            {
                if (key.Get(entry.WireName, context)?.Peek() is not { } member) continue;
                var declared = System.Nullable.GetUnderlyingType(entry.Property.PropertyType) ?? entry.Property.PropertyType;
                var loaded = Found(declared) ? await Load(declared, member as global::app.type.item.dict.@this, context)
                    : Found(clr) && typeof(global::app.type.item.path.@this).IsAssignableFrom(declared)
                      && types[declared].Make(member, context) is global::app.type.item.path.@this location
                        ? await types[clr].Load(location, context.App)
                        : context.Ok();
                if (!loaded.Success) return loaded;
            }
            return context.Ok();
        }

        // Whether a type tags what finds it: read once per type.
        private readonly System.Collections.Concurrent.ConcurrentDictionary<System.Type, bool> _found = new();
        private bool Found(System.Type type)
            => _found.GetOrAdd(type, t => t.GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance)
                .Any(Is<SnapshotAttribute>));

        internal override void Sensitive(string characters, global::app.type.format.IWriter writer) => writer.Null();

        // a Data is named by where it is held (an entry's key): the name a reader gives it after (the variable it is read
        // into) is no part of what was signed
        internal override string? Name(global::app.data.@this data) => null;

        public override string ToString() => "Snapshot";
    }

    private sealed class @out : View
    {
        // a sensitive member never leaves
        internal override bool Shows(System.Reflection.PropertyInfo property, bool tagged)
            => !Is<SensitiveAttribute>(property) && (!tagged || Is<OutAttribute>(property));

        internal override bool Masks(System.Reflection.PropertyInfo property)
            => Is<SensitiveAttribute>(property) || Is<MaskedAttribute>(property);

        // How deep a write of values that name each other has gone, on this flow: using one can cycle (a=%b%, b=%a%), and
        // the cycle fails typed, not by stack overflow.
        private readonly System.Threading.AsyncLocal<int> _depth = new();
        private const int Deepest = 50;

        // used: a reference writes the Data it names, a template its render — each opened, as writing out is a use
        private protected override async System.Threading.Tasks.ValueTask Template(global::app.data.@this data, global::app.type.format.IWriter writer, bool layer)
        {
            if (_depth.Value++ > Deepest)
            {
                _depth.Value = 0;
                throw new global::app.error.AppException($"self-referential variable '{data.Name}' on output", "OutputSelfReference", 500);
            }
            try
            {
                var record = await Settle(data);
                await writer.BeginRecord(record, layer);
                await (await Open(record)).Output(writer);
                await writer.EndRecord(record);
            }
            finally { _depth.Value--; }
        }

        private protected override async System.Threading.Tasks.ValueTask Template(global::app.type.item.@this value, global::app.type.format.IWriter writer)
            => await (await Open(await Settle(new global::app.data.@this("", value, context: writer.Context)))).Output(writer);

        // opens what the Data holds (a reference's content); a value that fails to open is the write's error
        private async System.Threading.Tasks.ValueTask<global::app.type.item.@this> Open(global::app.data.@this data)
        {
            var value = await data.Value();
            if (!data.Success) throw new global::app.error.AppException(data.Error!);
            return value;
        }

        public override string ToString() => "Out";
    }

    private sealed class debug : View
    {
        // a tagged type shows its tagged members (what crosses, what is kept, what only a dump shows); an untagged one
        // is transparent
        internal override bool Shows(System.Reflection.PropertyInfo property, bool tagged)
            => !IsIgnored(property) && (!tagged || Is<OutAttribute>(property) || Is<StoreAttribute>(property)
                || Is<DebugAttribute>(property) || Is<SensitiveAttribute>(property) || Is<MaskedAttribute>(property));

        // a dump tells "not set" from "set but hidden": it never unmasks
        internal override bool Masks(System.Reflection.PropertyInfo property)
            => Is<SensitiveAttribute>(property) || Is<MaskedAttribute>(property);

        internal override bool Declares(System.Type type)
            => Tagged(type) || type.GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance)
                .Any(Is<DebugAttribute>);

        // a dump never throws: what declares nothing is written by its name, never walked
        internal override void Undeclared(object value, global::app.type.format.IWriter writer)
            => writer.String(value.ToString() ?? value.GetType().Name);

        // a member that fails to read shows its error: a dump never ends on one
        internal override async System.Threading.Tasks.ValueTask Unread(global::app.type.item.kind.reflection.Tagged.Entry member,
            System.Exception thrown, global::app.type.format.IWriter writer)
        {
            writer.Name(member.WireName);
            await global::app.error.@this.FromException(thrown).Output(writer);
        }

        // The resolved values each dump (its writer) is writing now, by reference: held weakly by the writer, so a dump
        // that ends leaves on its own.
        private readonly System.Runtime.CompilerServices.ConditionalWeakTable<global::app.type.format.IWriter,
            System.Collections.Generic.HashSet<global::app.type.item.@this>> _writing = new();

        // as written, and beside it what it resolves to with its type; that it is not set, when a name it names reads
        // nothing; or the error it resolves to, as itself
        private protected override async System.Threading.Tasks.ValueTask Template(global::app.type.item.@this value, global::app.type.format.IWriter writer)
        {
            writer.BeginObject();
            writer.Name("written");
            await value.Output(writer);
            // a dump never ends the program it watches: a failure to resolve is shown, never thrown
            global::app.data.@this used;
            try { used = await new global::app.data.@this("", value, context: writer.Context).Settle(); }
            catch (global::app.error.AppException failed) { used = writer.Context.Error(failed.Error); }
            // a name that reads nothing: the miss, or the error its own status calls not found
            var unread = !used.IsInitialized || used.Error is { Status.IsNotFound: true };
            if (unread)
            {
                writer.Name("value");
                writer.String("not set");
            }
            else if (used.Error is { } error)
            {
                writer.Name("error");
                await error.Output(writer);
            }
            else
            {
                var resolved = await used.Value();
                writer.Name("value");
                // what a value inside a dump resolves to can be the structure the dump is in (a goal whose action reads
                // %!data%, while %!data% holds that goal): one this dump is already writing is named, never walked again
                var writing = _writing.GetValue(writer, _ => new(System.Collections.Generic.ReferenceEqualityComparer.Instance));
                if (!writing.Add(resolved)) writer.String(resolved.ToString() ?? resolved.Type.Name);
                else
                {
                    try { await resolved.Output(writer); }
                    finally { writing.Remove(resolved); }
                }
                // the type as the format writes one (text its word, json and plang its identity)
                writer.Name("type");
                await resolved.Type.Output(writer);
            }
            writer.EndObject();
        }

        public override string ToString() => "Debug";
    }
}

[AttributeUsage(AttributeTargets.Property)]
public sealed class StoreAttribute : Attribute { }

/// <summary>Marks what a value is found by (a goal's name, its .pr and hash; a file's path): in a snapshot, a value whose
/// type tags any writes those members alone, and comes back as what they find — a value found elsewhere (the program,
/// the disk) is never saved whole.</summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class SnapshotAttribute : Attribute { }

/// <summary>Catalog visibility for the builder LLM. On a method it marks a member that needs the
/// asker's context (one <c>actor.context.@this</c> parameter) — listed as a field like a property, and read
/// like one: navigation (<c>%!app.name%</c>) calls it with the asker's context. So such a method must be a fact
/// about its owner, with no effects — a read may call it any number of times.</summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Method)]
public sealed class LlmBuilderAttribute : Attribute { }

[AttributeUsage(AttributeTargets.Property)]
public sealed class DebugAttribute : Attribute { }

[AttributeUsage(AttributeTargets.Property)]
public sealed class DefaultAttribute : Attribute { }

/// <summary>
/// Marks properties that should be serialized when Data leaves the system (transport/wire outbound).
/// Used for transport metadata like Signature.
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class OutAttribute : Attribute { }

/// <summary>
/// Marks properties that should be deserialized when Data arrives from the wire (transport inbound).
/// Used for transport metadata like Signature that needs to round-trip.
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class InAttribute : Attribute { }

/// <summary>
/// Marks properties that contain sensitive data (e.g., private keys).
/// Left out of what crosses to others (Out); kept whole in plang's own store (Store); masked in a dump (Debug).
/// Does NOT block code-level access.
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class SensitiveAttribute : Attribute { }

/// <summary>
/// Marks a property that ships on the wire with its name visible but its value replaced
/// by the literal string "****". Distinct from <see cref="SensitiveAttribute"/> (which
/// excludes the property entirely). Canonical use: <c>setting.value</c> — receivers know
/// the setting is configured without seeing the secret.
/// Combines with <see cref="OutAttribute"/>; honored in both Out and Debug views.
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class MaskedAttribute : Attribute { }
