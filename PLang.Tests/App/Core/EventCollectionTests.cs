using App.Context;
using App;
using App.Errors;
using App.Variables;

namespace PLang.Tests.App.Core;

public class EventsTests
{
    private static Context.@this CreateContext()
    {
        var engine = new App.@this("/app");
        return new Context.@this(engine);
    }

    [Test]
    public async Task Count_EmptyCollection_ReturnsZero()
    {
        var events = new EngineEvents();

        await Assert.That(events.Count).IsEqualTo(0);
    }

    [Test]
    public async Task Register_Binding_IncreasesCount()
    {
        var events = new EngineEvents();
        var binding = new EventBinding(EventType.BeforeGoal, _ => Task.FromResult(Data.Ok()));

        events.Register(binding);

        await Assert.That(events.Count).IsEqualTo(1);
    }

    [Test]
    public async Task Register_Binding_ReturnsBindingId()
    {
        var events = new EngineEvents();
        var binding = new EventBinding(EventType.BeforeGoal, _ => Task.FromResult(Data.Ok()));

        var id = events.Register(binding);

        await Assert.That(id).IsEqualTo(binding.Id);
    }

    [Test]
    public async Task Register_WithHandler_ReturnsId()
    {
        var events = new EngineEvents();

        var id = events.Register(new EventBinding(EventType.BeforeGoal, _ => Task.FromResult(Data.Ok())));

        await Assert.That(id).IsNotNull();
        await Assert.That(id.Length).IsEqualTo(8);
    }

    [Test]
    public async Task Unregister_ById_RemovesBinding()
    {
        var events = new EngineEvents();
        var id = events.Register(new EventBinding(EventType.BeforeGoal, _ => Task.FromResult(Data.Ok())));

        var removed = events.Unregister(id);

        await Assert.That(removed).IsTrue();
        await Assert.That(events.Count).IsEqualTo(0);
    }

    [Test]
    public async Task Unregister_NonexistentId_ReturnsFalse()
    {
        var events = new EngineEvents();

        var removed = events.Unregister("nonexistent");

        await Assert.That(removed).IsFalse();
    }

    [Test]
    public async Task Clear_RemovesAllBindings()
    {
        var events = new EngineEvents();
        events.Register(new EventBinding(EventType.BeforeGoal, _ => Task.FromResult(Data.Ok())));
        events.Register(new EventBinding(EventType.AfterGoal, _ => Task.FromResult(Data.Ok())));

        events.Clear();

        await Assert.That(events.Count).IsEqualTo(0);
    }

    [Test]
    public async Task GetBindings_ReturnsBindingsOfType()
    {
        var events = new EngineEvents();
        events.Register(new EventBinding(EventType.BeforeGoal, _ => Task.FromResult(Data.Ok())));
        events.Register(new EventBinding(EventType.AfterGoal, _ => Task.FromResult(Data.Ok())));
        events.Register(new EventBinding(EventType.BeforeGoal, _ => Task.FromResult(Data.Ok())));

        var bindings = events.GetBindings(EventType.BeforeGoal);

        await Assert.That(bindings.Count).IsEqualTo(2);
    }

    [Test]
    public async Task GetBindings_NoMatchingType_ReturnsEmpty()
    {
        var events = new EngineEvents();
        events.Register(new EventBinding(EventType.BeforeGoal, _ => Task.FromResult(Data.Ok())));

        var bindings = events.GetBindings(EventType.OnError);

        await Assert.That(bindings.Count).IsEqualTo(0);
    }

    [Test]
    public async Task GetMatchingBindings_MatchesGoalPattern()
    {
        var events = new EngineEvents();
        events.Register(new EventBinding(EventType.BeforeGoal, _ => Task.FromResult(Data.Ok()), goalNamePattern: "Start"));
        events.Register(new EventBinding(EventType.BeforeGoal, _ => Task.FromResult(Data.Ok()), goalNamePattern: "Other"));

        var bindings = events.GetMatchingBindings(EventType.BeforeGoal, "Start");

        await Assert.That(bindings.Count).IsEqualTo(1);
    }

    [Test]
    public async Task GetMatchingBindings_WildcardPattern_MatchesAll()
    {
        var events = new EngineEvents();
        events.Register(new EventBinding(EventType.BeforeGoal, _ => Task.FromResult(Data.Ok()), goalNamePattern: "*"));

        var bindings = events.GetMatchingBindings(EventType.BeforeGoal, "AnyGoal");

        await Assert.That(bindings.Count).IsEqualTo(1);
    }

    [Test]
    public async Task GetMatchingBindings_PrefixPattern_MatchesPrefix()
    {
        var events = new EngineEvents();
        events.Register(new EventBinding(EventType.BeforeGoal, _ => Task.FromResult(Data.Ok()), goalNamePattern: "User*"));

        var matchUser = events.GetMatchingBindings(EventType.BeforeGoal, "UserLogin");
        var matchOther = events.GetMatchingBindings(EventType.BeforeGoal, "AdminLogin");

        await Assert.That(matchUser.Count).IsEqualTo(1);
        await Assert.That(matchOther.Count).IsEqualTo(0);
    }

    [Test]
    public async Task GetMatchingBindings_NullGoalPattern_MatchesAll()
    {
        var events = new EngineEvents();
        events.Register(new EventBinding(EventType.BeforeGoal, _ => Task.FromResult(Data.Ok())));

        var bindings = events.GetMatchingBindings(EventType.BeforeGoal, "AnyGoal");

        await Assert.That(bindings.Count).IsEqualTo(1);
    }

