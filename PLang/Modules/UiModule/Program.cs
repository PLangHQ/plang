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
	[Description("Takes any user command and tries to convert it to html. Add, remove, insert content to css selector. Set the (default) layout for the UI")]
	public class Program : BaseProgram, IFlush
	{
		private readonly IOutputStreamFactory outputStreamFactory;
		private readonly IPLangFileSystem fileSystem;
		private readonly MemoryStack memoryStack;
		private string? clientTarget;
		public Program(IOutputStreamFactory outputStream, IPLangFileSystem fileSystem, MemoryStack memoryStack) : base()
		{
			this.outputStreamFactory = outputStream;
			this.fileSystem = fileSystem;
			this.memoryStack = memoryStack;

			clientTarget = memoryStack.Get("!target") as string;
		}

		public record LayoutOptions(bool IsDefault, string Name = "default", string DefaultTarget = "main",  string Description = "");
		[Description("set the layout for the gui")]
		public async Task<(List<LayoutOptions>, IError?)> SetLayout(LayoutOptions options)
		{
			context.TryGetValue("Layouts", out object? obj);
			var layouts = obj as List<LayoutOptions>;
			if (layouts == null)
			{
				layouts = new List<LayoutOptions>();
			}

			var idx = layouts.FindIndex(p => p.Name == options.Name);
			if (idx == -1)
			{
				layouts.Add(options);
			} else
			{
				layouts[idx] = options;
			}
			context.AddOrReplace("Layouts", layouts);
			return (layouts, null);
		}

		private LayoutOptions? GetLayoutOptions(string? name = null)
		{
			context.TryGetValue("Layouts", out object? obj);
			var layouts = obj as List<LayoutOptions>;
			if (layouts == null)
			{
				return null;				
			}
			if (string.IsNullOrEmpty(name))
			{
				return layouts.FirstOrDefault(p => p.IsDefault);
			}
			return layouts.FirstOrDefault(p => p.Name.Equals(name, StringComparison.OrdinalIgnoreCase));			
		}


		public record RenderTemplateOptions(string CssFramework, string Template, string Html, string? Target = null, string Layout = "default", bool RenderToOutputstream = false);
		[Description(@"template=table|form|button|card|etc..... target is a css selector in html. When user doesn't write the return value into any variable, set it as renderToOutputstream=true, or when user defines it. Examples:
```plang
- render form, with 'Name', 'Address', append to #main => Template: form 
- render table for %users%, 
	Header: Name, Age
	Rows: %name%, %age%
		=> Properties: { Key: Header, Value : [""Name"", ""Age""], Key: Rows, Value: [""name"", ""age""] }
```")]
		public async Task<(object?, IError?)> RenderTemplate(RenderTemplateOptions options)
		{
			var templateEngine = GetProgramModule<TemplateEngineModule.Program>();
			var outputStream = outputStreamFactory.CreateHandler();

			string html = options.Html;
			var filePath = GetPath(html);
			if (fileSystem.File.Exists(filePath))
			{
				html = await fileSystem.File.ReadAllTextAsync(filePath);
			}

			(var content, var error) = await templateEngine.RenderContent(html);
			if (error != null) return (content, error);

			options = options with { Html = html };

			if (!outputStream.IsFlushed && !memoryStack.Get<bool>("!request.IsAjax")) 
			{
				var layoutOptions = GetLayoutOptions();

				if (layoutOptions != null)
				{
					var parameters = new Dictionary<string, object?>();
					parameters.Add((clientTarget ?? options.Target ?? layoutOptions.DefaultTarget).TrimStart('#'), content);

					(content, error) = await templateEngine.RenderFile(layoutOptions.Name, parameters, options.RenderToOutputstream);

					if (error != null) return (content, error);

					return (content, null);
				}
			}

			if (options.RenderToOutputstream)
			{
				await outputStreamFactory.CreateHandler().Write(options);
			}

			return (options, null);
		}

		public async Task<(string?, IError?)> RenderImageToHtml(string path)
		{
			var param = new Dictionary<string, object?>();
			param.Add("path", path);
			var result = await Executor.RunGoal("/modules/ui/RenderFile", param);
			if (result.Error != null) return (null, result.Error);

			var html = result.Engine.GetMemoryStack().Get<string>("html");
			return (html, null);
		}

		private string EscapeTextForJavascript(string content)
		{
			return string.IsNullOrEmpty(content) ? "''" : JsonConvert.ToString(content); ;
		}

		public async Task SetInputValue(string cssSelector, string value = "")
		{
			string escapedHtmlContent = EscapeTextForJavascript(value);
			await ExecuteJavascript($"updateContent({escapedHtmlContent}, '{cssSelector}')");
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

			memoryStack.Put(ReservedKeywords.OutputTarget, outputTarget, goalStep: goalStep);
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

			var indent = goal.GetVariable<int?>(ReservedKeywords.ParentGoalIndent) ?? 0;
			indent += goalStep.Indent;
			AddToTree(content, indent);
		}

		public record Notification(string message,
			[Description("status(primary|success|warning|danger)")]
			string? status = null, 
			[Description("position(top-left|top-center|top-right|bottom-left|bottom-center|bottom-right)")]
			string? position = null, int timeout = 10*1000, string? id = null,
			[Description("icon(UIkit icon e.g. icon: check|uri)")]
			string? icon = null, string? group = null);
		public async Task ShowNotification(Notification notification)
		{
			await outputStreamFactory.CreateHandler().Write(notification);
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
				expandoObject.Add(kvp.Name, kvp.Value);

				var sv = ScriptVariable.Create(kvp.Name, ScriptVariableScope.Global);
				templateContext.SetValue(sv, kvp.Value);
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

