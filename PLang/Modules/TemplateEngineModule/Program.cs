using PLang.Errors;
using PLang.Errors.Runtime;
using PLang.Interfaces;
using PLang.Utils;
using RazorEngineCore;
using Scriban;
using Scriban.Syntax;
using System.Dynamic;
using System.Text.RegularExpressions;

namespace PLang.Modules.TemplateEngineModule
{
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
			{

				return (null, new ProgramError($"File {path} could not be found. Full path to the file is {fullPath}", goalStep, this.function));
			}
			var expandoObject = new ExpandoObject() as IDictionary<string, object?>;
			var templateContext = new TemplateContext();
			foreach (var kvp in memoryStack.GetMemoryStack())
			{
				expandoObject.Add(kvp.Key, kvp.Value.Value);

				var sv = ScriptVariable.Create(kvp.Key, ScriptVariableScope.Global);
				templateContext.SetValue(sv, kvp.Value.Value);
			}

			

			string content = fileSystem.File.ReadAllText(fullPath);
			
			var parsed = Template.Parse(content);
			try
			{
				var result = await parsed.RenderAsync(templateContext);

				return (result, null);
			} catch (ScriptRuntimeException ex)
			{
				var relativeFilePath = fullPath.AdjustPathToOs().Replace(fileSystem.RootDirectory, "");
				var innerException = ex.InnerException as ScriptRuntimeException ?? ex;
				string message;
				string pattern = @"\((\d+),(\d+)\)";
				Match match = Regex.Match(innerException.Message, pattern);
				if (match.Success)
				{
					int.TryParse(match.Groups[1].Value?.Trim(), out int lineNumber);
					int.TryParse(match.Groups[2].Value?.Trim(), out int columnNumber);
					
					message = $"{innerException.OriginalMessage} in {relativeFilePath} - line: {lineNumber} | column: {columnNumber}";
					var lines = content.Split('\n');
					if (lines.Length > lineNumber)
					{
						var startPos = lineNumber - 3;
						if (startPos < 0) startPos = 0;

						var errorLines = lines.ToList().Skip(startPos).Take(lineNumber - startPos);
						foreach (var errorLine in errorLines)
						{
							message += $"\n{startPos++}: {errorLine}";
						}
					}

				} else {
					message = $"{ex.Message} in {relativeFilePath}";
				}
				var pe = new ProgramError(message,
						goalStep, function, Exception: ex,
						HelpfulLinks: @"Description of the language syntax: https://github.com/scriban/scriban/blob/master/doc/language.md
Built-in functions: https://github.com/scriban/scriban/blob/master/doc/builtins.md
Runtime documentation: https://github.com/scriban/scriban/blob/master/doc/runtime.md"
						);

				return (null, pe);

			}
		}
	}
}
