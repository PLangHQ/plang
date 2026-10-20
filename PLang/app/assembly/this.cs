namespace app.assembly;

/// <summary>
/// What a plang assembly's code says — the facts every app made from it reads the same: its classes that are types,
/// its kind classes, its formats, its closed sets, its family subclasses and its actions. Each fact is read from the
/// classes (reflection) the first time it is asked, then held; nothing here is any app's. An app is born holding the
/// assembly it is made from (<c>new app(path, assembly: …)</c>) — whoever makes many apps reads the assembly once and
/// hands it to each; an app given none reads its own.
/// </summary>
public sealed class @this
{
    /// <summary>The assembly read.</summary>
    public System.Reflection.Assembly Clr { get; }

    public @this(System.Reflection.Assembly clr) => Clr = clr;

    // Each fact is read once: the first to ask reads it, and anyone asking meanwhile (a server's first requests, a test
    // run's first tests, all at once) waits for that answer instead of reading it again. One gate for them all — a fact
    // reads others while it is read.
    private object? _gate;

    private System.Type[]? _classes;
    // every class the assembly holds that its code wrote, read once for every fact below — not the ones the compiler
    // makes (a lambda's closure, an async method's state machine: `<…>` in the name), which are never a type, a kind,
    // a format or an action
    private System.Type[] Classes => _classes ?? System.Threading.LazyInitializer.EnsureInitialized(ref _classes, ref _gate,
        () => Clr.GetTypes().Where(clr => !clr.Name.Contains('<')).ToArray());

    /// <summary>A class that is a plang type of its own: the name it goes by, and what the class declares of itself —
    /// its aliases, the C# shapes it owns, whether plang keeps it for itself, what it is offered. Read from the class
    /// once; every entry made from it holds the same answers.</summary>
    internal sealed class Type
    {
        public string Name { get; }
        public System.Type Class { get; }
        public System.Collections.Generic.IReadOnlyList<string> Alias { get; }
        public System.Collections.Generic.IReadOnlyList<global::app.type.convert.OwnedClr> Owned { get; }
        public bool Internal { get; }
        public System.Collections.Generic.IReadOnlyList<global::app.type.item.@this>? Offer { get; }
        /// <summary>Every name the type answers to — its name, its namespace, its aliases: what no other type may claim.</summary>
        public string[] Claims { get; }

        public Type(string name, System.Type clr)
        {
            Name = name;
            Class = clr;
            Alias = Declared<System.Collections.Generic.IReadOnlyList<string>>("Alias") ?? [];
            var space = typeof(global::app.type.item.@this).IsAssignableFrom(clr) ? global::app.type.item.@this.NamespaceOf(clr) : null;
            Claims = [name.ToLowerInvariant(), .. space != null && !string.Equals(space, name, System.StringComparison.OrdinalIgnoreCase) ? [space] : System.Array.Empty<string>(), .. Alias];
            Owned = Declared<System.Collections.Generic.IReadOnlyList<global::app.type.convert.OwnedClr>>("OwnedClrTypes") ?? [];
            // declared by the class itself: a base that is internal (reference) leaves its subclasses (file, url) named
            Internal = clr.GetProperty("Internal", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static
                                                   | System.Reflection.BindingFlags.DeclaredOnly)?.GetValue(null) is true;
            Offer = Declared<System.Collections.Generic.IReadOnlyList<global::app.type.item.@this>>("Offer");

            T? Declared<T>(string member) where T : class
                => clr.GetProperty(member, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static
                                           | System.Reflection.BindingFlags.FlattenHierarchy)?.GetValue(null) as T;
        }
    }

    private Type[]? _type;
    /// <summary>The classes that are types of their own, by the name each goes by.</summary>
    internal System.Collections.Generic.IReadOnlyList<Type> type
        => _type ?? System.Threading.LazyInitializer.EnsureInitialized(ref _type, ref _gate, () => Classes
            .Select(clr => global::app.type.list.@this.NameOf(clr) is { } name ? new Type(name, clr) : null)
            .OfType<Type>().ToArray());

    /// <summary>The code's types by every name they answer to and by their class — each the type's place in
    /// <see cref="type"/>, the first that answers keeping a name. A name two classes claim is plang broken, thrown.</summary>
    internal sealed class Index
    {
        public System.Collections.Generic.IReadOnlyDictionary<string, int> Name { get; }
        public System.Collections.Generic.IReadOnlyDictionary<System.Type, int> Class { get; }

