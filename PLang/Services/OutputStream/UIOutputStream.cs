using Microsoft.AspNetCore.Razor.Language;
using NBitcoin.Secp256k1;
using Org.BouncyCastle.Utilities.IO;
using Org.BouncyCastle.Utilities.Zlib;
using PLang.Building.Model;
using PLang.Interfaces;
using PLang.Modules.UiModule;
using PLang.Runtime;
using RazorEngineCore;
using System.Dynamic;
using System.IO;
using System.IO.Abstractions;
using System.Text;

namespace PLang.Services.OutputStream
{
	public class UIOutputStream : IOutputStream
	{
		private readonly IRazorEngine razorEngine;
		private readonly IFileSystem fileSystem;

		public MemoryStack? MemoryStack { get; internal set; }
		public Goal? Goal { get; internal set; }
		public GoalStep? GoalStep { get; internal set; }
		public Stream Stream { get; private set; }
		public Stream ErrorStream { get; private set; }
		StringBuilder sb;
		public Action<string>? onFlush { get; set; }
		public IForm IForm { get; set; }
		public UIOutputStream(IRazorEngine razorEngine, IFileSystem fileSystem, IForm iForm)
		{
			this.razorEngine = razorEngine;
			this.fileSystem = fileSystem;
			IForm = iForm;
			Stream = new MemoryStream();
			ErrorStream = new MemoryStream();

			sb = new StringBuilder();
		}

		public async Task<string> Ask(string text, string type = "ask", int statusCode = 104)
		{
			return "";
			//throw new NotImplementedException();
		}

		public async Task Execute(string javascriptToCall)
		{

		}

		public void Flush()
		{
			int i = 0;
			IForm.SynchronizationContext.Post(_ =>
			{
				int be = 0;

				IForm.Flush();
			}, null);

		}

		public string Read()
		{
			return "";
		}

		public async Task Write(object? obj, string type = "text", int statusCode = 200)
		{
			await Write(obj, type, statusCode, -1);
		}



		public async Task Write(object? obj, string type = "text", int statusCode = 200, int stepNr = -1)
		{
			if (obj == null) return;
			if (statusCode >= 300)
			{
				byte[] bytes = Encoding.UTF8.GetBytes(sb.ToString());

				ErrorStream.Write(bytes, 0, bytes.Length);
				// the app can listen to the ErrorStream
				// when it happens, get the info
				// send info to llm with question how to solve with relavant information
				// get suggestion
				// execute suggestion
				// this is auto recovering software

				// just random thought about output stream
				// this could be audio stream, convert audio to text
				// user: "play Radiohead on spotify"
				// input sends to llm, llm responds {app:spotify, search: Radiohead, options: %options%, action:PlayDefaultList}
				// google home plays Radiohead on spotify, since you are subscriber there
				// or write the same in search bar
				return;
			}

			await IForm.ExecuteCode($"showNotification('{obj.ToString().Replace("'", "\\'")}');");
			//byte[] bytes = Encoding.UTF8.GetBytes(html);

			//await Stream.WriteAsync(bytes, 0, bytes.Length);
			/*

			ErrorStream = new MemoryStream();

			string errorMessage = ex.Message;
			string cshtmlFile = ex.Source;
			string stackTrace = ex.StackTrace;
			int line = 0;
			string searchIndex = "cshtml:line";
			int lineIdx = stackTrace.IndexOf(searchIndex);
			if (lineIdx != -1)
			{
				int endIdx = stackTrace.IndexOf(Environment.NewLine) - lineIdx - searchIndex.Length;
				int startIdx = lineIdx + searchIndex.Length;
				if (endIdx > 0 && startIdx > 0 && startIdx < stackTrace.Length)
				{
					int.TryParse(stackTrace.Substring(startIdx, endIdx).Trim(), out line); ;
				}
			}
			if (compiled != null)
			{
				var ms = new MemoryStream();
				compiled.SaveToStream(ms);
				ms.Position = 0;

				ReadLong(ms);
				SkipBuffer(ms); // Skip assembly bytecode
				SkipBuffer(ms);
				SkipBuffer(ms);

				string sourceCode = ReadString(ms);
				string[] lines = sourceCode.Split("\n");

				string error = $@"{errorMessage} at line: {line}";
				if (lines.Length > line)
				{
					int lineIndex = (line - 1 >= 0) ? line : 0;
					error += Environment.NewLine + Environment.NewLine + lines[lineIndex];
				}
				error += Environment.NewLine + Environment.NewLine + $@"Following is the generated source code:

{sourceCode}

# plang code being executed #
{GoalStep.Text}
# plang code being executed #

# full plang source code #
{Goal.GetGoalAsString()}
# full plang source code #
";

				byte[] bytes = Encoding.UTF8.GetBytes(error);
				await ErrorStream.WriteAsync(bytes, 0, bytes.Length);

				int i = 0;


			}
			else
			{

				throw;
			}*/
		}




		public async Task WriteToBuffer(object? obj, string type = "text", int statusCode = 200)
		{
			await Write(obj, type, statusCode);
		}







		long ReadLong(Stream stream)
		{
			byte[] buffer = new byte[8];
			stream.Read(buffer, 0, buffer.Length);
			return BitConverter.ToInt64(buffer, 0);
		}

		void SkipBuffer(Stream stream)
		{
			long length = ReadLong(stream);
			if (length > 0)
			{
				stream.Seek(length, SeekOrigin.Current);
			}
		}

		string ReadString(Stream stream)
		{
			long length = ReadLong(stream);
			if (length > 0)
			{
				byte[] buffer = new byte[length];
				stream.Read(buffer, 0, (int)length);
				return Encoding.UTF8.GetString(buffer);
			}
			return null;
		}


	}
}
