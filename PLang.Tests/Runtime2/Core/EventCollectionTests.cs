using PLang.Runtime2.Core;

namespace PLang.Tests.Runtime2.Core;

public class EventCollectionTests
{
    [Test]
    public async Task Count_EmptyCollection_ReturnsZero()
    {
        var events = new EventCollection();

        await Assert.That(events.Count).IsEqualTo(0);
    }

    [Test]
    public async Task Register_Binding_IncreasesCount()
    {
        var events = new EventCollection();
        var binding = new EventBinding(EventType.BeforeGoal, _ => GoalResult.SuccessTask());

        events.Register(binding);

        await Assert.That(events.Count).IsEqualTo(1);
    }

    [Test]
    public async Task Register_Binding_ReturnsBindingId()
    {
        var events = new EventCollection();
        var binding = new EventBinding(EventType.BeforeGoal, _ => GoalResult.SuccessTask());

        var id = events.Register(binding);

        await Assert.That(id).IsEqualTo(binding.Id);
    }

    [Test]
    public async Task Register_WithHandler_ReturnsId()
    {
        var events = new EventCollection();

        var id = events.Register(EventType.BeforeGoal, _ => GoalResult.SuccessTask());

        await Assert.That(id).IsNotNull();
        await Assert.That(id.Length).IsEqualTo(8);
    }

    [Test]
    public async Task Unregister_ById_RemovesBinding()
    {
        var events = new EventCollection();
        var id = events.Register(EventType.BeforeGoal, _ => GoalResult.SuccessTask());

        var removed = events.Unregister(id);

        await Assert.That(removed).IsTrue();
        await Assert.That(events.Count).IsEqualTo(0);
    }

    [Test]
    public async Task Unregister_NonexistentId_ReturnsFalse()
    {
        var events = new EventCollection();

        var removed = events.Unregister("nonexistent");

        await Assert.That(removed).IsFalse();
    }

    [Test]
    public async Task Clear_RemovesAllBindings()
    {
        var events = new EventCollection();
        events.Register(EventType.BeforeGoal, _ => GoalResult.SuccessTask());
        events.Register(EventType.AfterGoal, _ => GoalResult.SuccessTask());

        events.Clear();

        await Assert.That(events.Count).IsEqualTo(0);
    }

    [Test]
    public async Task GetBindings_ReturnsBindingsOfType()
    {
        var events = new EventCollection();
        events.Register(EventType.BeforeGoal, _ => GoalResult.SuccessTask());
        events.Register(EventType.AfterGoal, _ => GoalResult.SuccessTask());
        events.Register(EventType.BeforeGoal, _ => GoalResult.SuccessTask());

        var bindings = events.GetBindings(EventType.BeforeGoal);

        await Assert.That(bindings.Count).IsEqualTo(2);
    }

    [Test]
    public async Task GetBindings_NoMatchingType_ReturnsEmpty()
    {
        var events = new EventCollection();
        events.Register(EventType.BeforeGoal, _ => GoalResult.SuccessTask());

        var bindings = events.GetBindings(EventType.OnError);

        await Assert.That(bindings.Count).IsEqualTo(0);
    }

    [Test]
    public async Task GetMatchingBindings_MatchesGoalPattern()
    {
        var events = new EventCollection();
        events.Register(EventType.BeforeGoal, _ => GoalResult.SuccessTask(), goalNamePattern: "Start");
        events.Register(EventType.BeforeGoal, _ => GoalResult.SuccessTask(), goalNamePattern: "Other");

        var bindings = events.GetMatchingBindings(EventType.BeforeGoal, "Start");

        await Assert.That(bindings.Count).IsEqualTo(1);
    }

    [Test]
    public async Task GetMatchingBindings_WildcardPattern_MatchesAll()
    {
        var events = new EventCollection();
        events.Register(EventType.BeforeGoal, _ => GoalResult.SuccessTask(), goalNamePattern: "*");

        var bindings = events.GetMatchingBindings(EventType.BeforeGoal, "AnyGoal");

        await Assert.That(bindings.Count).IsEqualTo(1);
    }

