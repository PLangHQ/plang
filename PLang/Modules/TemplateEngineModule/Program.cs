using System.Text.RegularExpressions;
using PLang.Errors;
using PLang.Errors.Runtime;
using PLang.Interfaces;
using PLang.Utils;
using Scriban;
using Scriban.Runtime;
using Scriban.Syntax;

namespace PLang.Modules.TemplateEngineModule;

public class Program : BaseProgram
{
    private readonly IPLangFileSystem fileSystem;

    public Program(IPLangFileSystem fileSystem)
    {
        this.fileSystem = fileSystem;
    }

    public async Task<(string?, IError?)> RenderFile(string path)
    {
        var fullPath = GetPath(path);
        if (!fileSystem.File.Exists(fullPath))
            return (null,
                new ProgramError($"File {path} could not be found. Full path to the file is {fullPath}", goalStep,
                    function));
        var content = fileSystem.File.ReadAllText(fullPath);
        return await RenderContent(content, fullPath);
    }

    public async Task<(string?, IError?)> RenderContent(string content, string fullPath)
    {
        var templateContext = new TemplateContext();


        templateContext.MemberRenamer = member => member.Name;


        var scriptObject = new ScriptObject();
        scriptObject.Import("date_format", new Func<object, string, string>((input, format) =>
        {
            if (input is DateTime dateTime) return dateTime.ToString(format);

            if (input is string str && DateTime.TryParse(str, out var parsedDate)) return parsedDate.ToString(format);
            return input?.ToString() ?? string.Empty;
        }));

        // Push the custom function into the template context
        templateContext.PushGlobal(scriptObject);


        foreach (var kvp in memoryStack.GetMemoryStack())
        {
            var sv = ScriptVariable.Create(kvp.Key, ScriptVariableScope.Global);
            templateContext.SetValue(sv, kvp.Value.Value);
        }

        try
        {
            var parsed = Template.Parse(content);
            var result = await parsed.RenderAsync(templateContext);

            return (result, null);
        }
        catch (ScriptRuntimeException ex)
        {
            var relativeFilePath = fullPath.AdjustPathToOs().Replace(fileSystem.RootDirectory, "");
            var innerException = ex.InnerException as ScriptRuntimeException ?? ex;
            string message;
            var pattern = @"\((\d+),(\d+)\)";
            var match = Regex.Match(innerException.Message, pattern);
            if (match.Success)
            {
                int.TryParse(match.Groups[1].Value?.Trim(), out var lineNumber);
                int.TryParse(match.Groups[2].Value?.Trim(), out var columnNumber);

                message =
                    $"{innerException.OriginalMessage} in {relativeFilePath} - line: {lineNumber} | column: {columnNumber}";
                var lines = content.Split('\n');
                if (lines.Length > lineNumber)
                {
                    var startPos = lineNumber - 3;
                    if (startPos < 0) startPos = 0;

                    var errorLines = lines.ToList().Skip(startPos).Take(lineNumber - startPos + 2);
                    foreach (var errorLine in errorLines) message += $"\n{startPos++}: {errorLine}";
                }
            }
            else
            {
                message = $"{ex.Message} in {relativeFilePath}";
            }

            var pe = new ProgramError(message,
                goalStep, function, Exception: ex,
                HelpfulLinks:
                @"Description of the language syntax: https://github.com/scriban/scriban/blob/master/doc/language.md
Built-in functions: https://github.com/scriban/scriban/blob/master/doc/builtins.md
Runtime documentation: https://github.com/scriban/scriban/blob/master/doc/runtime.md"
            );

            return (null, pe);
        }
    }
}