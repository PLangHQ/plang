using PLang.Building.Model;
using PLang.Exceptions;
using System.Text.RegularExpressions;

namespace PLang.Services.CompilerService
{
	public class CodeExceptionHandler
	{

		public static void Handle(Exception ex, Implementation implementation, GoalStep step)
		{
			if (ex.InnerException == null)
			{
				throw new RuntimeStepException($@"{ex.Message} in step {step.Text}. 
You might have to define your step bit more, try including variable type, such as %name%(string), %age%(number), %tags%(array).

The C# code is this:
{implementation.Code}

", step);
			}

			var inner = ex.InnerException;
			var match = Regex.Match(inner.StackTrace, "cs:line (?<LineNr>[0-9]+)");
			if (!match.Success) return;

			var strLineNr = match.Groups["LineNr"].Value;
			if (!int.TryParse(strLineNr, out int lineNr)) return;

			(string errorLine, lineNr) = GetErrorLine(lineNr, implementation, inner.Message);

			throw new RuntimeStepException($@"{inner.Message} in line: {lineNr}. You might have to define your step bit more, try including variable type, such as %name%(string), %age%(number), %tags%(array).
The error occured in this line:
{errorLine}

The C# code is this:
{implementation.Code}

", step);

		}


		private static (string errorLine, int lineNr) GetErrorLine(int lineNr, Implementation implementation, string message)
		{
			lineNr -= (implementation.Using.Length + 4);
			string[] codeLines = implementation.Code.ReplaceLineEndings().Split(Environment.NewLine);
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
