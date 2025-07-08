using PLang.Building.Model;
using PLang.Errors;
using PLang.Runtime;
using static PLang.Modules.OutputModule.Program;
using static PLang.Utils.StepHelper;

namespace PLang.Services.OutputStream
{
	public interface IOutputStream
	{
		public Stream Stream { get; }
		public Stream ErrorStream { get; }
		public string Output { get; }
		public bool IsStateful { get; }
		public bool IsFlushed { get; set; }
		public GoalStep Step { get; set; }
		public Task Write(object? obj, string type = "text", int statusCode = 200, Dictionary<string, object?>? parameters = null);
        public string Read();
        public Task<(object?, IError?)> Ask(AskOptions askOptions, Callback? callback = null, IError? error = null);

    }

    
}
