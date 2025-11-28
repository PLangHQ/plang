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
using PLang.Services.OutputStream.Messages;
using PLang.Services.OutputStream.Sinks;
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
using System.Threading.Channels;
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
		public Program() : base()
		{

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




			appContext.TryGetValue("Layouts", out object? obj);
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
			appContext.AddOrReplace("Layouts", layouts);
			return (layouts, null);
		}


		public record DialogCommand(bool CloseAll = false, string Type = "dialogCommand", string Actor = "user", string Channel = "default");
		public async Task<IError?> CloseWindow(DialogCommand dialogCommand)
		{
			var executeMessage = new ExecuteMessage("dialogCommand", dialogCommand, Channel: dialogCommand.Channel, Actor: dialogCommand.Actor, Properties: new Dictionary<string, object?> { ["step"] = goalStep });

			var sink = context.GetSink(dialogCommand.Actor);
			return await sink.SendAsync(executeMessage);
		}
		public enum UiFacet
		{
			Property,      // innerHTML, className, etc.
			Attribute,     // data-id, src, …
			Style,         // backgroundColor, width, …
			Event,          // click, change, …
		}

		[Description(@"Member is the action that should be executed. Available actions are 'replace, replaceSelf, append, prepend, appendOrReplace, prependOrReplace, scrollToTop, scrollIntoView, focus, highlight, show, hide, showDialog, hideDialog, showModal, hideModal, notify, alert, vibrate, navigate, replaceState, reload, open, close'
User can define a custom action.
Attribute: Member is the key in the SetAttribute js method, make sure to convert to valid js name, e.g. Member class must be className
")]
		public record UiInstruction(string Selector, string Member, object? Value, UiFacet Kind = UiFacet.Property);
		public async Task<IError?> SetElement(List<UiInstruction> uiInstructions, string actor = "user", string channel = "default")
		{

			Dictionary<string, object> param = new();
			param.Add("instructions", uiInstructions);

			var executeMessage = new ExecuteMessage("uiInstruction", param, Channel: channel, Actor: actor, Properties: new Dictionary<string, object?> { ["step"] = goalStep });

			var sink = context.GetSink(actor);
			return await sink.SendAsync(executeMessage);
		}

		public record UiRemove(string Selector);
		[Description("Remove/delete an element by a css selector")]
		public async Task<IError?> RemoveElement(List<UiRemove> domRemoves, string actor = "user", string channel = "default")
		{
			Dictionary<string, object> param = new();
			param.Add("instructions", domRemoves);

			var executeMessage = new ExecuteMessage("uiRemove", param, Channel: channel, Actor: actor, Properties: new Dictionary<string, object?> { ["step"] = goalStep });

			var sink = context.GetSink(actor);
			return await sink.SendAsync(executeMessage);

		}

		public async Task<IError?> ExecuteJavascript(ExecuteMessage executeMessage)
		{
			var sink = context.GetSink(executeMessage.Actor);
			return await sink.SendAsync(executeMessage);
		}

		private LayoutOptions? GetLayoutOptions(string? name = null)
		{
			appContext.TryGetValue("Layouts", out object? obj);
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

		[Description("Target defines where in the UI to write the content, e.g. a cssSelector when ui is html")]
		public async Task SetTargetArea(string? target)
		{
			context.UiOutputProperties.Target = target;
		}

		public record Event(string EventType, string CssSelectorOrVariable, GoalToCallInfo GoalToCall);


		public record RenderTemplateOptions(RenderMessage RenderMessage, bool ReRender = true, string LayoutName = "default", 
			bool RenderToOutputstream = false, bool DontRenderMainLayout = false,
			[property: Description("set as true when RenderMessage.Content looks like a fileName, e.g. %fileName%, %template%, etc. If Content is clearly a text, set as false")]
			bool? IsTemplateFile = null)
		{

			[LlmIgnore]
			public bool GuessIfTemplateFile
			{
				get
				{
					return PathHelper.IsTemplateFile(RenderMessage.Content);
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
- render product.html => isTemplateFile=true, renderToOutputstream = true
- render frontpage.html, write to %html% => isTemplateFile=true, renderToOutputstream = false
- render ""Is this correct file content.html"" => isTemplateFile = false, renderToOutputstream = true
- render product.html to #main => renderToOutputstream = true, ReRender=true, Target=""#main""
- replace #main with template.html => Target=#main, actions=[""replace""], ReRender=true, FileName=template.html, renderToOutputStream= true
- set html of #product to product.html => Target=#product, actions=[""replace""], ReRender=true, FileName=product.html, renderToOutputStream= true
- append to #list to item.html, scroll to view => Target=#list, actions=[""replace"", ""scrollIntoView""], ReRender=true, FileName=item.html, renderToOutputStream= true

Target can be null when not defined by user.
Actions: list of action to preform, the default is 'replace'(innerHTML).
ReRender: default is true. normal behaviour is to re-render the content, like user browsing a website
When user doesn't write the return value into any variable, set it as renderToOutputstream=true, or when user defines it.
IsTemplateFile: set as true when RenderMessage.Content looks like a fileName, e.g. %fileName%, %template%, etc. If Content is clearly a text, set as false
```")]
		public async Task<(object?, IError?)> RenderTemplate(RenderTemplateOptions options)
		{
			string html;
			if (options.IsTemplateFile == true || (options.IsTemplateFile == null && options.GuessIfTemplateFile))
			{
				var filePath = GetPath(options.RenderMessage.Content);
				if (!fileSystem.File.Exists(filePath))
				{
					string? similarFilesMessage = FileSuggestionHelper.BuildNotFoundMessage(fileSystem, filePath);
					return (null, new ProgramError($"Template file {options.RenderMessage.Content} not found at {filePath}", goalStep, StatusCode: 404,
						FixSuggestion: similarFilesMessage));
				}

				html = await fileSystem.File.ReadAllTextAsync(filePath);
			}
			else
			{
				html = options.RenderMessage.Content;
			}
			var url = (HttpContext?.Request.Path.Value ?? "/");
			Dictionary<string, object?> Parameters = new();

			if (!Parameters.ContainsKey("url"))
			{
				Parameters.Add("url", url);
			}
			if (!Parameters.ContainsKey("id"))
			{
				string path = GetCallbackPath();
				Parameters.AddOrReplace("id", Path.Join(path, goalStep.Goal.GoalName, goalStep.Number.ToString()).Replace("\\", "/"));
			}
			
			var templateEngine = GetProgramModule<TemplateEngineModule.Program>();
			(var content, var error) = await templateEngine.RenderContent(html, variables: Parameters);
			if (error != null) return (content, error);

			var sink = context.GetSink(options.RenderMessage.Actor);
			if (sink is HttpSink hs && !hs.IsFlushed && !memoryStack.Get<bool>("request!IsAjax") && !options.DontRenderMainLayout)
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

			var rm = options.RenderMessage with { Content = content };
			options = options with {  RenderMessage = rm };	

			Parameters.Add("reRender", options.ReRender);
			
			if (options.RenderToOutputstream || function.ReturnValues == null || function.ReturnValues?.Count == 0)
			{				
				error = await sink.SendAsync(options.RenderMessage);
				return (null, error);
			}

			return (content, null);
		}
		public record Html(string Value, string? TargetElement = null);
		public async Task<(object?, IError?)> RenderImageToHtml(string path)
		{
			var param = new Dictionary<string, object?>();
			param.Add("path", path);

			var goalToCall = new GoalToCallInfo("/modules/ui/RenderFile", param);

			var result = await engine.RunGoal(goalToCall, goal, context);
			if (result.Error != null) return (null, result.Error);

			return (result.Variables, null);
		}

		public Task Flush()
		{
			throw new NotImplementedException();
		}
	}

}

