using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PLang.Services.OutputStream
{
	public class ConsoleOutputStream : IOutputStream
	{
		public Stream Stream => Console.OpenStandardOutput();
		public Stream ErrorStream => Console.OpenStandardError();

		public async Task<string> Ask(string text, string type = "ask", int statusCode = 104)
		{
		
			Console.WriteLine(text);
			return Console.ReadLine();
		}

		public void Dispose()
		{			
		}

		public string Read()
		{
			return Console.ReadLine();
		}

		public async Task Write(object obj, string type = "text", int statusCode = 200)
		{
			if (obj == null) return;
			if (obj.GetType().IsPrimitive || obj is string)
			{
				Console.WriteLine(obj);
			}
			else
			{
				Console.WriteLine(JsonConvert.SerializeObject(obj, Formatting.Indented));
			}

		}

		public async Task WriteToBuffer(object obj, string type, int statusCode = 200)
		{
			await Write(obj, type);
		}
	}
}
