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
	[Description("Takes any user command and tries to convert it to html. Add, remove, insert content to css selector. Set the (default) layout for the UI")]
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

		[Description("Member should match case sensitive the javascript member attribute, e.g. innerHTML")]
		public record DomInstruction(string Selector, string Member, object? Value, DomMemberKind Kind = DomMemberKind.Property);
		public async Task<IError?> SetElement(List<DomInstruction> domInstructions)
		{
			var outputStream = outputStreamFactory.CreateHandler();
			await outputStream.Write(domInstructions);
			return null;

		}

		public record DomRemove(string Selector);
		[Description("Remove/delete an element by a css selector")]
		public async Task<IError?> RemoveElement(List<DomRemove> domRemoves)
		{
			var outputStream = outputStreamFactory.CreateHandler();
			await outputStream.Write(domRemoves);
			return null;

		}

		public record JavascriptFunction(string MethodName, Dictionary<string, object> Parameters);
		public async Task<IError?> ExecuteJavascript(JavascriptFunction javascriptFunction)
		{
			var outputStream = outputStreamFactory.CreateHandler();
			await outputStream.Write(javascriptFunction);
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
		public record RenderTemplateOptions(string FileName, Dictionary<string, object?>? Parameters = null, string? Target = null, string LayoutName = "default", bool RenderToOutputstream = false);
		[Description(@"When user doesn't write the return value into any variable, set it as renderToOutputstream=true, or when user defines it. Examples:
```plang
- render product.html => renderToOutputstream = true
- render frontpage.html, write to %html% => renderToOutputstream = false
```")]
		public async Task<(object?, IError?)> RenderTemplate(RenderTemplateOptions options, List<Event>? events = null)
		{
			var filePath = GetPath(options.FileName);
			if (!fileSystem.File.Exists(filePath))
			{
				return (null, new ProgramError($"Template file {options.FileName} not found", goalStep, StatusCode: 404));
			}

			var html = await fileSystem.File.ReadAllTextAsync(filePath);

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

			if (options.RenderToOutputstream)
			{

				await outputStreamFactory.CreateHandler().Write(content);
			}

			return (options, null);
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

