using Microsoft.Extensions.Logging;
using PLang.Building.Model;
using PLang.Building.Parsers;
using PLang.Errors.Builder;
using PLang.Interfaces;
using PLang.Runtime;
using PLang.Services.CompilerService;
using PLang.Utils.Extractors;
using System.Text.RegularExpressions;

namespace PLang.Modules.ConditionalModule
{
	public class Builder : BaseBuilder
	{
		private readonly IPLangFileSystem fileSystem;
		private readonly PrParser prParser;
		private readonly MemoryStack memoryStack;
		private readonly ILogger logger;

		public Builder(IPLangFileSystem fileSystem, PrParser prParser, ILogger logger) : base()
		{
			this.fileSystem = fileSystem;
			this.prParser = prParser;
			this.logger = logger;
		}

		public override async Task<(Instruction?, IBuilderError?)> Build(GoalStep step, IBuilderError? previousBuildError = null)
		{
			if (step.NextStep?.Indent > step.Indent)
			{
				AppendToSystemCommand(@"This if statement has nested block.");
			}
			if (step.Text.Contains("end goal") || step.Text.Contains("throw"))
			{
				AppendToSystemCommand(@"The body of the condition may be an instruction to end the goal or to throw.
Map it onto throwErrorOnTrue (or throwErrorOnFalse when the body belongs to the else side).
NEVER return the function with every parameter null: a condition whose body does something must
carry either goalToCallIfTrue/goalToCallIfFalse or throwErrorOnTrue/throwErrorOnFalse.

`- if %imprint% is empty then end goal` =>
    IsEmpty(item: ""%imprint%"", throwErrorOnTrue: { ""Message"": ""End goal"", ""Type"": ""EndGoal"", ""StatusCode"": 0, ""Key"": ""EndGoal"" })

`- if %book.imprintId% == %imprint.vsImprintId% then end goal` =>
    Equals(item1: ""%book.imprintId%"", item2: ""%imprint.vsImprintId%"", throwErrorOnTrue: { ""Message"": ""End goal"", ""Type"": ""EndGoal"", ""StatusCode"": 0, ""Key"": ""EndGoal"" })

`- if %a% is not empty and %b% == %c% then end goal` is still ONE condition; combine it in the
condition itself and keep the same throwErrorOnTrue shape.

`- if %x% is not empty then throw ""Not found"", 404` =>
    IsNotEmpty(item: ""%x%"", throwErrorOnTrue: { ""Message"": ""Not found"", ""Type"": ""Error"", ""StatusCode"": 404 })
");
			}
			if (Regex.IsMatch(Regex.Replace(step.Text, "\"[^\"]*\"|'[^']*'", "\"\""), @"\bcall\b", RegexOptions.IgnoreCase))
			{
				AppendToSystemCommand(@"This condition calls a goal. Whatever method you choose (IsEmpty, IsNotEmpty, ContainsString, IsEqual, SimpleCondition, ...),
ALWAYS set goalToCallIfTrue to the goal named after `then call` / `then` / `call`, also when the whole condition is written on ONE line,
and goalToCallIfFalse to the goal named after `else`. ALWAYS keep the condition itself (the variable or value that is checked); a function with only goals is never correct.

`- if %name% is empty then call goal AskName` => IsEmpty(item: ""%name%"", goalToCallIfTrue: { ""Name"": ""AskName"" })
`- if %x% == 5 then call goal Five, else call goal Other` => IsEqual(item1: ""%x%"", item2: ""5"", goalToCallIfTrue: { ""Name"": ""Five"" }, goalToCallIfFalse: { ""Name"": ""Other"" })
");
			}
			if (step.Text.Contains("%!"))
			{
				AppendToSystemCommand(@"variables starting with ! are global or app scoped variable, such a %!error%, %!goal%, etc. 
statments like `if %!error.Handled% does NOT mean it is false
");
			}

			return await Build(step, previousBuildError, null);
		}

		// previousBuildError goes on to the llm request: without it a retry is answered from the goal's batched answers
		// (or the llm cache) with the same answer that failed validation.
		private async Task<(Instruction?, IBuilderError?)> Build(GoalStep step, IBuilderError? previousBuildError, CompilerError? error = null, int errorCount = 0)
		{

			var result = await PrepareStep(step, previousBuildError);
			if (result.Error != null) return result;

			var gf = result.Instruction.Function as GenericFunction;
			if (!ShouldMakeCode(gf.Name)) return result;

			return await MakeCode(step, error, errorCount);

		}

		// A condition whose step calls a goal must carry that goal. goalToCallIfTrue and goalToCallIfFalse are
		// optional parameters, so an answer without them passes every other check and builds a condition that
		// is evaluated and then does nothing.
		public async Task<(Instruction, IBuilderError?)> BuilderValidate(GoalStep step, Instruction instruction, IGenericFunction gf)
		{
			var message = UnnamedGoalToCall(step.Text, gf) ?? StrayNegation(step.Text, gf);
			if (message != null) return (instruction, new StepBuilderError(message, step));

			message = MissingCondition(step.Text, gf) ?? MissingGoalToCall(step.Text, gf);
			bool hasBlock = step.NextStep != null && step.NextStep.Indent > step.Indent;
			message ??= DroppedAction(step.Text, hasBlock, gf);
			if (message == null) return (instruction, null);

			return (instruction, new StepBuilderError(message, step,
				FixSuggestion: "Set goalToCallIfTrue to the goal called after `then` and goalToCallIfFalse to the goal called after `else`, e.g. goalToCallIfTrue={Name=\"HandleOverLimit\"}, goalToCallIfFalse={Name=\"IssueCodeNow\"}"));
		}

		/// <summary>
		/// A step that starts with `if` is a condition. A condition method that carries no value for what it checks, only the goals,
		/// evaluates nothing: the step would call its goal every time, or never. Returns the reason, or null when the condition is there.
		/// </summary>
		public static string? MissingCondition(string? stepText, IGenericFunction gf)
		{
			if (string.IsNullOrEmpty(stepText) || string.IsNullOrEmpty(gf?.Name)) return null;
			if (!Regex.IsMatch(stepText.TrimStart(), @"^if\b", RegexOptions.IgnoreCase)) return null;

			var methods = typeof(Program).GetMethods().Where(m => m.Name == gf.Name && m.GetParameters().Any(p => p.Name == "goalToCallIfTrue")).ToList();
			if (methods.Count == 0) return null;

			bool hasOperand = gf.Parameters != null && gf.Parameters.Any(p =>
				!p.Name.StartsWith("goalToCall", StringComparison.OrdinalIgnoreCase) && !p.Name.StartsWith("throwError", StringComparison.OrdinalIgnoreCase) && HasValue(gf, p.Name));
			if (hasOperand) return null;

			return $"The step is a condition but {gf.Name} was built without any value to check, only the goals to call, so nothing would be evaluated. " +
				"Put the variable or value that the step compares, e.g. `if %x% is empty then call A` => IsEmpty(item: \"%x%\", goalToCallIfTrue: {Name: \"A\"}).";
		}

		/// <summary>
		/// A one-line condition can call a goal, end the goal or throw. `- if %x% is empty, set %x% = 1` was built as the
		/// test alone and the set was dropped without a word, so the step did nothing. Returns the reason, or null.
		/// </summary>
		public static string? DroppedAction(string? stepText, bool hasBlock, IGenericFunction gf)
		{
			if (hasBlock || string.IsNullOrEmpty(stepText) || string.IsNullOrEmpty(gf?.Name)) return null;
			if (!Regex.IsMatch(stepText.TrimStart(), @"^if\b", RegexOptions.IgnoreCase)) return null;

			// The test written as a constant: `if %x% is empty, set %x% = 1` built as SetVariableWithCondition(boolValue: true)
			// set %x% every time.
			if (gf.Name == nameof(Program.SetVariableWithCondition))
			{
				var boolValue = gf.Parameters?.FirstOrDefault(p => p.Name == "boolValue")?.Value;
				var asText = boolValue?.ToString()?.Trim();
				if (boolValue is bool || asText == "true" || asText == "false" || asText == "True" || asText == "False")
				{
					return "The step tests a condition, but SetVariableWithCondition was built with boolValue as a constant, so the test was dropped and the variable would be set every time. " +
						"Write the action as an indented step under the condition:\n- if %x% is empty then\n    - set %x% = 1";
				}
				return null;
			}

			var takesGoals = typeof(Program).GetMethods().Where(m => m.Name == gf.Name)
				.Any(m => m.GetParameters().Any(p => p.Name == "goalToCallIfTrue"));
			if (!takesGoals) return null;

			if (HasValue(gf, "goalToCallIfTrue") || HasValue(gf, "goalToCallIfFalse")
				|| HasValue(gf, "throwErrorOnTrue") || HasValue(gf, "throwErrorOnFalse")) return null;

			// What comes after the condition: `then`, a comma or nothing at all.
			var text = Regex.Replace(stepText, "\"[^\"]*\"|'[^']*'", "\"\"");
			var rest = Regex.Match(text, @"(?:\bthen\b|,)\s*(\S.*)$", RegexOptions.IgnoreCase | RegexOptions.Singleline);
			if (!rest.Success) return null;

			return $"The step does something when the condition holds (`{rest.Groups[1].Value.Trim()}`), but {gf.Name} was built with no goal to call and nothing to throw, so that part would be dropped. " +
				"A one-line condition can only call a goal (`- if %x% is empty then call goal SetX`), end the goal or throw. Write any other action as an indented step under the condition:\n" +
				"- if %x% is empty then\n    - set %x% = 1";
		}

		public static string? MissingGoalToCall(string? stepText, IGenericFunction gf)
		{
			if (string.IsNullOrEmpty(stepText) || string.IsNullOrEmpty(gf?.Name)) return null;

			// Only methods that take the goals. RunInlineCode keeps them in its implementation.
			var takesGoals = typeof(Program).GetMethods().Where(m => m.Name == gf.Name)
				.Any(m => m.GetParameters().Any(p => p.Name == "goalToCallIfTrue"));
			if (!takesGoals) return null;

			// Quoted text is a value, not an instruction, and an `on error ... call X` clause is the step's error
			// handler, not its condition.
			var text = Regex.Replace(stepText, "\"[^\"]*\"|'[^']*'", "\"\"");
			var onError = Regex.Match(text, @"\bon\s+error\b", RegexOptions.IgnoreCase);
			if (onError.Success) text = text.Substring(0, onError.Index);

			if (!Regex.IsMatch(text, @"\bcall\b", RegexOptions.IgnoreCase)) return null;

			bool hasTrue = HasValue(gf, "goalToCallIfTrue");
			bool hasFalse = HasValue(gf, "goalToCallIfFalse");
			if (!hasTrue && !hasFalse)
			{
				return $"The step calls a goal but {gf.Name} was built with neither goalToCallIfTrue nor goalToCallIfFalse, so the condition would be evaluated and call nothing.";
			}

			var elseAt = Regex.Match(text, @"\belse\b", RegexOptions.IgnoreCase);
			if (!elseAt.Success) return null;

			bool callsOnThen = Regex.IsMatch(text.Substring(0, elseAt.Index), @"\bcall\b", RegexOptions.IgnoreCase);
			bool callsOnElse = Regex.IsMatch(text.Substring(elseAt.Index), @"\bcall\b", RegexOptions.IgnoreCase);
			if (callsOnThen && callsOnElse && !(hasTrue && hasFalse))
			{
				return $"The step calls one goal after `then` and another after `else`, but {gf.Name} was built with only {(hasTrue ? "goalToCallIfTrue" : "goalToCallIfFalse")}. Both goals must be set.";
			}
			return null;
		}

		private static readonly string[] ConditionWords = ["then", "else", "call", "goal", "if", "and", "or"];

		/// <summary>
		/// A goal to call must be one the step names. `if %a% is false and %b% > 300 then` with a block under it was built
		/// with goalToCallIfTrue "then", which fails at build time ("Could not find goal matching then"). Returns the reason, or null.
		/// </summary>
		public static string? UnnamedGoalToCall(string? stepText, IGenericFunction gf)
		{
			if (string.IsNullOrEmpty(stepText) || gf?.Parameters == null) return null;

			foreach (var parameterName in new[] { "goalToCallIfTrue", "goalToCallIfFalse" })
			{
				var name = GoalName(gf.Parameters.FirstOrDefault(p => string.Equals(p.Name, parameterName, StringComparison.OrdinalIgnoreCase))?.Value);
				if (string.IsNullOrWhiteSpace(name)) continue;

				var lastSegment = name.Trim().TrimEnd('/').Split('/').Last();
				bool isKeyword = ConditionWords.Contains(lastSegment, StringComparer.OrdinalIgnoreCase);
				bool isNamed = Regex.IsMatch(stepText, @"(?<!\w)" + Regex.Escape(lastSegment) + @"(?!\w)", RegexOptions.IgnoreCase);
				if (!isKeyword && isNamed) continue;

				return $"{parameterName} is \"{name}\", but the step names no goal of that name. Set {parameterName} only to a goal the step calls " +
					"(`then call goal X`); a condition with an indented block under it and no `call` has no goal to call: leave both goals null.";
			}
			return null;
		}

		private static string? GoalName(object? value)
		{
			return value switch
			{
				null => null,
				PLang.Models.GoalToCallInfo info => info.Name,
				Newtonsoft.Json.Linq.JObject obj => (obj.GetValue("Name", StringComparison.OrdinalIgnoreCase))?.ToString(),
				Newtonsoft.Json.Linq.JValue v => v.Value?.ToString(),
				string s => s.TrimStart().StartsWith("{") ? GoalName(Newtonsoft.Json.Linq.JObject.Parse(s)) : s,
				_ => GoalName(Newtonsoft.Json.Linq.JObject.FromObject(value))
			};
		}

		/// <summary>
		/// IsNot negates a test. A negative test is written with the negative operator (isNotEmpty, !=), so IsNot is true only
		/// for a step that says not. `%x% is false` was built as Operator == RightValue false with IsNot true, a double negation,
		/// so the condition was never true. Returns the reason, or null.
		/// </summary>
		public static string? StrayNegation(string? stepText, IGenericFunction gf)
		{
			if (string.IsNullOrEmpty(stepText) || gf?.Parameters == null) return null;
			var condition = gf.Parameters.FirstOrDefault(p => string.Equals(p.Name, "condition", StringComparison.OrdinalIgnoreCase))?.Value;
			if (condition == null) return null;

			Newtonsoft.Json.Linq.JToken token;
			try
			{
				token = condition is Newtonsoft.Json.Linq.JToken t ? t
					: condition is string s ? Newtonsoft.Json.Linq.JToken.Parse(s)
					: Newtonsoft.Json.Linq.JToken.FromObject(condition);
			}
			catch { return null; }

			bool hasNegatedNode = token.SelectTokens("$..IsNot").Any(p => p.Type == Newtonsoft.Json.Linq.JTokenType.Boolean && (bool)p);
			if (!hasNegatedNode) return null;

			var text = Regex.Replace(stepText, "\"[^\"]*\"|'[^']*'", "\"\"");
			if (Regex.IsMatch(text, @"\bnot\b|n't\b|!|\bnever\b|\bunless\b|\bneither\b|\bnor\b", RegexOptions.IgnoreCase)) return null;

			return "A condition was built with IsNot true, but the step says no `not`. IsNot negates the test, so the condition is the opposite of the step " +
				"(`%x% is false` built as Operator == RightValue false with IsNot true is never true). Set IsNot false; a negative test takes the negative operator.";
		}

		private static bool HasValue(IGenericFunction gf, string name)
		{
			var parameter = gf.Parameters?.FirstOrDefault(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase));
			if (parameter?.Value == null) return false;
			if (parameter.Value is Newtonsoft.Json.Linq.JToken token && token.Type == Newtonsoft.Json.Linq.JTokenType.Null) return false;
			return true;
		}

		private bool ShouldMakeCode(string functionName)
		{
			return (string.IsNullOrWhiteSpace(functionName) || functionName.Equals("n/a", StringComparison.OrdinalIgnoreCase)
				|| functionName == "RunInlineCode");
		}

		public async Task<(Instruction? Instruction, IBuilderError? Error)> PrepareStep(GoalStep step, IBuilderError? previousBuildError = null)
		{
			if (step.NextStep != null && step.NextStep.Indent > step.Indent)
			{
				AppendToSystemCommand("Next line of code will be executed if this return true");
			}

			return await base.Build(step, previousBuildError);
		}

		public async Task<(Instruction?, IBuilderError?)> MakeCode(GoalStep step, CompilerError? error = null, int errorCount = 0) {

			if (errorCount++ > 3)
			{
				return (null, error ?? new StepBuilderError("Could not compile code for this step", step));
			}

			var compiler = new CSharpCompiler(fileSystem, prParser, logger);
			var dllName = compiler.GetPreviousBuildDllNamesToExclude(step);

			SetSystem(@$"Act as a senior C# developer, that converts the user intent into a valid C#(Version. 11) code. 

## Rules ##
- Generate static class. The code generated should have 1 method with the static method named ExecutePlangCode and return bool. 
- A variable in user intent is defined by starting and ending %.
- Variables defined in the user intent can be passed into the ExecutePlangCode function by value, but only if defined by user. 
- Variable names passed to ExecutePlangCode function MUST be unmodified from the user statement
- The code will not be modified after you generate it.
- ALWAYS use long or long? instead of int or int?
- Use long.TryParse when validating if variable is long
- Do not reference any DTO classes. Choose the type that seems to fit best when not defined by user input. Use dynamic? if complex object is needed unless user defines type.
- Strings are defined with double quote ("")
- Any class from System.IO, should be replaced with PLang.SafeFileSystem.PLangFileSystem. It contains same classes and methods. 
- ONLY IF PLangFileSystem is needed, add parameter PLang.SafeFileSystem.PLangFileSystem fileSystem into ExecutePlangCode method, but ONLY if needed. Assembly for PLangFileSystem is already include, do not list it in Assemblies response.
- When condition is checking if variable is null, the variable needs to be defined with ? in the parameter, e.g. ExecutePlangCode(dynamic? variable)
- Replace the dot(.) in variables with the letter α e.g. %user.id% to userαid, %product.items[0].title% to productαitemsα0ααtitle, %list[1]% to listα1α
- Keep underscore in variables if defined by user, e.g.  if %user_id%(string) is null => ExecutePlangCode(string? user_id)
- Consider top security measures when generating code and validate code
- append @ sign for variable that match reserved keywords in C#
- C# code MUST only contain the contition code and return bool, an external system will call goals(methods) that user defines in his intent.
- initialize variables before using them in TryParse
- Always convert object to type before doing return.
- Always convert object to correct type in code, e.g. if code requires the object to be string use Convert.ToString(obj), when object should be bool Convert.ToBoolean(obj), etc.
- when checking if string is empty, check if it's null and do .ToString() on it and check if empty
- GoalToCallOnTrueParameters and GoalToCallOnFalseParameters should not be in c# code
- Variable with ! reference variables stored in context, e.g. %!request%.
## Rules ##

## Response information ##
- Namespace: MUST be PLangGeneratedCode
- Name: is name of class, it should represent the intent of what the code is doing. 
{dllName}
- GoalToCallOnTrue or GoalToCallOnFalse is optional, if not defined by user, set as null
- GoalToCallOnTrue and GoalToCallOnFalse MUST not be in c# code
- Goals can be called with parameters using GoalToCallOnTrueParameters and GoalToCallOnFalseParameters, e.g. if %isValid% then, call UpdateProduct id=%id%, call FalseCall status='false'. Then id is parameter for True, and status for False
- Using: must include namespaces that are needed to compile code.
- InputParameters: InputParameters MUST match parameter count sent to ExecutePlangCode. Keep format as is defined by user, e.g. if %!error% then => Parameters would be [""%!error%""], if %items.count% > 0 and %isValid% then => Parameters would be [""%list[0]%"", ""%isValid%""]
- Assemblies: dll to reference to compile using Roslyn
## Response information ##
");
			AppendToAssistantCommand(@"## examples ##
'if %isValid% is true then', this condition would return true if %isValid% is true. 
'if %address% is empty then', this would check if the %address% variable is empty and return true if it is, else false.
'if %data.user%(object) is not empty, then call ProcessUser user=%data.user%', => public static bool ExecutePlangCode(object? dataαuuser) { return (dataαuuser == null || string.IsNullOrEmpty(dataαuuser?.ToString()); }, GoalToCallOnTrue=ProcessUser, GoalToCallOnTrueParameters={user:%data.user%}
'if %data% (string) is null, call CreateData, else AppendData' => public static bool ExecutePlangCode(string? dataαuser_id) { return string.IsNullOrEmpty(userIdentity); }, GoalToCallOnTrue=CreateData, GoalToCallOnFalse=AppendData
'if %exists% (bool) is null, call CreateUser' => public static bool ExecutePlangCode(bool? dataαuser_id) { return exists == null;}, GoalToCallOnTrue=CreateUser, GoalToCallOnFalse=null
'if %exists% (bool) is not null, call CreateUser' => public static bool ExecutePlangCode(bool? dataαuser_id) { return exists != null;}, GoalToCallOnTrue=CreateUser, GoalToCallOnFalse=null
'if %data.user_id% is empty, call CreateUser, else UpdateUser user=%data%' => public static bool ExecutePlangCode(dynamic? dataαuser_id) { return (dataαuser_id == null || (dataαuser_id is string str && string.IsNullOrEmpty(str))); } //if we dont know the type of %data.user_id%, , GoalToCallOnTrue=CreateUser, GoalToCallOnFalse=UpdateUser, GoalToCallOnFalseParameters={user:%data%}
'if !%isValid% then => public static bool ExecutePlangCode(bool? isValid) { return !isValid; }, GoalToCallOnTrue=null, GoalToCallOnFalse=null
'if %first_name% is null, call UpdateFirstName' => public static bool ExecutePlangCode(string? first_name) { return (first_name == null || string.IsNullOrEmpty(str)); }
'if directory %path% exists, call DoStuff => public static bool ExecutePlangCode(string? path, IPlangFileSystem fileSystem) { return fileSystem.Directory.Exists(path); }
'if file %path% exists, call DoStuff => public static bool ExecutePlangCode(string? path, IPlangFileSystem fileSystem) { return fileSystem.File.Exists(path); }
'if %!response.IsHtml% then call ParseHtml => public static bool ExecutePlangCode(bool? isHtml) { return isHtml; }
## examples ##
");
			if (error != null)
			{
				AppendToAssistantCommand(error.LlmInstruction);
			}

			base.SetContentExtractor(new CSharpExtractor());
			(var codeInstruction, var buildError) = await Build<ConditionImplementationResponse>(step);
			if (buildError != null) return (null, buildError);

			//go back to default extractor
			base.SetContentExtractor(new JsonExtractor());

			var answer = (ConditionImplementationResponse)codeInstruction.Function;
			(var implementation, var compilerError) = await compiler.BuildCode<ConditionImplementationResponse>(answer, step, memoryStack);
			if (compilerError != null)
			{
				logger.LogWarning($"- Error compiling code - will ask LLM again ({errorCount} of 3 attempts) - Error:{compilerError}");
				return await Build(step, null, compilerError, errorCount);
			}
			List<Parameter> parameters = new List<Parameter>();
			parameters.Add(new Parameter(implementation.GetType().FullName, "implementation", implementation));

			List<ReturnValue> returnValues = new List<ReturnValue>();
			if (implementation.ReturnValues != null)
			{
				foreach (var rv in implementation.ReturnValues)
				{
					returnValues.Add(new ReturnValue(rv.Type, rv.VariableName));
				}
			}

			var gf = new GenericFunction("No explaination", "RunInlineCode", parameters, returnValues);

			var newInstruction = InstructionCreator.Create(gf, step, codeInstruction.LlmRequest);
			return (newInstruction, null);


		}


	}
}

