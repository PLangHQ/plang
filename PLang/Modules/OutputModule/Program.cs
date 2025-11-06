using Microsoft.AspNetCore.SignalR.Protocol;
using NBitcoin;
using Newtonsoft.Json;
using PLang.Attributes;
using PLang.Building.Model;
using PLang.Errors;
using PLang.Errors.Runtime;
using PLang.Models;
using PLang.Runtime;
using PLang.Services.OutputStream;
using PLang.Services.OutputStream.Messages;
using PLang.Services.OutputStream.Sinks;
using PLang.Utils;
using System.ComponentModel;
using static PLang.Utils.StepHelper;

namespace PLang.Modules.OutputModule
{
	[Description("Writes to the output stream. Ask a question with either text or template file. output stream can be to the user(default), system, to different channels such audit|metric|debug|...., and it can have different serialization, text, json, csv, binary, etc.")]
	public class Program : BaseProgram
	{
		private readonly VariableHelper variableHelper;
		private readonly ProgramFactory programFactory;

		public Program(VariableHelper variableHelper, ProgramFactory programFactory) : base()
		{
			this.variableHelper = variableHelper;
			this.programFactory = programFactory;
		}

		[Description("channel=audit|metric|trace|debug|info|warning|error| or user defined channel, serializer=default(current serializer)|json|csv|xml or user defined")]
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
					json += $"<{option.Key}>\n{JsonConvert.SerializeObject(memoryStack.LoadVariables(option.Value))}\n<{option.Key}>\n";
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