        public Index(System.Collections.Generic.IReadOnlyList<Type> types)
        {
            var name = new System.Collections.Generic.Dictionary<string, int>(System.StringComparer.OrdinalIgnoreCase);
            var @class = new System.Collections.Generic.Dictionary<System.Type, int>();
            for (var at = 0; at < types.Count; at++)
            {
                foreach (var claim in types[at].Claims)
                    if (name.TryGetValue(claim, out var held))
                    {
                        if (types[held].Class != types[at].Class)
                            throw new System.InvalidOperationException(
                                $"type name '{claim}' is claimed by both {types[held].Class.FullName} and {types[at].Class.FullName} — one name, one class.");
                    }
                    else name[claim] = at;
                @class.TryAdd(types[at].Class, at);
            }
            Name = name;
            Class = @class;
        }
    }

    private Index? _index;
    /// <summary>The code's types by name and by class.</summary>
    internal Index index => _index ?? System.Threading.LazyInitializer.EnsureInitialized(ref _index, ref _gate, () => new Index(type));

    /// <summary>A kind class, the name of the type it is a kind of (the T of its kind&lt;T&gt;), and how it is made
    /// from that type's entry.</summary>
    internal readonly record struct Kind(string Owner, System.Reflection.ConstructorInfo Made);

    private Kind[]? _kind;
    /// <summary>The kind classes: each born from nothing but the entry of the type it is a kind of.</summary>
    internal System.Collections.Generic.IReadOnlyList<Kind> kind
        => _kind ?? System.Threading.LazyInitializer.EnsureInitialized(ref _kind, ref _gate, () => KindClasses().ToArray());

    private System.Collections.Generic.IEnumerable<Kind> KindClasses()
    {
        foreach (var clr in Classes)
            if (typeof(global::app.type.kind.@this).IsAssignableFrom(clr) && clr is { IsAbstract: false, ContainsGenericParameters: false }
                && clr.GetConstructor([typeof(global::app.type.@this)]) is { } made)
                for (var parent = clr.BaseType; parent != null; parent = parent.BaseType)
                    if (parent.IsGenericType && parent.GetGenericTypeDefinition() == typeof(global::app.type.kind.@this<>))
                    {
                        yield return new Kind(global::app.type.item.@this.NameOf(parent.GetGenericArguments()[0]), made);
                        break;
                    }
    }

    /// <summary>A class that declares formats: its formats, the name it goes by, and how one of its format kinds is made.</summary>
    internal readonly record struct Format(System.Type Class, string Name, global::app.Attributes.FormatAttribute[] Attribute,
        System.Reflection.ConstructorInfo Made);

    private Format[]? _format;
    /// <summary>The classes that declare formats (<c>[Format]</c>), each with its declarations.</summary>
    internal System.Collections.Generic.IReadOnlyList<Format> format
        => _format ?? System.Threading.LazyInitializer.EnsureInitialized(ref _format, ref _gate, () => Classes
            .Where(clr => clr.IsDefined(typeof(global::app.Attributes.FormatAttribute), inherit: false))
            .Select(clr => new Format(clr, global::app.type.item.@this.NameOf(clr),
                System.Reflection.CustomAttributeExtensions.GetCustomAttributes<global::app.Attributes.FormatAttribute>(clr, inherit: false).ToArray(),
                typeof(global::app.type.kind.format.@this<>).MakeGenericType(clr)
                    .GetConstructor([typeof(global::app.Attributes.FormatAttribute), typeof(global::app.type.@this)])!))
            .ToArray());

    /// <summary>A class marked a choice, and how its closed reader is made from the set's name.</summary>
    internal readonly record struct Choice(System.Type Class, System.Reflection.ConstructorInfo Reader);

    private Choice[]? _choice;
    /// <summary>The classes marked a choice (<c>[Choice]</c>): each a closed set a <c>choice&lt;T&gt;</c> draws on.</summary>
    internal System.Collections.Generic.IReadOnlyList<Choice> choice
        => _choice ?? System.Threading.LazyInitializer.EnsureInitialized(ref _choice, ref _gate, () => Classes
            .Where(clr => clr.IsDefined(typeof(global::app.Attributes.ChoiceAttribute), inherit: false))
            .Select(clr => new Choice(clr, typeof(global::app.type.item.choice.serializer.Reader<>).MakeGenericType(clr)
                .GetConstructor([typeof(string)])!))
            .ToArray());

