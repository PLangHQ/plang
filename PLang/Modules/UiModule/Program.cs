using Newtonsoft.Json;
using Org.BouncyCastle.Utilities.Zlib;
using PLang.Exceptions;
using PLang.Interfaces;
using PLang.Services.OutputStream;
using System.ComponentModel;
using static System.Net.Mime.MediaTypeNames;

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
		private readonly IPLangFileSystem fileSystem;

		public Program(IOutputStreamFactory outputStream, IPLangFileSystem fileSystem) : base()
		{
			this.outputStreamFactory = outputStream;
			this.fileSystem = fileSystem;
		}

		public async Task SetInputValue(string cssSelector, string value = "")
		{
			string escapedHtmlContent = string.IsNullOrEmpty(value) ? "''" : JsonConvert.ToString(value);
			await ExecuteJavascript($"document.querySelector('{cssSelector}').value = {escapedHtmlContent}");
		}

			[Description("Executes javascript code. The javascript code should be structure in following way: (function() { function nameOfFunction() { // System;your job is to create the code here } nameOfFunction(); }")]
		public async Task ExecuteJavascript(string javascript)
		{
			var outputStream = outputStreamFactory.CreateHandler();
			if (outputStream is ConsoleOutputStream)
			{
				throw new RuntimeException("Incorrect output stream. You probably ran the command: plang run, but you should run: plangw run");
			}

			string content = variableHelper.LoadVariables(javascript).ToString();

			if (string.IsNullOrEmpty(content)) return;

			var os = (UIOutputStream)outputStream;
			os.MemoryStack = memoryStack;
			os.Goal = goal;
			os.GoalStep = goalStep;
			await os.IForm.ExecuteCode(content);


		}
			public async Task AppendToElement(string cssSelector, string? html = null, string? css = null, string? javascript = null)
		{
			var outputStream = outputStreamFactory.CreateHandler();
			if (outputStream is ConsoleOutputStream)
			{
				throw new RuntimeException("Incorrect output stream. You probably ran the command: plang run, but you should run: plangw run");
			}
			if (css != null && !css.Contains("<style")) css = $"<style>{css}</style>";
			if (javascript != null && !javascript.Contains("<script")) javascript = $"<script>{javascript}</script>";
			string content = css + "\n" + html + "\n" + javascript;
			content = variableHelper.LoadVariables(content).ToString();

			if (string.IsNullOrEmpty(content)) return;

			var os = (UIOutputStream)outputStream;
			os.MemoryStack = memoryStack;
			os.Goal = goal;
			os.GoalStep = goalStep;
			await os.IForm.AppendContent(cssSelector, content);

			var nextStep = goalStep.NextStep;
			while (nextStep != null && nextStep.Indent > 0)
			{
				nextStep.Execute = true;
				nextStep = nextStep.NextStep;
			}
		}

		public async Task RenderHtml(string? html = null, string? css = null, string? javascript = null)
		{
			var outputStream = outputStreamFactory.CreateHandler();
			if (outputStream is not UIOutputStream os)
			{
				throw new RuntimeException("Incorrect output stream. You probably ran the command: plang run, but you should run: plangw run");
			}

			string content = css + "\n" + html + "\n" + javascript;
			content = variableHelper.LoadVariables(content).ToString();

			if (string.IsNullOrEmpty(content)) return;
			
			os.MemoryStack = memoryStack;
			os.Goal = goal;
			os.GoalStep = goalStep;

			var nextStep = goalStep.NextStep;
			while (nextStep != null && nextStep.Indent > 0)
			{
				nextStep.Execute = true;
				nextStep = nextStep.NextStep;
			}
			SetupCssAndJsFiles();

			await os.IForm.BufferContent(content);
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


		private void SetupCssAndJsFiles()
		{

			if (!fileSystem.File.Exists("ui/bootstrap.min.css"))
			{
				fileSystem.File.WriteAllText("ui/bootstrap.min.css", Resources.InternalApps.bootstrap_5_0_2_min_css);
			}
			if (!fileSystem.File.Exists("ui/bootstrap.bundle.min.js"))
			{
				fileSystem.File.WriteAllText("ui/bootstrap.bundle.min.js", Resources.InternalApps.bootstrap_bundle_5_0_2_min_js);
			}
			if (!fileSystem.File.Exists("ui/fontawesome.min.css"))
			{
				fileSystem.File.WriteAllText("ui/fontawesome.min.css", Resources.InternalApps.fontawesome_5_15_3_min_css);
			}
			if (!fileSystem.File.Exists("ui/fontawesome.min.js"))
			{
				fileSystem.File.WriteAllText("ui/fontawesome.min.js", Resources.InternalApps.fontawesome_5_15_3_min_js);
			}
		}
	}

}

