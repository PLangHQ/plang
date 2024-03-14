using IdGen;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
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


		public override async Task Run()
		{
			Implementation? answer = null;
			try
			{
				answer = JsonConvert.DeserializeObject<Implementation?>(instruction.Action.ToString()!);
				if (answer == null)
				{
					throw new RuntimeStepException("Code implementation was empty", goalStep);
				}

				string dllName = goalStep.PrFileName.Replace(".pr", ".dll");
				Assembly assembly = Assembly.LoadFile(Path.Combine(Goal.AbsolutePrFolderPath, dllName));
				
				if (assembly == null)
				{
					throw new RuntimeStepException($"Could not find {dllName}. Stopping execution for step {goalStep.Text}", goalStep);
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
					throw new RuntimeStepException($"Type could not be loaded for {answer.Name}. Stopping execution for step {goalStep.Text}", goalStep);
				}

				MethodInfo? method = type.GetMethod("ExecutePlangCode");
				if (method == null)
				{
					throw new RuntimeStepException($"Method could not be loaded for {answer.Name}. Stopping execution for step {goalStep.Text}", goalStep);
				}
				var parameters = method.GetParameters();

				List<object?> parametersObject = new();
				for (var i = 0; i < parameters.Length; i++)
				{
					var parameterType = parameters[i].ParameterType;
					if (parameterType.FullName == "PLang.SafeFileSystem.PLangFileSystem")
					{
						parametersObject.Add(fileSystem);
					}
					else if (parameters[i].IsOut || parameters[i].ParameterType.IsByRef)
					{
						Type? outType = parameters[i].ParameterType.GetElementType();
						if (outType == null) continue;

						if (outType.IsValueType)
						{							
							parametersObject.Add(Activator.CreateInstance(outType));
						}
						else
						{
							var value = memoryStack.Get(parameters[i].Name!, parameters[i].ParameterType);
							parametersObject.Add(value);
						}

					}
					else
					{
						var value = memoryStack.Get(parameters[i].Name!, parameters[i].ParameterType);
						parametersObject.Add(value);
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
			}
			catch (RuntimeStepException) { throw; }
			catch (RuntimeProgramException) { throw; }
			catch (Exception ex)
			{
				CodeExceptionHandler.Handle(ex, answer, goalStep);

				throw;
			}

		}

	}

}

