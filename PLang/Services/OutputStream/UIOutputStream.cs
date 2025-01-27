using Newtonsoft.Json;
using PLang.Building.Model;
using PLang.Interfaces;
using PLang.Runtime;
using RazorEngineCore;
using System.IO;
using System.IO.Abstractions;
using System.Text;

namespace PLang.Services.OutputStream
{
	public class UIOutputStream : IOutputStream
	{
		private readonly IFileSystem fileSystem;

		public MemoryStack? MemoryStack { get; internal set; }
		public Goal? Goal { get; internal set; }
		public GoalStep? GoalStep { get; internal set; }
		public Stream Stream { get; private set; }
		public Stream ErrorStream { get; private set; }
		StringBuilder sb;
		public Action<string>? onFlush { get; set; }
		public IForm IForm { get; set; }
		public UIOutputStream(IFileSystem fileSystem, IForm iForm)
		{
			this.fileSystem = fileSystem;
			IForm = iForm;
			Stream = new MemoryStream();
			ErrorStream = new MemoryStream();

			sb = new StringBuilder();
		}

		public async Task<string> Ask(string text, string type = "ask", int statusCode = 104, Dictionary<string, object>? parameters = null)
		{
			return "";
			//throw new NotImplementedException();
		}

		public async Task Execute(string javascriptToCall)
		{

		}

		public void Flush()
		{
			if (!Stream.CanRead) return;

			string str = "";			
			using (var tw = new StreamReader(Stream, leaveOpen: true))
			{
				Stream.Position = 0;
				str = tw.ReadToEnd();

				Stream.SetLength(0); // Truncate the stream
				Stream.Position = 0;
			}

			IForm.SynchronizationContext.Post(_ =>
			{
				int be = 0;
				try
				{
					IForm.Flush(str);
				} catch (Exception e)
				{
					int i = 0;
				}
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
			if (statusCode == 200)
			{
				byte[] bytes;
				if (obj is string || obj.GetType().IsPrimitive) {
					bytes = Encoding.UTF8.GetBytes(obj.ToString());
				} else {
					bytes = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(obj));
				}

				Stream.Write(bytes, 0, bytes.Length);
				
			}
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