    [Test]
    public async Task GetMatchingBindings_PrefixPattern_MatchesPrefix()
    {
        var events = new EventCollection();
        events.Register(EventType.BeforeGoal, _ => GoalResult.SuccessTask(), goalNamePattern: "User*");

        var matchUser = events.GetMatchingBindings(EventType.BeforeGoal, "UserLogin");
        var matchOther = events.GetMatchingBindings(EventType.BeforeGoal, "AdminLogin");

        await Assert.That(matchUser.Count).IsEqualTo(1);
        await Assert.That(matchOther.Count).IsEqualTo(0);
    }

    [Test]
    public async Task GetMatchingBindings_NullGoalPattern_MatchesAll()
    {
        var events = new EventCollection();
        events.Register(EventType.BeforeGoal, _ => GoalResult.SuccessTask());

        var bindings = events.GetMatchingBindings(EventType.BeforeGoal, "AnyGoal");

        await Assert.That(bindings.Count).IsEqualTo(1);
    }

    [Test]
    public async Task GetMatchingBindings_StepPattern_MatchesContaining()
    {
        var events = new EventCollection();
        events.Register(EventType.BeforeStep, _ => GoalResult.SuccessTask(), stepPattern: "http");

        var matchHttp = events.GetMatchingBindings(EventType.BeforeStep, stepText: "call http endpoint");
        var matchOther = events.GetMatchingBindings(EventType.BeforeStep, stepText: "set variable");

        await Assert.That(matchHttp.Count).IsEqualTo(1);
        await Assert.That(matchOther.Count).IsEqualTo(0);
    }

    [Test]
    public async Task DispatchAsync_CallsMatchingHandlers()
    {
        var events = new EventCollection();
        var called = false;
        events.Register(EventType.BeforeGoal, _ =>
        {
            called = true;
            return GoalResult.SuccessTask();
        });

        await events.DispatchAsync(new EventContext { EventType = EventType.BeforeGoal });

        await Assert.That(called).IsTrue();
    }

    [Test]
    public async Task DispatchAsync_CallsHandlersInPriorityOrder()
    {
        var events = new EventCollection();
        var order = new List<int>();
        events.Register(EventType.BeforeGoal, _ => { order.Add(1); return GoalResult.SuccessTask(); }, priority: 1);
        events.Register(EventType.BeforeGoal, _ => { order.Add(3); return GoalResult.SuccessTask(); }, priority: 3);
        events.Register(EventType.BeforeGoal, _ => { order.Add(2); return GoalResult.SuccessTask(); }, priority: 2);

        await events.DispatchAsync(new EventContext { EventType = EventType.BeforeGoal });

        await Assert.That(order[0]).IsEqualTo(3);
        await Assert.That(order[1]).IsEqualTo(2);
        await Assert.That(order[2]).IsEqualTo(1);
    }

    [Test]
    public async Task DispatchAsync_StopsOnError_WhenStopOnErrorTrue()
    {
        var events = new EventCollection();
        var secondCalled = false;
        events.Register(EventType.BeforeGoal, _ => GoalResult.ErrorTask("Error"), priority: 2, stopOnError: true);
        events.Register(EventType.BeforeGoal, _ => { secondCalled = true; return GoalResult.SuccessTask(); }, priority: 1);

        var result = await events.DispatchAsync(new EventContext { EventType = EventType.BeforeGoal });

        await Assert.That(result.Success).IsFalse();
        await Assert.That(secondCalled).IsFalse();
    }

    [Test]
    public async Task DispatchAsync_ContinuesOnError_WhenStopOnErrorFalse()
    {
        var events = new EventCollection();
        var secondCalled = false;
        events.Register(EventType.BeforeGoal, _ => GoalResult.ErrorTask("Error"), priority: 2, stopOnError: false);
        events.Register(EventType.BeforeGoal, _ => { secondCalled = true; return GoalResult.SuccessTask(); }, priority: 1);

        await events.DispatchAsync(new EventContext { EventType = EventType.BeforeGoal });

        await Assert.That(secondCalled).IsTrue();
    }

