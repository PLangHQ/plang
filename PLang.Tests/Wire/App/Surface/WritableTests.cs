namespace PLang.Tests.App.Surface;

/// <summary>
/// What a program may write: every member plang reads (<c>[LlmBuilder]</c>) on a plang value that has a public setter
/// that isn't init — the one door a navigation set goes through (<c>type.property.Write</c>). A shared object's own (a
/// goal's steps, a step's place) is never on it; a new public setter moves this list, and is reviewed.
/// </summary>
public class WritableTests
{
    // Kept writable on purpose: a frame's own place, which a program sets in a snapshot to say where its run goes on. The
    // rest are a run's own objects (an error, an llm message, a screen, a process) or a copy a program holds (an
    // identity, always its Public() copy, never the registry's own).
    private static readonly string[] Writable =
    [
        "app.call.action.this.index",
        "app.call.step.this.index",
        "app.error.this.Details",
        "app.error.this.Goal",
        "app.error.this.Params",
        "app.error.this.Step",
        "app.error.this.Variables",
        "app.error.this.call",
        "app.module.identity.Identity.Created",
        "app.module.identity.Identity.IsArchived",
        "app.module.identity.Identity.IsDefault",
        "app.module.identity.Identity.Name",
        "app.module.identity.Identity.PublicKey",
        "app.module.llm.type.message.this.Content",
        "app.module.llm.type.message.this.Images",
        "app.module.llm.type.message.this.Role",
        "app.module.screen.type.screen.this.Verbose",
        "app.module.terminal.type.process.this.Id",
        "app.module.terminal.type.process.this.Program",
    ];

    [Test]
    public async Task WhatAProgramMayWrite_IsThePinnedList()
    {
        var item = typeof(global::app.type.item.@this);
        var found = item.Assembly.GetTypes()
            .Where(type => item.IsAssignableFrom(type))
            .SelectMany(type => type.GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.DeclaredOnly)
                .Where(member => System.Reflection.CustomAttributeExtensions.GetCustomAttribute<global::app.LlmBuilderAttribute>(member) != null
                    && member.SetMethod is { IsPublic: true } setter
                    && !setter.ReturnParameter.GetRequiredCustomModifiers().Contains(typeof(System.Runtime.CompilerServices.IsExternalInit)))
                .Select(member => $"{type.FullName}.{member.Name}"))
            .Order(System.StringComparer.Ordinal)
            .ToArray();

        await Assert.That(string.Join("\n", found)).IsEqualTo(string.Join("\n", Writable));
    }
}
