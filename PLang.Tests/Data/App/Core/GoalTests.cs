using app;
using app.type.item.variable;

namespace PLang.Tests.App.Core;

public class GoalTests : System.IAsyncDisposable
{
    private readonly global::app.@this _app = new global::app.@this("/tmp/GoalTests-" + System.Guid.NewGuid().ToString("N")[..6], global::PLang.Tests.Parent.Actor).Testing();
    public async System.Threading.Tasks.ValueTask DisposeAsync() => await _app.DisposeAsync();

    [Test]
    public async Task Properties_CanBeInitialized()
    {
        var goal = new Goal(_app.type.list)
        {
            Name = "TestGoal",
            Comment = "This is a comment",
            Path = global::app.type.item.path.@this.Resolve("/setup/to/goal.goal", _app.actor.list.User.Context),
            Hash = "abc123",
            Step = new GoalSteps(_app.type.list)
            {
                new Step(_app.type.list) { Index = 0, Text = "first step" },
                new Step(_app.type.list) { Index = 1, Text = "second step" }
            }
        };
        goal.Child.Add(new global::app.goal.@this(_app.type.list) { Name = "SubGoal1" });
        goal.Child.Add(new global::app.goal.@this(_app.type.list) { Name = "SubGoal2" });

        await Assert.That(goal.Name).IsEqualTo("TestGoal");
        await Assert.That(goal.Comment).IsEqualTo("This is a comment");
        await Assert.That(goal.Visibility.Value).IsEqualTo(Visibility.Public);
        await Assert.That(goal.Path?.ToString()).IsEqualTo("/setup/to/goal.goal");
        await Assert.That(goal.PrPath?.ToString()).IsEqualTo("/setup/to/.build/goal.pr");
        await Assert.That(goal.Hash).IsEqualTo("abc123");
        await Assert.That(goal.IsSetup).IsTrue();
        await Assert.That(goal.Child.CountRaw).IsEqualTo(2);
        await Assert.That(goal.Child.Items().All(c => c.Parent == goal)).IsTrue();
        await Assert.That(goal.Step.Count).IsEqualTo(2);
    }

    [Test]
    public async Task Name_DefaultsToEmptyString()
    {
        var goal = new Goal(_app.type.list);

        await Assert.That(goal.Name).IsEqualTo("");
    }

    [Test]
    public async Task Visibility_AFilesGoalIsPublic_ASubGoalPrivate()
    {
        var goal = new Goal(_app.type.list);
        var sub = new Goal(_app.type.list);
        goal.Child.Add(sub);

        await Assert.That(goal.Visibility.Value).IsEqualTo(Visibility.Public);
        await Assert.That(sub.Visibility.Value).IsEqualTo(Visibility.Private);
    }

    [Test]
    public async Task IsSetup_DefaultsToFalse()
    {
        var goal = new Goal(_app.type.list);

        await Assert.That(goal.IsSetup).IsFalse();
    }

