using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Emit;
using Microsoft.CodeAnalysis.Text;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using PLang.Building.Model;
using PLang.Building.Parsers;
using PLang.Errors;
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
using static PLang.Modules.BaseBuilder;
using static PLang.Modules.CodeModule.Builder;
using static PLang.Runtime.Startup.ModuleLoader;
using ReturnValue = PLang.Modules.BaseBuilder.ReturnValue;

namespace PLang.Services.CompilerService
{
	/*
	 * todo: probably security issues here, adding lot of assemblies that probably should not be added
	 * */
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
			if (prevBuildGoalFile != null && prevBuildGoalFile.Function != null)
			{
				string dllFileName = ((dynamic)prevBuildGoalFile.Function).Name + ".dll";
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

		//public record Parameter(string VariableName, string ParameterName, string ParameterType);

		public async Task<(T?, CompilerError?)> BuildCode<T>(T answer, GoalStep step, MemoryStack memoryStack) where T : ImplementationResponse
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

			string dllFilePath = Path.Join(step.Goal.AbsolutePrFolderPath, step.PrFileName.Replace(".pr", ".dll"));
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
			var answerParameters = answer.Parameters ?? [];

			if (parameters.GroupBy(p => p.Identifier.Text).Where(p => p.Count() > 1).Count() > 0)
			{
				var parameterName = parameters.GroupBy(p => p.Identifier.Text).Where(p => p.Count() > 1).FirstOrDefault();

				string error = @$"
### Previous LLM generated code ###
Json response:
{JsonConvert.SerializeObject(answer)}

C# code:
{sourceCode}
### Previous LLM generated code ###

CSharp Compiler Error:
Parameter {parameterName.FirstOrDefault().Identifier.Text} is defined ExecutePlangCode twice, c# compiler does not support this. You can define it just once as ref if needed.
Fix this error.
					";

				return (null, new CompilerError($"Parameter count not matching. Need to request new code from LLM.", error, step, FixSuggestion: error));
			}

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

				return (null, new CompilerError($"Parameter count not matching. Need to request new code from LLM.", error, step, FixSuggestion: error));
			}

