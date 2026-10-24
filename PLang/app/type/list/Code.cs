using System.Reflection;
using app.Attributes;

namespace app.type.list;

/// <summary>
/// Code partial of <see cref="@this"/> — the facts the code's classes give, read once per code: what every list made
/// from the same code reads alike. A list born from another takes its facts whole; the entries, kinds and readers it
/// makes from them are its own.
/// </summary>
public sealed partial class @this
{
    // The facts of the code this list's types are made from.
    internal readonly Code code;

    /// <summary>The facts of a code: each assembly's classes read once, and each class's declared statics.</summary>
    internal sealed class Code
    {
        private readonly System.Collections.Concurrent.ConcurrentDictionary<Assembly, Lazy<Library>> _library = new();
        private readonly System.Collections.Concurrent.ConcurrentDictionary<System.Type, Class> _class = new();

        /// <summary>The facts <paramref name="assembly"/>'s classes give, read on the first ask.</summary>
        internal Library this[Assembly assembly]
            => _library.GetOrAdd(assembly, each => new Lazy<Library>(() => new Library(each))).Value;

        /// <summary>What <paramref name="clr"/> declares of itself, read on the first ask.</summary>
        internal Class this[System.Type clr] => _class.GetOrAdd(clr, each => new Class(each));
    }

    /// <summary>What a class declares of itself: its aliases, the C# shapes it owns, whether plang keeps it for
    /// itself, and what it offers.</summary>
    internal sealed class Class
    {
        internal IReadOnlyList<string> Alias { get; }
        internal IReadOnlyList<global::app.type.convert.OwnedClr> Owned { get; }
        internal bool Internal { get; }
        internal IReadOnlyList<global::app.type.item.@this>? Offer { get; }

        internal Class(System.Type clr)
        {
            Alias = Declared<IReadOnlyList<string>>("Alias") ?? [];
            Owned = Declared<IReadOnlyList<global::app.type.convert.OwnedClr>>("OwnedClrTypes") ?? [];
            // declared by the class itself: a base that is internal (reference) leaves its subclasses (file, url) named
            Internal = clr.GetProperty("Internal", BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly)
                ?.GetValue(null) is true;
            Offer = Declared<IReadOnlyList<global::app.type.item.@this>>("Offer");

            T? Declared<T>(string member) where T : class
                => clr.GetProperty(member, BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)
                    ?.GetValue(null) as T;
        }
    }

    /// <summary>The facts an assembly's classes give: each read once from its classes, in the order they come.</summary>
    internal sealed class Library
    {
        /// <summary>Every class the assembly holds.</summary>
        internal System.Type[] Classes { get; }

        /// <summary>Each class that is a type of its own, with the name it goes by.</summary>
        internal IReadOnlyList<(string Name, System.Type Clr)> Item { get; }

        /// <summary>Each kind class born from the entry it is a kind of, with that type's name.</summary>
        internal IReadOnlyList<(System.Type Clr, string Owner)> Kind { get; }

        /// <summary>Each class the second pass marks: the formats it declares, whether it is the list, whether it is a
        /// closed set.</summary>
        internal IReadOnlyList<Mark> Marked { get; }

        /// <summary>Each class that is a kind of a family, with its family, its kind and the reader it ships.</summary>
        internal IReadOnlyList<Subclass> Subclasses { get; }

        /// <summary>The token readers it ships under a serializer folder: the type they read, the kind each names.</summary>
        internal IReadOnlyList<(string Type, string Kind, ConstructorInfo Made)> Typed { get; }

        /// <summary>The whole-payload readers it ships under a serializer folder, by the type and kind they read.</summary>
        internal IReadOnlyList<(string Type, string Kind, global::app.type.reader.@this.Read Read)> Read { get; }

        internal Library(Assembly assembly)
        {
            Classes = assembly.GetTypes();
            Item = Classes.Select(clr => (Name: NameOf(clr), Clr: clr)).Where(each => each.Name != null)
                .Select(each => (each.Name!, each.Clr)).ToList();
            Kind = Classes
                .Where(t => typeof(global::app.type.kind.@this).IsAssignableFrom(t) && t is { IsAbstract: false, ContainsGenericParameters: false }
                    && t.GetConstructor([typeof(global::app.type.@this)]) != null)
                .Select(t => (Clr: t, Owner: Owner(t))).Where(each => each.Owner != null)
                .Select(each => (each.Clr, each.Owner!)).ToList();
            Marked = Classes.Select(t => new Mark(t)).Where(mark => mark.Codec != null || mark.List || mark.Choice).ToList();
            Subclasses = Subclassed(Classes);
            (Typed, Read) = Readers(assembly, Classes);
        }