    /// <summary>A class under a family that has kinds: the family's name, its kind's name, how the kind is made, and
    /// how the reader it ships (in its serializer namespace) is made; null when it ships none.</summary>
    internal readonly record struct Subclass(System.Type Class, string Family, string Kind, System.Reflection.ConstructorInfo Made,
        System.Reflection.ConstructorInfo? Reader);

    private Subclass[]? _subclass;
    /// <summary>The classes that are a kind of their family, by their own name — not one named as its family.</summary>
    internal System.Collections.Generic.IReadOnlyList<Subclass> subclass
        => _subclass ?? System.Threading.LazyInitializer.EnsureInitialized(ref _subclass, ref _gate, () => Subclasses().ToArray());

    private System.Collections.Generic.IEnumerable<Subclass> Subclasses()
    {
        var readers = Classes.Where(r => r.Namespace is { } ns && ns.EndsWith(".serializer", System.StringComparison.Ordinal)
                && typeof(global::app.type.reader.ITypeReader).IsAssignableFrom(r)
                && r is { IsAbstract: false } && r.GetConstructor(System.Type.EmptyTypes) != null)
            .GroupBy(r => r.Namespace!).ToDictionary(group => group.Key, group => group.First());
        foreach (var clr in Classes)
            if (clr is { IsAbstract: false, ContainsGenericParameters: false } && global::app.type.item.@this.FamilyOf(clr) is { } member
                && global::app.type.item.@this.NameOf(member.Family) is { } family && member.Kind != family)
                yield return new Subclass(clr, family, member.Kind,
                    typeof(global::app.type.kind.subclass.@this<>).MakeGenericType(member.Family)
                        .GetConstructor([typeof(string), typeof(System.Type), typeof(global::app.type.@this)])!,
                    readers.GetValueOrDefault($"{clr.Namespace}.serializer")?.GetConstructor(System.Type.EmptyTypes));
    }

    /// <summary>A reader the code ships in a type's <c>serializer</c> folder: the name of the type it reads (the word
    /// its folder's type goes by, else the folder's last segment) and either how its token reader is made (an instance
    /// class, its kind its own) or its whole-payload read — a static <c>Read(raw, kind, ReadContext)</c> bound once —
    /// under the kind its class name says (<c>Default</c> any kind).</summary>
    internal sealed record Reader(string Type, string Kind, System.Reflection.ConstructorInfo? Made,
        global::app.type.reader.@this.Read? Read);

    private Reader[]? _reader;
    /// <summary>The readers the code ships, each type's from its serializer folder.</summary>
    internal System.Collections.Generic.IReadOnlyList<Reader> reader
        => _reader ?? System.Threading.LazyInitializer.EnsureInitialized(ref _reader, ref _gate, () => Shipped().ToArray());

    /// <summary>A pair the code ships: how values of a class (and every class under it) look in an output kind, when it
    /// isn't their own write — the IOutput class in the class's <c>format</c> folder, named for the kind. Made once,
    /// shared by every app (a pair holds nothing of an app).</summary>
    internal sealed record Pair(string Kind, System.Type Class, global::app.type.format.IOutput Output);

    private Pair[]? _pair;
    /// <summary>The pairs the code ships, each type's from its format folder.</summary>
    internal System.Collections.Generic.IReadOnlyList<Pair> pair
        => _pair ?? System.Threading.LazyInitializer.EnsureInitialized(ref _pair, ref _gate, () => Classes
            .Where(clr => clr is { IsAbstract: false, Namespace: { } ns } && ns.EndsWith(".format", System.StringComparison.Ordinal)
                && typeof(global::app.type.format.IOutput).IsAssignableFrom(clr))
            .Select(clr => (clr, owner: Clr.GetType(clr.Namespace![..^".format".Length] + ".this"), made: clr.GetConstructor(System.Type.EmptyTypes)))
            .Where(each => each.owner != null && each.made != null)
            .Select(each => new Pair(each.clr.Name, each.owner!, (global::app.type.format.IOutput)each.made!.Invoke(null)))
            .ToArray());

    /// <summary>The code's readers by (type, kind), made once and shared by every app made from the code (a reader holds
    /// nothing of an app — what it reads arrives with its read): each type's from its serializer folder, and the closed
    /// sets' and family subclasses' own, which answer before a folder's under the same key.</summary>
    internal sealed class Readers
    {
        public System.Collections.Generic.IReadOnlyDictionary<(string Type, string Kind), global::app.type.reader.@this.Read> Read { get; }
        public System.Collections.Generic.IReadOnlyDictionary<(string Type, string Kind), global::app.type.reader.ITypeReader> Typed { get; }

