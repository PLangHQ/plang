using PLang.Services.OutputStream;
using System.ComponentModel;

namespace PLang.Modules.OutputModule
{
    [Description("Outputs and writes out, to the UI a text or a variable. In console, code can ask user and he gives response")]
	public class Program : BaseProgram
	{
		private readonly IOutputStream outputStream;

		public Program(IOutputStream outputStream) : base()
		{
			this.outputStream = outputStream;
		}
		[Description("Send response to user and waits for answer. type can be text|warning|error|info|debug|trace. statusCode(like http status code) should be defined by user.")]
		public async Task<string> Ask(string text, string type = "text", int statusCode = 200)
		{		
			return await outputStream.Ask(text, type, statusCode);			
		}

		[Description("Write to the output. type can be text|warning|error|info|debug|trace. statusCode(like http status code) should be defined by user.")]
		public async Task Write(object? content = null, bool writeToBuffer = false, string type = "text", int statusCode = 200) {
			if (writeToBuffer)
			{
				await outputStream.WriteToBuffer(content, type, statusCode);
			}
			else
			{
				await outputStream.Write(content, type, statusCode);
			}
		}

	}
}
