using LightInject;
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



				return (MapReturn(result), null);
			}
			catch (Exception ex)
			{
				var engine = ((ServiceContainer) container).GetInstance<IEngine>();
				var error = await CodeExceptionHandler.GetError(engine, ex, implementation, goalStep, context);

				return (null, error);
			}

		}

		private object? MapReturn(object? result)
		{
			if (function.ReturnValues == null || function.ReturnValues.Count == 0) return result;

			Type resultType = result.GetType();

			// Check if the type is a tuple (ValueTuple or Tuple)
			if (resultType.IsGenericType &&
				(resultType.FullName?.StartsWith("System.ValueTuple`") == true ||
				 resultType.FullName?.StartsWith("System.Tuple`") == true))
			{
				var tupleItems = new List<ObjectValue>();
				int counter = 0;
				// Get all fields/properties from the tuple
				if (resultType.FullName.StartsWith("System.ValueTuple`"))
				{
					// ValueTuple uses fields (Item1, Item2, etc.)
					foreach (var field in resultType.GetFields())
					{
						var value = field.GetValue(result);
						if (value != null)
						{
							var rv = function.ReturnValues[counter++];
							tupleItems.Add(new ObjectValue(rv.VariableName, value));
						}
					}
				}
				else
				{
					// Tuple uses properties (Item1, Item2, etc.)
					foreach (var property in resultType.GetProperties())
					{
						var value = property.GetValue(result);
						if (value != null)
						{
							var rv = function.ReturnValues[counter++];
							tupleItems.Add(new ObjectValue(rv.VariableName, value));
						}
					}
				}

				result = tupleItems;
			}
			return result;
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

			}
			catch (Exception ex)
			{
				return (null, new ExceptionError(ex));
			}

		}

		private (List<object?>? Parameters, IError? Error) GetParameters(ParameterInfo[] parameters, IReadOnlyList<Parameter>? inputParameters, IReadOnlyList<BaseBuilder.ReturnValue>? returnValues)
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

					var outParameter = returnValues.FirstOrDefault(p => p.VariableName.Trim('%').Equals(parameters[i].Name, StringComparison.OrdinalIgnoreCase));
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
						var inParameter = inputParameters.FirstOrDefault(p => p.Name.Trim('%').Equals(parameters[i].Name, StringComparison.OrdinalIgnoreCase));
						if (inParameter == null)
						{
							return (null, new ProgramError($"{parameters[i].Name} could not be found in build code. Please rebuild step", goalStep, function, StatusCode: 500));
						}

						object? value;
						if (VariableHelper.IsVariable(inParameter.Value))
						{
							value = memoryStack.Get(inParameter.Value?.ToString(), parameters[i].ParameterType);
						}
						else
						{
							value = inParameter.Value;
						}
						parametersObject.Add(value);


					}
				}
			}
			return (parametersObject, null);
		}

	}

}

