using PLang.Exceptions;
using PLang.Services.OutputStream;
using System.ComponentModel;

namespace PLang.Modules.UiModule
{
	public interface IFlush
	{
		void Flush();
	}
    [Description("Takes any user command and tries to convert it to html")]
	public class Program : BaseProgram, IFlush
	{
		private readonly IOutputStream outputStream;

		public Program(IOutputStream outputStream) : base()
		{
			this.outputStream = outputStream;
			
		}

		public async Task RenderHtml(string html)
		{
			html = variableHelper.LoadVariables(html).ToString();

			if (string.IsNullOrEmpty(html)) return;
			if (outputStream is ConsoleOutputStream) {
				throw new RuntimeException("Incorrect output stream. You probably ran the command: plang run, but you should run: plangw run");
			}
			var os = (UIOutputStream) outputStream;
			os.MemoryStack = memoryStack;
			os.Goal = goal;
			os.GoalStep = goalStep;

			var nextStep = goalStep.NextStep;
			while (nextStep != null && nextStep.Indent > 0)
			{
				nextStep.Execute = true;
				nextStep = nextStep.NextStep;
			}
			await os.Write(html, "text", 200, (goalStep.Indent > 0) ? goalStep.Number : -1);
		}

		public async Task AskUserHtml(string html)
		{
			html = variableHelper.LoadVariables(html).ToString();

			if (string.IsNullOrEmpty(html)) return;
			((UIOutputStream)outputStream).MemoryStack = memoryStack;
			await outputStream.Ask(html);
		}

		public void Flush()
		{
			if (outputStream is UIOutputStream)
			{
				((UIOutputStream)outputStream).Flush();
			}
		}
	}

}

