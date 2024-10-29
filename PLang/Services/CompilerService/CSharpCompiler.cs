using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Emit;
using Microsoft.CodeAnalysis.Text;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using PLang.Building.Model;
using PLang.Building.Parsers;
using PLang.Errors.Builder;
using PLang.Exceptions;
using PLang.Interfaces;
using PLang.Runtime;
using PLang.Utils;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Loader;
using System.Text;
using System.Text.RegularExpressions;

namespace PLang.Services.CompilerService
{
	public class CSharpCompiler
	{
		private List<string> Assemblies = new List<string>();
		private readonly IPLangFileSystem fileSystem;
		private readonly PrParser prParser;
		private readonly ILogger logger;

		public CSharpCompiler(IPLangFileSystem fileSystem, PrParser prParser, ILogger logger)
		{
			this.fileSystem = fileSystem;
			this.prParser = prParser;
			this.logger = logger;
			Assemblies.Add("System.Runtime.dll");
			Assemblies.Add("System.Private.CoreLib.dll");
			Assemblies.Add("System.Collections.dll");
			Assemblies.Add("netstandard.dll");
			Assemblies.Add("System.Linq.dll");
			Assemblies.Add("System.Threading.Tasks.dll");
			Assemblies.Add("System.Console.dll");
			Assemblies.Add("System.Net.Http.dll");
			Assemblies.Add("System.Net.Primitives.dll");
			Assemblies.Add("System.ObjectModel.dll");
			Assemblies.Add("System.Text.Json.dll");
			Assemblies.Add("System.Text.RegularExpressions.dll");
			Assemblies.Add("System.Linq.Expressions.dll");
			Assemblies.Add("Microsoft.CSharp.dll");
			Assemblies.Add("System.Diagnostics.Process.dll");
			Assemblies.Add("System.Memory.dll");
			Assemblies.Add("TestableIO.System.IO.Abstractions.Wrappers.dll");
			Assemblies.Add("PlangLibrary.dll");
			Assemblies.Add("Newtonsoft.Json.dll");
		}

		public string GetPreviousBuildDllNamesToExclude(GoalStep step)
		{
			var prevBuildGoalFile = prParser.ParseInstructionFile(step);
			if (prevBuildGoalFile != null && prevBuildGoalFile.Action != null)
			{
				string dllFileName = ((dynamic)prevBuildGoalFile.Action).Name + ".dll";
				string dllFilePath = Path.Join(step.Goal.RelativePrFolderPath, dllFileName);
				if (fileSystem.File.Exists(dllFilePath))
				{
					fileSystem.File.Delete(dllFilePath);
				}
				return "";
			}
			else
			{
				var excludedName = "";
				if (!fileSystem.Directory.Exists(step.Goal.AbsolutePrFolderPath)) return "";
				var dllFiles = fileSystem.Directory.GetFiles(step.Goal.AbsolutePrFolderPath, "*.dll");
				foreach (var dllFile in dllFiles)
				{
					if (excludedName != "") excludedName += ", ";
					string fileName = Path.GetFileNameWithoutExtension(dllFile).AdjustPathToOs();
					if (fileName.Contains("."))
					{
						fileName = fileName.Substring(fileName.IndexOf(".") + 1).Trim();
					}

					excludedName += fileName;
				}
				if (string.IsNullOrEmpty(excludedName)) return "";
				return "- Name of class cannot be: " + excludedName + "\n";
			}
		}

		public record Parameter(string VariableName, string ParameterName, string ParameterType);

