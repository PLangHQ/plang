﻿using AngleSharp.Css.Values;
using MimeKit;
using Newtonsoft.Json;
using PLang.Building.Model;
using PLang.Errors;
using PLang.Errors.Runtime;
using PLang.Runtime;
using PLang.Utils;
using System;
using System.Collections.Generic;
using System.Drawing;
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
		public IEngine Engine { get; set; }
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

		public async Task Write(object? obj, string type = "text", int statusCode = 200, Dictionary<string, object?>? parameters = null)
		{
			if (obj == null) return;

			string content = obj.ToString() ?? string.Empty;
			var fullName = obj.GetType().FullName ?? "";
			if (fullName.IndexOf("[") != -1)
			{
				fullName = fullName.Substring(0, fullName.IndexOf("["));
			}

			if (parameters != null)
			{
				long? position = parameters.FirstOrDefault(p => p.Key.Equals("position", StringComparison.OrdinalIgnoreCase)).Value as long?;
				if (position != null) {
					Console.SetCursorPosition(0, Console.CursorTop - (int)position);
				}
			}

			SetColor(statusCode, parameters);

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

		private void SetColor(int statusCode, Dictionary<string, object?>? parameters = null)
		{
			string? color = null;
			string? background = null;
			if (parameters != null)
			{
				color = parameters.FirstOrDefault(p => p.Key.Equals("color", StringComparison.OrdinalIgnoreCase)).Value as string;
				background = parameters.FirstOrDefault(p => p.Key.Equals("background", StringComparison.OrdinalIgnoreCase)).Value as string;

				if (!string.IsNullOrEmpty(color))
				{
					if (Enum.TryParse(typeof(ConsoleColor), color.Replace(" ", ""), true, out object? result))
					{
						Console.ForegroundColor = (ConsoleColor)result;
					}
				}
				if (!string.IsNullOrEmpty(background))
				{
					if (Enum.TryParse(typeof(ConsoleColor), background.Replace(" ", ""), true, out object? result))
					{
						Console.BackgroundColor = (ConsoleColor)result;
					}
				}

				if (color != null || background != null) return;
			}			

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
