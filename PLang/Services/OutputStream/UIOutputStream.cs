using Microsoft.AspNetCore.Razor.Language;
using PLang.Building.Model;
using PLang.Runtime;
using RazorEngineCore;
using System.Dynamic;
using System.IO;
using System.Text;

namespace PLang.Services.OutputStream
{
	public class UIOutputStream : IOutputStream
	{
		private readonly IRazorEngine razorEngine;

		public MemoryStack? MemoryStack { get; internal set; }
		public Goal? Goal { get; internal set; }
		public GoalStep? GoalStep { get; internal set; }
		public Stream Stream { get; private set; }
		public Stream ErrorStream { get; private set; }
		StringBuilder sb;
		public UIOutputStream(IRazorEngine razorEngine)
		{
			this.razorEngine = razorEngine;
			Stream = new MemoryStream();
			ErrorStream = new MemoryStream();

			sb = new StringBuilder(); 
		}

		public async Task<string> Ask(string text, string type = "ask", int statusCode = 104)
		{
			return "";
			//throw new NotImplementedException();
		}

		public void Flush()
		{
			if (sb.Length == 0) return;

			sb.Append(@"</body>
</html>");
			byte[] bytes = Encoding.UTF8.GetBytes(sb.ToString());

			Stream.Write(bytes, 0, bytes.Length);
			sb.Clear();
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
			var expandoObject = new ExpandoObject() as IDictionary<string, object>;
			foreach (var kvp in MemoryStack.GetMemoryStack())
			{
				expandoObject.Add(kvp.Key, kvp.Value.Value);
			}

			IRazorEngineCompiledTemplate compiled = null;
			try
			{
				compiled = await razorEngine.CompileAsync(obj.ToString(), (compileOptions) => {
					compileOptions.Options.IncludeDebuggingInfo = true; 
				});

				var content = compiled.Run(expandoObject as dynamic);
				if (sb.Length == 0)
				{
					string html = $@"<!DOCTYPE html>
<html lang=""en"">

<head>
    <meta charset=""UTF-8"">
    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
    <title></title>
	<link href=""https://cdn.jsdelivr.net/npm/bootstrap@5.1.3/dist/css/bootstrap.min.css"" rel=""stylesheet"" integrity=""sha384-1BmE4kWBq78iYhFldvKuhfTAU6auU8tT94WrHftjDbrCEXSU1oBoqyl2QvZ6jIW3"" crossorigin=""anonymous"">
	<script src=""https://cdn.jsdelivr.net/npm/bootstrap@5.1.3/dist/js/bootstrap.bundle.min.js"" integrity=""sha384-ka7Sk0Gln4gmtz2MlQnikT1wXgYsOg+OMhuP+IlRH9sENBO0LRn5q+8nbTov4+1p"" crossorigin=""anonymous""></script>
<script src=""https://cdn.jsdelivr.net/npm/@fortawesome/fontawesome-free@5.15.0/js/all.min.js""></script>
<link href=""https://cdn.jsdelivr.net/npm/@fortawesome/fontawesome-free@5.15.0/css/fontawesome.min.css"" rel=""stylesheet"">
<script>
		function callGoal(goalName, args) {{
console.log(args);
			window.chrome.webview.postMessage({{GoalName:goalName, args:args}});
		}}
</script>
<style>body{{margin:2rem;}}</style>
</head>

<body>
";
					sb.Append(html);
					sb.Append(content);
				} else
				{
					if (stepNr == -1)
					{
						sb.Append(content.ToString());
					} else
					{
						sb.Replace($"{{step{stepNr}}}", content.ToString());
					}

				}
				//byte[] bytes = Encoding.UTF8.GetBytes(html);
					
				//await Stream.WriteAsync(bytes, 0, bytes.Length);
				
			}
			catch (Exception ex)
			{
				ErrorStream = new MemoryStream();

				string errorMessage = ex.Message;
				string cshtmlFile = ex.Source;
				string stackTrace = ex.StackTrace;
				int line = 0;
				string searchIndex = "cshtml:line";
				int lineIdx = stackTrace.IndexOf(searchIndex);
				if (lineIdx != -1) {
					int.TryParse(stackTrace.Substring(lineIdx + searchIndex.Length, stackTrace.IndexOf(Environment.NewLine) - lineIdx - searchIndex.Length).Trim(), out line); ;
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
						error += Environment.NewLine + Environment.NewLine + lines[line - 1];
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
				}
			}
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