    // an os /system/ goal, loaded in an app with no /system/ of its own, is a system goal: its plang path is /system/…
    [Test]
    public async Task AnOsSystemGoal_InAnAppWithoutItsOwn_IsSystem()
    {
        var root = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "issystem-" + System.Guid.NewGuid().ToString("N")[..8]);
        System.IO.Directory.CreateDirectory(root);
        try
        {
            await using var other = new global::app.@this(root, global::PLang.Tests.Parent.Actor).Testing();
            var loaded = await other.goal.Load("/system/error/Show.goal");

            await loaded.IsSuccess();
            await Assert.That(((Goal)(await loaded.Value())!).IsSystem).IsTrue();
        }
        finally { System.IO.Directory.Delete(root, true); }
    }

    // what a goal is, it answers from where it lives: /system/, /setup/ (or named Setup), a .test.goal
    [Test]
    [Arguments("/system/error/Show.goal", "Show", true, false, false)]
    [Arguments("/setup/Db.goal", "Db", false, true, false)]
    [Arguments("/Setup.goal", "Setup", false, true, false)]
    [Arguments("/test/a.test.goal", "A", false, false, true)]
    [Arguments("/Start.goal", "Start", false, false, false)]
    public async Task AGoalIs_WhatItsPathSays(string path, string name, bool system, bool setup, bool test)
    {
        var goal = new Goal(_app.type.list) { Name = name, Path = global::app.type.item.path.@this.Resolve(path, _app.actor.list.User.Context) };

        await Assert.That(goal.IsSystem).IsEqualTo(system);
        await Assert.That(goal.IsSetup).IsEqualTo(setup);
        await Assert.That(goal.IsTest).IsEqualTo(test);
    }

    [Test]
    public async Task Steps_DefaultsToEmptyList()
    {
        var goal = new Goal(_app.type.list);

        await Assert.That(goal.Step).IsNotNull();
        await Assert.That(goal.Step.Count).IsEqualTo(0);
    }

    [Test]
    public async Task SubGoals_DefaultsToEmptyList()
    {
        var goal = new Goal(_app.type.list);

        await Assert.That(goal.Child).IsNotNull();
        await Assert.That(goal.Child.CountRaw).IsEqualTo(0);
    }

    [Test]
    public async Task Parent_CanBeSet()
    {
        var parent = new Goal(_app.type.list) { Name = "ParentGoal" };
        var child = new Goal(_app.type.list) { Name = "ChildGoal" };

        child.Parent = parent;

        await Assert.That(child.Parent).IsEqualTo(parent);
    }

    [Test]
    public async Task FullPath_NoParent_ReturnsName()
    {
        var goal = new Goal(_app.type.list) { Name = "TestGoal" };

        await Assert.That(goal.FullPath).IsEqualTo("TestGoal");
    }

    [Test]
    public async Task FullPath_WithParent_IncludesParentPath()
    {
        var parent = new Goal(_app.type.list) { Name = "ParentGoal" };
        var child = new Goal(_app.type.list) { Name = "ChildGoal", Parent = parent };

        await Assert.That(child.FullPath).IsEqualTo("ParentGoal/ChildGoal");
    }

    [Test]
    public async Task FullPath_MultipleParents_IncludesFullHierarchy()
    {
        var grandparent = new Goal(_app.type.list) { Name = "Grandparent" };
        var parent = new Goal(_app.type.list) { Name = "Parent", Parent = grandparent };
        var child = new Goal(_app.type.list) { Name = "Child", Parent = parent };

        await Assert.That(child.FullPath).IsEqualTo("Grandparent/Parent/Child");
    }

    [Test]
    public async Task ToText_ReturnsFormattedGoal()
    {
        var goal = new Goal(_app.type.list)
        {
            Name = "TestGoal",
            Step = new GoalSteps(_app.type.list)
            {
                new Step(_app.type.list) { Index = 0, Text = "first step" },
                new Step(_app.type.list) { Index = 1, Text = "second step" }
            }
        };
        var text = goal.ToText();

        await Assert.That(text).Contains("TestGoal");
        await Assert.That(text).Contains("first step");
        await Assert.That(text).Contains("second step");
    }

    [Test]
    public async Task ToText_IncludesComment()
    {
        var goal = new Goal(_app.type.list)
        {
            Name = "TestGoal",
            Comment = "This is a goal comment"
        };
        var text = goal.ToText();

        await Assert.That(text).Contains("/ This is a goal comment");
    }

    [Test]
    public async Task ToText_IncludesStepComments()
    {
        var goal = new Goal(_app.type.list)
        {
            Name = "TestGoal",
            Step = new GoalSteps(_app.type.list)
            {
                new Step(_app.type.list) { Index = 0, Text = "step", Comment = "step comment" }
            }
        };
        var text = goal.ToText();

        await Assert.That(text).Contains("/ step comment");
    }

    [Test]
    public async Task ToText_RespectsStepIndent()
    {
        var goal = new Goal(_app.type.list)
        {
            Name = "TestGoal",
            Step = new GoalSteps(_app.type.list)
            {
                new Step(_app.type.list) { Index = 0, Text = "no indent", Line = new() { Indent = 0 } },
                new Step(_app.type.list) { Index = 1, Text = "one indent", Line = new() { Indent = 1 } },
                new Step(_app.type.list) { Index = 2, Text = "two indent", Line = new() { Indent = 2 } }
            }
        };
        var text = goal.ToText();

        await Assert.That(text).Contains("- no indent");
        await Assert.That(text).Contains(" - one indent");
        await Assert.That(text).Contains("  - two indent");
    }

    [Test]
    public async Task AGoalFile_WithCrLfLineEnds_ReadsAsItsLines()
    {
        var goals = global::app.goal.@this.Parse("Start\r\n- write out \"hello\"\r\n- write out \"bye\"\r\n", null, _app.actor.list.User.Context)!;

        await Assert.That(goals.Name).IsEqualTo("Start");
        await Assert.That(goals.Step.Count).IsEqualTo(2);
        await Assert.That(goals.Step[0].Text).IsEqualTo("write out \"hello\"");
        await Assert.That(goals.Step[1].Text).IsEqualTo("write out \"bye\"");
    }

    [Test]
    public async Task ToString_ReturnsGoalText()
    {
        var goal = new Goal(_app.type.list)
        {
            Name = "Start",
            Step = new GoalSteps(_app.type.list)
            {
                new Step(_app.type.list) { Index = 0, Text = "write out \"hello\"" }
            }
        };
        var str = goal.ToString();

        await Assert.That(str).IsEqualTo("Start\n- write out \"hello\"");
    }

}

