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
using PLang.Models;
using Org.BouncyCastle.Asn1;
using PLang.Errors.Runtime;
using PLang.Errors;
using Sprache;
using System.Threading;

namespace PLang.Modules.UiModule
{
	public interface IFlush
	{
		Task Flush();
	}
	[Description("Takes any user command and tries to convert it to html. Add, remove, insert content to css selector")]
	public class Program : BaseProgram, IFlush
	{
		private readonly IOutputStreamFactory outputStreamFactory;
		private readonly IPLangFileSystem fileSystem;

		public Program(IOutputStreamFactory outputStream, IPLangFileSystem fileSystem) : base()
		{
			this.outputStreamFactory = outputStream;
			this.fileSystem = fileSystem;
		}

		private string EscapeTextForJavascript(string content)
		{
			return string.IsNullOrEmpty(content) ? "''" : JsonConvert.ToString(content); ;
		}

		public async Task SetInputValue(string cssSelector, string value = "")
		{
			string escapedHtmlContent = EscapeTextForJavascript(value);
			await ExecuteJavascript($"document.querySelector('{cssSelector}').value = {escapedHtmlContent}");
		}


		[Description("Executes javascript code. The javascript code should be structure in following way: (function() { function nameOfFunction() { // System;your job is to create the code here } nameOfFunction(); }")]
		public async Task ExecuteJavascript(string javascript)
		{
			if (string.IsNullOrEmpty(javascript)) return;

			var outputStream = outputStreamFactory.CreateHandler();
			if (outputStream is not UIOutputStream)
			{
				throw new RuntimeException("Incorrect output stream. You probably ran the command: plang run, but you should run: plangw run");
			}

			var os = (UIOutputStream)outputStream;
			os.MemoryStack = memoryStack;
			os.Goal = goal;
			os.GoalStep = goalStep;
			await os.IForm.ExecuteCode(javascript);


		}

		[Description("Set the target request in the whole app. User input is on the lines of `- set default target...`")]
		public async Task SetAsDefaultTargetElement(string cssSelector)
		{
			context.AddOrReplace(ReservedKeywords.DefaultTargetElement, cssSelector);
		}

		public record OutputTarget(string[] cssSelectors,
			[Description("replace|replaceOuter|appendTo|afterElement|beforeElement|prependTo|insertAfter|insertBefore")]
			string elementPosition = "replace",
			bool overwriteIfExists = true, string? overwriteElement = null);

		[Description("Tells the output where to write the content in the UI.")]
		public async Task<IError?> SetTargetElement(
			[Description("where to place content")]
			string[] cssSelectors,
			[Description("replace|replaceOuter|appendTo|afterElement|beforeElement|prependTo|insertAfter|insertBefore")]
			string elementPosition = "replace",
			bool overwriteIfExists = true, string? overwriteElement = null)
		{

			if (cssSelectors == null || cssSelectors.Length == 0)
			{
				return new ProgramError("You haven't defined any target element. Either remove the step to use the default target or define a target"
					, goalStep, function,
					FixSuggestion: $@"Here are examples of how to write step to target an element:
	- append content to #chatWindow
	- replace everyting in #main
	- set as outerText of #text
	- put content before #mydata starts
				");
			}
			var outputTarget = new OutputTarget(cssSelectors, elementPosition, overwriteIfExists, overwriteElement);

			memoryStack.Put(ReservedKeywords.OutputTarget, outputTarget);
			return null;
		}
		/*
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
		}*/


		[Description("Will generate html from user input. It might start with an element name")]
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
			indent += goalStep.Indent;
			AddToTree(content, indent);
		}

		[Description("status(primary|success|warning|danger), position(top-left|top-center|top-right|bottom-left|bottom-center|bottom-right), icon(UIkit icon e.g. icon: check|uri)")]
		public async Task ShowNotification(string message, string status = "primary", string position = "top-center",
				int timeout = 5000, string? id = null, string? icon = null, string? group = null)
		{
			if (icon != null)
			{
				if (icon.Contains("."))
				{
					message = @$"<span><img class=""notification_icon"" src=""{icon}""></span>" + message;
				}
				else
				{
					message = @$"<span uk-icon='{icon}'></span>" + message;
				}
			}
			message = EscapeTextForJavascript(message);
			await ExecuteJavascript(@$"plangUi.showNotification(""{message}"", {{status: '{status}', position: '{position}', timeout: {timeout}, group: '{group}', id:'{id}' }})");
		}

		public async Task CloseNotification(string? id = null, string? group = null)
		{
			await ExecuteJavascript(@$"plangUi.hideNoticiation('{id}', '{group}');");
		}

		private void AddToTree(object? obj, int indent)
		{
			if (obj == null) return;

			var root = context.GetOrDefault(ReservedKeywords.GoalTree, default(GoalTree<string>));
			if (root == null)
			{
				root = new GoalTree<string>("", -1);
				root.StepHash = goalStep.Hash;
				root.GoalHash = goal.Hash;
				context.AddOrReplace(ReservedKeywords.GoalTree, root);
			}

			var nodeToAddTo = GetNodeToAddByIndent(root.Current, indent);
			var newNode = new GoalTree<string>(obj.ToString(), indent);
			newNode.StepHash = goalStep.Hash; // goalStep.Hash;
			newNode.GoalHash = goal.Hash; //goal.Hash;
			nodeToAddTo.AddChild(newNode);
			root.Current = newNode;

		}

		private GoalTree<string> GetNodeToAddByIndent(GoalTree<string> node, int indent)
		{
			if (node.Indent < indent) return node;
			if (indent == 0) return context.GetOrDefault(ReservedKeywords.GoalTree, default(GoalTree<string>));
			if (node.Indent == indent) return node.Parent;

			return GetNodeToAddByIndent(node.Parent, indent);
		}

		private GoalTree<string>? GetNodeToAdd(GoalTree<string>? node)
		{
			if (node == null) return null;
			if (node.Value.Contains("{{ ChildElement")) return node;
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