        public Readers(@this code)
        {
            var read = new System.Collections.Generic.Dictionary<(string, string), global::app.type.reader.@this.Read>();
            var typed = new System.Collections.Generic.Dictionary<(string, string), global::app.type.reader.ITypeReader>();
            foreach (var reader in code.reader)
                if (reader.Made is { } made)
                {
                    var instance = (global::app.type.reader.ITypeReader)made.Invoke(null);
                    typed[(reader.Type, instance.Kind)] = instance;
                }
                else read[(reader.Type, reader.Kind)] = reader.Read!;
            foreach (var owned in code.kinds.Owned.Values)
            {
                foreach (var (choice, name) in owned.Choice)
                    typed[("choice", name)] = (global::app.type.reader.ITypeReader)choice.Reader.Invoke([name]);
                foreach (var subclass in owned.Subclass)
                    if (subclass.Reader is { } made)
                        typed[(subclass.Family, subclass.Kind)] = (global::app.type.reader.ITypeReader)made.Invoke(null);
            }
            Read = read;
            Typed = typed;
        }
    }

    private Readers? _readers;
    /// <summary>The code's readers, shared by every app made from it.</summary>
    internal Readers readers => _readers ?? System.Threading.LazyInitializer.EnsureInitialized(ref _readers, ref _gate, () => new Readers(this));

    private System.Collections.Generic.IEnumerable<Reader> Shipped()
    {
        // the words the item classes of each namespace declare ([PlangType]) — a folder whose @this is no type reads
        // the one type its classes declare by word (a module's: [PlangType("ask")] in app.module.output)
        var words = Classes.Where(clr => clr.Namespace != null && typeof(global::app.type.item.@this).IsAssignableFrom(clr))
            .GroupBy(clr => clr.Namespace!)
            .ToDictionary(group => group.Key, group => group
                .Select(clr => System.Reflection.CustomAttributeExtensions.GetCustomAttribute<global::app.Attributes.PlangTypeAttribute>(clr, inherit: false)?.Name)
                .Where(word => !string.IsNullOrEmpty(word)).Distinct().ToList());
        foreach (var clr in Classes)
        {
            if (!clr.IsClass || clr.Namespace is not { } ns || !ns.EndsWith(".serializer", System.StringComparison.Ordinal)) continue;
            var head = ns[..ns.LastIndexOf(".serializer", System.StringComparison.Ordinal)];
            var lastDot = head.LastIndexOf('.');
            if (lastDot < 0) continue;
            var type = global::app.type.item.@this.NameOf(Clr, head)
                       ?? (words.GetValueOrDefault(head) is [var word] ? word : null)
                       ?? head[(lastDot + 1)..].TrimStart('@');
            if (!clr.IsAbstract && !clr.IsGenericTypeDefinition && typeof(global::app.type.reader.ITypeReader).IsAssignableFrom(clr)
                && clr.GetConstructor(System.Type.EmptyTypes) is { } made)
            {
                yield return new Reader(type, "", made, null);
                continue;
            }
            if (!clr.IsAbstract || !clr.IsSealed) continue; // a static class
            if (clr.GetMethod("Read", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static
                    | System.Reflection.BindingFlags.FlattenHierarchy) is { } read
                && read.GetParameters() is { Length: 3 } parameters && parameters[2].ParameterType == typeof(global::app.type.reader.ReadContext))
                yield return new Reader(type, clr.Name.Equals("Default", System.StringComparison.Ordinal)
                        ? global::app.type.reader.@this.AnyKind : clr.Name.ToLowerInvariant(),
                    null, Bound(read));
        }
    }

    // A static Read bound into the delegate once: called directly, never through reflection per read. A Read whose
    // shape the delegate can't take as it is (another first parameter) is called through Invoke, its own exception
    // rethrown as thrown.
    private static global::app.type.reader.@this.Read Bound(System.Reflection.MethodInfo read)
    {
        if (read.ReturnType.IsClass && read.GetParameters()[0].ParameterType == typeof(object)
            && read.GetParameters()[1].ParameterType == typeof(string))
            return read.CreateDelegate<global::app.type.reader.@this.Read>();
        return (raw, kind, context) =>
        {
            try { return read.Invoke(null, [raw, kind, context]); }
            catch (System.Reflection.TargetInvocationException thrown) when (thrown.InnerException != null)
            {
                System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(thrown.InnerException).Throw();
                throw;
            }
        };
    }

