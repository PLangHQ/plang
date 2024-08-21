﻿using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PLang.Services.OutputStream
{
	

	public class ConsoleOutputStream : IOutputStream
	{
		public ConsoleOutputStream() {
			Console.OutputEncoding = Encoding.UTF8;
		}
		public Stream Stream => Console.OpenStandardOutput();
		public Stream ErrorStream => Console.OpenStandardError();

		public async Task<string> Ask(string text, string type = "text", int statusCode = 104)
		{
			Console.WriteLine(text);

			string line = Console.ReadLine();
			return line;
		}

		public void Dispose()
		{			
		}

		public string Read()
		{
			return Console.ReadLine() ?? "";
		}

		public async Task Write(object? obj, string type = "text", int statusCode = 200)
		{
			if (obj == null) return;

			string content = obj.ToString();
			var fullName = obj.GetType().FullName ?? "";
			if (fullName.IndexOf("[") != -1)
			{
				fullName = fullName.Substring(0, fullName.IndexOf("["));
			}
			SetColor(statusCode);
			if (!content.StartsWith(fullName))
			{
				Console.WriteLine(content);
			}
			else
			{
				Console.WriteLine(JsonConvert.SerializeObject(obj, Formatting.Indented));
			}
			Console.ResetColor();

		}

		public async Task WriteToBuffer(object? obj, string type, int statusCode = 200)
		{
			await Write(obj, type);
		}

		private void SetColor(int statusCode)
		{
			if (statusCode >= 500)
			{
				Console.ForegroundColor = ConsoleColor.Red;
				Console.BackgroundColor = ConsoleColor.Yellow;
			}
			else if (statusCode >= 400)
			{
				Console.ForegroundColor = ConsoleColor.Yellow;
				Console.BackgroundColor = ConsoleColor.Red;
			}
			else if (statusCode >= 300)
			{
				Console.ForegroundColor = ConsoleColor.Magenta;
			}
			else if (statusCode >= 200)
			{
				Console.ResetColor();
			}
			else if (statusCode >= 100)
			{
				Console.ForegroundColor = ConsoleColor.Cyan;
			} else
			{
				Console.ResetColor();
			}
		}
	}
}