			for (int i = 0; i < parameters.Count; i++)
			{
				var parameter = parameters[i];
				var parameterName = parameter.Identifier.Text;
				var variableName = answerParameters[i].Name;

				var stepText = step.Text.ToLower();

				if (!StepTextContainsVariable(step, variableName))
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

					return (null, new CompilerError($"{variableName} is not in step.Text. Will retry with LLM", error, step, FixSuggestion: error));
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
			compilation = AddDefaultReferences(compilation);

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

				var assemblyPath = Path.Join(RuntimeEnvironment.GetRuntimeDirectory(), dllName);
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
						foreach (var filePath in files)
						{
							compilation = compilation.AddReferences(MetadataReference.CreateFromFile(filePath));
							string relativePath = filePath.Substring(filePath.LastIndexOf(".services"));
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
								return (null, new CompilerError($"dll couldn't be found in path {RuntimeEnvironment.GetRuntimeDirectory()}", error, step, FixSuggestion: error));
							}
						}
						catch (System.IO.FileNotFoundException fex)
						{
							string fileName = fex.FileName ?? "";
							if (fileName.Contains(",")) fileName = fileName.Substring(0, fileName.IndexOf(","));
							return (null, new CompilerError($@"File {fileName} not found. You might need to put {fileName}.dll file into the .services folder.", "", step,
								Exception: fex,
								FixSuggestion: $@"You need to download the library for {fileName}. 
Check out Plang help documentation to assist you:
	https://github.com/PLangHQ/plang/blob/main/Documentation/3rdPartyLibrary.md

Read the documentation link provided above to get understanding.",
								HelpfulLinks: $@"
You can find those 3rd party plugins at https://nuget.org
Search for {fileName} - https://www.nuget.org/packages?q={fileName}", Retry: false));
						}
						catch (Exception ex)
						{
							string error = "== Code generated by ChatGPT in previous request, start ==\n" + code + "\n== Code generated ends ==\nBut it had errors:\n";
							error += $"{ex}";
							error += "\nFix the error and generate the C# code again.";
							return (null, new CompilerError($"Unspecified compiler error", error, step, Exception: ex, FixSuggestion: error));
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


			using var file = fileSystem.File.Create(dllFilePath);
			using var pdbFile = fileSystem.File.Create(pdbFilePath);

			var emitOptions = new EmitOptions(debugInformationFormat: DebugInformationFormat.PortablePdb);
			var emitResult = compilation.Emit(file, embeddedTexts: embeddedTexts, pdbStream: pdbFile, options: emitOptions);

			file.Dispose();
			pdbFile.Dispose();
			if (!emitResult.Success)
			{
				string error = "Fix the <compilation_error> in <code> that is provided.\n<code>\n" + code + "\n<code>\n";
				var errors = emitResult.Diagnostics.Where(p => p.Severity == DiagnosticSeverity.Error).ToList();

				foreach (var diagnostic in errors)
				{
					string str = diagnostic.ToString();
					error += "<compilation_error>\n" + diagnostic.ToString() + "\n<compilation_error>\n";
					if (str.Contains("PLangFileSystem.PLangFileSystem("))
					{
						error += "PLangFileSystem.PLangFileSystem cannot be contructed it is an abstract class. It must me injected into ExecutePlangCode(IPlangFileSystem fileSystem...)\n";
					}
					else if (str.Contains("Use of unassigned local variable"))
					{
						var message = diagnostic.GetMessage();
						var variableName = message.Substring(message.IndexOf("'") + 1).Replace("'", "");
						error += @$"To solve the error 'Use of unassigned local variable'. Make sure to initialize the unassigned local variable {variableName}, see <example>

<example>
long {variableName} = 0;
// use {variableName}
<example>
";
					}

				}
				error += "\nFix the error and generate the C# code again. Make sure to reference all assemblies needed.";

				return (null, new CompilerError("Compiler error", error, step));

			}

			List<string> listOfAssembliesUsed = new();
			var assembliesUsed = compilation.GetUsedAssemblyReferences().Distinct();
			foreach (var au in assembliesUsed)
			{
				string fileName = Path.GetFileName(au.Display);
				if (fileName is null) continue;

				if (!listOfAssembliesUsed.Contains(fileName))
				{
					listOfAssembliesUsed.Add(fileName);
				}
			}

			answer = answer with { Implementation = code, Assemblies = listOfAssembliesUsed };

			foreach (var vars in answer.ReturnValues ?? [])
			{
				memoryStack.PutForBuilder(vars.VariableName, vars.Type.ToString());
			}

			return (answer, null);
		}


		private MetadataReference? TryResolveReference(List<string> usingList, string assemblyName)
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

		private bool StepTextContainsVariable(GoalStep step, string variableName)
		{
			//todo: could do better here
			if (variableName.Contains("α"))
			{
				variableName = variableName.Substring(0, variableName.IndexOf("α"));
			}
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

		internal async Task<IBuilderError?> BuildFile(FileCodeImplementationResponse file, GoalStep step, MemoryStack memoryStack)
		{
			var tree = CSharpSyntaxTree.ParseText(file.SourceCode);
			var coreLibPath = typeof(object).Assembly.Location;

			var refs = new List<MetadataReference>
			{
				MetadataReference.CreateFromFile(coreLibPath)
			};

			var list = AppDomain.CurrentDomain.GetAssemblies()
						  .Where(a => !a.IsDynamic && !string.IsNullOrEmpty(a.Location) && a.Location != coreLibPath) // Avoid duplicates
						  .Select(a => MetadataReference.CreateFromFile(a.Location))
						  .Cast<MetadataReference>();
			refs.AddRange(list);

			List<string> defaultUsings = new[]
			{
				"System",
				"System.Collections.Generic",
				"System.Linq",
				"System.Text",
				"System.Text.RegularExpressions",
				"System.Threading.Tasks"
			}.ToList();

			var compilationOptions = new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
					.WithOptimizationLevel(OptimizationLevel.Debug)
					.WithPlatform(Platform.AnyCpu).WithUsings(defaultUsings);
			var compilation = CSharpCompilation.Create(
					Path.GetFileNameWithoutExtension(file.FileName), new[] { tree },
					refs,
					options: compilationOptions)
				.AddReferences(MetadataReference.CreateFromFile(typeof(object).Assembly.Location));
			List<string> servicesAssembly = new();
			compilation = compilation.AddReferences(MetadataReference.CreateFromFile(typeof(SafeFileSystem.PLangFileSystem).Assembly.Location));
			compilation = compilation.AddReferences(MetadataReference.CreateFromFile(typeof(System.IO.Abstractions.IDirectory).Assembly.Location));



			foreach (var assembly in Assemblies)
			{
				if (assembly.ToLower() == "plang.safefilesystem") continue;

				string dllName = assembly;
				if (!dllName.Contains(".dll")) dllName += ".dll";

				var assemblyPath = Path.Join(RuntimeEnvironment.GetRuntimeDirectory(), dllName);
				if (File.Exists(assemblyPath))
				{
					compilation = compilation.AddReferences(MetadataReference.CreateFromFile(assemblyPath));
					var loadedAssembly = Assembly.Load(assembly.Replace(".dll", ""));
					if (loadedAssembly != null)
					{
						assemblyPath = loadedAssembly.Location;
						compilation = compilation.AddReferences(MetadataReference.CreateFromFile(assemblyPath));
					}
				}
				else
				{
					int i = 0;
				}
			}

			var model = compilation.GetSemanticModel(tree);
			var diagnostics = model.GetDiagnostics().Where(d => d.Id == "CS0246"); // CS0246: The type or namespace name could not be found

			foreach (var diagnostic in diagnostics)
			{
				var missingAssemblyName = diagnostic.GetMessage().Split('\'')[1];
				var resolvedReference = TryResolveReference(defaultUsings, missingAssemblyName);

				if (resolvedReference != null)
				{
					compilation = compilation.AddReferences(resolvedReference);
				}
			}

			string dllFile = file.FileName.Replace(".cs", ".dll");
			if (fileSystem.File.Exists(dllFile))
			{
				fileSystem.File.Delete(dllFile);
			}


			using var filestream = fileSystem.FileStream.New(dllFile, FileMode.CreateNew);
			var result = compilation.Emit(filestream);
			filestream.Close();

			if (!result.Success)
			{

				var errors = result.Diagnostics
								   .Where(d => d.Severity == DiagnosticSeverity.Error)
								   .Select(d => d.ToString());
				if (errors.Count() > 0)
				{
					fileSystem.File.Delete(dllFile);
					string errorTxt = string.Join("\n", errors);
					return new CompilerError(
						$"Compilation failed:\n{errorTxt}\nSource:\n{file.SourceCode}", "", step
					);
				}
			}

			return null;
		}



		public CSharpCompilation AddDefaultReferences(CSharpCompilation compilation)
		{

			// Get all essential system assemblies
			var essentialAssemblies = new List<string>
	{
		// Core runtime
		"System.Private.CoreLib.dll",
		"System.Runtime.dll",
		"System.dll",
		"netstandard.dll",
		"mscorlib.dll",
		
		// Common System assemblies
		"System.Collections.dll",
		"System.Collections.Concurrent.dll",
		"System.Console.dll",
		"System.Diagnostics.Debug.dll",
		"System.Diagnostics.Process.dll",
		"System.Diagnostics.TraceSource.dll",
		"System.IO.dll",
		"System.IO.FileSystem.dll",
		"System.Linq.dll",
		"System.Linq.Expressions.dll",
		"System.Net.Http.dll",
		"System.Net.Primitives.dll",
		"System.ObjectModel.dll",
		"System.Private.Uri.dll", // This fixes your Uri error
		"System.Reflection.dll",
		"System.Runtime.Extensions.dll",
		"System.Runtime.InteropServices.dll",
		"System.Runtime.Serialization.dll",
		"System.Security.Cryptography.dll",
		"System.Text.Encoding.dll",
		"System.Text.Json.dll",
		"System.Text.RegularExpressions.dll",
		"System.Threading.dll",
		"System.Threading.Tasks.dll",
		"System.Threading.Thread.dll",
		"System.Xml.dll",
		"System.Xml.Linq.dll",
		
		// Additional commonly used assemblies
		"System.ComponentModel.dll",
		"System.ComponentModel.Primitives.dll",
		"System.Data.Common.dll",
		"System.Drawing.Primitives.dll",
		"System.Globalization.dll",
		"System.Memory.dll",
		"System.Numerics.dll",
		"System.Web.dll"
	};

			// Add type-based references (more reliable than file paths)
			var typeBasedReferences = new List<MetadataReference>
	{
		MetadataReference.CreateFromFile(typeof(object).Assembly.Location), // System.Private.CoreLib
		MetadataReference.CreateFromFile(typeof(Console).Assembly.Location), // System.Console
		MetadataReference.CreateFromFile(typeof(Uri).Assembly.Location), // System.Private.Uri
		MetadataReference.CreateFromFile(typeof(System.Collections.Generic.List<>).Assembly.Location), // System.Collections
		MetadataReference.CreateFromFile(typeof(System.Linq.Enumerable).Assembly.Location), // System.Linq
		MetadataReference.CreateFromFile(typeof(System.Text.RegularExpressions.Regex).Assembly.Location), // System.Text.RegularExpressions
		MetadataReference.CreateFromFile(typeof(System.Net.Http.HttpClient).Assembly.Location), // System.Net.Http
		MetadataReference.CreateFromFile(typeof(System.Threading.Tasks.Task).Assembly.Location), // System.Threading.Tasks
		MetadataReference.CreateFromFile(typeof(System.IO.File).Assembly.Location), // System.IO
		MetadataReference.CreateFromFile(typeof(System.Xml.XmlDocument).Assembly.Location), // System.Xml
	};

			// Add all type-based references
			compilation = compilation.AddReferences(typeBasedReferences);

			// Add file-based references for assemblies that exist
			foreach (var assemblyName in essentialAssemblies)
			{
				var assemblyPath = Path.Combine(RuntimeEnvironment.GetRuntimeDirectory(), assemblyName);
				if (File.Exists(assemblyPath))
				{
					try
					{
						compilation = compilation.AddReferences(MetadataReference.CreateFromFile(assemblyPath));
					}
					catch
					{
						// Ignore if assembly can't be loaded
					}
				}
			}

			return compilation;
		}


	}
}
