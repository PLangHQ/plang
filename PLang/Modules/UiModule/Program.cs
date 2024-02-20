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
		private readonly IOutputStreamFactory outputStreamFactory;

		public Program(IOutputStreamFactory outputStream) : base()
		{
			this.outputStreamFactory = outputStream;
			
		}

		public async Task RenderHtml(string html, string css, string javascript)
		{
			if (outputStreamFactory is ConsoleOutputStream)
			{
				throw new RuntimeException("Incorrect output stream. You probably ran the command: plang run, but you should run: plangw run");
			}

			string content = css + "\n" + html + "\n" + javascript;
			content = variableHelper.LoadVariables(content).ToString();

			if (string.IsNullOrEmpty(content)) return;
			
			var os = (UIOutputStream) outputStreamFactory;
			os.MemoryStack = memoryStack;
			os.Goal = goal;
			os.GoalStep = goalStep;

			var nextStep = goalStep.NextStep;
			while (nextStep != null && nextStep.Indent > 0)
			{
				nextStep.Execute = true;
				nextStep = nextStep.NextStep;
			}
			await os.Write(content, "text", 200, (goalStep.Indent > 0) ? goalStep.Number : -1);
		}

		public async Task AskUserHtml(string html)
		{
			html = variableHelper.LoadVariables(html).ToString();

			if (string.IsNullOrEmpty(html)) return;

			var os = outputStreamFactory.CreateHandler();
			if (os is UIOutputStream uios)
			{
				uios.MemoryStack = memoryStack;
			}

			await os.Ask(html);
		}

		public void Flush()
		{
			var os = outputStreamFactory.CreateHandler();
			if (os is UIOutputStream)
			{
				((UIOutputStream)os).Flush();
			}
		}
	}

}

