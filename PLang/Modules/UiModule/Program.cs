using AngleSharp.Dom;
using LightInject;
using Newtonsoft.Json;
using Org.BouncyCastle.Asn1;
using Org.BouncyCastle.Utilities.Zlib;
using PLang.Attributes;
using PLang.Errors;
using PLang.Errors.Runtime;
using PLang.Exceptions;
using PLang.Interfaces;
using PLang.Models;
using PLang.Resources;
using PLang.Runtime;
using PLang.Services.OutputStream;
using PLang.Utils;
using RazorEngineCore;
using Scriban;
using Scriban.Syntax;
using Sprache;
using System;
using System.ComponentModel;
using System.Dynamic;
using System.IO.Compression;
using System.Threading;
using static PLang.Modules.UiModule.Program;
using static System.Net.Mime.MediaTypeNames;

namespace PLang.Modules.UiModule
{
	public interface IFlush
	{
		Task Flush();
	}
	[Description("Takes any user command and tries to convert it to html. Add, remove, insert content to css selector. Set the (default) layout for the UI. Execute javascript.")]
	public class Program : BaseProgram, IFlush
	{
		private readonly IOutputStreamFactory outputStreamFactory;
		private readonly MemoryStack memoryStack;
		private string? clientTarget;
		public Program(IOutputStreamFactory outputStream, IPLangFileSystem fileSystem, MemoryStack memoryStack) : base()
		{
			this.outputStreamFactory = outputStream;
			this.fileSystem = fileSystem;
			this.memoryStack = memoryStack;

			clientTarget = memoryStack.Get("!target") as string;
		}
		public enum Type { css, js };
		[Description("Type=css|js")]
		public record UiFrameworkFile(Type Type, string Path);
		public record UiFramework(string Name = "default", List<string>? TargetDevices = null, List<UiFrameworkFile>? Types = null);
		public async Task SetFrameworks(UiFramework framework)
		{
			var variable = GetProgramModule<VariableModule.Program>();
			var storedFrameworks = await variable.GetSettings<List<UiFramework>>("UiFrameworks");

			if (storedFrameworks == null)
			{
				storedFrameworks = new List<UiFramework>();
				storedFrameworks.Add(framework);
			}
			else
			{
				var idx = storedFrameworks.FindIndex(p => p.Name.Equals(framework.Name, StringComparison.OrdinalIgnoreCase));
				if (idx != -1)
				{
					storedFrameworks[idx] = framework;
				}
				else
				{
					storedFrameworks.Add(framework);
				}
			}

			await variable.SetSettingValue("UiFrameworks", storedFrameworks);
			goal.AddVariable(framework);
		}

		[Description("Device=web|desktop|mobile|tablet|console|tv|watch|other")]
		public record LayoutOptions(string Name = "default", string DefaultRenderVariable = "main",
			[IsBuiltParameter("File")]
			string TemplateFile = "/ui/{name}/layout.html", string Device = "desktop");
		[Description("set the layout for the gui")]
		public async Task<(List<LayoutOptions>, IError?)> SetLayout(LayoutOptions options)
		{
			// read readme.md in /ui/template/{default}/readme.md, allowReadFromSystem = true
			// read /outputfile, "" if empty
			// ask llm, creata layout for me, %readme%
			// write to output file




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
			}
			else
			{
				layouts[idx] = options;
			}
			context.AddOrReplace("Layouts", layouts);
			return (layouts, null);
		}


		public enum DomMemberKind
		{
			Property,      // innerHTML, className, etc.
			Attribute,     // data-id, src, …
			Style,         // backgroundColor, width, …
			Event,          // click, change, …
		}