        // The type a kind class is a kind of — the T of the kind<T> it derives from, by its name; null for a class that
        // is no kind<T>.
        private static string? Owner(System.Type kind)
        {
            for (var parent = kind.BaseType; parent != null; parent = parent.BaseType)
                if (parent.IsGenericType && parent.GetGenericTypeDefinition() == typeof(global::app.type.kind.@this<>))
                    return global::app.type.item.@this.NameOf(parent.GetGenericArguments()[0]);
            return null;
        }

        // Each class under a family that has kinds ([Kinds]), by its own kind's name — but a class named as its family
        // (a typed list, a class of settings: the family holds those its own way). A subclass kind's own reader is the
        // one in its serializer namespace, among the readers the assembly ships.
        private static List<Subclass> Subclassed(System.Type[] classes)
        {
            var readers = classes.Where(r => r.Namespace is { } ns && ns.EndsWith(".serializer", StringComparison.Ordinal)
                    && typeof(global::app.type.reader.ITypeReader).IsAssignableFrom(r)
                    && r is { IsAbstract: false } && r.GetConstructor(System.Type.EmptyTypes) != null)
                .GroupBy(r => r.Namespace!).ToDictionary(group => group.Key, group => group.First());
            var subclasses = new List<Subclass>();
            foreach (var t in classes)
                if (t is { IsAbstract: false, ContainsGenericParameters: false } && global::app.type.item.@this.FamilyOf(t) is { } member
                    && global::app.type.item.@this.NameOf(member.Family) is { } family && member.Kind != family)
                    subclasses.Add(new Subclass(t, family, member.Kind,
                        // the kind object is a kind<family> typed to the family; the value class stays a family subclass
                        typeof(global::app.type.kind.subclass.@this<>).MakeGenericType(member.Family),
                        readers.GetValueOrDefault($"{t.Namespace}.serializer")));
            return subclasses;
        }

        // The readers the assembly ships under a serializer folder (<the type's namespace>.serializer), each under the
        // name its type goes by (its word, else its namespace): a module's serializer folder reads the one type the
        // module declares by its word ([PlangType("ask")] in app.module.output); a folder with no such type registers
        // under its last segment.
        private static (List<(string, string, ConstructorInfo)>, List<(string, string, global::app.type.reader.@this.Read)>) Readers(
            Assembly assembly, System.Type[] classes)
        {
            var typed = new List<(string, string, ConstructorInfo)>();
            var read = new List<(string, string, global::app.type.reader.@this.Read)>();
            // the word each namespace declares its one item type by ([PlangType]); none when it declares none, or more
            var words = classes.Where(t => t.Namespace != null && typeof(global::app.type.item.@this).IsAssignableFrom(t))
                .GroupBy(t => t.Namespace!)
                .ToDictionary(group => group.Key, group => group
                    .Select(t => t.GetCustomAttribute<PlangTypeAttribute>(inherit: false)?.Name)
                    .Where(w => !string.IsNullOrEmpty(w)).Distinct().ToList() is [var one] ? one : null);
            foreach (var type in classes)
            {
                if (!type.IsClass || string.IsNullOrEmpty(type.Namespace)) continue;
                if (!type.Namespace!.EndsWith(".serializer", StringComparison.Ordinal)) continue;
                var ns = type.Namespace;
                var head = ns[..ns.LastIndexOf(".serializer", StringComparison.Ordinal)];
                var lastDot = head.LastIndexOf('.');
                if (lastDot < 0) continue;
                var typeName = global::app.type.item.@this.NameOf(assembly, head) ?? words.GetValueOrDefault(head)
                    ?? head[(lastDot + 1)..].TrimStart('@');

                // The typed (ITypeReader) pull reader — an instance class whose own Kind names the (type, kind) variant.
                // A type may ship both (its wire read and its content decode).
                if (!type.IsAbstract && !type.IsGenericTypeDefinition
                    && typeof(global::app.type.reader.ITypeReader).IsAssignableFrom(type)
                    && type.GetConstructor(System.Type.EmptyTypes) is { } ctor)
                {
                    typed.Add((typeName, ((global::app.type.reader.ITypeReader)ctor.Invoke(null)).Kind, ctor));
                    continue;
                }

                if (!type.IsAbstract || !type.IsSealed) continue; // static class

                // file/class name maps to kind/format token; "Default" → wildcard.
                var kind = type.Name.Equals("Default", StringComparison.Ordinal)
                    ? global::app.type.reader.@this.AnyKind
                    : type.Name.ToLowerInvariant();

                // READ — static Read(object raw, string? kind, ReadContext).
                var method = type.GetMethod("Read", BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy);
                if (method is not null
                    && method.GetParameters() is { Length: 3 } parameters
                    && parameters[2].ParameterType == typeof(global::app.type.reader.ReadContext))
                    // Reflection wraps a thrown exception in TargetInvocationException; unwrap so callers (source.Value's
                    // parse-failure catch) see the real JsonException/FormatException and author their own error.
                    read.Add((typeName, kind, (raw, k, ctx) =>
                    {
                        try { return method.Invoke(null, new object?[] { raw, k, ctx }); }
                        catch (TargetInvocationException tie) when (tie.InnerException != null)
                        {
                            System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(tie.InnerException).Throw();
                            throw; // unreachable — Throw() always rethrows
                        }
                    }));
            }
            return (typed, read);
        }
    }

