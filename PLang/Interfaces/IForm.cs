using PLang.Errors;
using PLang.Services.OutputStream;
using static PLang.Modules.OutputModule.Program;
using static PLang.Modules.UiModule.Program;
using static PLang.Utils.StepHelper;

namespace PLang.Interfaces
{
	public interface IForm
	{
		void SetSize(int width, int height);
		void SetIcon(string? icon);
		void SetTitle(string? title);
		/*
		Task ModifyContent(string content, OutputTarget outputTarget, string id);
		Task ExecuteCode(string content);
		*/
		Task Flush(string content);
		Task<(object?, IError?)> Ask(AskOptions askOptions, Callback? callback = null, IError? error = null);

		public SynchronizationContext SynchronizationContext { get; set; }
		bool Visible { get; set; }
	}
}