		[Description(@"Member should match case sensitive the javascript member attribute, e.g. innerHTML

Attribute: Member is the key in the SetAttribute js method
")]
		public record DomInstruction(string Selector, string Member, object? Value, DomMemberKind Kind = DomMemberKind.Property);
		public async Task<IError?> SetElement(List<DomInstruction> domInstructions)
		{
			var outputStream = outputStreamFactory.CreateHandler();
			await outputStream.Write(goalStep, domInstructions, "domInstruction");
			return null;

		}

		public record DomRemove(string Selector);
		[Description("Remove/delete an element by a css selector")]
		public async Task<IError?> RemoveElement(List<DomRemove> domRemoves)
		{
			var outputStream = outputStreamFactory.CreateHandler();
			await outputStream.Write(goalStep, domRemoves, "domRemove");
			return null;

		}

		public record JavascriptFunction(string MethodName, Dictionary<string, object> Parameters);
		public async Task<IError?> ExecuteJavascript(JavascriptFunction javascriptFunction)
		{
			var outputStream = outputStreamFactory.CreateHandler();
			await outputStream.Write(goalStep, javascriptFunction, "javascriptFunction");
			return null;
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
				name = "default";
			}

			return layouts.FirstOrDefault(p => p.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
		}


		public record Event(string EventType, string CssSelectorOrVariable, GoalToCallInfo GoalToCall);


		public record RenderTemplateOptions(string FileNameOrHtml, Dictionary<string, object?>? Parameters = null,
			string? CssSelector = null, string Action = "innerHTML", bool ReRender = true, string LayoutName = "default", bool RenderToOutputstream = false)
		{

			[LlmIgnore]
			public bool IsTemplateFile
			{
				get
				{
					if (FileNameOrHtml.Contains("\n") || FileNameOrHtml.Contains("\r") || FileNameOrHtml.Contains("\r")) return false;
					string ext = Path.GetExtension(FileNameOrHtml);
					return (!string.IsNullOrEmpty(ext) && ext.Length < 10);
				}

			}
		}

		private string GetCallbackPath()
		{
			string path = "/";
			if (HttpContext != null)
			{
				path = (HttpContext.Request.Path.Value ?? "/") + goalStep.Goal.GoalName + "_" + goalStep.Number;
			}
			return path;
		}

		[Description(@" Examples:
```plang
- render product.html => renderToOutputstream = true
- render frontpage.html, write to %html% => renderToOutputstream = false
- render product.html to #main => renderToOutputstream = true, ReRender=true, cssSelector=""#main""
- replace #main with template.html => cssSelector=#main, action=replace, ReRender=true, FileName=template.html, renderToOutputStream= true
- set html of #product to product.html => cssSelector=#product, action=innerHTML, ReRender=true, FileName=product.html, renderToOutputStream= true
- append to #list to item.html => cssSelector=#list, action=append, ReRender=true, FileName=item.html, renderToOutputStream= true

CssSelector can be null when not defined by user.
Action:innerHTML|innerText|append|prepend|replace|outerHTML|outerText
ReRender: default is true. normal behaviour is to re-render the content, like user browsing a website
When user doesn't write the return value into any variable, set it as renderToOutputstream=true, or when user defines it.
```")]
		public async Task<(object?, IError?)> RenderTemplate(RenderTemplateOptions options, List<Event>? events = null)
		{
			string html;
			if (options.IsTemplateFile)
			{
				var filePath = GetPath(options.FileNameOrHtml);
				if (!fileSystem.File.Exists(filePath))
				{
					string? similarFilesMessage = FileSuggestionHelper.BuildNotFoundMessage(fileSystem, filePath);
					return (null, new ProgramError($"Template file {options.FileNameOrHtml} not found at {filePath}", goalStep, StatusCode: 404,
						FixSuggestion: similarFilesMessage));
				}

				html = await fileSystem.File.ReadAllTextAsync(filePath);
			}
			else
			{
				html = options.FileNameOrHtml;
			}
			var url = (HttpContext?.Request.Path.Value ?? "/");
			if (options.Parameters == null) options = options with { Parameters = new() };
			if (!options.Parameters.ContainsKey("url"))
			{
				options.Parameters.Add("url", url);
			}
			if (!options.Parameters.ContainsKey("id"))
			{
				string path = GetCallbackPath();
				options.Parameters.AddOrReplace("id", Path.Join(path, goalStep.Goal.GoalName, goalStep.Number.ToString()).Replace("\\", "/"));
			}
			
			var templateEngine = GetProgramModule<TemplateEngineModule.Program>();
			(var content, var error) = await templateEngine.RenderContent(html, variables: options.Parameters);
			if (error != null) return (content, error);

			var outputStream = outputStreamFactory.CreateHandler();
			if (!outputStream.IsFlushed && !memoryStack.Get<bool>("request!IsAjax"))
			{
				var layoutOptions = GetLayoutOptions();

				if (layoutOptions != null)
				{
					var parameters = new Dictionary<string, object?>();
					parameters.Add(layoutOptions.DefaultRenderVariable, content);

					(content, error) = await templateEngine.RenderFile(layoutOptions.TemplateFile, parameters, options.RenderToOutputstream);

					if (error != null) return (content, error);

					return (content, null);
				}
			}

			if (options.Parameters == null)
			{
				options = options with { Parameters = new() };
			}
			options.Parameters.Add("reRender", options.ReRender);

			if (!string.IsNullOrEmpty(options.CssSelector))
			{
				options.Parameters.Add("cssSelector", options.CssSelector);
			}

			if (!string.IsNullOrEmpty(options.Action))
			{
				options.Parameters.Add("action", options.Action);
			}

			if (options.RenderToOutputstream || function.ReturnValues == null || function.ReturnValues?.Count == 0)
			{
				await outputStreamFactory.CreateHandler().Write(goalStep, content, "html", parameters: options.Parameters);
			}

			return (content, null);
		}
		public record Html(string Value, string? TargetElement = null);
		public async Task<(string?, IError?)> RenderImageToHtml(string path)
		{
			var param = new Dictionary<string, object?>();
			param.Add("path", path);
			var result = await Executor.RunGoal("/modules/ui/RenderFile", param);
			if (result.Error != null) return (null, result.Error);

			var html = result.Engine.GetMemoryStack().Get<string>("html");
			return (html, null);
		}

		public Task Flush()
		{
			throw new NotImplementedException();
		}
	}

}

