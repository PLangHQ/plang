using Microsoft.Extensions.Logging;
using PLang.Building.Model;
using PLang.Building.Parsers;
using PLang.Errors;
using PLang.Errors.Builder;
using PLang.Exceptions;
using PLang.Runtime;
using System.ComponentModel;
using System.Text.RegularExpressions;

namespace PLang.Modules.PlangCodeModule
{
	[Description("Compiles plang code, gets goals from .goal files, method descriptions and class scheme")]
	public class Program : BaseProgram
	{
		private readonly ILogger logger;
		private readonly IGoalParser goalParser;

		public Program(ILogger logger, IGoalParser goalParser) : base()
		{
			this.logger = logger;
			this.goalParser = goalParser;

		}


		public async Task<List<Goal>> GetGoals(string filePath)
		{
			string path = GetPath(filePath);
			return goalParser.ParseGoalFile(path);
		} 

		public async Task<(string?, IError?)> GetModules(string stepText, List<string> excludeModules)
		{
			var modulesAvailable = typeHelper.GetModulesAsString(excludeModules);
			var userRequestedModule = GetUserRequestedModule(stepText);
			if (excludeModules != null && excludeModules.Count == 1 && userRequestedModule.Count == 1
				&& userRequestedModule.FirstOrDefault(p => p.Equals(excludeModules[0])) != null)
			{
				return (null, new BuilderError($"Could not map {stepText} to {userRequestedModule[0]}"));
			}

			if (userRequestedModule.Count > 0)
			{
				modulesAvailable = string.Join(", ", userRequestedModule);
			}
			return (modulesAvailable, null);
		}

		public async Task<string> GetMethods(string moduleName)
		{
			var programType = typeHelper.GetRuntimeType(moduleName);
			var methods = typeHelper.GetMethodsAsString(programType);
			return methods;
		} 

		public async Task<string> GetMethodDescription(string moduleName, string methodName)
		{
			return "";
		}

		public async Task<string> GetMethodMappingScheme()
		{
			return "";
		}

		private List<string> GetUserRequestedModule(string stepText)
		{
			var modules = typeHelper.GetRuntimeModules();
			List<string> forceModuleType = new List<string>();
			var match = Regex.Match(stepText, @"\[[\w]+\]", RegexOptions.IgnoreCase | RegexOptions.Multiline);
			if (match.Success)
			{
				var matchValue = match.Value.ToLower().Replace("[", "").Replace("]", "");
				List<string> userRequestedModules = new List<string>();
				var module = modules.FirstOrDefault(p => p.Name.ToLower() == matchValue);
				if (module != null)
				{
					userRequestedModules.Add(module.Name);
				}
				else
				{
					foreach (var tmp in modules)
					{
						if (tmp.FullName != null && tmp.FullName.ToLower().Contains(matchValue.ToLower()))
						{
							userRequestedModules.Add(tmp.FullName.Replace(".Program", ""));
						}
					}
				}
				if (userRequestedModules.Count == 1)
				{
					forceModuleType.Add(userRequestedModules[0]);
				}
				else if (userRequestedModules.Count > 1)
				{
					forceModuleType = userRequestedModules;
				}
			}
			return forceModuleType;
		}

	}
}

