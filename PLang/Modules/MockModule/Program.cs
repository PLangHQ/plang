using PLang.Errors;
using PLang.Models;
using System.ComponentModel;

namespace PLang.Modules.MockModule
{
	[Description(@"Mock other modules. Code should start with `mock XXX` where XXX would be the module, each mock needs to call a goal that will perform the mocking
Example
`mock http post http://example.org, call goal ProcessExample` => ModuleType=""Namespace.HttpModule"", MethodName=""post"", GoalToCall={Name:ProcessExample}

")]
	public class Program : BaseProgram
	{

		public record MockData(GoalToCallInfo GoalToCall, string ModuleType, string MethodName, Dictionary<string, object?>? Parameters = null);
		public async Task<IError?> MockMethod(MockData mockData)
		{
			if (context.Mocks.Contains(mockData)) return null;

			context.Mocks.Add(mockData);
			return null;
		}

	}
}
