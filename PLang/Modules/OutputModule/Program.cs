using Microsoft.AspNetCore.Http;
using NBitcoin;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Org.BouncyCastle.Utilities.Zlib;
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
using PLang.Services.Transformers;
using PLang.Utils;
using RazorEngineCore;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO.Pipelines;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Channels;
using UAParser.Objects;
using static PLang.Modules.FileModule.CsvHelper;
using static PLang.Modules.OutputModule.Program;
using static PLang.Modules.UiModule.Program;
using static PLang.Modules.WebserverModule.Program;
using static PLang.Runtime.Startup.ModuleLoader;
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

		[Description("QuestionOrTemplateFile either the question or points to a file for custom rendering")]
		public record AskOptions(string QuestionOrTemplateFile, int StatusCode = 202,
			Dictionary<string, object?>? CallbackData = null, GoalToCallInfo? OnCallback = null,
			UserOrSystemEnum? UserOrSystem = UserOrSystemEnum.User, string? Channel = "default")

		{
			[LlmIgnore]
			public bool IsTemplateFile
			{
				get
				{
					if (QuestionOrTemplateFile.Contains("\n") || QuestionOrTemplateFile.Contains("\r") || QuestionOrTemplateFile.Contains("\r")) return false;
					string ext = Path.GetExtension(QuestionOrTemplateFile);
					return (!string.IsNullOrEmpty(ext) && ext.Length < 10);
				}
			}
		}
			;

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
		

		[Description("Send to a question to the output stream and waits for answer. It always returns and answer will be written into variable")]
		public async Task<(object?, IError?)> Ask(AskOptions askOptions)
		{
			var result = await AskInternal(askOptions);
			/*while (result.Error is IUserInputError ude)
			{
				result = await AskInternal(askOptions, ude);
			}*/
			return result;
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

		private async Task<(object? Answer, IError? Error)> AskInternal(AskOptions askOptions, IError? error = null)
		{
			List<ObjectValue>? answers = new();
			IOutputStream outputStream;
			if (askOptions.Channel == "system")
			{
				outputStream = outputSystemStreamFactory.CreateHandler();
			}
			else
			{
				outputStream = outputStreamFactory.CreateHandler();
			}
			if (goalStep.Callback != null)
			{
				(answers, error) = await ProcessCallbackAnswer(askOptions, error);
				if (error != null && error is UserInputError uie2)
				{
					var newCallBack = await StepHelper.GetCallback(GetCallbackPath(), askOptions.CallbackData, memoryStack, goalStep, programFactory);
					uie2 = uie2 with { Callback = newCallBack };
					
					error = await Write(uie2);
					if (error != null) return (answers, error);

					uie2.Handled = true;

					return (answers, uie2);

				}
				return (answers, error);
			}

			var path = GetCallbackPath();
			var url = (HttpContext.Request.Path.Value ?? "/");
			var callback = await StepHelper.GetCallback(url, askOptions.CallbackData, memoryStack, goalStep, programFactory);
			
			Dictionary<string, object?> parameters = new();
			parameters.AddOrReplace("askOptions", askOptions);
			parameters.AddOrReplace("callback", JsonConvert.SerializeObject(callback).ToBase64());
			parameters.AddOrReplace("error", error);
			parameters.Add("url", url);
			parameters.AddOrReplace("id", Path.Join(path, goalStep.Goal.GoalName, goalStep.Number.ToString()).Replace("\\", "/"));

			if (outputStream is HttpOutputStream httpOutputStream)
			{
				foreach (var rp in httpOutputStream.ResponseProperties)
				{
					parameters.AddOrReplace(rp.Key, rp.Value);
				}
			}			

			string? content = null;
			if (askOptions.IsTemplateFile)
			{
				var templateEngine = GetProgramModule<Modules.TemplateEngineModule.Program>();
				(content, var renderError) = await templateEngine.RenderFile(askOptions.QuestionOrTemplateFile, parameters);
				if (renderError != null) return (null, renderError);
			}
			else
			{
				content = variableHelper.LoadVariables(askOptions.QuestionOrTemplateFile)?.ToString();
			}
						
			(var answer, error) = await outputStream.Ask(goalStep, content, askOptions.StatusCode, callback, error);
			if (error != null) return (null, error);

			if (!outputStream.IsStateful) return (null, new EndGoal(new Goal() { RelativePrPath = "RootOfApp" }, goalStep, "Asking user a question", Levels: 9999));

			if (function.ReturnValues == null || function.ReturnValues.Count == 0)
			{
				return (null, new ProgramError("No variable to write into", goalStep,
					FixSuggestion: @$"add `write to %answer%` to you step, e.g. `- {goalStep.Text}, write into %answer%"));
			}

			answers.Add(new ObjectValue(function.ReturnValues[0].VariableName, answer));

			(answers, error) = await ValidateAnswers(answers, askOptions);
			if (error != null && error is UserInputError uie)
			{
				var newCallBack = await StepHelper.GetCallback(GetCallbackPath(), askOptions.CallbackData, memoryStack, goalStep, programFactory);
				uie = uie with { Callback = newCallBack };

				error = await Write(uie);
				if (error != null) return (answers, error);

				uie.Handled = true;

				return (answers, uie);

			}
			return (answers, error);
		}

		
		private async Task<(List<ObjectValue>? Answers, IError? Error)> ProcessCallbackAnswer(AskOptions askOptions, IError? error = null)
		{
			if (HttpContext == null || !HttpContext.Request.HasFormContentType)
			{
				return (null, new ProgramError("Request no longer available", goalStep));
			}

			var callbackBase64 = memoryStack.Get<string>("request.body.callback");
			if (string.IsNullOrEmpty(callbackBase64))
			{
				return (null, new ProgramError("callback was invalid", goalStep));
			}

			var callback = JsonConvert.DeserializeObject<Callback>(callbackBase64.FromBase64());
			var encryption = programFactory.GetProgram<Modules.CryptographicModule.Program>(goalStep);
			if ((callback?.CallbackData != null && callback?.CallbackData?.Count > 0))
			{
				foreach (var item in callback?.CallbackData ?? [])
				{
					string? value = item.Value?.ToString();
					if (string.IsNullOrEmpty(value)) continue;

					var decryptedValue = await encryption.Decrypt(value);
					memoryStack.Put(item.Key, decryptedValue);
					if (askOptions.CallbackData?.ContainsKey(item.Key) == true)
					{
						askOptions.CallbackData[item.Key] = decryptedValue;
					}
				}
			}

			(List<ObjectValue> answers, error) = GetStatelessAnswers();
			if (error != null) return (null, error);

			return await ValidateAnswers(answers!, askOptions);
		}

		private async Task<(List<ObjectValue> Answers, IError? Error)> ValidateAnswers(List<ObjectValue> answers, AskOptions askOptions)
		{
			if (askOptions.OnCallback == null) return (answers, null);

			foreach (var answer in answers)
			{
				askOptions.OnCallback.Parameters.AddOrReplace(answer.Name, answer.Value);
			}

			var caller = programFactory.GetProgram<CallGoalModule.Program>(goalStep);
			var runGoalResult = await caller.RunGoal(askOptions.OnCallback);
			if (runGoalResult.Error != null)
			{
				return (answers, runGoalResult.Error);
			}
			return (answers, null);

		}

		private (List<ObjectValue>?, IError?) GetStatelessAnswers()
		{
			List<ObjectValue> answers = new();

			if (function.ReturnValues == null || function.ReturnValues.Count == 0)
			{
				return (null, new ProgramError("No variable to write into", goalStep,
					FixSuggestion: @$"add `write to %answer%` to you step, e.g. `- {goalStep.Text}, write into %answer%"));
			}


			foreach (var rv in function.ReturnValues)
			{
				var variableName = rv.VariableName.Replace("%", "");
				var result = memoryStack.Get("request.body." + variableName);
				if (result == null)
				{
					var dict = memoryStack.Get<Dictionary<string, object?>>("request.body");
					var newDict = dict.Where(p => p.Key != "callback").ToDictionary();
					answers.Add(new ObjectValue(variableName, newDict));
				}
				else
				{
					answers.Add(new ObjectValue(variableName, result));
				}
			}

			if (answers.Count == 0)
			{
				return (null, new UserInputError("No answer provided", goalStep));
			}

			return (answers, null);
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
		public async Task<IError?> WriteJson(GoalStep step, object? content, JsonOptions? jsonOptions = null, string type = "text", int statusCode = 200, Dictionary<string, object?>? paramaters = null, string? channel = null)
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

		public async Task<IError?> WriteWithStreamInfo(object content, OutputStreamInfo outputStreamInfo)
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
				await os.Write(goalStep, content);
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

				await os.Write(goalStep, content);
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

			// todo: quick fix, this should be dynamic with multiple channels, such as, user(default), system, notification, loading, audit, metric, logs warning, and user custom channel.
			if (channel == "system")
			{
				await outputSystemStreamFactory.CreateHandler().Write(goalStep, content, type, statusCode, paramaters);
			}
			else
			{
				await outputStreamFactory.CreateHandler().Write(goalStep, content, type, statusCode, paramaters);
			}

			return null;
		}

	}
}
