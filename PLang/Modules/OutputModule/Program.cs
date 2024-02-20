using PLang.Services.OutputStream;
using System.ComponentModel;
using System.Text.RegularExpressions;

namespace PLang.Modules.OutputModule
{
	[Description("Outputs and writes out, to the UI a text or a variable. In console, code can ask user and he gives response")]
	public class Program : BaseProgram
	{
		private readonly IOutputStreamFactory outputStream;

		public Program(IOutputStreamFactory outputStream) : base()
		{
			this.outputStream = outputStream;
		}
		[Description("Send response to user and waits for answer. type can be text|warning|error|info|debug|trace. statusCode(like http status code) should be defined by user. errorMessage is message to user when answer does not match expected regexPattern, use good grammar and correct formatting.")]
		public async Task<string> Ask(string text, string type = "text", int statusCode = 200, string? regexPattern = null, string? errorMessage = null)
		{
			var result = await outputStream.CreateHandler().Ask(text, type, statusCode);
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

		[Description("Write to the output. type can be text|warning|error|info|debug|trace. statusCode(like http status code) should be defined by user.")]
		public async Task Write(object? content = null, bool writeToBuffer = false, string type = "text", int statusCode = 200)
		{
			if (writeToBuffer)
			{
				await outputStream.CreateHandler().WriteToBuffer(content, type, statusCode);
			}
			else
			{
				await outputStream.CreateHandler().Write(content, type, statusCode);
			}
		}

	}
}
