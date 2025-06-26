using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PLang.Building.Model;
using PLang.Errors;
using PLang.Errors.Runtime;
using PLang.Interfaces;
using PLang.Models;
using PLang.Runtime;
using PLang.Services.OutputStream;
using PLang.Utils;
using ReverseMarkdown.Converters;
using Scriban;
using Scriban.Runtime;
using Scriban.Syntax;
using System.Collections;
using System.ComponentModel;
using System.Dynamic;
using System.Text;
using System.Text.RegularExpressions;
using System.Text.RegularExpressions;
using static PLang.Modules.BaseBuilder;
using static PLang.Modules.BaseBuilder;

namespace PLang.Modules.TemplateEngineModule
{
	[Description("Render html (files) using template engine")]
	public class Program : BaseProgram
	{
		private readonly IPLangFileSystem fileSystem;
		private readonly IOutputStreamFactory outputStreamFactory;

		public Program(IPLangFileSystem fileSystem, IOutputStreamFactory outputStreamFactory)
		{
			this.fileSystem = fileSystem;
			this.outputStreamFactory = outputStreamFactory;
		}

		[Description("Render a file path either into a write into value or straight to the output stream when no return variable is defined. Set writeToOutputStream=true when no variable is defined to write into")]
		public async Task<(string?, IError?)> RenderFile(string path, Dictionary<string, object?>? variables = null, bool writeToOutputStream = false)
		{
			var fullPath = GetPath(path);
			if (!fileSystem.File.Exists(fullPath))
			{
				return (null, new ProgramError($"File {path} could not be found. Full path to the file is {fullPath}", goalStep, this.function));
			}
			string content = fileSystem.File.ReadAllText(fullPath);
			var result = await RenderContent(content, fullPath);

			if (result.Error != null) return (result.Result, result.Error);

			if (!writeToOutputStream) return result;

			if (outputStreamFactory != null && (function.ReturnValues == null || function.ReturnValues.Count == 0))
			{
				await outputStreamFactory.CreateHandler().Write(result.Result);
			}

			return result;
		}

		public async Task<(string? Result, IError? Error)> RenderContent(string content, string fullPath, Dictionary<string, object?>? variables = null)
		{

			var templateContext = new TemplateContext();
			templateContext.MemberRenamer = member => member.Name;



			if (memoryStack != null)
			{
				foreach (var kvp in memoryStack.GetMemoryStack())
				{
					var sv = ScriptVariable.Create(kvp.Name, ScriptVariableScope.Global);
					templateContext.SetValue(sv, kvp.Value);
				}
			}
			if (variables != null)
			{
				foreach (var kvp in variables)
				{
					var sv = ScriptVariable.Create(kvp.Key, ScriptVariableScope.Global);
					templateContext.SetValue(sv, kvp.Value);
				}
			}

			SetFunctionsOnTemplate(templateContext);




			try
			{
				var parsed = Template.Parse(content);
				var result = await parsed.RenderAsync(templateContext);

				return (result, null);
			}
			catch (ScriptRuntimeException ex)
			{
				var relativeFilePath = fullPath.AdjustPathToOs().Replace(fileSystem.RootDirectory, "");
				var innerException = ex.InnerException as ScriptRuntimeException ?? ex;
				string message;
				string pattern = @"\((\d+),(\d+)\)";
				Match match = Regex.Match(innerException.Message, pattern);
				if (match.Success)
				{
					int.TryParse(match.Groups[1].Value?.Trim(), out int lineNumber);
					int.TryParse(match.Groups[2].Value?.Trim(), out int columnNumber);

					message = $"{innerException.OriginalMessage} in {relativeFilePath} - line: {lineNumber} | column: {columnNumber}";
					var lines = content.Split('\n');
					if (lines.Length > lineNumber)
					{
						var startPos = lineNumber - 3;
						if (startPos < 0) startPos = 0;

						var errorLines = lines.ToList().Skip(startPos).Take(lineNumber - startPos + 2);
						foreach (var errorLine in errorLines)
						{
							message += $"\n{startPos++}: {errorLine}";
						}
					}

				}
				else
				{
					message = $"{ex.Message} in {relativeFilePath}";
				}
				var pe = new ProgramError(message,
						goalStep, function, Exception: ex,
						HelpfulLinks: @"Description of the language syntax: https://github.com/scriban/scriban/blob/master/doc/language.md
Built-in functions: https://github.com/scriban/scriban/blob/master/doc/builtins.md
Runtime documentation: https://github.com/scriban/scriban/blob/master/doc/runtime.md"
						);

				return (null, pe);

			}
		}