		public async Task<(Implementation?, CompilerError?)> BuildCode(ImplementationResponse answer, GoalStep step, MemoryStack memoryStack)
		{
			if (answer.Assemblies != null)
			{
				Assemblies.AddRange(answer.Assemblies);
			}
			Assemblies.Distinct();

			if (answer.Implementation == null)
			{
				return (null, new CompilerError("Implementation was empty", "Create c# code from user intent", step));
			}

			var strUsing = "";
			if (answer.Using != null)
			{
				foreach (var u in answer.Using)
				{
					if (!answer.Implementation.Contains($"using {u};"))
					{
						strUsing += $"using {u};\r\n";
					}
				}
			}
			if (!strUsing.Contains("using System.Diagnostics;") && !answer.Implementation.Contains("using System.Diagnostics;"))
			{
				strUsing += "using System.Diagnostics;\n";
			}
			if (!strUsing.Contains("using System;") && !answer.Implementation.Contains("using System.Diagnostics;"))
			{
				strUsing += "using System;\n";
			}
			string namepaceCode = "";
			if (!answer.Implementation.Contains(answer.Namespace))
			{
				namepaceCode = $"namespace {answer.Namespace};\n\n";
			}
			var sourceCode = Transform(answer.Implementation, step);
			var code = strUsing + namepaceCode + sourceCode;
			var sourceText = SourceText.From(code, Encoding.UTF8);
			if (!sourceCode.Contains($"public static class {answer.Name}"))
			{
				string error = @$"Error building code. The class must be defined as: public static class {answer.Name}.
### Previous LLM generated code ###
{sourceCode}
### Previous LLM generated code ###
";
				return (null, new CompilerError("Could not find class name in code", error, step));
			}

			string dllFilePath = Path.Combine(step.Goal.AbsolutePrFolderPath, step.PrFileName.Replace(".pr", ".dll"));
			string dllFileName = Path.GetFileName(dllFilePath.AdjustPathToOs().RemoveExtension());
			string pdbFilePath = dllFilePath.Replace(".dll", ".pdb");
			string sourceCodePath = dllFileName + ".cs";


			SyntaxTree tree = CSharpSyntaxTree.ParseText(sourceText, path: sourceCodePath);

			var root = tree.GetRoot();
			var method = root.DescendantNodes().OfType<MethodDeclarationSyntax>().FirstOrDefault();
			if (method == null)
			{
				code = strUsing + namepaceCode + "public static class " + answer.Name + " {\n\t " + sourceCode + " \n}";
				tree = CSharpSyntaxTree.ParseText(code);
				root = tree.GetRoot();
				method = root.DescendantNodes().OfType<MethodDeclarationSyntax>().FirstOrDefault();
			}
			var parameters = method.ParameterList.Parameters.Where(p => p.Identifier.Text != "fileSystem").ToList();
			var answerParameters = GetParameters(answer);

			if (parameters.Count != answerParameters.Count)
			{
				string error = @$"
### Previous LLM generated code ###
Json response:
{JsonConvert.SerializeObject(answer)}

C# code:
{sourceCode}
### Previous LLM generated code ###
 Parameters in json does not match parameter count in method. Please fix.
					";

				return (null, new CompilerError($"Parameter count not matching. Need to request new code from LLM.", error, step));
			}
			List<Parameter> inputParameters = new();
			List<Parameter> outputParameters = new();
			for (int i = 0; i < parameters.Count; i++)
			{
				var parameter = parameters[i];
				var parameterName = parameter.Identifier.Text;
				var variableName = answerParameters[i];

				var stepText = step.Text.ToLower();

				if (inputParameters.FirstOrDefault(p => p.ParameterName == parameterName) != null)
				{
					logger.LogWarning($"{parameterName} was defined twice in code. Need to request new code from LLM.");
					//retry with gpt with error that parameter is not in step text.
					string error = @$"
### Previous LLM generated code ###
{code}
### Previous LLM generated code ###
 The parameter name '{parameterName}' is a duplicate. Ambiguity between '{parameterName}'. Please fix.
					";

					return (null, new CompilerError($"{parameterName} was defined twice in code. Need to request new code from LLM.", error, step));
				}
				else if (parameter.Type?.ToString().Trim() == "PLang.SafeFileSystem.PLangFileSystem" || parameter.Type?.ToString().Trim() == "PLangFileSystem")
				{
					inputParameters.Add(new Parameter(variableName, parameterName, parameter.Type.ToString()));
				}
				else if (!StepTextContainsVariable(step, variableName))
				{
					logger.LogWarning($"{variableName} is not in step.Text. Will retry with LLM");
					//retry with gpt with error that parameter is not in step text.
					string error = @$"
### Previous LLM generated code ###
{code}
### Previous LLM generated code ###

This generated code has error:

{variableName} is not defined in user command: {stepText}

Fix the error and generate the C# code again.

These are the rules with variables:
- Replace the dot(.) in variables with the letter α e.g. %user.id% to userαid, %product.items[0].title% to productαitemsα0ααtitle, %list[1]% to listα1α
- Make sure to keep underscore in variables if the user defined it like that
					";

					return (null, new CompilerError($"{variableName} is not in step.Text. Will retry with LLM", error, step));
				}
				else
				{
					if (answer is CodeImplementationResponse cir2)
					{
						if (IsOutOrRef(parameter))
						{
							outputParameters.Add(new Parameter(variableName, parameterName, parameter.Type.ToString()));
						}
						else
						{
							inputParameters.Add(new Parameter(variableName, parameterName, parameter.Type.ToString()));
						}
					}
					else
					{
						inputParameters.Add(new Parameter(variableName, parameterName, parameter.Type.ToString()));
					}
				}

			}

			var compilationOptions = new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
					.WithOptimizationLevel(OptimizationLevel.Debug)
					.WithPlatform(Platform.AnyCpu);
			var compilation = CSharpCompilation.Create(dllFileName, options: compilationOptions)
				.AddReferences(MetadataReference.CreateFromFile(typeof(object).Assembly.Location));
			List<string> servicesAssembly = new();
			compilation = compilation.AddReferences(MetadataReference.CreateFromFile(typeof(SafeFileSystem.PLangFileSystem).Assembly.Location));
			compilation = compilation.AddReferences(MetadataReference.CreateFromFile(typeof(System.IO.Abstractions.IDirectory).Assembly.Location));

			compilation = compilation.AddSyntaxTrees(tree);

			var model = compilation.GetSemanticModel(tree);
			var diagnostics = model.GetDiagnostics().Where(d => d.Id == "CS0246"); // CS0246: The type or namespace name could not be found

			foreach (var diagnostic in diagnostics)
			{
				var missingAssemblyName = diagnostic.GetMessage().Split('\'')[1];
				var resolvedReference = TryResolveReference(answer.Using, missingAssemblyName);

				if (resolvedReference != null)
				{
					compilation = compilation.AddReferences(resolvedReference);
				}
			}




			foreach (var assembly in Assemblies)
			{
				if (assembly.ToLower() == "plang.safefilesystem") continue;

				string dllName = assembly;
				if (!dllName.Contains(".dll")) dllName += ".dll";

				var assemblyPath = Path.Combine(RuntimeEnvironment.GetRuntimeDirectory(), dllName);
				if (File.Exists(assemblyPath))
				{
					compilation = compilation.AddReferences(MetadataReference.CreateFromFile(assemblyPath));
				}
				else
				{
					string[] files = [];
					if (fileSystem.Directory.Exists(".services"))
					{
						files = fileSystem.Directory.GetFiles(".services", dllName, SearchOption.AllDirectories);
					}
					if (files.Length > 0)
					{
						foreach (var file in files)
						{
							compilation = compilation.AddReferences(MetadataReference.CreateFromFile(file));
							string relativePath = file.Substring(file.LastIndexOf(".services"));
							servicesAssembly.Add(relativePath);
						}
					}
					else
					{

						try
						{
							var loadedAssembly = Assembly.Load(assembly.Replace(".dll", ""));
							if (loadedAssembly != null)
							{
								assemblyPath = loadedAssembly.Location;
								compilation = compilation.AddReferences(MetadataReference.CreateFromFile(assemblyPath));
							}
							else
							{
								string error = "== Code generated by ChatGPT in previous request, start ==\n" + code + "\n== Code generated ends ==\nBut it had errors:\n";
								error += $"The dll {dllName} does not exist in path {RuntimeEnvironment.GetRuntimeDirectory()}";
								error += "\nFix the error and generate the C# code again.";
								return (null, new CompilerError($"dll couldn't be found in path {RuntimeEnvironment.GetRuntimeDirectory()}", error, step));
							}
						}
						catch (System.IO.FileNotFoundException fex)
						{
							string fileName = fex.FileName ?? "";
							if (fileName.Contains(",")) fileName = fileName.Substring(0, fileName.IndexOf(","));
							return (null, new CompilerError($@"File {fileName} not found. You might need to put {fileName}.dll file into the .services folder.", "", step, ContinueBuild: false,
								Exception: fex,
								FixSuggestion: $@"You need to download the library for {fileName}. 
Check out Plang help documentation to assist you:
	https://github.com/PLangHQ/plang/blob/main/Documentation/3rdPartyLibrary.md

Read the documentation link provided above to get understanding.",
								HelpfulLinks: $@"
You can find those 3rd party plugins at https://nuget.org
Search for {fileName} - https://www.nuget.org/packages?q={fileName}"));
						}
						catch (Exception ex)
						{
							string error = "== Code generated by ChatGPT in previous request, start ==\n" + code + "\n== Code generated ends ==\nBut it had errors:\n";
							error += $"{ex}";
							error += "\nFix the error and generate the C# code again.";
							return (null, new CompilerError($"Unspecified compiler error", error, step, Exception: ex));
						}
					}
				}

			}
			//tree = tree.WithFilePath(dllFilePath.Replace(".dll", ".cs"));
			

			if (!fileSystem.Directory.Exists(step.Goal.AbsolutePrFolderPath))
			{
				fileSystem.Directory.CreateDirectory(step.Goal.AbsolutePrFolderPath);
			}

			var embeddedTexts = new List<EmbeddedText> { EmbeddedText.FromSource(tree.FilePath, tree.GetText()) };


			using (var file = fileSystem.File.Create(dllFilePath))
			using (var pdbFile = fileSystem.File.Create(pdbFilePath))
			{
				var emitOptions = new EmitOptions(debugInformationFormat: DebugInformationFormat.PortablePdb);
				var emitResult = compilation.Emit(file, embeddedTexts: embeddedTexts, pdbStream: pdbFile, options: emitOptions);

				file.Dispose();
				pdbFile.Dispose();
				if (!emitResult.Success)
				{
					string error = "== Code generated by ChatGPT in previous request, start ==\n" + code + "\n== Code generated ends ==\nBut it had errors:\n";
					var errors = emitResult.Diagnostics.Where(p => p.Severity == DiagnosticSeverity.Error).ToList();

					foreach (var diagnostic in errors)
					{
						string str = diagnostic.ToString();
						if (str.Contains("PLangFileSystem.PLangFileSystem("))
						{
							error += "PLangFileSystem.PLangFileSystem cannot be contructed it is an abstract class. It must me injected into ExecutePlangCode(IPlangFileSystem fileSystem...)\n";
						}
						else if (str.Contains("Use of unassigned local variable"))
						{
							error += @"To solve the error 'Use of unassigned local variable'. Make sure to initialize the variable, for example

Use of unassigned local variable 'number'

long number = 0;
long.TryParse(str, out number);
";


						}
						error += diagnostic.ToString() + "\n";
					}
					error += "\nFix the error and generate the C# code again. Make sure to reference all assemblies needed.";

					return (null, new CompilerError("Compiler error", error, step));

				}
			}
			if (answer is CodeImplementationResponse cir)
			{

				foreach (var vars in outputParameters)
				{
					memoryStack.PutForBuilder(vars.VariableName, vars.ParameterType.ToString());
				}

				var implementation = new Implementation(answer.Namespace, answer.Name, answer.Implementation, answer.Using,
					inputParameters, outputParameters, null, null, null, null, servicesAssembly);
				return (implementation, null);
			}
			else if (answer is ConditionImplementationResponse conIr)
			{
				var implementation = new Implementation(answer.Namespace, answer.Name, answer.Implementation, answer.Using,
					inputParameters, null, conIr.GoalToCallOnTrue, conIr.GoalToCallOnFalse,
					conIr.GoalToCallOnTrueParameters, conIr.GoalToCallOnFalseParameters, servicesAssembly);
				return (implementation, null);
			}

			return (null, new CompilerError("Could not generate code", "Create C# code from user input", step));


		}
		public bool IsOutOrRef(ParameterSyntax parameterSyntax)
		{
			return parameterSyntax.Modifiers.Any(m => m.IsKind(SyntaxKind.RefKeyword)) ||
				parameterSyntax.Modifiers.Any(m => m.IsKind(SyntaxKind.OutKeyword));
		}

		private MetadataReference TryResolveReference(string[] usingList, string assemblyName)
		{
			var assemblies = AssemblyLoadContext.Default.Assemblies;

			foreach (var assembly in assemblies)
			{
				if (assembly.GetName().Name == assemblyName)
				{
					return MetadataReference.CreateFromFile(assembly.Location);
				}
			}

			// Optionally, log or handle the fact that the assembly could not be resolved
			return null;
		}

		private List<string> GetParameters(ImplementationResponse answer)
		{
			List<string> parameters = new();
			if (answer is CodeImplementationResponse cir)
			{
				if (cir.InputParameters != null) parameters.AddRange(cir.InputParameters);
				if (cir.OutParameters != null)
				{
					foreach (var parameter in cir.OutParameters)
					{
						if (!parameters.Contains(parameter))
						{
							parameters.Add(parameter);
						}
					}
					
				}
			}
			else
			{
				if (answer.InputParameters != null) parameters.AddRange(answer.InputParameters);
			}
			return parameters;
		}

		private bool StepTextContainsVariable(GoalStep step, string variableName)
		{
			return step.Text.Contains(variableName, StringComparison.OrdinalIgnoreCase);
		}

		private string Transform(string implementation, GoalStep step)
		{
			if (string.IsNullOrEmpty(implementation)) return implementation;

			if (implementation.Contains("Regex(\""))
			{

				implementation = implementation.Replace("Regex(\"", "Regex(@\"");
			}

			//dont insert Debugger.Break to SendDebug code, it's just annoying
			var debugPath = Path.Join(".build", Path.DirectorySeparatorChar.ToString(), "events", Path.DirectorySeparatorChar.ToString(), "senddebug");
			if (!step.Goal.AbsolutePrFilePath.ToLower().Contains(debugPath) && !implementation.Contains($@"(!AppContext.TryGetSwitch(""skipCode"""))
			{
				int idx = implementation.IndexOf("ExecutePlangCode");
				int curlyIdx = implementation.IndexOf("{", idx);
				implementation = implementation.Insert(curlyIdx + 1, @"
    if (!AppContext.TryGetSwitch(""skipCode"", out bool _))
	{
        Debugger.Break();
	}

");
			}

			return implementation;
		}

		private string GetStepParameterName(string text)
		{
			if (text.EndsWith("α"))
			{
				throw new BuildStatusException($"variable {text} cannot end with 'α'");
			}
			var str = text.Replace("α", ".").Replace("!", "");
			str = Regex.Replace(str, @"\.([0-9]+)\.?", evaluator);
			return str;
		}

		private string evaluator(Match match)
		{
			if (match.Success)
			{
				return "[" + match.Groups[1].Value + "]";
			}
			return match.Value;
		}
	}
}
