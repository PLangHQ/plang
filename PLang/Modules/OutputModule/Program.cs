using PLang.Errors;
using PLang.Services.OutputStream;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;
using PLang.Services.Channels;

namespace PLang.Modules.OutputModule
{
	public class AskProperties
	{
		[Required]
		public string Question { get; set; }
		public MessageType Type { get; set; } = MessageType.UserAsk;
		public int StatusCode { get; set; } = 200;
		public string? RegexPattern { get; set; } = null;
		public string? ErrorMessage { get; set; } = null;
		[Description("Allows user to add custom arguments")]
		public Dictionary<string, object>? Args { get; set; } = null;
		[NonSerialized]
		public bool HasError = false;
	}


	public class WriteProperties
	{
		public object Data { get; set; }
		public MessageType MessageType { get; set; } = MessageType.SystemAudit;
		public int StatusCode { get; set; } = 200;
		public Dictionary<string, object>? Args = null;
	}
	
	
	[Description("Outputs and writes out, to the UI a text or a variable. In console, code can ask user and he gives response")]
	public class Program : BaseProgram, IDisposable
	{
		private readonly IOutputStreamFactory outputStream;
		private readonly IOutputSystemStreamFactory outputSystemStream;
		private readonly ChannelManager channelManager;

		public Program(IOutputStreamFactory outputStream, IOutputSystemStreamFactory outputSystemStream, ChannelManager channelManager) : base()
		{
			this.outputStream = outputStream;
			this.outputSystemStream = outputSystemStream;
			this.channelManager = channelManager;
		}

		public Task<IError?> Write(string location, WriteProperties writeProperties)
		{
			/*
			 * return await channel.Write({content, type, statusCode, callBack})
			 */
			var output = channelManager.GetChannel(writeProperties.MessageType);
			if (output.Error != null) return Task.FromResult(output.Error);
			
			var error = output.Channel!.Write(writeProperties);
			return error;
		}

		
		
		
		public async Task<(string? Answer, IError? Error)> Ask(AskProperties askProperties)
		{
			/*
			 * return await channel.Ask({content, type, statusCode, callBack})
			 */
			
			var input = channelManager.GetChannel(askProperties.Type);
			if (input.Error != null) return (null, input.Error);
			
			var answer = await input.Channel!.Ask(askProperties);
			if (answer == null) return (null, null);
			
			// escape any variable that user inputs
			answer = answer.Replace("%", @"\%");
			if (askProperties.RegexPattern != null && !Regex.IsMatch(answer, askProperties.RegexPattern))
			{
				askProperties.HasError = true;
				return await Ask(askProperties);
			}
			return (answer, null);
		}
		
		
		/*
		[Description("Send response to user and waits for answer. type can be text|warning|error|info|debug|trace. statusCode(like http status code) should be defined by user. regexPattern should contain start and end character if user input needs to match fully. errorMessage is message to user when answer does not match expected regexPattern, use good grammar and correct formatting.")]
		public async Task<string> Ask(string text, string type = "text", int statusCode = 200, string? regexPattern = null, string? errorMessage = null)
		{
			var result = await outputSystemStream.CreateHandler().Ask(text, type, statusCode);
			
			// escape any variable that user inputs
			result = result.Replace("%", @"\%");

			if (regexPattern != null && !Regex.IsMatch(result, regexPattern))
			{
				if (errorMessage != null && !text.Contains(errorMessage))
				{
					text = errorMessage + "\n\n" + text;
				}
				return await Ask(text, type, statusCode, regexPattern, errorMessage);
			}
			return result;
		}
*/
		public void Dispose()
		{
			/*
			var stream = outputStream.CreateHandler();
			if (stream is IDisposable disposable)
			{
				disposable.Dispose();
			}*/
			
		}
		
		
		/*
		[Description("Write to the system output. type can be text|warning|error|info|debug|trace. statusCode(like http status code) should be defined by user. type=error should have statusCode between 400-599, depending on text")]
		public async Task<IError?> WriteToSystemOutput(object? content = null, bool writeToBuffer = false, string type = "text", int statusCode = 200)
		{
			if (statusCode >= 400)
			{
				await outputSystemStream.CreateHandler().Write(content, type, statusCode);
			}
			if (writeToBuffer)
			{
				await outputSystemStream.CreateHandler().WriteToBuffer(content, type, statusCode);
			}
			else
			{
				await outputSystemStream.CreateHandler().Write(content, type, statusCode);
			}
			return null;
		}

		[Description("Write to the json output. Make sure content is valid JSON format. type can be text|warning|error|info|debug|trace. statusCode(like http status code) should be defined by user. type=error should have statusCode between 400-599, depending on text")]
		public async Task<IError?> WriteJson([HandlesVariable] object? content = null, bool writeToBuffer = false, string type = "text", int statusCode = 200)
		{
			object? ble = variableHelper.LoadVariables(content);
			int i = 0;
			return null;
		}

		[Description("Write to the output. type can be text|warning|error|info|debug|trace. statusCode(like http status code) should be defined by user. type=error should have statusCode between 400-599, depending on text")]
		public async Task<IError?> Write(object? content = null, bool writeToBuffer = false, string type = "text", int statusCode = 200)
		{
			if (statusCode >= 400)
			{
				//await outputStream.CreateHandler().Write(content, type, statusCode);
				return new UserDefinedError(content.ToString(), goalStep, StatusCode: statusCode);
			}
			if (writeToBuffer)
			{
				await outputStream.CreateHandler().WriteToBuffer(content, type, statusCode);
			}
			else
			{
				await outputStream.CreateHandler().Write(content, type, statusCode);
			}
			return null;
		}*/

	}
}