		[Description("Send to a question to the output stream and waits for answer. It always returns and answer will be written into variable")]
		[Example("ask user template.html, open modal, validate ValidateData, call back data: %id%, write to %result%", 
			@"Content=""template.html"", Actor=""user"", Channel=""default"", Actions:[""showModal""], CallbackData:{id:""%id""} ")]
		public async Task<(object?, IError?)> Ask(AskMessage askMessage)
		{
			var result = await AskInternal(askMessage);
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

		private async Task<(object? Answer, IError? Error)> AskInternal(AskMessage askMessage, IError? error = null)
		{
			List<ObjectValue>? answers = new();
			IOutputSink outputStream = context.GetSink(askMessage.Actor);

			if (context.Callback != null)
			{
				(answers, error) = await ProcessCallbackAnswer(askMessage, context.Callback, error);


				
				return (answers, error);
			}
			Dictionary<string, object?> parameters = new();

			if (!outputStream.IsStateful)
			{
				var path = GetCallbackPath();
				var url = (HttpContext?.Request.Path.Value ?? "/");
				var callback = await StepHelper.GetCallback(url, askMessage.CallbackData, memoryStack, goalStep, programFactory);

				askMessage = askMessage with { Callback = callback };

				parameters.Add("callback", JsonConvert.SerializeObject(callback).ToBase64());
				parameters.Add("url", url);

				if (context.DebugMode)
				{
					parameters.Add("prFile", goalStep.PrFileName);
				}

				if (outputStream is HttpSink httpOutputStream)
				{
					foreach (var rp in httpOutputStream.ResponseProperties)
					{
						parameters.AddOrReplace(rp.Key, rp.Value);
					}
				}
			}

			parameters.AddOrReplace("askMessage", askMessage);
			parameters.AddOrReplace("error", error);

			string? content = null;
			if (askMessage.IsTemplateFile == true || (askMessage.IsTemplateFile == null && PathHelper.IsTemplateFile(askMessage.Content)))
			{
				var templateEngine = GetProgramModule<Modules.TemplateEngineModule.Program>();
				(content, var renderError) = await templateEngine.RenderFile(askMessage.Content, parameters);
				if (renderError != null) return (null, renderError);
			}
			else
			{
				content = memoryStack.LoadVariables(askMessage.Content)?.ToString();
			}

			askMessage = askMessage with { Content = content };

			(var answer, error) = await outputStream.AskAsync(askMessage);
			if (error != null) return (null, error);

			if (!outputStream.IsStateful) return (null, new EndGoal(true, new Goal() { RelativePrPath = "RootOfApp" }, goalStep, "Asking user a question", Levels: 9999));

			if (function.ReturnValues == null || function.ReturnValues.Count == 0)
			{
				return (null, new ProgramError("No variable to write into", goalStep,
					FixSuggestion: @$"add `write to %answer%` to you step, e.g. `- {goalStep.Text}, write into %answer%"));
			}

			answers.Add(new ObjectValue(function.ReturnValues[0].VariableName, answer));

			(answers, error) = await ValidateAnswers(answers, askMessage);
			if (error != null && error is UserInputError uie)
			{
				throw new NotImplementedException("ask user error");
				/*
				var newCallBack = await StepHelper.GetCallback(GetCallbackPath(), askMessage.CallbackData, memoryStack, goalStep, programFactory);
				uie = uie with { Callback = newCallBack };

				error = await Write(uie);
				if (error != null) return (answers, error);

				uie.Handled = true;

				return (answers, uie);*/

			}
			return (answers, error);
		}


		private async Task<(List<ObjectValue>? Answers, IError? Error)> ProcessCallbackAnswer(AskMessage askMessage, Callback callback, IError? error = null)
		{
			if (HttpContext == null || !HttpContext.Request.HasFormContentType)
			{
				return (null, new ProgramError("Request no longer available", goalStep));
			}

			var encryption = programFactory.GetProgram<Modules.CryptographicModule.Program>(goalStep);
			if (callback.CallbackData != null && callback.CallbackData?.Count > 0)
			{
				foreach (var item in callback.CallbackData ?? [])
				{
					string? value = item.Value?.ToString();
					if (string.IsNullOrEmpty(value)) continue;

					var decryptedValue = await encryption.Decrypt(value);
					memoryStack.Put(item.Key, decryptedValue);
					if (askMessage.CallbackData?.ContainsKey(item.Key) == true)
					{
						askMessage.CallbackData[item.Key] = decryptedValue;
					}
				}
			}

			(List<ObjectValue>? answers, error) = GetStatelessAnswers(askMessage.CallbackData);
			if (error != null) return (null, error);

			(answers, error) = await ValidateAnswers(answers!, askMessage);

			if (error != null && error is UserInputError uie2)
			{
				var newCallBack = await StepHelper.GetCallback(GetCallbackPath(), askMessage.CallbackData, memoryStack, goalStep, programFactory);
				newCallBack.PreviousHash = context.Callback.Hash;

				var errorMessage = uie2.ErrorMessage with { Callback = newCallBack };
				uie2 = uie2 with { Callback = newCallBack, ErrorMessage = errorMessage };
				
				context.Callback = null;

				return (answers, uie2);

			}

			context.Callback = null;
			return (answers, error);
		}

		private async Task<(List<ObjectValue> Answers, IError? Error)> ValidateAnswers(List<ObjectValue> answers, AskMessage askMessage)
		{
			if (askMessage.OnCallback == null) return (answers, null);

			foreach (var answer in answers)
			{
				askMessage.OnCallback.Parameters.AddOrReplace(answer.Name, answer.Value);
			}
			foreach (var askOptionsData in askMessage.CallbackData ?? [])
			{
				askMessage.OnCallback.Parameters.AddOrReplace(askOptionsData.Key, askOptionsData.Value);
			}

			var caller = programFactory.GetProgram<CallGoalModule.Program>(goalStep);
			var runGoalResult = await caller.RunGoal(askMessage.OnCallback);
			if (runGoalResult.Error != null)
			{
				return (answers, runGoalResult.Error);
			}
			return (answers, null);

		}

		private (List<ObjectValue>?, IError?) GetStatelessAnswers(Dictionary<string, object?> callbackData)
		{
			List<ObjectValue> answers = new();

			if (function.ReturnValues == null || function.ReturnValues.Count == 0)
			{
				return (null, new ProgramError("No variable to write into", goalStep,
					FixSuggestion: @$"add `write to %answer%` to you step, e.g. `- {goalStep.Text}, write into %answer%"));
			}

			if (function.ReturnValues.Count == 1)
			{
				var rv = function.ReturnValues[0];
				var variableName = rv.VariableName.Replace("%", "");
				var result = memoryStack.Get("request.body." + variableName);
				if (result == null)
				{
					var dict = memoryStack.Get<Dictionary<string, object?>>("request.body");
					if (dict != null && dict.Count > 0)
					{
						var newDict = dict.Where(p => p.Key != "__plang_callback_hash").ToDictionary();
						answers.Add(new ObjectValue(variableName, newDict));
					}
				}
				else
				{

					answers.Add(new ObjectValue(variableName, result));
				}
			}
			else
			{
				foreach (var rv in function.ReturnValues)
				{
					var variableName = rv.VariableName.Replace("%", "");
					var result = memoryStack.Get("request.body." + variableName);
					if (result == null)
					{
						answers.Add(ObjectValue.Nullable(variableName));
					}
					else
					{
						answers.Add(new ObjectValue(variableName, result));
					}
				}
			}


			if (answers.Count == 0)
			{
				return (null, new UserInputError("No answer provided", goalStep, ErrorMessage: new ErrorMessage("No answer provided")));
			}

			return (answers, null);
		}



		public record JsonOptions(NullValueHandling NullValueHandling = NullValueHandling.Include, DateFormatHandling DateFormatHandling = DateFormatHandling.IsoDateFormat,
			string? DateFormatString = null, DefaultValueHandling DefaultValueHandling = DefaultValueHandling.Include, Formatting Formatting = Formatting.Indented,
			ReferenceLoopHandling ReferenceLoopHandling = ReferenceLoopHandling.Ignore);

		[Description("Write out json content. Only choose this method when it's clear user is defining a json output, e.g. `- write out '{name:John}'. Do your best to make sure that TextMessage.Content is valid json. Any %variable% should have double quotes around it. statusCode(like http status code) should be defined by user. type=error should have statusCode between 400-599, depending on text. actor=user|system, channel=default|trace|debug|info(default for log)|warning|error|audit|metric|security|. User can also define his custom channel")]
		public async Task<IError?> WriteJson(TextMessage textMessage, JsonOptions? jsonOptions = null)
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

			textMessage = textMessage with { Content = JsonConvert.SerializeObject(textMessage.Content, settings) };

			return await Write(textMessage);
		}
		/*
		public async Task<IError?> WriteWithStreamInfo(object content, OutputStreamInfo outputStreamInfo)
		{
			IOutputSink os;
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
				await os.SendAsync(new TextMessage(content.ToString()));
			}
			else if (outputStreamInfo.Serializer.Equals("csv", StringComparison.OrdinalIgnoreCase))
			{
				throw new NotImplementedException(ErrorReporting.CreateIssueNotImplemented.ToString());
				
				CsvOptions options = RecordInitializer.FromDictionary<CsvOptions>(new CsvOptions(), outputStreamInfo.Options);

				using var memoryStream = new MemoryStream();
				var writer = new StreamWriter(memoryStream, leaveOpen: true);
				await Modules.FileModule.CsvHelper.WriteToStream(writer, content, options);
				await writer.FlushAsync();

				memoryStream.Position = 0;

				if (os is HttpSink httpOutputStream)
				{
					httpOutputStream.SetContentType("text/csv");
				}
				await memoryStream.CopyToAsync(os.Stream);

			}
			else
			{

				await os.SendAsync(new TextMessage(content.ToString()));
			}
			return null;
		}*/

		[Description("Write appends by default a text message to the target. User can define different actions, but when it is not defined set as 'append'. statusCode(like http status code) should be defined by user. type=error should have statusCode between 400-599, depending on text. actor=user|system.")]
		public async Task<IError?> Write(TextMessage textMessage)
		{


			string stepPath = Path.Join(goalStep.Goal.GoalName, goalStep.Number.ToString()).Replace("\\", "/");
			textMessage = textMessage with { Path = stepPath };

			var sink = context.GetSink(textMessage.Actor);
			return await sink.SendAsync(textMessage);
		}


	}
}
