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

    private System.Type[]? _classes;
    // every class the assembly holds, read once for every fact below
    private System.Type[] Classes => _classes ??= Clr.GetTypes();

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

        public Type(string name, System.Type clr)
        {
            Name = name;
            Class = clr;
            Alias = Declared<System.Collections.Generic.IReadOnlyList<string>>("Alias") ?? [];
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
        => _type ??= Classes.Select(clr => global::app.type.list.@this.NameOf(clr) is { } name ? new Type(name, clr) : null)
            .OfType<Type>().ToArray();

    /// <summary>A kind class, the name of the type it is a kind of (the T of its kind&lt;T&gt;), and how it is made
    /// from that type's entry.</summary>
    internal readonly record struct Kind(string Owner, System.Reflection.ConstructorInfo Made);

    private Kind[]? _kind;
    /// <summary>The kind classes: each born from nothing but the entry of the type it is a kind of.</summary>
    internal System.Collections.Generic.IReadOnlyList<Kind> kind => _kind ??= KindClasses().ToArray();

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
        => _format ??= Classes.Where(clr => clr.IsDefined(typeof(global::app.Attributes.FormatAttribute), inherit: false))
            .Select(clr => new Format(clr, global::app.type.item.@this.NameOf(clr),
                System.Reflection.CustomAttributeExtensions.GetCustomAttributes<global::app.Attributes.FormatAttribute>(clr, inherit: false).ToArray(),
                typeof(global::app.type.kind.format.@this<>).MakeGenericType(clr)
                    .GetConstructor([typeof(global::app.Attributes.FormatAttribute), typeof(global::app.type.@this)])!))
            .ToArray();

    /// <summary>A class marked a choice, and how its closed reader is made from the set's name.</summary>
    internal readonly record struct Choice(System.Type Class, System.Reflection.ConstructorInfo Reader);

    private Choice[]? _choice;
    /// <summary>The classes marked a choice (<c>[Choice]</c>): each a closed set a <c>choice&lt;T&gt;</c> draws on.</summary>
    internal System.Collections.Generic.IReadOnlyList<Choice> choice
        => _choice ??= Classes.Where(clr => clr.IsDefined(typeof(global::app.Attributes.ChoiceAttribute), inherit: false))
            .Select(clr => new Choice(clr, typeof(global::app.type.item.choice.serializer.Reader<>).MakeGenericType(clr)
                .GetConstructor([typeof(string)])!))
            .ToArray();

    /// <summary>A class under a family that has kinds: the family's name, its kind's name, how the kind is made, and
    /// how the reader it ships (in its serializer namespace) is made; null when it ships none.</summary>
    internal readonly record struct Subclass(System.Type Class, string Family, string Kind, System.Reflection.ConstructorInfo Made,
        System.Reflection.ConstructorInfo? Reader);

    private Subclass[]? _subclass;
    /// <summary>The classes that are a kind of their family, by their own name — not one named as its family.</summary>
    internal System.Collections.Generic.IReadOnlyList<Subclass> subclass => _subclass ??= Subclasses().ToArray();

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
            // what a kind answers to, recorded for its owner; a MIME or extension is one format's
            void Keyed(string owner, global::app.type.kind.@this kind)
            {
                foreach (var each in kind.Keys.Concat(kind.Extension).Concat(kind.Extension.Select(e => e.TrimStart('.'))))
                    (key.TryGetValue(each, out var owners) ? owners : key[each] = new(System.StringComparer.OrdinalIgnoreCase)).Add(owner);
                foreach (var each in kind.Mime.Concat(kind.Extension))
                    if (format.TryGetValue(each, out var taken) && !(taken.Owner == owner && taken.Kind == kind.Name))
                        throw new System.InvalidOperationException($"'{each}' is already the format '{taken.Owner}{(taken.Kind.Length == 0 ? "" : "/" + taken.Kind)}' — "
                            + $"'{owner}{(kind.IsEmpty ? "" : "/" + kind.Name)}' cannot answer to it too.");
                    else format[each] = (owner, kind.Name);
            }
            // each kind's facts read once, from a kind made over a type by its name alone
            static global::app.type.@this Bare(string name) => new(name);

            foreach (var kind in code.kind)
                if (named.GetValueOrDefault(kind.Owner) is { } owner)
                {
                    Of(owner.Name).Kind.Add(kind);
                    Keyed(owner.Name, (global::app.type.kind.@this)kind.Made.Invoke([Bare(owner.Name)]));
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
                    Keyed(owner.Name, (global::app.type.kind.@this)declared.Made.Invoke([attribute, Bare(owner.Name)]));
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
                    Keyed(family.Name, (global::app.type.kind.@this)subclass.Made.Invoke([subclass.Kind, subclass.Class, Bare(family.Name)]));
                }
            Owned = owned;
            Key = key.ToDictionary(pair => pair.Key, pair => pair.Value.ToArray(), System.StringComparer.OrdinalIgnoreCase);
        }
    }

    private Kinds? _kinds;
    /// <summary>The code's kinds: each type's own, and the types a key may name one of.</summary>
    internal Kinds kinds => _kinds ??= new Kinds(this);

    /// <summary>An action class: its module (the namespace segment after the base), its name, and whether its result
    /// may be cached.</summary>
    internal readonly record struct Action(string Module, string Name, System.Type Class, bool Cacheable);

    /// <summary>A module the code holds: its name and its action classes, in the order the code lists them.</summary>
    internal sealed record Module(string Name, System.Collections.Generic.IReadOnlyList<Action> Action);

    private Module[]? _module;
    /// <summary>The modules of plang's built-in actions (the classes under <c>app.module</c>), in the order their
    /// first action comes.</summary>
    internal System.Collections.Generic.IReadOnlyList<Module> module => _module ??= Under("app.module").ToArray();

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