    [Test]
    public async Task DispatchAsync_StopsWhenCancelTrue()
    {
        var events = new EventCollection();
        var secondCalled = false;
        events.Register(EventType.BeforeGoal, ctx => { ctx.Cancel = true; return GoalResult.SuccessTask(); }, priority: 2);
        events.Register(EventType.BeforeGoal, _ => { secondCalled = true; return GoalResult.SuccessTask(); }, priority: 1);

        await events.DispatchAsync(new EventContext { EventType = EventType.BeforeGoal });

        await Assert.That(secondCalled).IsFalse();
    }

    [Test]
    public async Task DispatchAsync_ReturnsOk_WhenCancelled()
    {
        var events = new EventCollection();
        events.Register(EventType.BeforeGoal, ctx => { ctx.Cancel = true; return GoalResult.SuccessTask(); });

        var result = await events.DispatchAsync(new EventContext { EventType = EventType.BeforeGoal });

        await Assert.That(result.Success).IsTrue();
    }

    [Test]
    public async Task DispatchAsync_NoMatchingHandlers_ReturnsOk()
    {
        var events = new EventCollection();

        var result = await events.DispatchAsync(new EventContext { EventType = EventType.BeforeGoal });

        await Assert.That(result.Success).IsTrue();
    }

    [Test]
    public async Task DispatchAsync_PassesContextToHandler()
    {
        var events = new EventCollection();
        EventContext? capturedContext = null;
        events.Register(EventType.BeforeGoal, ctx => { capturedContext = ctx; return GoalResult.SuccessTask(); });

        var context = new EventContext { EventType = EventType.BeforeGoal, GoalName = "TestGoal" };
        await events.DispatchAsync(context);

        await Assert.That(capturedContext).IsNotNull();
        await Assert.That(capturedContext!.GoalName).IsEqualTo("TestGoal");
    }
}

public class EventBindingTests
{
    [Test]
    public async Task Constructor_GeneratesId()
    {
        var binding = new EventBinding(EventType.BeforeGoal, _ => GoalResult.SuccessTask());

        await Assert.That(binding.Id).IsNotNull();
        await Assert.That(binding.Id.Length).IsEqualTo(8);
    }

    [Test]
    public async Task Constructor_SetsProperties()
    {
        Func<EventContext, Task<GoalResult>> handler = _ => GoalResult.SuccessTask();
        var binding = new EventBinding(EventType.AfterStep, handler, "TestGoal", "http", 10, false);

        await Assert.That(binding.Type).IsEqualTo(EventType.AfterStep);
        await Assert.That(binding.GoalNamePattern).IsEqualTo("TestGoal");
        await Assert.That(binding.StepPattern).IsEqualTo("http");
        await Assert.That(binding.Priority).IsEqualTo(10);
        await Assert.That(binding.StopOnError).IsFalse();
    }

    [Test]
    public async Task MatchesGoal_NullPattern_ReturnsTrue()
    {
        var binding = new EventBinding(EventType.BeforeGoal, _ => GoalResult.SuccessTask(), goalNamePattern: null);

        await Assert.That(binding.MatchesGoal("AnyGoal")).IsTrue();
    }

    [Test]
    public async Task MatchesGoal_EmptyPattern_ReturnsTrue()
    {
        var binding = new EventBinding(EventType.BeforeGoal, _ => GoalResult.SuccessTask(), goalNamePattern: "");

        await Assert.That(binding.MatchesGoal("AnyGoal")).IsTrue();
    }

    [Test]
    public async Task MatchesGoal_WildcardPattern_ReturnsTrue()
    {
        var binding = new EventBinding(EventType.BeforeGoal, _ => GoalResult.SuccessTask(), goalNamePattern: "*");

        await Assert.That(binding.MatchesGoal("AnyGoal")).IsTrue();
    }