		private bool ContainsVariable(string key, TemplateContext templateContext)
		{
			var sv = ScriptVariable.Create(key, ScriptVariableScope.Global);
			return (templateContext.GetValue(sv) != null);
		}

		private void SetFunctionsOnTemplate(TemplateContext templateContext)
		{
			var scriptObject = new ScriptObject(StringComparer.OrdinalIgnoreCase);

			if (!ContainsVariable("date_format", templateContext))
			{
				scriptObject.Import("date_format", new Func<object, string, string>((input, format) =>
				{
					if (input is DateTime dateTime)
					{
						return dateTime.ToString(format);
					}
					else if (input is string str && DateTime.TryParse(str, out var parsedDate))
					{
						return parsedDate.ToString(format);
					}
					return input?.ToString() ?? string.Empty;
				}));
			}
			if (!ContainsVariable("json", templateContext))
			{
				scriptObject.Import("json", new Func<object, bool, string>((input, indent) =>
				{
					return JsonConvert.SerializeObject(input, (indent) ? Formatting.Indented : Formatting.None);
				}));
			}
			if (!ContainsVariable("md", templateContext))
			{
				scriptObject.Import("md", new Func<string, Task<string?>>(async (input) =>
				{
					var convert = GetProgramModule<ConvertModule.Program>();
					var md = await convert.ConvertToMd(input);
					return md;

				}));
			}

			if (!ContainsVariable("goalToCall", templateContext))
			{
				scriptObject.Import("goalToCall", new Func<object, string, Task<object?>>(async (data, goalName) =>
				{
					var parameters = new Dictionary<string, object?>();
					parameters.Add("data", data);
					parameters.Add(ReservedKeywords.Goal, goal);
					parameters.Add(ReservedKeywords.Step, goalStep);
					parameters.Add(ReservedKeywords.Instruction, goalStep.Instruction);

					var caller = GetProgramModule<CallGoalModule.Program>();
					var result = await caller.RunGoal(new Models.GoalToCallInfo(goalName, parameters));
					if (result.Error != null) return result.Error.ToString();

					if (result.Return is IList<ObjectValue> list)
					{
						if (list.Count == 0) return data;
						if (list.Count == 1) return list[0].Value;

						return GetScriptObject(list);

					} else if (result.Return is ObjectValue ov)
					{
						return ov.Value;
					}

					return result.Return ?? data;

				}));
			}

			if (!ContainsVariable("appToCall", templateContext))
			{
				scriptObject.Import("appToCall", new Func<object, string, Task<object?>>(async (data, appName) =>
				{
					var parameters = new Dictionary<string, object?>();
					parameters.Add("data", data);
					parameters.Add(ReservedKeywords.Goal, goal);
					parameters.Add(ReservedKeywords.Step, goalStep);
					parameters.Add(ReservedKeywords.Instruction, goalStep.Instruction);

					var caller = GetProgramModule<CallGoalModule.Program>();
					(appName, var goalName, var error) = AppToCallInfo.GetAppAndGoalName(appName);
					if (error != null) return error.ToString();

					var result = await caller.RunApp(new Models.AppToCallInfo(appName, goalName, parameters));
					if (result.Error != null) return result.Error.ToString();

					if (result.Variables is IList<ObjectValue> list)
					{
						if (list.Count == 0) return data;
						if (list.Count == 1) return list[0].Value;

						if (list.Count == 0) return data;
						if (list.Count == 1) return list[0].Value;

						return GetScriptObject(list);
						
					}
					else if (result.Variables is ObjectValue ov)
					{
						return ov.Value;
					}

					return result.Variables ?? data;

				}));
			}

			if (!ContainsVariable("render", templateContext))
			{
				scriptObject.Import("render", new Func<string, Task<string>>(async (path) =>
				{
					var result = await RenderFile(path, writeToOutputStream: false);
					if (result.Item2 != null)
					{
						throw new ExceptionWrapper(result.Item2);
					}
					return result.Item1;
				}));
			}

			// Push the custom function into the template context
			templateContext.PushGlobal(scriptObject);
		}

		private ScriptObject? GetScriptObject(IList<ObjectValue> list)
		{
			var scriptObj = new Scriban.Runtime.ScriptObject();
			foreach (var item in list)
			{
				object? value = item.Value;
				if (value is JValue v)
				{
					value = v.ToString();
				}
				scriptObj[item.Name] = value;
			}
			return scriptObj;
		}
	}
}
