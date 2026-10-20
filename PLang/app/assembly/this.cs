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

    /// <summary>An action class: its module (the namespace segment after the base), its name, and whether its result
    /// may be cached.</summary>
    internal readonly record struct Action(string Module, string Name, System.Type Class, bool Cacheable);

    private Action[]? _action;
    /// <summary>The action classes under <c>app.module</c> — plang's built-ins.</summary>
    internal System.Collections.Generic.IReadOnlyList<Action> action => _action ??= Actions("app.module").ToArray();

    /// <summary>The action classes under <paramref name="baseNamespace"/>.</summary>
    internal System.Collections.Generic.IEnumerable<Action> Actions(string baseNamespace)
    {
        foreach (var clr in Classes)
        {
            if (clr.IsAbstract || !typeof(global::app.module.ICodeGenerated).IsAssignableFrom(clr)) continue;
            if (System.Reflection.CustomAttributeExtensions.GetCustomAttribute<global::app.module.ActionAttribute>(clr) is not { } attr) continue;
            if (clr.Namespace == null || !clr.Namespace.StartsWith(baseNamespace + ".")) continue;
            yield return new Action(clr.Namespace[(baseNamespace.Length + 1)..], attr.Name ?? clr.Name.ToLowerInvariant(), clr, attr.Cacheable);
        }
    }
}
