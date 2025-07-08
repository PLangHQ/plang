using MimeKit;
using Newtonsoft.Json;
using PLang.Building.Model;
using PLang.Errors;
using PLang.Errors.Runtime;
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
		public GoalStep Step { get; set; }
		public string Output { get => "text"; }
		public bool IsStateful { get { return true; } }

		public bool IsFlushed { get;set; }

		public async Task<(object?, IError?)> Ask(AskOptions askOptions, Callback? callback = null, IError? error = null)
		{
			string? strOptions = GetStringFormattedOptions(askOptions.Choices);
			SetColor(askOptions.StatusCode);
			if (error != null)
			{
				Console.WriteLine(error);
			}

			Console.WriteLine($"[Ask] {askOptions.Question}{strOptions}");
			IsFlushed = true;

			object? answer = Console.ReadLine();
			Console.ResetColor();
			
			if (strOptions != null && askOptions.Choices != null)
			{
				if (answer == null)
				{
					return await Ask(askOptions, callback, new UserInputError($"'{answer}' is not valid answer", Step));
				}

				var option = askOptions.Choices.FirstOrDefault(p => p.Key.Equals(answer.ToString().Trim(), StringComparison.OrdinalIgnoreCase));
				if (option.Key == null)
				{
					return await Ask(askOptions, callback, new UserInputError($"{answer} is not valid answer", Step));
				}

			}

			return (answer, null);
		}

		private string? GetStringFormattedOptions(Dictionary<string, string>? choices)
		{
			if (choices == null) return null;

			string? strOptions = null;
			foreach (var option in choices)
			{
				if (option.Key != option.Value)
				{
					strOptions += $"\n\t{option.Key}. {option.Value}";
				} else
				{
					strOptions += $"\n\t{option.Key}.";
				}
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

			string content = obj.ToString() ?? string.Empty;
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
				if (!TypeHelper.IsConsideredPrimitive(obj.GetType()) && !TypeHelper.IsRecordWithToString(obj))
				{
					Console.WriteLine(ToJson(obj));
				}
				else
				{
					Console.WriteLine(content);
				}
			}
			else
			{
				Console.WriteLine(ToJson(obj));
			}
			Console.ResetColor();
			IsFlushed = true;
		}

		private string ToJson(object obj) {
			JsonSerializerSettings settings = new JsonSerializerSettings()
			{
				Formatting = Formatting.Indented,
				ReferenceLoopHandling = ReferenceLoopHandling.Ignore,
			};
			return JsonConvert.SerializeObject(obj, settings);
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
