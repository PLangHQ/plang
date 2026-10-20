using System.Reflection;
using System.Reflection.Emit;

namespace PLang.Tests.App.Types;

/// <summary>One name, one closed set: a second class naming its set as another's (two enums both `level`) is refused
/// loudly at enlisting, naming both — never a silent swap of which set the name reads.</summary>
public class ChoiceSetNameTests
{
    // An assembly of its own (so no other test enlists it): an enum named `level` — debug's set's name — marked a
    // choice, which is how a set is enlisted.
    private static Assembly Clashing()
    {
        // emitted, saved and loaded back: a loaded assembly, as a program's would be (a dynamic one exports nothing)
        var assembly = new PersistedAssemblyBuilder(new AssemblyName("clashing_" + Guid.NewGuid().ToString("N")), typeof(object).Assembly);
        var module = assembly.DefineDynamicModule("clashing");
        var level = module.DefineEnum("Clashing.Level", TypeAttributes.Public, typeof(int));
        level.DefineLiteral("low", 0);
        level.DefineLiteral("high", 1);
        level.SetCustomAttribute(new CustomAttributeBuilder(
            typeof(global::app.Attributes.PlangTypeAttribute).GetConstructor([typeof(string)])!, ["level"]));
        level.SetCustomAttribute(new CustomAttributeBuilder(
            typeof(global::app.Attributes.ChoiceAttribute).GetConstructor(System.Type.EmptyTypes)!, []));
        level.CreateType();
        using var saved = new MemoryStream();
        assembly.Save(saved);
        return System.Runtime.Loader.AssemblyLoadContext.Default.LoadFromStream(new MemoryStream(saved.ToArray()));
    }

    [Test]
    public async Task ASecondSetWithATakenName_IsRefused_NamingBoth()
    {
        await using var app = global::PLang.Tests.Session.App("/app").Testing();
        var ctx = app.actor.list.User.Context;

        var added = app.type.list.Add(Clashing(), ctx);

        await added.IsFailure();
        await Assert.That(added.Error!.Key).IsEqualTo("TypeLoadCollision");
        await Assert.That(added.Error.Message).Contains("'level'");
        await Assert.That(added.Error.Message).Contains("app.module.debug.Level");
        await Assert.That(added.Error.Message).Contains("Clashing.Level");
    }
}
