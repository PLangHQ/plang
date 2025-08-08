using PLang.Errors;
using PLang.Events;

namespace PLang.Modules.EventModule;

public class Program : BaseProgram
{
	public Program() : base()
	{

	}

	public async Task<IError?> BindEvent(EventBinding eventBinding)
	{

		engine.GetEventRuntime().AddEvent(eventBinding);
		return null;
	}

}


