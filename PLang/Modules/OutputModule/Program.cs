﻿using Microsoft.AspNetCore.Http;
using NBitcoin;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PLang.Attributes;
using PLang.Building.Model;
using PLang.Errors;
using PLang.Errors.Interfaces;
using PLang.Errors.Runtime;
using PLang.Errors.Types;
using PLang.Exceptions;
using PLang.Models;
using PLang.Modules.WebCrawlerModule.Models;
using PLang.Runtime;
using PLang.Services.OutputStream;
using PLang.Utils;
using System.ComponentModel;
using System.Diagnostics;
using System.IO.Pipelines;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Channels;
using UAParser.Objects;
using static PLang.Modules.FileModule.CsvHelper;
using static PLang.Utils.StepHelper;

namespace PLang.Modules.OutputModule
{
	[Description("Writes to the output stream. Ask user a question, with LLM or without, either straight or with help of llm. output stream can be to the user(default), system, log, audit, metric and it can have different serialization, text, json, csv, binary, etc.")]
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

		[Description("channel=user|system|audit|metric|trace|debug|info|warning|error| or user defined channel, serializer=default(current serializer)|json|csv|xml or user defined")]
		public record OutputStreamInfo(string Channel = "user", string Serializer = "default", Dictionary<string, object?>? Options = null);

		public async Task<IError?> SetOutputStream(string channel, GoalToCallInfo goalToCall, Dictionary<string, object?>? parameters = null)
		{
			throw new NotImplementedException();
			/*
			var validate = programFactory.GetProgram<ValidateModule.Program>(goalStep);
			var error = await validate.IsNotEmpty([channel], "Channel cannot be empty");
			if (error != null) return error;

			error = await validate.IsNotEmpty([goalToCall], "GoalToCall cannot be empty");
			if (error != null) return error;

			goal.AddVariable(new GoalToCallInfo(goalToCall, parameters), variableName: "!output_stream_" + channel);
			
			return null;*/
		}
		/*
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
		*/

		public enum UserOrSystemEnum
		{
			User = 0, System = 1
		}

		[Description("Remove % from AnswerVariableName. TemplateFile points to a file for custom rendering")]
		public class AskOptions
		{
			public string Question { get; set; }

			[IgnoreWhenInstructed]
			public int StatusCode { get; set; }

			public Dictionary<string, string>? ValidationRegexPatternAndErrorMessage { get; set; }
			public Dictionary<string, object?>? CallbackData { get; set; }
			[IgnoreWhenInstructed]
			public Dictionary<string, object>? Parameters { get; set; }
			public Dictionary<string, string>? Choices { get; set; }
			public bool IsMultiChoice { get; set; }
			public string? TemplateFile { get; set; }
			[IgnoreWhenInstructed]
			public GoalToCallInfo? OnCallback { get; set; }
			public string AnswerVariableName { get; set; }
			[IgnoreWhenInstructed]
			public UserOrSystemEnum? UserOrSystem { get; set; }
			[IgnoreWhenInstructed]
			public string? Channel { get; set; }
			public bool IncludeNonce { get; set; } = false;

			[JsonIgnore]
			public TemplateEngineModule.Program? TemplateEngine { get; set; }

			[Description("answerVariableName is the variable being written to without %, e.g. write into %answer% => answerVariableName=\"answer\"")]
			public AskOptions(
				string question,
				int statusCode = 202,
				Dictionary<string, string>? validationRegexPatternAndErrorMessage = null,
				Dictionary<string, object>? callbackData = null,
				Dictionary<string, object>? parameters = null,
				Dictionary<string, string>? choices = null,
				bool isMultiChoice = false,
				string? templateFile = null,
				GoalToCallInfo? onCallback = null,
				string answerVariableName = "answer",
				UserOrSystemEnum? userOrSystem = null,
				string? channel = null, bool includeNonce = false)
			{
				Question = question;
				StatusCode = statusCode;
				ValidationRegexPatternAndErrorMessage = validationRegexPatternAndErrorMessage;
				CallbackData = callbackData;
				Parameters = parameters;
				Choices = choices;
				IsMultiChoice = isMultiChoice;
				TemplateFile = templateFile;
				OnCallback = onCallback;
				AnswerVariableName = answerVariableName;
				UserOrSystem = userOrSystem;
				Channel = channel;
				IncludeNonce = includeNonce;
			}
		}

