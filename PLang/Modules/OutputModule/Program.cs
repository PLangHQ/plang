using Newtonsoft.Json;
using PLang.Attributes;
using PLang.Errors;
using PLang.Errors.Runtime;
using PLang.Exceptions;
using PLang.Models;
using PLang.Services.OutputStream;
using System.ComponentModel;
using System.Net.Http;
using System.Text.RegularExpressions;

namespace PLang.Modules.OutputModule
{
	[Description("Writes to the output stream. Ask user a question, either straight or with help of llm. output stream can be to the user(default), system, log, audit, metric")]
	public class Program : BaseProgram, IDisposable
	{
		private readonly IOutputStreamFactory outputStreamFactory;
		private readonly IOutputSystemStreamFactory outputSystemStreamFactory;

		public Program(IOutputStreamFactory outputStreamFactory, IOutputSystemStreamFactory outputSystemStreamFactory) : base()
		{
			this.outputStreamFactory = outputStreamFactory;
			this.outputSystemStreamFactory = outputSystemStreamFactory;
		}

		[Description("Uses llm to construct a question to user and to format the answer")]
		public async Task<(ReturnDictionary<string, object?>?, IError?)> AskUserUsingLlm(string text, string type = "text", int statusCode = 202, 
			string? developerInstructionForResult = "give me the object that matches, e.g. { \"id\": 123, \"name\": \"example\"}", [HandlesVariable] Dictionary<string, object?>? options = null)
		{
			var callGoalModule = GetProgramModule<CallGoalModule.Program>();
			var param = new Dictionary<string, object?> { { "type", type }, { "statusCode", statusCode }, { "text", text }, { "developerInstructionForResult", developerInstructionForResult } };
			if (options != null)
			{
				string json = "";
				foreach (var option in options)
				{
					json += option.Key + "\n" + JsonConvert.SerializeObject(option.Value);
				}
				param.Add("options", json);
			}
			
			return await callGoalModule.RunGoal("/modules/OutputModule/AskUserLlm", param, isolated: true);
		}

		[Description("Send response to user and waits for answer. type can be text|warning|error|info|debug|trace. statusCode(like http status code) should be defined by user. regexPattern should contain start and end character if user input needs to match fully. regexPattern can contain %variable%. errorMessage is message to user when answer does not match expected regexPattern, use good grammar and correct formatting.")]
		public async Task<(string?, IError?)> Ask(string text, string type = "text", int statusCode = 202, string? regexPattern = null, string? errorMessage = null, Dictionary<string, object>? parameters = null)
		{
			var outputStream = outputStreamFactory.CreateHandler();
			var result = await outputStream.Ask(text, type, statusCode, parameters);
			if (outputStream is JsonOutputStream) return (null, new EndGoal(goalStep, ""));

			// escape any variable that user inputs
			result = result.Replace("%", @"\%");

			regexPattern = variableHelper.LoadVariables(regexPattern)?.ToString() ?? null;
			if (!string.IsNullOrEmpty(regexPattern) && !Regex.IsMatch(result, regexPattern))
			{
				if (errorMessage != null && !text.Contains(errorMessage))
				{
					text = errorMessage + "\n\n" + text;
				}
				return await Ask(text, type, statusCode, regexPattern, errorMessage, parameters);
			}
			return (result, null);
		}

		public void Dispose()
		{
			var stream = outputStreamFactory.CreateHandler();
			if (stream is IDisposable disposable)
			{
				disposable.Dispose();
			}

		}

		[Description("Write to the system output. type can be text|warning|error|info|debug|trace. statusCode(like http status code) should be defined by user. type=error should have statusCode between 400-599, depending on text")]
		public async Task<IError?> WriteToSystemOutput(object? content = null, bool writeToBuffer = false, string type = "text", int statusCode = 200)
		{

			if (writeToBuffer)
			{
				await outputSystemStreamFactory.CreateHandler().WriteToBuffer(content, type, statusCode);
			}
			else
			{
				await outputSystemStreamFactory.CreateHandler().Write(content, type, statusCode);
			}

			return null;
		}


		[Description("Write to the output. type can be text|warning|error|info|debug|trace. statusCode(like http status code) should be defined by user. type=error should have statusCode between 400-599, depending on text")]
		public async Task<IError?> Write(object content, bool writeToBuffer = false, string type = "text", int statusCode = 200, Dictionary<string, object?>? paramaters = null)
		{
			if (statusCode >= 400)
			{
				if (content == null) return new UserDefinedError("Content is null", goalStep, StatusCode: statusCode);
				//await outputStream.CreateHandler().Write(content, type, statusCode);
				return new UserDefinedError(content?.ToString(), goalStep, StatusCode: statusCode);
			}
			if (writeToBuffer)
			{
				await outputStreamFactory.CreateHandler().WriteToBuffer(content, type, statusCode);
			}
			else
			{
				await outputStreamFactory.CreateHandler().Write(content, type, statusCode);
			}
			return null;
		}

	}
}
