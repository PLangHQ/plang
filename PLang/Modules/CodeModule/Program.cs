using IdGen;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using PLang.Errors;
using PLang.Errors.Runtime;
using PLang.Exceptions;
using PLang.Interfaces;
using PLang.Runtime;
using PLang.Services.CompilerService;
using PLang.Utils;
using System.ComponentModel;
using System.Data;
using System.Reflection;
using System.Text.RegularExpressions;
using static PLang.Runtime.Startup.ModuleLoader;
using static PLang.Services.CompilerService.CSharpCompiler;

namespace PLang.Modules.CodeModule
{
	[Description("Generate c# code from user description. Only use if no other module is found.")]
	public class Program : BaseProgram
	{
		private readonly IPLangFileSystem fileSystem;
		private readonly ILogger logger;

		public Program(IPLangFileSystem fileSystem, ILogger logger) : base()
		{
			this.fileSystem = fileSystem;
			this.logger = logger;
		}


		public override async Task<IError?> Run()
		{
			Implementation? answer = null;
			try
			{
				answer = JsonConvert.DeserializeObject<Implementation?>(instruction.Action.ToString()!);
				if (answer == null)
				{
					return new StepError("Code implementation was empty", goalStep);
				}

				string dllName = goalStep.PrFileName.Replace(".pr", ".dll");
				Assembly assembly = Assembly.LoadFile(Path.Combine(Goal.AbsolutePrFolderPath, dllName));
				
				if (assembly == null)
				{
					return new StepError($"Could not find {dllName}. Stopping execution for step {goalStep.Text}", goalStep);
				}

				List<Assembly> serviceAssemblies = new();

				if (answer.ServicesAssembly != null && answer.ServicesAssembly.Count > 0)
				{
					foreach (var serviceAssembly in answer.ServicesAssembly)
					{
						string assemblyPath = Path.Combine(Goal.AbsoluteAppStartupFolderPath, serviceAssembly).AdjustPathToOs();
						if (fileSystem.File.Exists(assemblyPath))
						{
							serviceAssemblies.Add(Assembly.LoadFile(assemblyPath));
						}
						else
						{
							logger.LogWarning($"Could not find file {assemblyPath}");
						}
					}
				}

				AppDomain.CurrentDomain.AssemblyResolve += (sender, args) =>
				{
					var assembly = serviceAssemblies.FirstOrDefault(p => p.FullName == args.Name);
					return assembly;
				};

				Type? type = assembly.GetType(answer.Namespace + "." + answer.Name);
				if (type == null)
				{
					return new StepError($"Type could not be loaded for {answer.Name}. Stopping execution for step {goalStep.Text}", goalStep);
				}

				MethodInfo? method = type.GetMethod("ExecutePlangCode");
				if (method == null)
				{
					return new StepError($"Method could not be loaded for {answer.Name}. Stopping execution for step {goalStep.Text}", goalStep);
				}
				var parameters = method.GetParameters();



				List<object?> parametersObject = new();
				for (var i = 0; i < parameters.Length; i++)
				{
					if (parameters[i].IsOut || parameters[i].ParameterType.IsByRef)
					{
						Type? outType = parameters[i].ParameterType.GetElementType();
						if (outType == null) continue;
						if (answer.OutParameters == null)
						{
							return new ProgramError($"{parameters[i].Name} is not defined in code. Please rebuild step", goalStep, function, StatusCode: 500);
						}

						var outParameter = answer.OutParameters.FirstOrDefault(p => p.ParameterName == parameters[i].Name);
						if (outParameter == null)
						{
							return new ProgramError($"{parameters[i].Name} could not be found in build code. Please rebuild step", goalStep, function, StatusCode: 500);
						}

						var value = memoryStack.Get(outParameter.VariableName, parameters[i].ParameterType);
						if (value == null && outType.IsValueType)
						{
							parametersObject.Add(Activator.CreateInstance(outType));
						}
						else
						{
							parametersObject.Add(value);
						}
					}
					else
					{

						var parameterType = parameters[i].ParameterType;
						if (parameterType.FullName == "PLang.SafeFileSystem.PLangFileSystem")
						{
							parametersObject.Add(fileSystem);
						}
						else
						{
							var inParameter = answer.InputParameters.FirstOrDefault(p => p.ParameterName == parameters[i].Name);
							if (inParameter == null)
							{
								return new ProgramError($"{parameters[i].Name} could not be found in build code. Please rebuild step", goalStep, function, StatusCode: 500);
							}
							var value = memoryStack.Get(inParameter.VariableName, parameters[i].ParameterType);
							parametersObject.Add(value);
						}
					}
				}
				var args = parametersObject.ToArray();

				object? result = method.Invoke(null, args);

				for (int i = 0; i < parameters.Length; i++)
				{
					var parameterInfo = parameters[i];
					if (parameterInfo.IsOut || parameterInfo.ParameterType.IsByRef)
					{
						memoryStack.Put(parameterInfo.Name!, args[i]);
					}
				}
				return null;
			}
			catch (Exception ex)
			{
				var error = CodeExceptionHandler.GetError(ex, answer, goalStep);

				return error;
			}

		}

	}

}

