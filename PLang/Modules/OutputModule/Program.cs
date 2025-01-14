using PLang.Attributes;
using PLang.Errors;
using PLang.Errors.Runtime;
using PLang.Exceptions;
using PLang.Services.OutputStream;
using System.ComponentModel;
using System.Net.Http;
using System.Text.RegularExpressions;

namespace PLang.Modules.OutputModule
{
	[Description("Outputs and writes out, to the UI a text or a variable. In console, code can ask user and he gives response")]
	public class Program : BaseProgram, IDisposable
	{
		private readonly IOutputStreamFactory outputStreamFactory;
		private readonly IOutputSystemStreamFactory outputSystemStreamFactory;

		public Program(IOutputStreamFactory outputStreamFactory, IOutputSystemStreamFactory outputSystemStreamFactory) : base()
		{
			this.outputStreamFactory = outputStreamFactory;
			this.outputSystemStreamFactory = outputSystemStreamFactory;
		}
		[Description("Send response to user and waits for answer. type can be text|warning|error|info|debug|trace. statusCode(like http status code) should be defined by user. regexPattern should contain start and end character if user input needs to match fully. errorMessage is message to user when answer does not match expected regexPattern, use good grammar and correct formatting.")]
		public async Task<(string?, IError?)> Ask(string text, string type = "text", int statusCode = 200, string? regexPattern = null, string? errorMessage = null, Dictionary<string, object>? parameters = null)
		{
			var outputStream = outputStreamFactory.CreateHandler();
			var result = await outputStream.Ask(text, type, statusCode, parameters);
			if (outputStream is JsonOutputStream) return (null, new EndGoal(goalStep, ""));

			// escape any variable that user inputs
			result = result.Replace("%", @"\%");

			if (regexPattern != null && !Regex.IsMatch(result, regexPattern))
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

		/*
		[Description("Write to the system output. type can be text|warning|error|info|debug|trace. statusCode(like http status code) should be defined by user. type=error should have statusCode between 400-599, depending on text")]
		public async Task<IError?> WriteToSystemOutput(object? content = null, bool writeToBuffer = false, string type = "text", int statusCode = 200)
		{
			if (statusCode >= 400)
			{
				await outputSystemStreamFactory.CreateHandler().Write(content, type, statusCode);
			}
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
		
		[Description("Write to the json output. Make sure content is valid JSON format. type can be text|warning|error|info|debug|trace. statusCode(like http status code) should be defined by user. type=error should have statusCode between 400-599, depending on text")]
		public async Task<IError?> WriteJson([HandlesVariable] object? content = null, bool writeToBuffer = false, string type = "text", int statusCode = 200)
		{
			object? ble = variableHelper.LoadVariables(content);
			int i = 0;
			return null;
		}*/

		public record WriteOutput(object content, bool writeToBuffer = false, string type = "text", int statusCode = 200);

		[Description("Write to the output. type can be text|warning|error|info|debug|trace. statusCode(like http status code) should be defined by user. type=error should have statusCode between 400-599, depending on text")]
		public async Task<IError?> Write(WriteOutput writeOutput)
		{
			if (writeOutput.statusCode >= 400)
			{
				//await outputStream.CreateHandler().Write(content, type, statusCode);
				return new UserDefinedError(writeOutput.content.ToString(), goalStep, StatusCode: writeOutput.statusCode);
			}
			if (writeOutput.writeToBuffer)
			{
				await outputStreamFactory.CreateHandler().WriteToBuffer(writeOutput.content, writeOutput.type, writeOutput.statusCode);
			}
			else
			{
				await outputStreamFactory.CreateHandler().Write(writeOutput.content, writeOutput.type, writeOutput.statusCode);
			}
			return null;
		}

	}
}
