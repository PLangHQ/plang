using System.Reflection;

namespace PLang.Tests.App.Context;

/// <summary>
/// A one-context [LlmBuilder] method is read by navigation (<c>%!app.name%</c> calls it with the asker's
/// context), so it must be a fact with no effects. Each one is named here after being read for that: a new
/// one fails this until it is read and added.
/// </summary>
public class NavigableFactsTests
{
    private static readonly string[] Facts =
    {
        "app.this.Name",
        "app.this.Environment",
        "app.this.call",
        "app.this.trace",
        "app.this.data",
        "app.this.event",
        "app.type.item.path.this.MimeType",
        // gated reads: the asker is asked (an http path sends a HEAD); a refusal is the answer
        "app.type.item.path.this.Exists",
        "app.type.item.path.file.this.Exists",
        "app.type.item.path.http.this.Exists",
        "app.type.item.path.file.this.Size",
    };

    [Test] public async Task EveryNavigableMethod_IsAFactReadForIt()
    {
        var found = typeof(global::app.@this).Assembly.GetTypes()
            .SelectMany(t => t.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            .Where(m => m.IsDefined(typeof(global::app.LlmBuilderAttribute))
                        && m.GetParameters() is [{ ParameterType: var p }] && p == typeof(global::app.actor.context.@this))
            .Select(m => $"{m.DeclaringType!.FullName!.Replace("@", "")}.{m.Name}")
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToList();

        await Assert.That(found).IsEquivalentTo(Facts.OrderBy(n => n, StringComparer.Ordinal).ToList())
            .Because("a one-context [LlmBuilder] method is called by a %…% read — read it for effects, then name it here");
    }
}
