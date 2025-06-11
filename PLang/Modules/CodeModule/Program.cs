using Microsoft.Extensions.Logging;
using Nethereum.ABI.Model;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PLang.Attributes;
using PLang.Building.Model;
using PLang.Errors;
using PLang.Errors.Builder;
using PLang.Errors.Runtime;
using PLang.Interfaces;
using PLang.Models;
using PLang.Runtime;
using PLang.Services.CompilerService;
using PLang.Utils;
using System.ComponentModel;
using System.Reflection;
using System.Runtime.InteropServices;
using static PLang.Modules.BaseBuilder;
using static PLang.Modules.CodeModule.Builder;
using static PLang.Runtime.Startup.ModuleLoader;
using Parameter = PLang.Modules.BaseBuilder.Parameter;

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


		public async Task<(object?, IError?)> RunInlineCode([HandlesVariable] CodeImplementationResponse implementation)
		{
			try
			{
				if (implementation == null)
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

				if (implementation.Assemblies != null && implementation.Assemblies.Count > 0)
				{
					foreach (var serviceAssembly in implementation.Assemblies)
					{/*
						var assemblyPath = Path.Join(RuntimeEnvironment.GetRuntimeDirectory(), serviceAssembly);
						if (File.Exists(assemblyPath))
						{
							serviceAssemblies.Add(Assembly.LoadFile(assemblyPath));
						}
						else
						{
							assemblyPath = Path.Join(Goal.AbsoluteAppStartupFolderPath, serviceAssembly).AdjustPathToOs();
							if (fileSystem.File.Exists(assemblyPath))
							{
								serviceAssemblies.Add(Assembly.LoadFile(assemblyPath));
							}
						}*/
					}
				}

				Type? type = assembly.GetType(implementation.Namespace + "." + implementation.Name);
				if (type == null)
				{
					return (null, new StepError($"Type could not be loaded for {implementation.Name}. Stopping execution for step {goalStep.Text}", goalStep));
				}

				MethodInfo? method = type.GetMethod("ExecutePlangCode");
				if (method == null)
				{
					return (null, new StepError($"Method could not be loaded for {implementation.Name}. Stopping execution for step {goalStep.Text}", goalStep));
				}
				var parameters = method.GetParameters();

				(var parametersObject, var error) = GetParameters(parameters, implementation.Parameters, implementation.ReturnValues);
				if (error != null) return (null, error);
				
				var args = parametersObject!.ToArray();
				logger.LogTrace("Parameters:{0}", args);
				object? result = method.Invoke(null, args);
			
				return (result, null);
			}
			catch (Exception ex)
			{
				var error = CodeExceptionHandler.GetError(ex, implementation, goalStep);

				return (null, error);
			}

		}


		public async Task<(object?, IError?)> RunFileCode([HandlesVariable] FileCodeImplementationResponse implementation)
		{
			if (implementation == null) return (null, new BuilderError("Could not map the instruction file"));

			var dllPath = GetPath(implementation.FileName.Replace(".cs", ".dll"));
			var assembly = Assembly.LoadFrom(dllPath);

			var typeName = $"{implementation.Namespace}.{implementation.ClassName}".TrimStart('.'); // Replace with actual class name including namespace
			var methodName = implementation.MethodName; // Replace with actual method name

			var type = assembly.GetType(typeName);
			if (type == null) return (null, new ProgramError($"Could not load {typeName}."));

			var method = type.GetMethod(methodName);
			if (method == null) return (null, new ProgramError($"Method {methodName} could not be found."));

			var instance = Activator.CreateInstance(type);			
			var parameters = method.GetParameters();

			(var parametersObject, var error) = GetParameters(parameters, implementation.Parameters, implementation.ReturnValues);
			if (error != null) return (null, error);

			try
			{
				var result = method.Invoke(instance, parametersObject!.ToArray());

				return (result, null);

			} catch (Exception ex)
			{
				return (null, new ExceptionError(ex));
			}

		}

		private (List<object?>? Parameters, IError? Error) GetParameters(ParameterInfo[] parameters, List<Parameter>? inputParameters, List<BaseBuilder.ReturnValue>? returnValues)
		{
			List<object?> parametersObject = new();
			for (var i = 0; i < parameters.Length; i++)
			{
				if (parameters[i].IsOut || parameters[i].ParameterType.IsByRef)
				{
					Type? outType = parameters[i].ParameterType.GetElementType();
					if (outType == null) continue;
					if (returnValues == null)
					{
						return (null, new ProgramError($"{parameters[i].Name} is not defined in code. Please rebuild step", goalStep, function, StatusCode: 500));
					}

					var outParameter = returnValues.FirstOrDefault(p => p.VariableName == parameters[i].Name);
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
				else if (inputParameters != null)
				{

					var parameterType = parameters[i].ParameterType;
					if (parameterType.FullName == "PLang.SafeFileSystem.PLangFileSystem")
					{
						parametersObject.Add(fileSystem);
					}
					else
					{
						var inParameter = inputParameters.FirstOrDefault(p => p.Name == parameters[i].Name);
						if (inParameter == null)
						{
							return (null, new ProgramError($"{parameters[i].Name} could not be found in build code. Please rebuild step", goalStep, function, StatusCode: 500));
						}
						var value = memoryStack.Get(inParameter.Name, parameters[i].ParameterType);
						parametersObject.Add(value);
					}
				}
			}
			return (parametersObject, null);
		}

	}

}

