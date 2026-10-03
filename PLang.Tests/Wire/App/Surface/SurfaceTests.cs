using System.Reflection;

namespace PLang.Tests.App.Surface;

// The core's surface, pinned: what other code calls in the locked parts (app's members, the type system and the core
// values, the serializer, data, channel, call and variable memory, the module registry) — each public type and its own
// public and protected members, with the plang marks on them — and what a plang program sees (each type's members,
// each module's actions and their properties). A change here is an addition to the core: it fails until it is
// re-pinned through AcceptTheSurface, and the commit that re-pins names the decision that allowed it.
public class SurfaceTests
{
    private const string Pinned = "PLang.Tests/Wire/App/Surface/surface_golden.txt";

    // The locked parts, by namespace: one exactly (app's own types, the module registry), or one and all below it.
    private static readonly string[] Exactly = ["app", "app.module", "app.module.list"];
    private static readonly string[] Below = ["app.type", "app.data", "app.channel", "app.call"];

    private static bool Locked(System.Type t)
        => t.Namespace is { } ns && (Exactly.Contains(ns) || Below.Any(b => ns == b || ns.StartsWith(b + ".")));

    private const BindingFlags Own = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance
        | BindingFlags.Static | BindingFlags.DeclaredOnly;

    private static async Task<string> Surface()
    {
        var lines = new List<string> { "# C# surface of the locked core" };
        var types = typeof(global::app.@this).Assembly.GetTypes()
            .Where(t => (t.IsPublic || t.IsNestedPublic) && Locked(t) && !t.IsDefined(typeof(System.Runtime.CompilerServices.CompilerGeneratedAttribute)))
            .OrderBy(Name, System.StringComparer.Ordinal);
        foreach (var t in types)
        {
            lines.Add("");
            lines.Add($"{Kind(t)} {Name(t)}{Bases(t)}{Marks(t)}");
            lines.AddRange(t.GetMembers(Own).Where(Visible).Select(Member).OfType<string>()
                .OrderBy(m => m, System.StringComparer.Ordinal).Select(m => "  " + m));
        }

        lines.Add("");
        lines.Add("# What a plang program sees");
        await using var os = new global::app.@this(System.IO.Path.Combine(Fixture.Root(), "os")).Testing();
        foreach (var type in os.type.list.Items().OrderBy(t => t.Name, System.StringComparer.Ordinal))
        {
            lines.Add("");
            lines.Add($"type {type.Name}");
            foreach (var p in type.Property ?? Enumerable.Empty<global::app.type.property.@this>())
                lines.Add($"  .{p.Name}{Arguments(p)} : {p.Type}");
        }
        foreach (var module in os.module.list.Items().OrderBy(m => m.Name, System.StringComparer.Ordinal))
            foreach (var action in module.ActionNames.OrderBy(a => a, System.StringComparer.Ordinal))
            {
                lines.Add("");
                lines.Add($"action {module.Name}.{action}");
                foreach (var p in module[action]?.Property ?? Enumerable.Empty<global::app.type.property.@this>())
                    lines.Add($"  {p.Name} : {p.Type}{(p.Required ? " required" : "")}");
            }
        return string.Join("\n", lines) + "\n";
    }

    private static string Arguments(global::app.type.property.@this p)
        => p.Arguments is { } args ? "(" + string.Join(", ", args.Select(a => $"{a.Name}: {a.Type}")) + ")" : "";

    private static bool Visible(MemberInfo m) => m switch
    {
        MethodInfo method => (method.IsPublic || method.IsFamily || method.IsFamilyOrAssembly) && !method.IsSpecialName,
        ConstructorInfo ctor => ctor.IsPublic || ctor.IsFamily || ctor.IsFamilyOrAssembly,
        PropertyInfo prop => (prop.GetMethod ?? prop.SetMethod) is { } acc && (acc.IsPublic || acc.IsFamily || acc.IsFamilyOrAssembly),
        FieldInfo field => (field.IsPublic || field.IsFamily || field.IsFamilyOrAssembly) && !field.IsSpecialName,
        EventInfo => true,
        _ => false,
    } && !m.IsDefined(typeof(System.Runtime.CompilerServices.CompilerGeneratedAttribute));

