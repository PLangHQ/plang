using Make = global::PLang.Tests.Shared.Make;

namespace PLang.Tests.App.Goals;

/// <summary>
/// A goal that calls itself forever is refused as the call stack's overflow before the thread's stack runs out,
/// whatever that stack's size: a stack that overflows can't be caught and would end the process.
/// </summary>
public class StackDepthTests
{
    [Test]
    public async Task ARunawayRecursion_OnASmallStack_AnswersTheOverflow()
    {
        await using var app = new global::app.@this("/tmp/stack-" + System.Guid.NewGuid().ToString("N")[..8], global::PLang.Tests.Parent.Actor).Testing();
        var context = app.actor.list.User.Context;
        var goal = await global::PLang.Tests.Shared.RealGoalLoad.ViaChannel(app, Make.Goal(context, "Again", "/Again.goal",
            Make.Step("call Again", Make.Action(context, "goal", "call", ("Name", "Again")))));
        app.goal.list.Add(goal);

        // the recursion runs on a thread whose stack is far smaller than MaxDepth's frames need
        global::app.data.@this? answer = null;
        System.Exception? thrown = null;
        var thread = new System.Threading.Thread(() =>
        {
            try { answer = goal.Start(context).GetAwaiter().GetResult(); }
            catch (System.Exception ex) { thrown = ex; }
        }, maxStackSize: 256 * 1024);
        thread.Start();
        thread.Join();

        await Assert.That(thrown).IsNull();
        await Assert.That(answer!.Error?.Key).IsEqualTo("CallStackOverflow");
        await Assert.That(answer.Error!.Message).Contains("stack ran short");
    }
}
