using MimeKit;
using Newtonsoft.Json;
using PLang.Errors;
using PLang.Runtime;
using PLang.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using static PLang.Modules.OutputModule.Program;
using static PLang.Utils.StepHelper;

namespace PLang.Services.OutputStream
{


	public class ConsoleOutputStream : IOutputStream
	{
		Stream standardOutputStream;
		Stream standardErrorStream;

		public ConsoleOutputStream()
		{
			// todo: dont think this is good, why override encoding?
			Console.OutputEncoding = Encoding.UTF8;
			Console.InputEncoding = Encoding.UTF8;

			standardOutputStream = Console.OpenStandardOutput();
			standardErrorStream = Console.OpenStandardError();
		}
		public Stream Stream => standardOutputStream;
		public Stream ErrorStream => standardErrorStream;

		public string Output { get => "text"; }
		public bool IsStateful { get { return true; } }

		public async Task<(string?, IError?)> Ask(string text, string type = "text", int statusCode = 202, Dictionary<string, object>? parameters = null,
			Callback? callback = null, List<Option>? options = null)
		{
			string? strOptions = GetStringFormattedOptions(options);
			SetColor(statusCode);

			Console.WriteLine($"[Ask] {text}{strOptions}");

			string? line = Console.ReadLine();
			Console.ResetColor();

			return (line, null);
		}

		private string? GetStringFormattedOptions(List<Option>? options)
		{
			if (options == null) return null;

			string? strOptions = null;
			foreach (var option in options)
			{
				strOptions += $"\n\t{option.ListNumber}. {option.SelectionInfo}";
			}
			return strOptions;
		}

		public string Read()
		{
			return Console.ReadLine() ?? "";
		}

		public async Task Write(object? obj, string type = "text", int statusCode = 200, Dictionary<string, object?>? paramaters = null)
		{
			if (obj == null) return;

			string content = obj.ToString();
			var fullName = obj.GetType().FullName ?? "";
			if (fullName.IndexOf("[") != -1)
			{
				fullName = fullName.Substring(0, fullName.IndexOf("["));
			}
			SetColor(statusCode);
			if (paramaters != null && paramaters.TryGetValue("Position", out object? value))
			{
				Console.SetCursorPosition(0, Console.CursorTop - (int)value);
			}

			if (!content.StartsWith(fullName))
			{
				if (TypeHelper.IsRecordWithoutToString(obj))
				{
					Console.WriteLine(JsonConvert.SerializeObject(obj, Formatting.Indented));
				}
				else
				{
					Console.WriteLine(content);
				}
			}
			else
			{
				Console.WriteLine(JsonConvert.SerializeObject(obj, Formatting.Indented));
			}
			Console.ResetColor();

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
			}
			else
			{
				Console.ResetColor();
			}
		}
	}
}
