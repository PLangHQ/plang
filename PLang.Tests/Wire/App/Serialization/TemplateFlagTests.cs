using PLang.Tests.Shared;

namespace PLang.Tests.App.Serialization;

/// <summary>
/// The template flag is EXPLICIT and rides the .pr type entity: a value resolves its %refs%
/// only when its type carries template="plang". A value with a %ref% but no flag is plain data —
/// never resolved. Verified through the real write+read goal path.
/// </summary>
public class TemplateFlagTests
{
    [Test]
    public async Task TemplateParam_ResolvesOnLoad_NoFlagStaysLiteral()
    {
        var app = global::PLang.Tests.TestApp.Create("/t");
        await app.User.Context.Variable.Set("name", "World");

        var flaggedType = new global::app.type.@this("text", template: "plang");
        var plainType = new global::app.type.@this("text");

        var goal = Make.Goal("G", "/g.goal",
            Make.Step("s",
                Make.Action("output", "write",
                    Make.Param("flagged", "Hello %name%", flaggedType),
                    Make.Param("plain", "Hello %name%", plainType))));

        var loaded = await RealGoalLoad.ViaChannel(app, goal);
        var prms = loaded.Steps[0].Actions[0].Parameters;
        var flagged = await prms.First(p => p.Name == "flagged").Value();
        var plain = await prms.First(p => p.Name == "plain").Value();

        await Assert.That(flagged.ToString()).IsEqualTo("Hello World");
        await Assert.That(plain.ToString()).IsEqualTo("Hello %name%");
    }
}
