﻿using Microsoft.AspNetCore.Razor.Language;
using PLang.Building.Model;
using PLang.Exceptions;
using PLang.Utils;
using System.Text;
using System.Text.RegularExpressions;

namespace PLang.Services.CompilerService
{
	public class CodeExceptionHandler
	{

		public static void Handle(Exception ex, Implementation? implementation, GoalStep step)
		{
			if (implementation == null)
			{
				implementation = new Implementation("No namespace", "No name provided", "No code provided", null, new(), new(), null, null);
			}

			if (ex.InnerException == null)
			{
				throw new RuntimeStepException($@"{ex.Message} in step {step.Text}. 
You might have to define your step bit more, try including variable type, such as %name%(string), %age%(number), %tags%(array).

The C# code is this:
{implementation.Code}

", step);
			}

			var lowestException = ExceptionHelper.GetLowestException(ex);
			if (lowestException.GetType().Namespace != null && lowestException.GetType().Namespace.StartsWith("PLang.Exceptions")) { throw lowestException; }

			var inner = ex.InnerException;
			var match = Regex.Match(inner.StackTrace, "cs:line (?<LineNr>[0-9]+)");
			if (!match.Success) return;

			var strLineNr = match.Groups["LineNr"].Value;
			if (!int.TryParse(strLineNr, out int lineNr)) return;

			(string errorLine, lineNr) = GetErrorLine(lineNr, implementation, inner.Message);

			throw new RuntimeStepException($@"{inner.Message} in line {lineNr} in C# code 👇. 

You might have to define your step bit more, try including variable type, such as %name%(string), %age%(number), %tags%(array).

The error occured in this line:

	{lineNr}. {errorLine.Trim()}

The C# code is this:
{InsertLineNumbers(implementation.Code)}

", step, inner);

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

		private static (string errorLine, int lineNr) GetErrorLine(int lineNr, Implementation implementation, string message)
		{
			lineNr -= ((implementation.Using != null) ? implementation.Using.Length : 0) + 4;
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