public class VisibilityTests
{
    [Test]
    public async Task Private_HasValueZero()
    {
        await Assert.That((int)Visibility.Private).IsEqualTo(0);
    }

    [Test]
    public async Task Public_HasValueOne()
    {
        await Assert.That((int)Visibility.Public).IsEqualTo(1);
    }
}

public class GoalCacheTests
{
    // A goal rebuilt from its .pr keeps every step cached, an indented body folded into its condition
    // included: the source lists the body as a step of its own, the .pr holds it inside the condition.
    [Test]
    public async Task Merge_AFoldedBody_IsCachedLikeAnyStep()
    {
        await using var _app = new global::app.@this("/app", global::PLang.Tests.Parent.Actor).Testing();
        var set = global::PLang.Tests.Shared.Make.Action(_app.actor.list.User.Context, "variable", "set", global::PLang.Tests.Shared.Make.Param(_app.actor.list.User.Context, "Name", "%x%", "variable"), ("Value", 1));
        var condition = global::PLang.Tests.Shared.Make.Action(_app.actor.list.User.Context, "condition", "if", ("Left", "%b%"));
        var body = new Step(_app.type.list) { Index = 1, Text = "set %x% = 1", Line = new() { Number = 5, Indent = 1 } };
        body.Code.Add(set);
        condition.Child.Add(body);
        var gate = new Step(_app.type.list) { Index = 0, Text = "if %b%", Line = new() { Number = 4 } };
        gate.Code.Add(condition);
        var cached = new Goal(_app.type.list) { Name = "Start", Step = new GoalSteps(_app.type.list) { gate } };

        var source = new Goal(_app.type.list)
        {
            Name = "Start",
            Comment = "a comment the cached goal didn't have",
            Step = new GoalSteps(_app.type.list)
            {
                new Step(_app.type.list) { Index = 0, Text = "if %b%", Line = new() { Number = 4 } },
                new Step(_app.type.list) { Index = 1, Text = "set %x% = 1", Line = new() { Number = 5, Indent = 1 } },
            },
        };
        source.Merge(cached);

        await Assert.That(source.Step.IsCached).IsTrue();
    }

    // The hash covers the source as written, comments included: a comment-only change is a change.
    [Test]
    public async Task Hash_CoversTheGoalsAndTheStepsComments()
    {
        await using var app = new global::app.@this("/app", global::PLang.Tests.Parent.Actor).Testing();
        var types = app.type.list;
        Goal Written(string? goalComment, string? stepComment) => new(types)
        {
            Name = "Show",
            Comment = goalComment,
            Step = new GoalSteps(types) { new Step(types) { Index = 0, Text = "write out 'hi'", Comment = stepComment } },
        };
        var plain = Written(null, null).Hash;
        await Assert.That(Written(null, null).Hash).IsEqualTo(plain);
        await Assert.That(Written("Shows hi", null).Hash).IsNotEqualTo(plain);
        await Assert.That(Written(null, "say hi").Hash).IsNotEqualTo(plain);
    }
}