    /// <summary>What the code gives one type as its kinds, in the order the type holds them: its kind classes, the
    /// formats its class declares, its element kinds (a list's), the closed sets (choice's), its family subclasses.</summary>
    internal sealed class Owned
    {
        public System.Collections.Generic.List<Kind> Kind { get; } = new();
        public System.Collections.Generic.List<(Format Class, global::app.Attributes.FormatAttribute Attribute)> Format { get; } = new();
        public bool Element { get; set; }
        public System.Collections.Generic.List<(Choice Class, string Name)> Choice { get; } = new();
        public System.Collections.Generic.List<Subclass> Subclass { get; } = new();
    }

    /// <summary>The code's kinds, read once: each type's own (by the name the type goes by), and every key a kind
    /// answers to — its name, aliases, MIMEs and extensions, its type's own format's too — to the types that may hold
    /// one by it. A lookup by key asks only those types. Reading them checks what the code may not say: a format no
    /// type reads, a choice that is no closed set, a set name two classes claim, a MIME or extension two formats
    /// answer to — plang broken, thrown.</summary>
    internal sealed class Kinds
    {
        public System.Collections.Generic.IReadOnlyDictionary<string, Owned> Owned { get; }
        public System.Collections.Generic.IReadOnlyDictionary<string, string[]> Key { get; }

        public Kinds(@this code)
        {
            var named = new System.Collections.Generic.Dictionary<string, Type>(System.StringComparer.OrdinalIgnoreCase);
            foreach (var type in code.type)
            {
                named.TryAdd(type.Name, type);
                named.TryAdd(global::app.type.item.@this.NamespaceOf(type.Class), type);
                foreach (var alias in type.Alias) named.TryAdd(alias, type);
            }
            var owned = new System.Collections.Generic.Dictionary<string, Owned>(System.StringComparer.OrdinalIgnoreCase);
            var key = new System.Collections.Generic.Dictionary<string, System.Collections.Generic.HashSet<string>>(System.StringComparer.OrdinalIgnoreCase);
            var format = new System.Collections.Generic.Dictionary<string, (string Owner, string Kind)>(System.StringComparer.OrdinalIgnoreCase);
            Owned Of(string owner) => owned.TryGetValue(owner, out var held) ? held : owned[owner] = new Owned();
            // what a kind answers to — its name and aliases, its MIMEs, its extensions with and without the dot —
            // recorded for its owner; a MIME or extension is one format's
            void Keyed(string owner, string name, System.Collections.Generic.IEnumerable<string> keys,
                System.Collections.Generic.IReadOnlyList<string> mime, System.Collections.Generic.IReadOnlyList<string> extension)
            {
                foreach (var each in keys.Concat(extension).Concat(extension.Select(e => e.TrimStart('.'))))
                    (key.TryGetValue(each, out var owners) ? owners : key[each] = new(System.StringComparer.OrdinalIgnoreCase)).Add(owner);
                foreach (var each in mime.Concat(extension))
                    if (format.TryGetValue(each, out var taken) && !(taken.Owner == owner && taken.Kind == name))
                        throw new System.InvalidOperationException($"'{each}' is already the format '{taken.Owner}{(taken.Kind.Length == 0 ? "" : "/" + taken.Kind)}' — "
                            + $"'{owner}{(name.Length == 0 ? "" : "/" + name)}' cannot answer to it too.");
                    else format[each] = (owner, name);
            }
            // a kind class or subclass kind says its keys itself (its aliases, MIMEs and extensions are its own members)
            void Made(string owner, global::app.type.kind.@this kind) => Keyed(owner, kind.Name, kind.Keys, kind.Mime, kind.Extension);
            // each kind's facts read once, from a kind made over a type by its name alone
            static global::app.type.@this Bare(string name) => new(name);

            foreach (var kind in code.kind)
                if (named.GetValueOrDefault(kind.Owner) is { } owner)
                {
                    Of(owner.Name).Kind.Add(kind);
                    Made(owner.Name, (global::app.type.kind.@this)kind.Made.Invoke([Bare(owner.Name)]));
                }
            foreach (var declared in code.format)
            {
                // the type that reads a format: its class's, else the list's for a list of things (a list of actions)
                if ((named.GetValueOrDefault(declared.Name)
                     ?? (typeof(global::app.type.item.list.@this).IsAssignableFrom(declared.Class) ? named.GetValueOrDefault("list") : null)) is not { } owner)
                    throw new System.InvalidOperationException($"{declared.Class.FullName} declares a format, but no type reads it — it is no type of its own "
                        + "and the list names none for it.");
                foreach (var attribute in declared.Attribute)
                {
                    Of(owner.Name).Format.Add((declared, attribute));
                    // a format's keys are its declaration's: its name, its MIME, its extensions
                    string[] mime = attribute.Mime is { } one ? [one] : [];
                    Keyed(owner.Name, attribute.Name, mime.Prepend(attribute.Name), mime, attribute.Extension);
                }
            }
            if (code.Clr == typeof(global::app.type.item.list.@this).Assembly
                && named.GetValueOrDefault(global::app.type.item.@this.NameOf(typeof(global::app.type.item.list.@this))) is { } list)
                Of(list.Name).Element = true;
            if (code.choice.Count > 0 && named.GetValueOrDefault("choice") is { } choice)
            {
                var sets = new System.Collections.Generic.Dictionary<string, System.Type>(System.StringComparer.OrdinalIgnoreCase);
                foreach (var marked in code.choice)
                {
                    if (global::app.type.item.choice.set.@this.NameOf(marked.Class) is not { } name)
                        throw new System.InvalidOperationException($"{marked.Class.FullName} is marked a choice but is no closed set — no enum members, no Choices(context?), no kinds of its own.");
                    // one name, one set: a second class named as a set is (two enums both `level`) would take its reader
                    if (sets.TryGetValue(name, out var taken) && taken != marked.Class)
                        throw new System.InvalidOperationException($"choice set name '{name}' is claimed by both {taken.FullName} and {marked.Class.FullName} — one name, one set.");
                    sets[name] = marked.Class;
                    Of(choice.Name).Choice.Add((marked, name));
                    (key.TryGetValue(name, out var owners) ? owners : key[name] = new(System.StringComparer.OrdinalIgnoreCase)).Add(choice.Name);
                }
            }
            // a family subclass is one of its family's kinds unless the family already holds a kind by its name
            foreach (var subclass in code.subclass)
                if (named.GetValueOrDefault(subclass.Family) is { } family)
                {
                    if (key.TryGetValue(subclass.Kind, out var holders) && holders.Contains(family.Name)) continue;
                    Of(family.Name).Subclass.Add(subclass);
                    Made(family.Name, (global::app.type.kind.@this)subclass.Made.Invoke([subclass.Kind, subclass.Class, Bare(family.Name)]));
                }
            Owned = owned;
            Key = key.ToDictionary(pair => pair.Key, pair => pair.Value.ToArray(), System.StringComparer.OrdinalIgnoreCase);
        }
    }

