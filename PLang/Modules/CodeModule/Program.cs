using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PLang.Building.Model;
using PLang.Errors;
using PLang.Errors.Builder;
using PLang.Errors.Runtime;
using PLang.Interfaces;
using PLang.Models;
using PLang.Services.CompilerService;
using PLang.Utils;
using System.ComponentModel;
using System.Reflection;
using static PLang.Modules.CodeModule.Builder;

namespace PLang.Modules.CodeModule
{
	[Description("Generate or Run existing c# code from user description. Only use if no other module is found or if [code] is defined.")]
	public class Program : BaseProgram
	{
		private readonly IPLangFileSystem fileSystem;
		private readonly ILogger logger;

		public Program(IPLangFileSystem fileSystem, ILogger logger) : base()
		{
			this.fileSystem = fileSystem;
			this.logger = logger;
		}


		public override async Task<(object?, IError?)> Run()
		{
			Implementation? answer = null;
			try
			{
				var jobj = instruction.Action as JObject;
				if (jobj != null && jobj.Property("FileName") != null)
				{
					return RunFileCode(jobj);
				}

				answer = JsonConvert.DeserializeObject<Implementation?>(instruction.Action.ToString()!);
				if (answer == null)
				{
					return (null, new StepError("Code implementation was empty", goalStep));
				}

				string dllName = goalStep.PrFileName.Replace(".pr", ".dll");
				Assembly assembly = Assembly.LoadFile(Path.Join(Goal.AbsolutePrFolderPath, dllName));
				
				if (assembly == null)
				{
					return (null, new StepError($"Could not find {dllName}. Stopping execution for step {goalStep.Text}", goalStep));
				}

				List<Assembly> serviceAssemblies = new();

				if (answer.ServicesAssembly != null && answer.ServicesAssembly.Count > 0)
				{
					foreach (var serviceAssembly in answer.ServicesAssembly)
					{
						string assemblyPath = Path.Join(Goal.AbsoluteAppStartupFolderPath, serviceAssembly).AdjustPathToOs();
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
				/*
				AppDomain.CurrentDomain.AssemblyResolve += (sender, args) =>
				{
					var assembly = serviceAssemblies.FirstOrDefault(p => p.FullName == args.Name);
					return assembly;
				};*/

				Type? type = assembly.GetType(answer.Namespace + "." + answer.Name);
				if (type == null)
				{
					return (null, new StepError($"Type could not be loaded for {answer.Name}. Stopping execution for step {goalStep.Text}", goalStep));
				}

				MethodInfo? method = type.GetMethod("ExecutePlangCode");
				if (method == null)
				{
					return (null, new StepError($"Method could not be loaded for {answer.Name}. Stopping execution for step {goalStep.Text}", goalStep));
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
							return (null, new ProgramError($"{parameters[i].Name} is not defined in code. Please rebuild step", goalStep, function, StatusCode: 500));
						}

						var outParameter = answer.OutParameters.FirstOrDefault(p => p.ParameterName == parameters[i].Name);
						if (outParameter == null)
						{
							return (null, new ProgramError($"{parameters[i].Name} could not be found in build code. Please rebuild step", goalStep, function, StatusCode: 500));
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
								return (null, new ProgramError($"{parameters[i].Name} could not be found in build code. Please rebuild step", goalStep, function, StatusCode: 500));
							}
							var value = memoryStack.Get(inParameter.VariableName, parameters[i].ParameterType);
							parametersObject.Add(value);
						}
					}
				}
				var args = parametersObject.ToArray();
				logger.LogTrace("Parameters:{0}", args);
				object? result = method.Invoke(null, args);
				ReturnDictionary<string, object?> rd = new();
				
				for (int i = 0; i < parameters.Length; i++)
				{
					var parameterInfo = parameters[i];
					if (parameterInfo.IsOut || parameterInfo.ParameterType.IsByRef)
					{
						memoryStack.Put(parameterInfo.Name!, args[i]);
						rd.AddOrReplace(parameterInfo.Name!, args[i]);
					}
				}
				return (rd, null);
			}
			catch (Exception ex)
			{
				var error = CodeExceptionHandler.GetError(ex, answer, goalStep);

				return (null, error);
			}

		}

		private (object?, IError?) RunFileCode(JObject jobj)
		{
			var fileCode = jobj.ToObject<FileCodeImplementationResponse>();
			if (fileCode == null) return (null, new BuilderError("Could not map the instruction file"));

			var dllPath = GetPath(fileCode.FileName.Replace(".cs", ".dll"));
			var assembly = Assembly.LoadFrom(dllPath);

			var typeName = $"{fileCode.Namespace}.{fileCode.ClassName}".TrimStart('.'); // Replace with actual class name including namespace
			var methodName = fileCode.MethodName; // Replace with actual method name

			var type = assembly.GetType(typeName);
			var method = type.GetMethod(methodName);

			var instance = Activator.CreateInstance(type);

			List<object?> parameters = [];
			foreach (var param in fileCode.InputParameters)
			{
				parameters.Add(variableHelper.LoadVariables(param.Value));
			}

			try
			{
				var result = method.Invoke(instance, parameters.ToArray());

				return (result, null);

			} catch (Exception ex)
			{
				return (null, new ExceptionError(ex));
			}

		}
	}

}

