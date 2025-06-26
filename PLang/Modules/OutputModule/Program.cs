using NBitcoin;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PLang.Attributes;
using PLang.Errors;
using PLang.Errors.Runtime;
using PLang.Errors.Types;
using PLang.Exceptions;
using PLang.Models;
using PLang.Runtime;
using PLang.Services.OutputStream;
using PLang.Utils;
using System.ComponentModel;
using System.Diagnostics;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Channels;
using static PLang.Utils.StepHelper;

namespace PLang.Modules.OutputModule
{
	[Description("Writes to the output stream. Ask user a question, with LLM or without, either straight or with help of llm. output stream can be to the user(default), system, log, audit, metric")]
	public class Program : BaseProgram, IDisposable
	{
		private readonly IOutputStreamFactory outputStreamFactory;
		private readonly IOutputSystemStreamFactory outputSystemStreamFactory;
		private readonly VariableHelper variableHelper;
		private readonly ProgramFactory programFactory;

		public Program(IOutputStreamFactory outputStreamFactory, IOutputSystemStreamFactory outputSystemStreamFactory, VariableHelper variableHelper, ProgramFactory programFactory) : base()
		{
			this.outputStreamFactory = outputStreamFactory;
			this.outputSystemStreamFactory = outputSystemStreamFactory;
			this.variableHelper = variableHelper;
			this.programFactory = programFactory;
		}

		public async Task<IError?> SetOutputStream(string channel, GoalToCallInfo goalToCall, Dictionary<string, object?>? parameters = null)
		{
			var validate = programFactory.GetProgram<ValidateModule.Program>(goalStep);
			var error = await validate.IsNotEmpty([channel], "Channel cannot be empty");
			if (error != null) return error;

			error = await validate.IsNotEmpty([goalToCall], "GoalToCall cannot be empty");
			if (error != null) return error;

			goal.AddVariable(new GoalToCallInfo(goalToCall, parameters), variableName: "!output_stream_" + channel);
			return null;
		}

		[Description("Send to user and waits for answer. Uses llm to construct a question to user and to format the answer. Developer defines specifically to use llm")]
		public async Task<(object?, IError?)> AskUserUsingLlm(string text, string type = "text", int statusCode = 202,
			string? developerInstructionForResult = "give me the object that matches, e.g. { \"id\": 123, \"name\": \"example\"}",
			[HandlesVariable] Dictionary<string, object?>? options = null, string? scheme = null)
		{
			var callGoalModule = programFactory.GetProgram<CallGoalModule.Program>(goalStep);
			var param = new Dictionary<string, object?> {
				{ "type", type }, { "statusCode", statusCode }, { "text", text },
				{ "developerInstructionForResult", developerInstructionForResult }, {"scheme", scheme } };
			if (options != null)
			{
				string json = "";
				foreach (var option in options)
				{
					json += $"<{option.Key}>\n{JsonConvert.SerializeObject(variableHelper.LoadVariables(option.Value))}\n<{option.Key}>\n";
				}
				param.Add("options", json);
			}

			var goalToCall = new GoalToCallInfo("/modules/OutputModule/AskUserLlm", param);

			return await callGoalModule.RunGoal(goalToCall, isolated: true);
		}
		[Description("Send to user and waits for answer. type can be text|warning|error|info|debug|trace. statusCode(like http status code) should be defined by user. regexPattern should contain start and end character if user input needs to match fully. regexPattern can contain %variable%. errorMessage is message to user when answer does not match expected regexPattern, use good grammar and correct formatting.")]
		public async Task<(object?, IError?)> AskUser(string text, string type = "text", int statusCode = 202, string? regexPattern = null, string? errorMessage = null, Dictionary<string, object>? parameters = null)
		{
			return await Ask(text, type, statusCode, regexPattern, errorMessage, parameters, "user");
		}

		[Description("Send to user and waits for answer. type can be text|warning|error|info|debug|trace. statusCode(like http status code) should be defined by user. regexPattern should contain start and end character if user input needs to match fully. regexPattern can contain %variable%. errorMessage is message to user when answer does not match expected regexPattern, use good grammar and correct formatting. channel=user|system|logger|audit|metric. User can also define his custom channel. selection is a %variable(s)% used to provide user with options to select from that will return the object of the selection ")]
		public async Task<(object?, IError?)> Ask(string text, string type = "text", int statusCode = 202,
			string? regexPattern = null, string? errorMessage = null,
			Dictionary<string, object>? parameters = null, string? channel = null,
			[HandlesVariable] string? selection = null, GoalToCallInfo? onCallback = null)
		{

			string? result;
			IError? error = null;
			List<Option>? options = null;
			if (context.ContainsKey("!answer"))
			{
				if (onCallback != null)
				{
					// onCallback is called before getting the options
					var caller = programFactory.GetProgram<CallGoalModule.Program>(goalStep);
					var runGoalResult = await caller.RunGoal(onCallback);
					if (runGoalResult.Error != null) return (null, runGoalResult.Error);
				}

				result = context["!answer"]?.ToString() ?? "";

				(options, error) = GetOptions(selection);
				if (error != null) return (null, error);

				if (options == null) return (result, null);

				if (!int.TryParse(result, out int listNumber))
				{
					return (null, new ProgramError($"You must type in an number between {options.First().ListNumber} and {options.Last().ListNumber}"));
				}

				var option = options.FirstOrDefault(p => p.ListNumber == listNumber);
				if (option == null)
				{
					return (null, new ProgramError($"You must type in an number between {options.First().ListNumber} and {options.Last().ListNumber}"));
				}
				return (option.ObjectValue, null);
			}

			var outputStream = outputStreamFactory.CreateHandler();

			var callback = await StepHelper.GetCallback(goalStep, programFactory);
			(options, error) = GetOptions(selection);
			if (error != null) return (null, error);

			(result, error) = await outputStream.Ask(text, type, statusCode, parameters, callback, options);
			
			if (!outputStream.IsStateful) return (null, new EndGoal(goalStep, ""));


			// escape any variable that user inputs
			result = result.Replace("%", @"\%");

			regexPattern = variableHelper.LoadVariables(regexPattern)?.ToString() ?? null;
			if (!string.IsNullOrEmpty(regexPattern) && !Regex.IsMatch(result, regexPattern))
			{
				if (errorMessage != null && !text.Contains(errorMessage))
				{
					text = errorMessage + "\n\n" + text;
				}
				context.Remove("!answer");
				return await Ask(text, type, statusCode, regexPattern, errorMessage, parameters);
			}
			return (result, null);
		}