    private Kinds? _kinds;
    /// <summary>The code's kinds: each type's own, and the types a key may name one of.</summary>
    internal Kinds kinds => _kinds ?? System.Threading.LazyInitializer.EnsureInitialized(ref _kinds, ref _gate, () => new Kinds(this));

    /// <summary>An action class: its module (the namespace segment after the base), its name, and whether its result
    /// may be cached.</summary>
    internal readonly record struct Action(string Module, string Name, System.Type Class, bool Cacheable);

    /// <summary>A module the code holds: its name and its action classes, in the order the code lists them.</summary>
    internal sealed record Module(string Name, System.Collections.Generic.IReadOnlyList<Action> Action);

    private Module[]? _module;
    /// <summary>The modules of plang's built-in actions (the classes under <c>app.module</c>), in the order their
    /// first action comes.</summary>
    internal System.Collections.Generic.IReadOnlyList<Module> module
        => _module ?? System.Threading.LazyInitializer.EnsureInitialized(ref _module, ref _gate, () => Under("app.module").ToArray());

    /// <summary>The modules under <paramref name="baseNamespace"/>: each the namespace segment after it, holding the
    /// action classes there, in the order their first action comes.</summary>
    internal System.Collections.Generic.IEnumerable<Module> Under(string baseNamespace)
    {
        var action = new System.Collections.Generic.List<Action>();
        foreach (var clr in Classes)
        {
            if (clr.IsAbstract || !typeof(global::app.module.ICodeGenerated).IsAssignableFrom(clr)) continue;
            if (System.Reflection.CustomAttributeExtensions.GetCustomAttribute<global::app.module.ActionAttribute>(clr) is not { } attr) continue;
            if (clr.Namespace == null || !clr.Namespace.StartsWith(baseNamespace + ".")) continue;
            action.Add(new Action(clr.Namespace[(baseNamespace.Length + 1)..], attr.Name ?? clr.Name.ToLowerInvariant(), clr, attr.Cacheable));
        }
        return action.GroupBy(each => each.Module).Select(group => new Module(group.Key, group.ToArray()));
    }
}
