using PLang.Errors;
using PLang.Errors.Runtime;
using PLang.Interfaces;
using RazorEngineCore;
using Scriban;
using Scriban.Syntax;
using System.Dynamic;

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
			var result = await parsed.RenderAsync(templateContext);

			return (result, null);
		}
	}
}