    private static string? Member(MemberInfo m) => m switch
    {
        ConstructorInfo c => $"ctor({Parameters(c)}){Marks(c)}",
        MethodInfo method => $"{Modifiers(method)}{method.Name}{Generic(method)}({Parameters(method)}) : {Name(method.ReturnType)}{Marks(method)}",
        PropertyInfo p => $"{Modifiers((p.GetMethod ?? p.SetMethod)!)}{p.Name}{Indexer(p)} : {Name(p.PropertyType)} {{{(p.GetMethod != null ? " get;" : "")}{Setter(p)} }}{Marks(p)}",
        FieldInfo f => $"{(f.IsStatic ? "static " : "")}{(f.IsLiteral ? "const " : f.IsInitOnly ? "readonly " : "")}{f.Name} : {Name(f.FieldType)}{Marks(f)}",
        EventInfo e => $"event {e.Name} : {Name(e.EventHandlerType!)}",
        _ => null,
    };

    private static string Setter(PropertyInfo p)
        => p.SetMethod is not { } set || !(set.IsPublic || set.IsFamily || set.IsFamilyOrAssembly) ? ""
            : set.ReturnParameter.GetRequiredCustomModifiers().Any(c => c.Name == "IsExternalInit") ? " init;" : " set;";

    private static string Indexer(PropertyInfo p)
        => p.GetIndexParameters() is { Length: > 0 } index ? "[" + string.Join(", ", index.Select(i => Name(i.ParameterType))) + "]" : "";

    private static string Generic(MethodInfo m)
        => m.IsGenericMethodDefinition ? "<" + string.Join(", ", m.GetGenericArguments().Select(a => a.Name)) + ">" : "";

    private static string Modifiers(MethodInfo m)
        => (m.IsFamily || m.IsFamilyOrAssembly ? "protected " : "")
           + (m.IsStatic ? "static " : "")
           + (m.IsAbstract ? "abstract " : m.GetBaseDefinition() != m ? "override " : m.IsVirtual && !m.IsFinal ? "virtual " : "");

    private static string Parameters(MethodBase m)
        => string.Join(", ", m.GetParameters().Select(p => $"{Name(p.ParameterType)} {p.Name}{(p.IsOptional ? " = …" : "")}"));

    private static string Kind(System.Type t)
        => t.IsInterface ? "interface" : t.IsEnum ? "enum" : t.IsValueType ? "struct"
            : t.IsAbstract && t.IsSealed ? "static class" : t.IsAbstract ? "abstract class" : "class";

    private static string Bases(System.Type t)
    {
        var bases = new List<string>();
        if (t.BaseType is { } b && b != typeof(object) && b != typeof(System.ValueType) && b != typeof(System.Enum)) bases.Add(Name(b));
        bases.AddRange(t.GetInterfaces().Where(i => t.BaseType?.GetInterfaces().Contains(i) != true).Select(Name).OrderBy(n => n, System.StringComparer.Ordinal));
        return bases.Count > 0 ? " : " + string.Join(", ", bases) : "";
    }

    // plang's own marks on a member — [LlmBuilder], [Out], [Store], [Sensitive], [Code], … — never the compiler's.
    private static string Marks(MemberInfo m)
    {
        var marks = m.GetCustomAttributes(inherit: false).Select(a => a.GetType())
            .Where(a => a.Namespace?.StartsWith("app") == true).Select(a => a.Name.Replace("Attribute", ""))
            .OrderBy(n => n, System.StringComparer.Ordinal).ToList();
        return marks.Count > 0 ? " [" + string.Join(", ", marks) + "]" : "";
    }

    private static string Name(System.Type t)
    {
        if (t.IsGenericParameter) return t.Name;
        if (t.IsArray) return Name(t.GetElementType()!) + "[]";
        if (t.IsByRef) return Name(t.GetElementType()!) + "&";
        var name = (t.IsNested ? Name(t.DeclaringType!) + "+" : t.Namespace + ".") + t.Name.Split('`')[0];
        return t.IsGenericType ? name + "<" + string.Join(", ", t.GetGenericArguments().Select(Name)) + ">" : name;
    }

    [Test]
    public async Task TheCoresSurface_IsThePinnedOne()
    {
        var ours = (await Surface()).Split('\n');
        var pinned = System.IO.File.ReadAllText(System.IO.Path.Combine(Fixture.Root(), Pinned)).Split('\n');
        var added = ours.Except(pinned).Take(20).ToList();
        var removed = pinned.Except(ours).Take(20).ToList();
        await Assert.That(string.Join("\n", added.Select(l => "+ " + l).Concat(removed.Select(l => "- " + l))))
            .IsEqualTo("").Because("the core's surface changed: an addition needs Ingi's yes, then AcceptTheSurface re-pins it and the commit names the decision");
    }

    // Re-pins surface_golden.txt from C#: run by hand, once the change is decided.
    [Test, Explicit]
    public async Task AcceptTheSurface()
        => System.IO.File.WriteAllText(System.IO.Path.Combine(Fixture.Root(), Pinned), await Surface());
}
