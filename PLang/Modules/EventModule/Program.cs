using PLang.Errors;
using PLang.Events;
using System.ComponentModel;

namespace PLang.Modules.EventModule;

[Description(@"Bind an event to app, goal, step, error, and any kind of custom errors.
Example:
- run before app starts, call goal Starting
- run on each step, call StepAfter (before|after(default))
- on error call HandleError
")]
public class Program : BaseProgram
{
	public Program() : base()
	{

	}

	public async Task<IError?> BindEvent(EventBinding eventBinding)
	{

		//engine.GetEventRuntime().AddEvent(eventBinding);
		return null;
	}

	public async Task<IError?> RunEvents(List<EventBinding> eventBinding)
	{

		//engine.GetEventRuntime().AddEvent(eventBinding);
		return null;
	}

}


