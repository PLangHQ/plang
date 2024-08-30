using Newtonsoft.Json;
using Org.BouncyCastle.Utilities.Zlib;
using PLang.Exceptions;
using PLang.Interfaces;
using PLang.Resources;
using PLang.Runtime;
using PLang.Services.OutputStream;
using PLang.Utils;
using Scriban.Syntax;
using Scriban;
using System.ComponentModel;
using System.Dynamic;
using System.IO.Compression;
using static System.Net.Mime.MediaTypeNames;
using System;

namespace PLang.Modules.UiModule
{
	public interface IFlush
	{
		Task Flush();
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
			if (outputStream is not UIOutputStream)
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
			await os.IForm.ModifyContent(cssSelector, content, "beforeend");

			var nextStep = goalStep.NextStep;
			while (nextStep != null && nextStep.Indent > 0)
			{
				nextStep.Execute = true;
				nextStep = nextStep.NextStep;
			}
		}

		public async Task RenderHtml(string? html = null, string? css = null, string? javascript = null, List<string>? variables = null)
		{
			var outputStream = outputStreamFactory.CreateHandler();
			if (outputStream is not UIOutputStream os)
			{
				throw new RuntimeException("Incorrect output stream. You probably ran the command: plang run, but you should run: plangw run");
			}

			string content = css + "\n" + html + "\n" + javascript;
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

			var indent = context.GetOrDefault(ReservedKeywords.ParentGoalIndent, 0);
			//indent += goalStep.Indent;
			AddToTree(content, indent);
		}


		private void AddToTree(object? obj, int indent)
		{
			if (obj == null) return;

			var root = context.GetOrDefault(ReservedKeywords.GoalTree, default(GoalTree<string>));
			if (root == null)
			{
				root = new GoalTree<string>(obj.ToString(), indent);
				context.AddOrReplace(ReservedKeywords.GoalTree, root);
				return;
			}

			var nodeToAddTo = root.Current;
			if (nodeToAddTo.Indent == indent)
			{
				nodeToAddTo = nodeToAddTo.Parent ?? root;
			}

			var newNode = new GoalTree<string>(obj.ToString(), indent);
			nodeToAddTo.AddChild(newNode);
			root.Current = newNode;

		}

		private GoalTree<string>? GetNodeToAdd(GoalTree<string>? node)
		{
			if (node == null) return null;
			if (node.Value.Contains("{{ ChildrenElements")) return node;
			return GetNodeToAdd(node.Parent);
		}


		public async Task Flush()
		{
			var root = context.GetOrDefault(ReservedKeywords.GoalTree, default(GoalTree<string>));
			if (root == null) return;

			var os = outputStreamFactory.CreateHandler();
			if (os is UIOutputStream)
			{
				var stringContent = root.PrintTree();
				var html = await CompileAndRun(stringContent);
				await os.Write(html);
				//((UIOutputStream)os).Flush();
			}
		}

		private async Task<string> CompileAndRun(object? obj)
		{
			var expandoObject = new ExpandoObject() as IDictionary<string, object?>;
			var templateContext = new TemplateContext();
			foreach (var kvp in memoryStack.GetMemoryStack())
			{
				expandoObject.Add(kvp.Key, kvp.Value.Value);

				var sv = ScriptVariable.Create(kvp.Key, ScriptVariableScope.Global);
				templateContext.SetValue(sv, kvp.Value.Value);
			}

			var parsed = Template.Parse(obj.ToString());
			var result = await parsed.RenderAsync(templateContext);

			return result;
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

		private void SetupCssAndJsFiles()
		{

			if (!fileSystem.File.Exists("ui/assets/htmx.min.js"))
			{

				using (MemoryStream ms = new MemoryStream(InternalApps.js_css))
				using (ZipArchive archive = new ZipArchive(ms))
				{
					archive.ExtractToDirectory(Path.Join(fileSystem.GoalsPath, "ui", "assets"), true);
				}
			}
		}
	}

}