    /// <summary>A class the second pass marks: each format it declares (written by its own encode, read by its own
    /// decode when it has one), whether it is the list (whose element kinds are made as a list names its element),
    /// whether it is marked a choice ([Choice]).</summary>
    internal sealed class Mark
    {
        internal System.Type Clr { get; }
        internal Codec? Codec { get; }
        internal bool List { get; }
        internal bool Choice { get; }

        internal Mark(System.Type t)
        {
            Clr = t;
            if (t.IsDefined(typeof(FormatAttribute), inherit: false)) Codec = new Codec(t);
            List = t == typeof(global::app.type.item.list.@this);
            Choice = t.IsDefined(typeof(ChoiceAttribute), inherit: false);
        }
    }

    /// <summary>The formats a class declares — each a kind&lt;declaring-type&gt; of one concrete class over it — and
    /// its encode and decode, each bound once into a delegate (no reflection per write or read).</summary>
    internal sealed class Codec
    {
        internal FormatAttribute[] Attribute { get; }
        internal System.Type Kind { get; }
        internal global::app.type.kind.@this.Encoder? Encode { get; }
        internal global::app.type.kind.@this.Decoder? Decode { get; }

        internal Codec(System.Type clr)
        {
            Attribute = clr.GetCustomAttributes<FormatAttribute>(inherit: false).ToArray();
            Kind = typeof(global::app.type.kind.format.@this<>).MakeGenericType(clr);
            Encode = clr.GetInterfaces().Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(global::app.type.item.IEncode<>))
                ? (global::app.type.kind.@this.Encoder)typeof(Codec).GetMethod(nameof(Bound), BindingFlags.NonPublic | BindingFlags.Static)!
                    .MakeGenericMethod(clr).Invoke(null, null)!
                : null;
            Decode = clr.GetInterfaces().Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(global::app.type.item.IDecode<>))
                ? (global::app.type.kind.@this.Decoder)typeof(Codec).GetMethod(nameof(Reading), BindingFlags.NonPublic | BindingFlags.Static)!
                    .MakeGenericMethod(clr).Invoke(null, null)!
                : null;
        }

        private static global::app.type.kind.@this.Encoder Bound<T>() where T : global::app.type.item.@this, global::app.type.item.IEncode<T>
            => T.Encode;

        private static global::app.type.kind.@this.Decoder Reading<T>() where T : global::app.type.item.@this, global::app.type.item.IDecode<T>
            => T.Decode;
    }

    /// <summary>A class that is a kind of a family: the family's name, its kind's name, the kind class over the family,
    /// and the reader it ships, when it ships one.</summary>
    internal sealed record Subclass(System.Type Clr, string Family, string Kind, System.Type Made, System.Type? Reader);
}
