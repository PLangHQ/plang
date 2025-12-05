using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PLang.Building.Model;
using PLang.Errors;
using PLang.Errors.Runtime;
using PLang.Interfaces;
using PLang.Models;
using PLang.Runtime;
using PLang.Services.OutputStream;
using PLang.Services.OutputStream.Messages;
using PLang.Utils;
using ReverseMarkdown.Converters;
using Scriban;
using Scriban.Runtime;
using Scriban.Syntax;
using System.Collections;
using System.ComponentModel;
using System.Dynamic;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Text.RegularExpressions;
using static PLang.Modules.BaseBuilder;
using static PLang.Modules.BaseBuilder;
using static PLang.Utils.VariableHelper;

namespace PLang.Modules.TemplateEngineModule
{
	[Description(@"Render template html, files, elements using template engine. plang examples: 
```
- render file.html
- render %content% to #main / will render the variable into the element #main
- render products.html, %products%, write to %result%
```")]
	public class Program : BaseProgram
	{

		public Program(IPLangFileSystem fileSystem, IMemoryStackAccessor memoryStackAccessor)
		{
			base.fileSystem = fileSystem;
			base.memoryStack = memoryStackAccessor.Current;
		}

		static IReadOnlyDictionary<string, HashSet<string>> GetMembers(Template template)
		{
			var collector = new MemberCollector();
			collector.Visit(template.Page);
			return collector.Members;
		}

		sealed class MemberCollector : ScriptVisitor
		{
			public Dictionary<string, HashSet<string>> Members { get; } =
				new(StringComparer.OrdinalIgnoreCase);

			public override void Visit(ScriptMemberExpression n)
			{
				if (n.Target is ScriptVariableGlobal g && n.Member is ScriptVariableGlobal m)
				{
					Members.TryAdd(g.Name, new HashSet<string>(StringComparer.OrdinalIgnoreCase));
					Members[g.Name].Add(m.Name);
				}
				base.Visit(n);
			}
		}

		[Description("Render a file path either into a write into value or straight to the output stream when no return variable is defined. Set writeToOutputStream=true when no variable is defined to write into")]
		public async Task<(string?, IError?)> RenderFile(string path, Dictionary<string, object?>? variables = null, bool writeToOutputStream = false)
		{
			var fullPath = GetPath(path);
			if (!fileSystem.File.Exists(fullPath))
			{
				
				string? similarFilesMessage = FileSuggestionHelper.BuildNotFoundMessage(fileSystem, fullPath);
				

				return (null, new ProgramError($"File {path} could not be found. Full path to the file is {fullPath}"
					, goalStep, this.function, StatusCode: 404,
					FixSuggestion: similarFilesMessage));
			}
			string content = fileSystem.File.ReadAllText(fullPath);
			var result = await RenderContent(content, fullPath, variables);

			if (result.Error != null) return (result.Result, result.Error);

			if (!writeToOutputStream) return result;

			if (writeToOutputStream || (function?.ReturnValues == null || function?.ReturnValues.Count == 0))
			{
				var renderMessage = new RenderMessage(result.Result, Properties: new Dictionary<string, object?> { ["step"] = goalStep });
				await context.UserSink.SendAsync(renderMessage);
			}

			return result;
		}

		public async Task<(string? Result, IError? Error)> RenderContent(string content, string? fullPath = null, Dictionary<string, object?>? variables = null)
		{

			var templateContext = new TemplateContext();
			templateContext.EnableNullIndexer = true;
			templateContext.EnableRelaxedMemberAccess = true;
			templateContext.MemberRenamer = member => member.Name;

			if (variables != null)
			{
				foreach (var kvp in variables)
				{
					AddVariable(kvp.Key, kvp.Value, templateContext);
				}
			}

			if (memoryStack != null)
			{
				foreach (var kvp in memoryStack.GetMemoryStack())
				{
					AddVariable(kvp.Name, kvp.Value, templateContext);
				}
			}
			if (goalStep != null)
			{
				var vars = goalStep.GetVariables();
				foreach (var variable in vars)
				{
					AddVariable(variable.VariableName, variable.Value, templateContext);
				}
			}
			if (goal != null)
			{
				var vars = goal.GetVariables();
				foreach (var variable in vars)
				{
					AddVariable(variable.VariableName, variable.Value, templateContext);
				}
			}


			SetFunctionsOnTemplate(templateContext);

			templateContext.PushGlobal(globals);

			try
			{
				var parsed = Template.Parse(content);
				//var members = GetMembers(parsed);

				var result = await parsed.RenderAsync(templateContext);

				return (result, null);
			}
			catch (Exception ex) when (ex is ScriptRuntimeException || ex is InvalidOperationException)
			{

				var relativeFilePath = (string.IsNullOrEmpty(fullPath)) ? "" : fullPath.AdjustPathToOs().Replace(fileSystem.RootDirectory, "");
				var innerException = ex.InnerException ?? ex;
				var exMessage = ex.Message;
				var originalMessage = "";
				if (innerException is ScriptRuntimeException sre)
				{
					exMessage = sre.Message;
					originalMessage = sre.OriginalMessage;
				}
				else
				{
					originalMessage = ex.Message;
				}
				string message;
				string pattern = @"\((\d+),(\d+)\)";
				Match match = Regex.Match(exMessage, pattern);
				if (match.Success)
				{
					int.TryParse(match.Groups[1].Value?.Trim(), out int lineNumber);
					int.TryParse(match.Groups[2].Value?.Trim(), out int columnNumber);

					message = $"{originalMessage} in {relativeFilePath} - line: {lineNumber} | column: {columnNumber}";
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
		ScriptObject globals = new ScriptObject(StringComparer.OrdinalIgnoreCase);
		private void AddVariable(string key, object value, TemplateContext templateContext)
		{

			if (value is ObjectValue ov)
			{
				value = ov.Value;
			}

			if (key.StartsWith("!"))
			{
				if (!globals.ContainsKey(key))
				{
					globals[key] = value;
				}

			}
			else
			{
				(var sv, var exists) = ContainsVariable(key, templateContext);
				if (exists) return;

				templateContext.SetValue(sv, value);
			}


		}

		private (ScriptVariable?, bool) ContainsVariable(string key, TemplateContext templateContext)
		{
			var sv = ScriptVariable.Create(key, ScriptVariableScope.Global);
			return (sv, templateContext.GetValue(sv) != null);
		}

		private void SetFunctionsOnTemplate(TemplateContext templateContext)
		{
			templateContext.LoopLimit = 50000;
			(_, var exists) = ContainsVariable("date_format", templateContext);
			if (!exists)
			{
				globals.Import("date_format", new Func<object, string, string>((input, format) =>
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

			(_, exists) = ContainsVariable("json", templateContext);
			if (!exists)
			{
				globals.Import("json", new Func<object, bool, string>((input, indent) =>
				{
					return JsonConvert.SerializeObject(input, (indent) ? Formatting.Indented : Formatting.None);
				}));
			}

			(_, exists) = ContainsVariable("md", templateContext);
			if (!exists)
			{
				globals.Import("md", new Func<string, Task<string?>>(async (input) =>
				{
					var convert = GetProgramModule<ConvertModule.Program>();
					var md = await convert.ConvertToMd(input);
					return md;

				}));
			}

			(_, exists) = ContainsVariable("callGoal", templateContext);
			if (!exists)
			{
				globals.Import("callGoal", new Func<TemplateContext, string, object[]?, Task<object?>>(async (context, goalName, data) =>
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
						if (list.Count == 0) return null;
						if (list.Count == 1) return list[0].Value;

						return GetScriptObject(list); 

					}
					else if (result.Return is ObjectValue ov)
					{
						return ov.Value;
					}

					return result.Return;

				}));
			}


			(_, exists) = ContainsVariable("callApp", templateContext);
			if (!exists)
			{
				globals.Import("callApp", new Func<object, string, Task<object?>>(async (data, appName) =>
				{
					var parameters = new Dictionary<string, object?>();
					parameters.Add("data", data);
					parameters.Add(ReservedKeywords.Goal, goal);
					parameters.Add(ReservedKeywords.Step, goalStep);
					parameters.Add(ReservedKeywords.Instruction, goalStep.Instruction);

					var caller = GetProgramModule<AppModule.Program>();
					(appName, var goalName, var error) = AppToCallInfo.GetAppAndGoalName(appName);
					if (error != null) return error.ToString();

					var result = await caller.RunApp(new Models.AppToCallInfo(appName, goalName, parameters));
					if (result.Error != null) return result.Error.ToString();

					if (result.Variables is IList<ObjectValue> list)
					{
						if (list.Count == 0) return data;
						if (list.Count == 1) return list[0].Value;

						return GetScriptObject(list);

					}
					else if (result.Variables is ObjectValue ov)
					{
						return ov.Value;
					}

					return result.Variables;

				}));
			}

			(_, exists) = ContainsVariable("render", templateContext);
			if (!exists)
			{
				globals.Import("render", new Func<TemplateContext, string, object[]?, Task<string>>(async (context, path, vars) =>
				{
					var modelDict = new Dictionary<string, object?>();

					foreach (var item in memoryStack.GetMemoryStack())
					{
						modelDict.AddOrReplace(item.Name, item.Value);
					}

					var arguments = ((ScriptFunctionCall)context.CurrentNode).Arguments.Skip(1);
					foreach (var argument in arguments)
					{
						if (argument is ScriptVariableGlobal svg)
						{

							var value = svg.Evaluate(context);
							modelDict.AddOrReplace(svg.Name, value);
						} else if (argument is ScriptNamedArgument sna)
						{
							var value = context.Evaluate(sna.Value);
							modelDict.AddOrReplace(sna.Name.ToString(), value);
						}
							
					}

					var forLoop = context.GetValue(new ScriptVariableGlobal("for"));
					if (forLoop != null)
					{
						modelDict.AddOrReplace("for", forLoop);
					}
					/*
					if (vars != null && vars.Length > 0)
					{
						foreach (var variable in vars)
						{
							if (variable is ScriptObject so)
							{
								foreach (var key in so.Keys)
								{
									modelDict.AddOrReplace(key, so[key]);
								}
							} else
							{
								modelDict.AddOrReplace("model", variable);
							}
						}

					}*/
					var result = await RenderFile(path, modelDict, writeToOutputStream: false);
					if (result.Item2 != null)
					{
						throw new ExceptionWrapper(result.Item2);
					}
					return result.Item1;
				}));
			}
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