    [Test]
    public async Task GetMatchingBindings_StepPattern_MatchesContaining()
    {
        var events = new EngineEvents();
        events.Register(new EventBinding(EventType.BeforeStep, _ => Task.FromResult(Data.Ok()), stepPattern: "http"));

        var matchHttp = events.GetMatchingBindings(EventType.BeforeStep, stepText: "call http endpoint");
        var matchOther = events.GetMatchingBindings(EventType.BeforeStep, stepText: "set variable");

        await Assert.That(matchHttp.Count).IsEqualTo(1);
        await Assert.That(matchOther.Count).IsEqualTo(0);
    }

}

public class EventBindingTests
{
    [Test]
    public async Task Constructor_GeneratesId()
    {
        var binding = new EventBinding(EventType.BeforeGoal, _ => Task.FromResult(Data.Ok()));

        await Assert.That(binding.Id).IsNotNull();
        await Assert.That(binding.Id.Length).IsEqualTo(8);
    }

    [Test]
    public async Task Constructor_SetsProperties()
    {
        Func<Context.@this, Task<Data>> handler = _ => Task.FromResult(Data.Ok());
        var binding = new EventBinding(EventType.AfterStep, handler, "TestGoal", "http", null, 10, false);

        await Assert.That(binding.Type).IsEqualTo(EventType.AfterStep);
        await Assert.That(binding.GoalNamePattern).IsEqualTo("TestGoal");
        await Assert.That(binding.StepPattern).IsEqualTo("http");
        await Assert.That(binding.Priority).IsEqualTo(10);
        await Assert.That(binding.StopOnError).IsFalse();
    }

    [Test]
    public async Task MatchesGoal_NullPattern_ReturnsTrue()
    {
        var binding = new EventBinding(EventType.BeforeGoal, _ => Task.FromResult(Data.Ok()), goalNamePattern: null);

        await Assert.That(binding.MatchesGoal("AnyGoal")).IsTrue();
    }

    [Test]
    public async Task MatchesGoal_EmptyPattern_ReturnsTrue()
    {
        var binding = new EventBinding(EventType.BeforeGoal, _ => Task.FromResult(Data.Ok()), goalNamePattern: "");

        await Assert.That(binding.MatchesGoal("AnyGoal")).IsTrue();
    }

    [Test]
    public async Task MatchesGoal_WildcardPattern_ReturnsTrue()
    {
        var binding = new EventBinding(EventType.BeforeGoal, _ => Task.FromResult(Data.Ok()), goalNamePattern: "*");

        await Assert.That(binding.MatchesGoal("AnyGoal")).IsTrue();
    }

    [Test]
    public async Task MatchesGoal_PrefixPattern_MatchesPrefix()
    {
        var binding = new EventBinding(EventType.BeforeGoal, _ => Task.FromResult(Data.Ok()), goalNamePattern: "User*");

        await Assert.That(binding.MatchesGoal("UserLogin")).IsTrue();
        await Assert.That(binding.MatchesGoal("UserRegister")).IsTrue();
        await Assert.That(binding.MatchesGoal("AdminLogin")).IsFalse();
    }

    [Test]
    public async Task MatchesGoal_ExactPattern_MatchesExact()
    {
        var binding = new EventBinding(EventType.BeforeGoal, _ => Task.FromResult(Data.Ok()), goalNamePattern: "Start");

        await Assert.That(binding.MatchesGoal("Start")).IsTrue();
        await Assert.That(binding.MatchesGoal("StartGoal")).IsFalse();
    }

    [Test]
    public async Task MatchesGoal_CaseInsensitive()
    {
        var binding = new EventBinding(EventType.BeforeGoal, _ => Task.FromResult(Data.Ok()), goalNamePattern: "start");

        await Assert.That(binding.MatchesGoal("START")).IsTrue();
        await Assert.That(binding.MatchesGoal("Start")).IsTrue();
    }

    [Test]
    public async Task MatchesStep_NullPattern_ReturnsTrue()
    {
        var binding = new EventBinding(EventType.BeforeStep, _ => Task.FromResult(Data.Ok()), stepPattern: null);

        await Assert.That(binding.MatchesStep("any step")).IsTrue();
    }

    [Test]
    public async Task MatchesStep_EmptyPattern_ReturnsTrue()
    {
        var binding = new EventBinding(EventType.BeforeStep, _ => Task.FromResult(Data.Ok()), stepPattern: "");

        await Assert.That(binding.MatchesStep("any step")).IsTrue();
    }

    [Test]
    public async Task MatchesStep_WildcardPattern_ReturnsTrue()
    {
        var binding = new EventBinding(EventType.BeforeStep, _ => Task.FromResult(Data.Ok()), stepPattern: "*");

        await Assert.That(binding.MatchesStep("any step")).IsTrue();
    }

    [Test]
    public async Task MatchesStep_ContainsPattern_MatchesSubstring()
    {
        var binding = new EventBinding(EventType.BeforeStep, _ => Task.FromResult(Data.Ok()), stepPattern: "http");

        await Assert.That(binding.MatchesStep("call http endpoint")).IsTrue();
        await Assert.That(binding.MatchesStep("set variable")).IsFalse();
    }

    [Test]
    public async Task MatchesStep_CaseInsensitive()
    {
        var binding = new EventBinding(EventType.BeforeStep, _ => Task.FromResult(Data.Ok()), stepPattern: "HTTP");

        await Assert.That(binding.MatchesStep("call http endpoint")).IsTrue();
    }
}
