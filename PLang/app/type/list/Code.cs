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
            _clr = clr;

            T? Declared<T>(string member) where T : class
                => clr.GetProperty(member, BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)
                    ?.GetValue(null) as T;
        }

        private readonly System.Type _clr;
        private System.Type? _setting;
        private bool _settingRead;

        /// <summary>The class of the concept's own settings, when the class names one (<c>IConcept&lt;test.setting&gt;</c>);
        /// null when it names none. Worked out the first time it is asked, once per code.</summary>
        internal System.Type? Setting
        {
            get
            {
                if (System.Threading.Volatile.Read(ref _settingRead)) return _setting;
                _setting = _clr.GetInterfaces()
                    .FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(global::app.type.item.setting.IConcept<>))
                    ?.GetGenericArguments()[0];
                System.Threading.Volatile.Write(ref _settingRead, true);
                return _setting;
            }
        }

        private string? _path;

        /// <summary>For a class of options, the path it is read by: its namespace — a module's read from the module
        /// (<c>app.module.llm.setting</c> is <c>llm.setting</c>), a type's where types are (<c>app.type.item.size.setting</c>
        /// is <c>app.type.size.setting</c>), a class declared inside what it configures through its owner
        /// (<c>app.module.llm.decider.setting</c> is <c>llm.decider.setting</c>). Worked out once per code.</summary>
        internal string Path => _path ??= Where();

        // The path a class of options is read by, from where the class is declared.
        private string Where()
        {
            var path = _clr is { DeclaringType: { } owner } ? $"{_clr.Namespace}.{owner.Name}.{_clr.Name}" : _clr.Namespace!;
            const string module = "app.module.", at = "app.type.item.";
            return path.StartsWith(module) ? path[module.Length..]
                : path.StartsWith(at) ? "app.type." + path[at.Length..]
                : path;
        }

        private (PropertyInfo Member, global::app.module.DefaultAttribute? Declared, ConstructorInfo? Empty)[]? _defaults;

        /// <summary>For a class of options, each public settable plang-valued member with its declared default, and —
        /// when it declares none and can't be null — the constructor its empty value is born by: what a value left out
        /// is given. Read once per code.</summary>
        internal IReadOnlyList<(PropertyInfo Member, global::app.module.DefaultAttribute? Declared, ConstructorInfo? Empty)> Defaults
            => _defaults ??= Members();

        // The members a value of this class left out may be given, as the class declares them.
        private (PropertyInfo, global::app.module.DefaultAttribute?, ConstructorInfo?)[] Members()
        {
            const BindingFlags any = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            var nullability = new NullabilityInfoContext();
            return _clr.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(member => member.SetMethod?.IsPublic == true && typeof(global::app.type.item.@this).IsAssignableFrom(member.PropertyType))
                .Select(member => (member, member.GetCustomAttribute<global::app.module.DefaultAttribute>(),
                    member.GetCustomAttribute<global::app.module.DefaultAttribute>() == null
                    && nullability.Create(member).WriteState != NullabilityState.Nullable
                        ? member.PropertyType.GetConstructor(any, [typeof(global::app.type.@this)]) : null))
                .ToArray();
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
        internal IReadOnlyList<KindClass> Kind { get; }

        /// <summary>Each class the second pass marks: the formats it declares, whether it is the list, whether it is a
        /// closed set.</summary>
        internal IReadOnlyList<Mark> Marked { get; }

        /// <summary>Each class that is a kind of a family, with its family, its kind and the reader it ships.</summary>
        internal IReadOnlyList<Subclass> Subclasses { get; }

        /// <summary>The token readers it ships under a serializer folder: the type they read, the kind each names.</summary>
        internal IReadOnlyList<Token> Typed { get; }

        /// <summary>The whole-payload readers it ships under a serializer folder, by the type and kind they read.</summary>
        internal IReadOnlyList<Payload> Read { get; }

        internal Library(Assembly assembly)
        {
            Classes = assembly.GetTypes();
            // each class's family read once: what names it (a family's kind is no type of its own) and what makes it a
            // subclass kind
            var item = new List<(string, System.Type)>();
            var subclasses = new List<Subclass>();
            var readers = Shipped;
            foreach (var t in Classes)
            {
                var member = global::app.type.item.@this.FamilyOf(t);
                var family = member is { } of ? global::app.type.item.@this.NameOf(of.Family) : null;
                if (NameOf(t, family) is { } name) item.Add((name, t));
                // a class under a family that has kinds ([Kinds]) is one of its kinds by its own name — but a class named
                // as its family (a typed list, a class of settings: the family holds those its own way)
                if (member is { } kind && family != null && kind.Kind != family && t is { IsAbstract: false, ContainsGenericParameters: false })
                    subclasses.Add(new Subclass(t, family, kind.Kind,
                        // the kind object is a kind<family> typed to the family; the value class stays a family subclass
                        typeof(global::app.type.kind.subclass.@this<>).MakeGenericType(kind.Family),
                        readers.GetValueOrDefault($"{t.Namespace}.serializer")));
            }
            Item = item;
            Subclasses = subclasses;
            Kind = Classes
                .Where(t => typeof(global::app.type.kind.@this).IsAssignableFrom(t) && t is { IsAbstract: false, ContainsGenericParameters: false }
                    && t.GetConstructor([typeof(global::app.type.@this)]) != null)
                .Select(t => new KindClass(t)).Where(each => each.Owner != null).ToList();
            // a Mark is asked of every class (a value: nothing made for one with none), kept for one that has one
            Marked = Classes.Select(t => new Mark(t)).Where(mark => mark.Codec != null || mark.List || mark.Choice).ToList();
            (Typed, Read) = Readers(assembly);
        }

        // The token readers the assembly ships, by their serializer namespace — a subclass kind's own reader is the one in
        // its serializer namespace.
        private Dictionary<string, System.Type> Shipped
            => Classes.Where(r => r.Namespace is { } ns && ns.EndsWith(".serializer", StringComparison.Ordinal)
                    && typeof(global::app.type.reader.ITypeReader).IsAssignableFrom(r)
                    && r is { IsAbstract: false } && r.GetConstructor(System.Type.EmptyTypes) != null)
                .GroupBy(r => r.Namespace!).ToDictionary(group => group.Key, group => group.First());

        // The readers the assembly ships under a serializer folder (<the type's namespace>.serializer), each under the
        // name its type goes by (its word, else its namespace): a module's serializer folder reads the one type the
        // module declares by its word ([PlangType("ask")] in app.module.output); a folder with no such type registers
        // under its last segment.
        private (List<Token>, List<Payload>) Readers(Assembly assembly)
        {
            var classes = Classes;
            var typed = new List<Token>();
            var read = new List<Payload>();
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
                    typed.Add(new Token(typeName, ctor));
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
                    read.Add(new Payload(typeName, kind, method));
            }
            return (typed, read);
        }
    }

    /// <summary>A kind class the startup scan holds: born from the entry of the type it is a kind of — the T of the
    /// kind&lt;T&gt; it derives from, by its name; no owner for a class that is no kind&lt;T&gt;.</summary>
    internal sealed class KindClass
    {
        internal System.Type Clr { get; }
        internal string? Owner { get; }

        internal KindClass(System.Type clr)
        {
            Clr = clr;
            for (var parent = clr.BaseType; parent != null && Owner == null; parent = parent.BaseType)
                if (parent.IsGenericType && parent.GetGenericTypeDefinition() == typeof(global::app.type.kind.@this<>))
                    Owner = global::app.type.item.@this.NameOf(parent.GetGenericArguments()[0]);
        }

        public void Deconstruct(out System.Type clr, out string owner) => (clr, owner) = (Clr, Owner!);
    }

    /// <summary>A token reader a serializer folder ships — the type it reads, and the reader, made once per code: it
    /// holds nothing of an app's, so every list holds the same one. Its kind is the one it names.</summary>
    internal sealed class Token
    {
        internal string Type { get; }
        internal global::app.type.reader.ITypeReader Reader { get; }

        internal Token(string type, ConstructorInfo made)
        {
            Type = type;
            Reader = (global::app.type.reader.ITypeReader)made.Invoke(null);
        }

        public void Deconstruct(out string type, out global::app.type.reader.ITypeReader reader) => (type, reader) = (Type, Reader);
    }

    /// <summary>A whole-payload reader a serializer folder ships — its static Read, by the type and kind it reads, as a
    /// delegate. Reflection wraps a thrown exception in TargetInvocationException; it is unwrapped so callers
    /// (source.Value's parse-failure catch) see the real JsonException/FormatException and author their own error.</summary>
    internal sealed class Payload
    {
        internal string Type { get; }
        internal string Kind { get; }
        internal global::app.type.reader.@this.Read Read { get; }

        internal Payload(string type, string kind, MethodInfo method)
        {
            Type = type;
            Kind = kind;
            Read = (raw, k, ctx) =>
            {
                try { return method.Invoke(null, new object?[] { raw, k, ctx }); }
                catch (TargetInvocationException tie) when (tie.InnerException != null)
                {
                    System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(tie.InnerException).Throw();
                    throw; // unreachable — Throw() always rethrows
                }
            };
        }

        public void Deconstruct(out string type, out string kind, out global::app.type.reader.@this.Read read)
            => (type, kind, read) = (Type, Kind, Read);
    }

    /// <summary>A class the second pass marks: each format it declares (written by its own encode, read by its own
    /// decode when it has one), whether it is the list (whose element kinds are made as a list names its element),
    /// whether it is marked a choice ([Choice]). A value, asked of every class: one with no mark costs nothing kept.</summary>
    internal readonly struct Mark
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