		public record AskTemplateError(object Error, string OnErrorMethod);
		/*
		[Description("Remove % from AnswerVariableName.")]
		public record AskTemplateOptions(string OutputFile, AskTemplateError error)
		{


		}

		[Description("Send to a question to the output stream and waits for answer. This is used when user defines complex options, it will build ui for it")]
		public async Task<(object?, IError?)> AskTemplate(AskTemplateOptions askOptions)
		{/*
			Dictionary<string, object?>

			var caller = GetProgramModule<CallGoalModule.Program>();
			caller.RunGoal("/modules/UiModule/RenderUserIntent", )
			return (null, null);
		}*/

		[Description("Send to a question to the output stream and waits for answer.")]
		public async Task<(object?, IError?)> Ask(AskOptions askOptions)
		{
			var result = await AskInternal(askOptions);
			while (result.Error is IUserInputError ude)
			{
				result = await AskInternal(askOptions, ude);
			}
			return result;
		}

		private async Task<(object? Answer, IError? Error)> AskInternal(AskOptions askOptions, IError? error = null)
		{

			object? answer;
			var outputStream = outputStreamFactory.CreateHandler(/*askOptions.UserOrSystem, askOptions.Channel*/);
			if (goalStep.Callback != null)
			{
				string answerVariableName = askOptions.AnswerVariableName.Replace("%", "");
				if (HttpContext.Request.HasFormContentType)
				{
					answer = memoryStack.Get("request.body." + answerVariableName);
				}
				else
				{
					return (null, new ProgramError("Could not find answer"));
				}

				if (askOptions.OnCallback != null)
				{
					Dictionary<string, object?> parameters = new();
					parameters.Add(answerVariableName, answer);

					// onCallback is called before getting the options
					var caller = programFactory.GetProgram<CallGoalModule.Program>(goalStep);
					var runGoalResult = await caller.RunGoal(askOptions.OnCallback);
					if (runGoalResult.Error != null) return (null, runGoalResult.Error);
				}
				return await ProcessAnswer(answer, askOptions, outputStream.IsStateful);
			}


			outputStream.Step = goalStep;
			string path = "/";
			if (HttpContext != null)
			{
				path = HttpContext.Request.Path;
			}

			var callback = await StepHelper.GetCallback(path, askOptions.CallbackData, memoryStack, goalStep, programFactory, !askOptions.IncludeNonce);
			(answer, error) = await outputStream.Ask(askOptions, callback, error);
			if (error != null) return (null, error); 

			if (!outputStream.IsStateful) return (null, new EndGoal(new Goal() { RelativePrPath = "RootOfApp" }, goalStep, "", Levels: 9999)); 

			return await ProcessAnswer(answer, askOptions, outputStream.IsStateful);

		}

		private async Task<(object?, IError?)> ProcessAnswer(object? answer, AskOptions askOptions, bool isStateful)
		{
			// escape any variable that user inputs
			answer = answer?.ToString()?.Replace("%", @"\%") ?? string.Empty;
			if (askOptions.ValidationRegexPatternAndErrorMessage == null)
			{
				return (answer, null);
			}

			GroupedUserInputErrors groupedErrors = new GroupedUserInputErrors();
			foreach (var validation in askOptions.ValidationRegexPatternAndErrorMessage)
			{
				var regexPattern = variableHelper.LoadVariables(validation.Key)?.ToString() ?? null;
				if (!string.IsNullOrEmpty(regexPattern) && !Regex.IsMatch(answer.ToString(), regexPattern))
				{
					groupedErrors.Add(new UserInputError(validation.Value, goalStep));
				}
			}
			if (groupedErrors.Count > 0) return (answer, groupedErrors);

			if (isStateful) return (answer, null);

			var callbackBase64 = memoryStack.Get<string>("request.body.callback");
			if (string.IsNullOrEmpty(callbackBase64)) return (null, new ProgramError("callback was invalid", goalStep));
			var callback = JsonConvert.DeserializeObject<Callback>(callbackBase64.FromBase64());
			var encryption = programFactory.GetProgram<Modules.CryptographicModule.Program>(goalStep);
			if ((callback?.CallbackData == null || callback?.CallbackData?.Count == 0) && function.ReturnValues?.Count > 0)
			{
				answer = memoryStack.Get("request.body");
			}
			else
			{
				foreach (var item in callback?.CallbackData ?? [])
				{
					string? value = item.Value?.ToString();
					if (string.IsNullOrEmpty(value)) continue;

					var decryptedValue = await encryption.Decrypt(value);
					memoryStack.Put(item.Key, decryptedValue);

				}
			}


			return (answer, null);

		}

