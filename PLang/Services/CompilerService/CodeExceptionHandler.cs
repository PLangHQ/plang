using Microsoft.AspNetCore.Razor.Language;
using NBitcoin.Protocol;
using PLang.Building.Model;
using PLang.Errors;
using PLang.Errors.AskUser;
using PLang.Errors.Builder;
using PLang.Errors.Runtime;
using PLang.Exceptions;
using PLang.Utils;
using System.Text;
using System.Text.RegularExpressions;

namespace PLang.Services.CompilerService
{
	public class CodeExceptionHandler
	{

		public static IError GetError(Exception ex, ImplementationResponse? implementation, GoalStep step)
		{
			if (implementation == null)
			{
				return new StepBuilderError("implementation was empty", step);
			}
			if (ex is MissingSettingsException mse)
			{
				return new AskUserError(mse.Message, async (object[]? objArray) =>
				{
					var value = "";
					if (objArray != null)
					{
						var obj = objArray[0];
						if (obj is Array) obj = ((object[])obj)[0];
						value = obj.ToString();
					}
					await mse.InvokeCallback(value);
					return (true, null);
				});
			}

			string message = FormatMessage(ex, implementation, step);
			return new StepError(message, step, "CodeException", Exception: ex);

		}

		private static string FormatMessage(Exception ex, ImplementationResponse implementation, GoalStep step)
		{
			string message = "";
			if (ex.InnerException == null)
			{

				message = $@"{ex.Message} in step {step.Text}. 
You might have to define your step bit more, try including variable type, such as %name%(string), %age%(number), %tags%(array).

The C# code is this:
{implementation.Implementation}

";
			}

			var lowestException = ExceptionHelper.GetLowestException(ex);
			if (lowestException.GetType().Namespace != null && lowestException.GetType().Namespace.StartsWith("PLang.Exceptions")) { throw lowestException; }

			var inner = ex.InnerException ?? ex;
			var match = Regex.Match(inner.StackTrace, "cs:line (?<LineNr>[0-9]+)");
			if (!match.Success) return message;

			var strLineNr = match.Groups["LineNr"].Value;
			if (!int.TryParse(strLineNr, out int lineNr)) return message;

			(string errorLine, lineNr) = GetErrorLine(lineNr, implementation, inner.Message);

			message += Environment.NewLine + $@"{inner.Message} in line {lineNr} in C# code 👇. 

You might have to define your step bit more, try including variable type, such as %name%(string), %age%(number), %tags%(array).

The error occured in this line:

	{lineNr}. {errorLine.Trim()}

The C# code is this:
{InsertLineNumbers(implementation.Implementation)}

";
			return message;
		}

		private static string InsertLineNumbers(string code)
		{
			var lines = code.Trim().Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
			for (int i = 0; i < lines.Length; i++)
			{
				// Format line number with padding for alignment, if needed
				lines[i] = $"{i + 1}. {lines[i]}";
			}
			return string.Join(Environment.NewLine, lines);
		}

		private static (string errorLine, int lineNr) GetErrorLine(int lineNr, ImplementationResponse implementation, string message)
		{
			lineNr -= ((implementation.Using != null) ? implementation.Using.Count : 0) + 7;
			string[] codeLines = implementation.Implementation.ReplaceLineEndings().Split(Environment.NewLine);
			if (lineNr == 0) return ("", -1);

			if (codeLines.Length > lineNr && !string.IsNullOrEmpty(codeLines[lineNr]))
			{
				return (codeLines[lineNr], lineNr);
			}

			for (int i = 0; i < codeLines.Length; i++)
			{
				if (codeLines[i].Contains(message)) return (codeLines[i], i);
			}


			return GetErrorLine((lineNr - 1), implementation, message);
		}
	}
}
