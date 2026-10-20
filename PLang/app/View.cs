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

    private View() { }

    /// <summary>Whether <paramref name="property"/> shows, on a type that is tag-aware (<paramref name="tagged"/>) or not.</summary>
    internal abstract bool Shows(System.Reflection.PropertyInfo property, bool tagged);

    /// <summary>Whether a shown <paramref name="property"/> writes <c>****</c> in place of its value.</summary>
    internal abstract bool Masks(System.Reflection.PropertyInfo property);

    /// <summary>Characters nobody should see (a key, a password), as this view shows them: <c>****</c>.</summary>
    internal virtual string Sensitive(string characters) => "****";

    /// <summary>The name <paramref name="record"/> shows under in this view, or none: a Data's name is plang's own store's
    /// (a server's binding label is not API surface).</summary>
    internal virtual string? Name(global::app.data.@this record) => null;

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

    /// <summary>A Data whose value holds variables (a template, a reference), written in this view: its record around
    /// its value, kept as written or used. <paramref name="layer"/>: the Data begins a layer of plang's own format.</summary>
    internal virtual async System.Threading.Tasks.ValueTask Write(global::app.data.@this data, global::app.type.format.IWriter writer, bool layer)
    {
        await writer.BeginRecord(data, layer);
        await Write(data.Peek(), writer);
        await writer.EndRecord(data);
    }

    /// <summary>A value that holds variables, written in this view, with no Data around it (a property row's value):
    /// kept as written, or used.</summary>
    internal abstract System.Threading.Tasks.ValueTask Write(global::app.type.item.@this value, global::app.type.format.IWriter writer);

    /// <summary>What only this view shows beside <paramref name="value"/>, as members of the object the value's own write
    /// is closing: plang's own store the variables a value kept as written names (a <c>.pr</c> row's <c>variable</c>,
    /// parsed once at build); a dump the value's diagnostics (its <c>[Debug]</c>-only members). What crosses shows
    /// nothing more.</summary>
    internal virtual System.Threading.Tasks.ValueTask Own(global::app.type.item.@this value, global::app.type.format.IWriter writer)
        => default;

    // The value as written: its own write — a template's text, a reference's name, as the step gave them.
    private protected System.Threading.Tasks.ValueTask Written(global::app.type.item.@this value, global::app.type.format.IWriter writer)
        => value.Output(writer);

    // What a Data settles to — a reference the Data it names, a template its render; not initialized when a name it
    // names holds nothing.
    private protected System.Threading.Tasks.ValueTask<global::app.data.@this> Settled(global::app.data.@this data) => data.Settle();

    // Used where it must be: a name it can't read is the write's error.
    private protected async System.Threading.Tasks.ValueTask<global::app.data.@this> Used(global::app.data.@this data)
    {
        var used = await Settled(data);
        if (!used.IsInitialized || !used.Success)
            throw new global::app.error.AppException(used.Error ?? new global::app.error.VariableNotFoundException(data.Name).Error);
        return used;
    }

    /// <summary>Whether <paramref name="type"/> carries any wire tag (Out, Store, Sensitive, Masked): a deliberate host
    /// whose members are a contract. A type with none is transparent.</summary>
    internal bool Tagged(System.Type type)
        => type.GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance)
            .Any(p => Is<OutAttribute>(p) || Is<StoreAttribute>(p) || Is<SensitiveAttribute>(p) || Is<MaskedAttribute>(p));

    private protected bool Is<TAttribute>(System.Reflection.PropertyInfo property) where TAttribute : System.Attribute
        => property.IsDefined(typeof(TAttribute), inherit: false);

    // [JsonIgnore] keeps a runtime-graph member (a path's goal call, a context) out of a store and a dump unless an
    // explicit [Out] or [Store] opts it back in.
    private protected bool Ignored(System.Reflection.PropertyInfo property)
        => Is<System.Text.Json.Serialization.JsonIgnoreAttribute>(property) && !Is<OutAttribute>(property) && !Is<StoreAttribute>(property);

    private sealed class store : View
    {
        internal override bool Shows(System.Reflection.PropertyInfo property, bool tagged)
            => !Ignored(property) && (!tagged || Is<StoreAttribute>(property));

        // nothing is hidden from plang's own store
        internal override bool Masks(System.Reflection.PropertyInfo property) => false;

        internal override string Sensitive(string characters) => characters;

        internal override string? Name(global::app.data.@this record) => record.Name;

        internal override System.Threading.Tasks.ValueTask Write(global::app.type.item.@this value, global::app.type.format.IWriter writer)
            => Written(value, writer);

        // a value kept as written names its variables beside it, so reading it back never parses
        internal override async System.Threading.Tasks.ValueTask Own(global::app.type.item.@this value, global::app.type.format.IWriter writer)
        {
            if (!value.HasVariable) return;
            writer.Name("variable");
            await new global::app.type.item.variable.serializer.Entry().Write(writer, value.Variable);
        }

        public override string ToString() => "Store";
    }

    private sealed class @out : View
    {
        // a sensitive member never leaves
        internal override bool Shows(System.Reflection.PropertyInfo property, bool tagged)
            => !Is<SensitiveAttribute>(property) && (!tagged || Is<OutAttribute>(property));

        internal override bool Masks(System.Reflection.PropertyInfo property)
            => Is<SensitiveAttribute>(property) || Is<MaskedAttribute>(property);

        // used: a reference writes the Data it names, a template its render — each opened, as writing out is a use
        internal override async System.Threading.Tasks.ValueTask Write(global::app.data.@this data, global::app.type.format.IWriter writer, bool layer)
        {
            var used = await Used(data);
            await writer.BeginRecord(used, layer);
            await Written(await Opened(used), writer);
            await writer.EndRecord(used);
        }

        internal override async System.Threading.Tasks.ValueTask Write(global::app.type.item.@this value, global::app.type.format.IWriter writer)
            => await Written(await Opened(await Used(new global::app.data.@this("", value, context: writer.Context))), writer);

        // what a used Data holds, opened (a reference's content); a value that fails to open is the write's error
        private async System.Threading.Tasks.ValueTask<global::app.type.item.@this> Opened(global::app.data.@this used)
        {
            var value = await used.Value();
            if (!used.Success) throw new global::app.error.AppException(used.Error!);
            return value;
        }

        public override string ToString() => "Out";
    }

    private sealed class debug : View
    {
        // a tagged type shows its tagged members (what crosses, what is kept, what only a dump shows); an untagged one
        // is transparent
        internal override bool Shows(System.Reflection.PropertyInfo property, bool tagged)
            => !Ignored(property) && (!tagged || Is<OutAttribute>(property) || Is<StoreAttribute>(property)
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

        // a value's diagnostics: the members only a dump shows ([Debug], neither crossing nor kept), each written itself
        internal override async System.Threading.Tasks.ValueTask Own(global::app.type.item.@this value, global::app.type.format.IWriter writer)
        {
            foreach (var property in value.GetType().GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance))
            {
                if (!Is<DebugAttribute>(property) || Is<OutAttribute>(property) || Is<StoreAttribute>(property)
                    || property.GetIndexParameters().Length > 0) continue;
                // a member that fails to read shows its error: a dump never ends on one
                object? member;
                global::app.error.@this? failed = null;
                try { member = property.GetValue(value); }
                catch (System.Reflection.TargetInvocationException thrown) { member = null; failed = global::app.error.@this.FromException(thrown.InnerException ?? thrown); }
                if (member is null && failed is null) continue;
                writer.Name(System.Reflection.CustomAttributeExtensions.GetCustomAttribute<System.Text.Json.Serialization.JsonPropertyNameAttribute>(property)?.Name
                    ?? System.Text.Json.JsonNamingPolicy.CamelCase.ConvertName(property.Name));
                // a plang value is itself; a C# value is the plang value it makes
                if (failed != null) await failed.Output(writer);
                else await global::app.type.item.@this.Create(member, writer.Context).Output(writer);
            }
        }

        // as written, and beside it what it resolves to with its type; that it is not set, when a name it names reads
        // nothing; or the error it resolves to, as itself
        internal override async System.Threading.Tasks.ValueTask Write(global::app.type.item.@this value, global::app.type.format.IWriter writer)
        {
            writer.BeginObject();
            writer.Name("written");
            await Written(value, writer);
            // a dump never ends the program it watches: a failure to resolve is shown, never thrown
            global::app.data.@this used;
            try { used = await Settled(new global::app.data.@this("", value, context: writer.Context)); }
            catch (global::app.error.AppException failed) { used = writer.Context.Error(failed.Error); }
            var unread = !used.IsInitialized || used.Error is { Key: "VariableNotFound" };
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
                await Written(resolved, writer);
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
