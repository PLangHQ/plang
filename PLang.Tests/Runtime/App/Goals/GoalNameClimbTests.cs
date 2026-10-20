namespace PLang.Tests.App.Goals;

// A goal call's name never climbs out of its folder: a name with a `..` segment is refused before it is resolved,
// so whoever writes the name can't pick a goal outside the folder it was written for.
public class GoalNameClimbTests
{
    private static string TempApp()
    {
        var dir = System.IO.Directory.CreateDirectory(System.IO.Path.Combine(System.IO.Path.GetTempPath(),
            "plang-climb-" + Guid.NewGuid().ToString("N")[..8])).FullName;
        Built(dir, "", "Evil");
        Built(dir, "sub", "Caller");
        Built(dir, "sub/inner", "Goal");
        return dir;
    }

    // a goal file and the .pr it is built to
    private static void Built(string dir, string folder, string name)
    {
        var at = System.IO.Path.Combine(dir, folder);
        System.IO.Directory.CreateDirectory(System.IO.Path.Combine(at, ".build"));
        System.IO.File.WriteAllText(System.IO.Path.Combine(at, name + ".goal"), name + "\n");
        System.IO.File.WriteAllText(System.IO.Path.Combine(at, ".build", name.ToLowerInvariant() + ".pr"),
            $$"""{"name":"{{name}}","step":[],"file":"{{name}}.goal"}""");
    }

    [Test]
    [Arguments("/sub/../Evil")]
    [Arguments("/system/x/../../Evil")]
    [Arguments("../Evil")]
    [Arguments("inner/../../Evil")]
    public async Task ANameThatClimbs_IsRefused(string name)
    {
        var dir = TempApp();
        try
        {
            await using var app = global::PLang.Tests.Session.App(dir).Testing();
            var caller = (global::app.goal.@this)(await (await app.goal.Load("/sub/Caller.goal")).Value())!;

            var found = await app.goal.list.Find(name, caller);

            await Assert.That(found.Success).IsFalse();
            await Assert.That(found.Error?.Key).IsEqualTo("GoalNameClimbs");
            await Assert.That(app.goal.list.Items().Any(g => g.Name == "Evil")).IsFalse();
        }
        finally { System.IO.Directory.Delete(dir, true); }
    }

    // a name going down still walks the caller's folders
    [Test]
    public async Task ANameGoingDown_IsFoundAsBefore()
    {
        var dir = TempApp();
        try
        {
            await using var app = global::PLang.Tests.Session.App(dir).Testing();
            var caller = (global::app.goal.@this)(await (await app.goal.Load("/sub/Caller.goal")).Value())!;

            var found = await app.goal.list.Find("inner/Goal", caller);

            await found.IsSuccess();
            await Assert.That((await found.Value())!.Path!.ToString()).IsEqualTo("/sub/inner/Goal.goal");
        }
        finally { System.IO.Directory.Delete(dir, true); }
    }
}
