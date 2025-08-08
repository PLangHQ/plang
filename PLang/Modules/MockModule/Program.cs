using PLang.Errors;
using PLang.Models;
using System.ComponentModel;

namespace PLang.Modules.MockModule
{
	[Description("Mock other modules. Code should start with `mock XXX` where XXX would be the module")]
	public class Program : BaseProgram
	{

		public record MockData(GoalToCallInfo GoalToCall, string ModuleType, string MethodName, Dictionary<string, object?>? Parameters = null);
		public async Task<IError?> MockMethod(MockData mockData)
		{
			engine.Mocks.Add(mockData);
			return null;
		}

	}
}
