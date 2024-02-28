using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Emit;
using Microsoft.CodeAnalysis.Text;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using PLang.Building.Model;
using PLang.Building.Parsers;
using PLang.Interfaces;
using PLang.Runtime;
using PLang.Utils;
using System.Runtime.InteropServices;
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
                        fileName = fileName.Substring(fileName.IndexOf(".")+1).Trim();
                    }

					excludedName += fileName;
                }
                if (string.IsNullOrEmpty(excludedName)) return "";
                return "- Name of class cannot be: " + excludedName + "\n";
            }
        }

        public async Task<BuildStatus> BuildCode(ImplementationResponse answer, GoalStep step, MemoryStack memoryStack)
        {
            if (answer.Assemblies != null)
            {
                Assemblies.AddRange(answer.Assemblies);
            }
            Assemblies.Distinct();


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
            if (!strUsing.Contains("using System.Diagnostics;"))
            {
                strUsing += "using System.Diagnostics;\n";
            }
            if (!strUsing.Contains("using System;"))
            {
                strUsing += "using System;\n";
            }

            var sourceCode = Transform(answer.Implementation, step);
            var code = strUsing + sourceCode;
            var sourceText = SourceText.From(code, Encoding.UTF8);

            string dllFilePath = Path.Combine(step.Goal.AbsolutePrFolderPath, step.PrFileName.Replace(".pr", ".dll"));
            string dllFileName = Path.GetFileName(dllFilePath.AdjustPathToOs().RemoveExtension());
            string pdbFilePath = dllFilePath.Replace(".dll", ".pdb");
            string sourceCodePath = dllFileName + ".cs";


            SyntaxTree tree = CSharpSyntaxTree.ParseText(sourceText, path: sourceCodePath);

            var root = tree.GetRoot();
            var method = root.DescendantNodes().OfType<MethodDeclarationSyntax>().FirstOrDefault();
            if (method == null)
            {
                code = strUsing + "public static class " + answer.Name + " {\n\t " + sourceCode + " \n}";
                tree = CSharpSyntaxTree.ParseText(code);
                root = tree.GetRoot();
                method = root.DescendantNodes().OfType<MethodDeclarationSyntax>().FirstOrDefault();
            }
            var parameters = method.ParameterList.Parameters;

            Dictionary<string, string> inputParameters = new Dictionary<string, string>();
            foreach (var parameter in parameters)
            {
                var stepText = step.Text.ToLower();
                var parameterName = GetStepParameterName(parameter.Identifier.Text);
                if (parameter.Type?.ToString().Trim() == "PLang.SafeFileSystem.PLangFileSystem" || parameter.Type?.ToString().Trim() == "PLangFileSystem")
                {
                    inputParameters.Add(parameterName, parameter.Type.ToString());
                }
                else if (!StepTextContainsParameter(step, parameterName))
                {
                    logger.LogWarning(parameter.Type + " " + parameterName + " is not in step.Text. Will retry with LLM");
                    //retry with gpt with error that parameter is not in step text.
                    string error = @$"== Code generated by ChatGPT in previous request, start ==\n{code}\n== Code generated ends ==
This generated code has error:

{parameterName} is not defined in user command: {stepText}

Fix the error and generate the C# code again.

These are the rules with variables:
- Replace the dot(.) in variables with the letter α e.g. %user.id% to userαid, %product.items[0].title% to productαitemsα0ααtitle, %list[1]% to listα1α
- Make sure to keep underscore in variables if the user defined it like that
					";

                    return new BuildStatus(null, error);
                }
                else
                {
                    inputParameters.Add(parameterName, parameter.Type.ToString());
                }

            }

            var compilationOptions = new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
                    .WithOptimizationLevel(OptimizationLevel.Debug)
                    .WithPlatform(Platform.AnyCpu);
            var compilation = CSharpCompilation.Create(dllFileName, options: compilationOptions)
                .AddReferences(MetadataReference.CreateFromFile(typeof(object).Assembly.Location));

            compilation = compilation.AddReferences(MetadataReference.CreateFromFile(typeof(SafeFileSystem.PLangFileSystem).Assembly.Location));
            compilation = compilation.AddReferences(MetadataReference.CreateFromFile(typeof(System.IO.Abstractions.IDirectory).Assembly.Location));
            foreach (var assembly in Assemblies)
            {
                if (assembly.ToLower() == "plang.safefilesystem") continue;

                string dllName = assembly;
                if (!dllName.Contains(".dll")) dllName += ".dll";

                var assemblyPath = Path.Combine(RuntimeEnvironment.GetRuntimeDirectory(), dllName);
                compilation = compilation.AddReferences(MetadataReference.CreateFromFile(assemblyPath));
            }
            //tree = tree.WithFilePath(dllFilePath.Replace(".dll", ".cs"));
            compilation = compilation.AddSyntaxTrees(tree);

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
                    foreach (var diagnostic in emitResult.Diagnostics)
                    {
                        error += diagnostic.ToString() + "\n";
                    }
                    error += "\nFix the error and generate the C# code again.";

                    return new BuildStatus(null, error);

                }
            }
            if (answer is CodeImplementationResponse cir)
            {
                var outParameters = cir.OutParameterDefinition;
                if (outParameters != null)
                {
                    foreach (var vars in outParameters)
                    {
                        memoryStack.PutForBuilder(vars.Key, JsonConvert.SerializeObject(vars.Value));
                    }
                }
				var implementation = new Implementation(answer.Name, answer.Implementation, answer.Using, inputParameters, cir.OutParameterDefinition, null, null);
				return new BuildStatus(implementation);
			} else if (answer is ConditionImplementationResponse conIr)
            {
				var implementation = new Implementation(answer.Name, answer.Implementation, answer.Using, inputParameters, null, conIr.GoalToCallOnTrue, conIr.GoalToCallOnFalse);
				return new BuildStatus(implementation);
			}

            return null;

            
        }

        private bool StepTextContainsParameter(GoalStep step, string parameterName)
        {
            string stepText = step.Text.ToLower();
            if (stepText.Contains("%" + parameterName.ToLower())) return true;
            if (stepText.Replace(".", "α").Contains("%" + parameterName.ToLower())) return true;

            return false;
        }

        private string Transform(string implementation, GoalStep step)
        {
            if (implementation.Contains("Regex(\""))
            {

                implementation = implementation.Replace("Regex(\"", "Regex(@\"");
            }

            //dont insert Debugger.Break to SendDebug code, it's just annoying
            var debugPath = Path.Join(".build", Path.DirectorySeparatorChar.ToString(), "events", Path.DirectorySeparatorChar.ToString(), "senddebug");
            if (!step.Goal.AbsolutePrFilePath.ToLower().Contains(debugPath))
            {
                int idx = implementation.IndexOf("Process");
                int curlyIdx = implementation.IndexOf("{", idx);
                implementation = implementation.Insert(curlyIdx + 1, @"
Debugger.Break();
");
            }

            return implementation;
        }

        private string GetStepParameterName(string text)
        {
            var str = text.Replace("α", ".");
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
