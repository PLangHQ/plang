using System.Text.RegularExpressions;
using PLang.Building.Model;
using PLang.Errors;
using PLang.Errors.AskUser;
using PLang.Errors.Runtime;
using PLang.Exceptions;
using PLang.Utils;

namespace PLang.Services.CompilerService;

public class CodeExceptionHandler
{
    public static IError GetError(Exception ex, Implementation? implementation, GoalStep step)
    {
        if (implementation == null)
            implementation = new Implementation("No namespace", "No name provided", "No code provided", null,
                new List<CSharpCompiler.Parameter>(), new List<CSharpCompiler.Parameter>(), null, null);
        if (ex is MissingSettingsException mse)
            return new AskUserError(mse.Message, async objArray =>
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

        var message = FormatMessage(ex, implementation, step);
        return new StepError(message, step, "CodeException", Exception: ex);
    }

    private static string FormatMessage(Exception ex, Implementation implementation, GoalStep step)
    {
        var message = "";
        if (ex.InnerException == null)
            message = $@"{ex.Message} in step {step.Text}. 
You might have to define your step bit more, try including variable type, such as %name%(string), %age%(number), %tags%(array).

The C# code is this:
{implementation.Code}

";

        var lowestException = ExceptionHelper.GetLowestException(ex);
        if (lowestException.GetType().Namespace != null &&
            lowestException.GetType().Namespace.StartsWith("PLang.Exceptions")) throw lowestException;

        var inner = ex.InnerException ?? ex;
        var match = Regex.Match(inner.StackTrace, "cs:line (?<LineNr>[0-9]+)");
        if (!match.Success) return message;

        var strLineNr = match.Groups["LineNr"].Value;
        if (!int.TryParse(strLineNr, out var lineNr)) return message;

        (var errorLine, lineNr) = GetErrorLine(lineNr, implementation, inner.Message);

        message += Environment.NewLine + $@"{inner.Message} in line {lineNr} in C# code 👇. 

You might have to define your step bit more, try including variable type, such as %name%(string), %age%(number), %tags%(array).

The error occured in this line:

	{lineNr}. {errorLine.Trim()}

The C# code is this:
{InsertLineNumbers(implementation.Code)}

";
        return message;
    }

    private static string InsertLineNumbers(string code)
    {
        var lines = code.Trim().Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
        for (var i = 0; i < lines.Length; i++)
            // Format line number with padding for alignment, if needed
            lines[i] = $"{i + 1}. {lines[i]}";
        return string.Join(Environment.NewLine, lines);
    }

    private static (string errorLine, int lineNr) GetErrorLine(int lineNr, Implementation implementation,
        string message)
    {
        lineNr -= (implementation.Using != null ? implementation.Using.Length : 0) + 7;
        string[] codeLines = implementation.Code.ReplaceLineEndings().Split(Environment.NewLine);
        if (lineNr == 0) return ("", -1);

        if (codeLines.Length > lineNr && !string.IsNullOrEmpty(codeLines[lineNr])) return (codeLines[lineNr], lineNr);

        for (var i = 0; i < codeLines.Length; i++)
            if (codeLines[i].Contains(message))
                return (codeLines[i], i);


        return GetErrorLine(lineNr - 1, implementation, message);
    }
}