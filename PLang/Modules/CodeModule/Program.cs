using IdGen;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Newtonsoft.Json;
using PLang.Exceptions;
using PLang.Interfaces;
using PLang.Runtime;
using PLang.Services.CompilerService;
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

		public Program(IPLangFileSystem fileSystem) : base()
		{
			this.fileSystem = fileSystem;
		}


		public override async Task Run()
		{
			
			var answer = JsonConvert.DeserializeObject<Implementation>(instruction.Action.ToString());
			string dllName = goalStep.PrFileName.Replace(".pr", ".dll");
			Assembly assembly = Assembly.LoadFile(Path.Combine(Goal.AbsolutePrFolderPath, dllName));

			if (assembly == null)
			{
				throw new Exception($"Could not find {dllName}. Stopping execution for step {goalStep.Text}");
			}

			Type type = assembly.GetType(answer.Name);
			MethodInfo method = type.GetMethod("Process");
			var parameters = method.GetParameters();

			List<object> parametersObject = new List<object>();
			for (var i = 0; i < parameters.Length; i++)
			{
				var parameterType = parameters[i].ParameterType;
				if (parameterType.FullName == "PLang.SafeFileSystem.PLangFileSystem")
				{
					parametersObject.Add(fileSystem);
				}
				else if (parameters[i].IsOut || parameters[i].ParameterType.IsByRef)
				{
					Type outType = parameters[i].ParameterType.GetElementType();
					if (outType.IsValueType)
					{
						parametersObject.Add(Activator.CreateInstance(outType));
					}
					else
					{
						var value = memoryStack.Get(parameters[i].Name, parameters[i].ParameterType);
						parametersObject.Add(value);
					}

				}
				else
				{
					var value = memoryStack.Get(parameters[i].Name, parameters[i].ParameterType);
					parametersObject.Add(value);
				}
			}
			var args = parametersObject.ToArray();
			try
			{
				object result = method.Invoke(null, args);

				for (int i = 0; i < parameters.Length; i++)
				{
					var parameterInfo = parameters[i];
					if (parameterInfo.IsOut || parameterInfo.ParameterType.IsByRef)
					{
						memoryStack.Put(parameterInfo.Name, args[i]);
					}
				}
			}
			catch (Exception ex)
			{
				CodeExceptionHandler.Handle(ex, answer, goalStep);

				throw;
			}

		}

	}

}