    [Test]
    public async Task MatchesGoal_PrefixPattern_MatchesPrefix()
    {
        var binding = new EventBinding(EventType.BeforeGoal, _ => GoalResult.SuccessTask(), goalNamePattern: "User*");

        await Assert.That(binding.MatchesGoal("UserLogin")).IsTrue();
        await Assert.That(binding.MatchesGoal("UserRegister")).IsTrue();
        await Assert.That(binding.MatchesGoal("AdminLogin")).IsFalse();
    }

    [Test]
    public async Task MatchesGoal_ExactPattern_MatchesExact()
    {
        var binding = new EventBinding(EventType.BeforeGoal, _ => GoalResult.SuccessTask(), goalNamePattern: "Start");

        await Assert.That(binding.MatchesGoal("Start")).IsTrue();
        await Assert.That(binding.MatchesGoal("StartGoal")).IsFalse();
    }

    [Test]
    public async Task MatchesGoal_CaseInsensitive()
    {
        var binding = new EventBinding(EventType.BeforeGoal, _ => GoalResult.SuccessTask(), goalNamePattern: "start");

        await Assert.That(binding.MatchesGoal("START")).IsTrue();
        await Assert.That(binding.MatchesGoal("Start")).IsTrue();
    }

    [Test]
    public async Task MatchesStep_NullPattern_ReturnsTrue()
    {
        var binding = new EventBinding(EventType.BeforeStep, _ => GoalResult.SuccessTask(), stepPattern: null);

        await Assert.That(binding.MatchesStep("any step")).IsTrue();
    }

    [Test]
    public async Task MatchesStep_EmptyPattern_ReturnsTrue()
    {
        var binding = new EventBinding(EventType.BeforeStep, _ => GoalResult.SuccessTask(), stepPattern: "");

        await Assert.That(binding.MatchesStep("any step")).IsTrue();
    }

    [Test]
    public async Task MatchesStep_WildcardPattern_ReturnsTrue()
    {
        var binding = new EventBinding(EventType.BeforeStep, _ => GoalResult.SuccessTask(), stepPattern: "*");

        await Assert.That(binding.MatchesStep("any step")).IsTrue();
    }

    [Test]
    public async Task MatchesStep_ContainsPattern_MatchesSubstring()
    {
        var binding = new EventBinding(EventType.BeforeStep, _ => GoalResult.SuccessTask(), stepPattern: "http");

        await Assert.That(binding.MatchesStep("call http endpoint")).IsTrue();
        await Assert.That(binding.MatchesStep("set variable")).IsFalse();
    }

    [Test]
    public async Task MatchesStep_CaseInsensitive()
    {
        var binding = new EventBinding(EventType.BeforeStep, _ => GoalResult.SuccessTask(), stepPattern: "HTTP");

        await Assert.That(binding.MatchesStep("call http endpoint")).IsTrue();
    }
}

public class EventContextTests
{
    [Test]
    public async Task Properties_CanBeSet()
    {
        var context = new EventContext
        {
            EventType = EventType.OnError,
            GoalName = "TestGoal",
            StepIndex = 5,
            StepText = "test step",
            Cancel = false
        };

        await Assert.That(context.EventType).IsEqualTo(EventType.OnError);
        await Assert.That(context.GoalName).IsEqualTo("TestGoal");
        await Assert.That(context.StepIndex).IsEqualTo(5);
        await Assert.That(context.StepText).IsEqualTo("test step");
        await Assert.That(context.Cancel).IsFalse();
    }

    [Test]
    public async Task Data_CanBeSetAndRetrieved()
    {
        var context = new EventContext();
        var data = new { Name = "Test" };

        context.Data = data;

        await Assert.That(context.Data).IsEqualTo(data);
    }

    [Test]
    public async Task Cancel_DefaultsFalse()
    {
        var context = new EventContext();

        await Assert.That(context.Cancel).IsFalse();
    }
}