		public record Option(int ListNumber, object SelectionInfo)
		{
			[LlmIgnore]
			public ObjectValue? ObjectValue { get; set; }
		};

		private (List<Option>? Options, IError? Error) GetOptions(string? answer)
		{
			if (string.IsNullOrEmpty(answer)) return (null, new ProgramError("Answer was empty", goalStep));

			List<Option> options = new();
			var variables = variableHelper.GetVariables(answer);
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
					options.Add(new Option((i + 1), value) { ObjectValue = variables[i] });
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

		[Description("Write out json content. Only choose this method when it's clear user is defining a json output, e.g. `- write out '{name:John}'. Do your best to make sure that content is valid json. Any %variable% should have double quotes around it. type can be text|warning|error|info|debug|trace. statusCode(like http status code) should be defined by user. type=error should have statusCode between 400-599, depending on text. channel=user|system|logger|audit|metric. User can also define his custom channel")]
		public async Task<IError?> WriteJson(object? content, JsonOptions? jsonOptions = null, string type = "text", int statusCode = 200, Dictionary<string, object?>? paramaters = null, string? channel = null)
		{

			JsonSerializerSettings settings = new();
			if (jsonOptions != null)
			{
				settings.NullValueHandling = jsonOptions.NullValueHandling;
				settings.DateFormatHandling = jsonOptions.DateFormatHandling;
				if (!string.IsNullOrEmpty(jsonOptions.DateFormatString))
				{
					settings.DateFormatString = jsonOptions.DateFormatString;
				}
				settings.DefaultValueHandling = jsonOptions.DefaultValueHandling;
				settings.Formatting = jsonOptions.Formatting;
				settings.ReferenceLoopHandling = jsonOptions.ReferenceLoopHandling;
			}
			else
			{
				settings.Formatting = Formatting.Indented;
				settings.ReferenceLoopHandling = ReferenceLoopHandling.Ignore;
			}

			return await Write(JsonConvert.SerializeObject(content, settings), type, statusCode, paramaters, channel);
		}

		public async Task<IError?> Write(object content, OutputStreamInfo outputStreamInfo)
		{
			IOutputStream os;
			if (outputStreamInfo.Channel.Equals("system", StringComparison.OrdinalIgnoreCase))
			{
				os = outputSystemStreamFactory.CreateHandler();
			}
			else
			{
				os = outputStreamFactory.CreateHandler();
			}


			if (outputStreamInfo.Serializer.Equals("json", StringComparison.OrdinalIgnoreCase))
			{
				content = JsonConvert.SerializeObject(content);
				await os.Write(content);
			}
			else if (outputStreamInfo.Serializer.Equals("csv", StringComparison.OrdinalIgnoreCase))
			{
				CsvOptions options = RecordInitializer.FromDictionary<CsvOptions>(new CsvOptions(), outputStreamInfo.Options);

				using var memoryStream = new MemoryStream();
				var writer = new StreamWriter(memoryStream, leaveOpen: true);
				await Modules.FileModule.CsvHelper.WriteToStream(writer, content, options);
				await writer.FlushAsync();

				memoryStream.Position = 0;

				if (os is HttpOutputStream httpOutputStream)
				{
					httpOutputStream.SetContentType("text/csv");
				}				
				await memoryStream.CopyToAsync(os.Stream);
				
			}
			else
			{
				
				await os.Write(content);
			}
			return null;
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
