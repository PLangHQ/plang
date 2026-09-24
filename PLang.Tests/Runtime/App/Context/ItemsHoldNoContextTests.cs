using System.Reflection;

namespace PLang.Tests.App.Context;

/// <summary>
/// An item never stores a context. When it needs one, the caller passes it. The named
/// exceptions own a context by what they are: an actor is the context's owner, an error
/// records where it happened, a snapshot is a captured running state.
/// </summary>
public class ItemsHoldNoContextTests
{
    private static readonly System.Type[] Exceptions =
    {
        typeof(global::app.actor.@this),
        typeof(global::app.error.Error),
        typeof(global::app.snapshot.@this),
    };

    private static bool IsContext(System.Type t) => t == typeof(global::app.actor.context.@this);

    // An action that is also an item (signing.sign) is wired by the source generator with its
    // running context; the program's actions are their own step.
    private static bool IsAction(System.Type t) =>
        t.GetCustomAttribute<global::app.module.ActionAttribute>() != null;

    [Test] public async Task NoItem_DeclaresAContextFieldOrProperty()
    {
        const BindingFlags declared = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
        var offenders = new List<string>();
        foreach (var t in typeof(global::app.type.item.@this).Assembly.GetTypes())
        {
            if (!typeof(global::app.type.item.@this).IsAssignableFrom(t)) continue;
            if (Exceptions.Any(e => e.IsAssignableFrom(t)) || IsAction(t)) continue;
            for (var c = t; c != null && c != typeof(object); c = c.BaseType)
            {
                foreach (var f in c.GetFields(declared).Where(f => IsContext(f.FieldType)))
                    offenders.Add($"{t.FullName}.{f.Name}");
                foreach (var p in c.GetProperties(declared).Where(p => IsContext(p.PropertyType)))
                    offenders.Add($"{t.FullName}.{p.Name}");
            }
        }
        await Assert.That(offenders.Distinct().ToList()).IsEmpty().Because(string.Join(", ", offenders.Distinct()));
    }
}