		public record Option(int ListNumber, object SelectionInfo, ObjectValue ObjectValue);

		private (List<Option>? Options, IError? Error) GetOptions(string? selection)
		{
			if (string.IsNullOrEmpty(selection)) return (null, null);

			List<Option> options = new();
			var variables = variableHelper.GetVariables(selection);
			if (variables.Count == 0) return (null, new ProgramError("No variables defined in selection. It must contain a variable.", goalStep));

			var roots = variables.Select(p => p.Root);
			var distinctRoots = roots.Distinct();
			if (distinctRoots.Count() > 1)
			{
				return (null, new ProgramError("Only one type of variable can be used.", goalStep));
			}

			for (int i = 0; i < variables.Count; i++)
			{
				object? value = variables[i].Value;
				if (value != null)
				{
					options.Add(new Option((i + 1), value, variables[i]));
				}
			}

			return (options, null);


		}

		public void Dispose()
		{
			var stream = outputStreamFactory.CreateHandler();
			if (stream is IDisposable disposable)
			{
				disposable.Dispose();
			}

		}

		public record JsonOptions(NullValueHandling NullValueHandling = NullValueHandling.Include, DateFormatHandling DateFormatHandling = DateFormatHandling.IsoDateFormat,
			string? DateFormatString = null, DefaultValueHandling DefaultValueHandling = DefaultValueHandling.Include, Formatting Formatting = Formatting.Indented, 
			ReferenceLoopHandling ReferenceLoopHandling = ReferenceLoopHandling.Ignore);

		[Description("Write out content. Do your best to make sure that content is valid json. Any %variable% should have double quotes around it. type can be text|warning|error|info|debug|trace. statusCode(like http status code) should be defined by user. type=error should have statusCode between 400-599, depending on text. channel=user|system|logger|audit|metric. User can also define his custom channel")]
		public async Task<IError?> WriteJson(object? content, JsonOptions? jsonOptions = null, string type = "text", int statusCode = 200, Dictionary<string, object?>? paramaters = null, string? channel = null)
		{
			
			JsonSerializerSettings settings = new();
			if (jsonOptions != null) {
				settings.NullValueHandling = jsonOptions.NullValueHandling;
				settings.DateFormatHandling = jsonOptions.DateFormatHandling;
				if (!string.IsNullOrEmpty(jsonOptions.DateFormatString))
				{
					settings.DateFormatString = jsonOptions.DateFormatString;
				}
				settings.DefaultValueHandling = jsonOptions.DefaultValueHandling;
				settings.Formatting = jsonOptions.Formatting;
				settings.ReferenceLoopHandling = jsonOptions.ReferenceLoopHandling;
			} else
			{
				settings.Formatting = Formatting.Indented;
				settings.ReferenceLoopHandling = ReferenceLoopHandling.Ignore;
			}

			return await Write(JsonConvert.SerializeObject(content, settings), type, statusCode, paramaters,channel);
		}
		
		[Description("Write to the output. type can be text|warning|error|info|debug|trace. statusCode(like http status code) should be defined by user. type=error should have statusCode between 400-599, depending on text. channel=user|system|logger|audit|metric. User can also define his custom channel")]
		public async Task<IError?> Write(object content, string type = "text", int statusCode = 200, Dictionary<string, object?>? paramaters = null, string? channel = null)
		{


			if (channel != null && goal.GetVariable<bool?>("!output_stream_" + channel + "_goal") == null)
			{
				var goalToCall = goal.GetVariable<GoalToCallInfo>("!output_stream_" + channel);
				if (goalToCall != null)
				{
					goalToCall.Parameters.AddOrReplace("type", type);
					goalToCall.Parameters.AddOrReplace("statusCode", statusCode);
					goalToCall.Parameters.AddOrReplace("content", content);
					goalToCall.Parameters.AddOrReplace("!output_stream_" + channel + "_goal", true);

					var callGoalModule = programFactory.GetProgram<CallGoalModule.Program>(goalStep);
					var result = await callGoalModule.RunGoal(goalToCall);
					if (result.Error != null) return result.Error;

					// writing to channel does not return any value
					return null;
				}
			}

			// todo: quick fix, this should be dynamic with multiple channels, such as, user(default), system, notification, audit, metric, logs warning, and user custom channel.
			if (channel == "system")
			{
				await outputSystemStreamFactory.CreateHandler().Write(content, type, statusCode, paramaters);
			}
			else
			{
				await outputStreamFactory.CreateHandler().Write(content, type, statusCode, paramaters);
			}

			return null;
		}

	}
}
