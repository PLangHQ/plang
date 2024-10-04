using Microsoft.Extensions.Logging;
using PLang.Interfaces;
using System.Reflection;

namespace PLang.Utils
{
	public class DependancyHelper
	{
		private readonly IPLangFileSystem fileSystem;
		private readonly ILogger logger;

		public DependancyHelper(IPLangFileSystem fileSystem, ILogger logger)
		{
			this.fileSystem = fileSystem;
			this.logger = logger;
		}

	

		public List<Type> LoadModules(Type assignableFromType, string goalPath)
		{
			List<Type> modules = new();

			var executingAssembly = Assembly.GetExecutingAssembly();

			var builderModules = executingAssembly.GetTypes()
				.Where(t => assignableFromType.IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract)
			.ToList();
			modules.AddRange(builderModules);

			string modulesDirectory = Path.Combine(goalPath, ".modules");
			if (!fileSystem.Directory.Exists(modulesDirectory)) return modules;
			var dllFiles = fileSystem.Directory.GetFiles(modulesDirectory, "*.dll");
			foreach (var dll in dllFiles)
			{
				Assembly loadedAssembly = Assembly.LoadFile(dll);
				try
				{
					var typesFromAssembly = loadedAssembly.GetTypes()
						.Where(t => assignableFromType.IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract)
						.ToList();
					modules.AddRange(typesFromAssembly);
				}
				catch (ReflectionTypeLoadException ex)
				{
					if (!AppContext.TryGetSwitch("InternalGoalRun", out bool isEnabled) || !isEnabled)
					{
						LoadDependencies(dll, ex, assignableFromType, goalPath);
					}
				}
			}
			return modules;

		}

		private void LoadDependencies(string file, ReflectionTypeLoadException ex, Type assignableFromType, string goalPath)
		{
			var fileNameWithoutExtension = fileSystem.Path.GetFileNameWithoutExtension(file);
			var dirPath = fileSystem.Path.GetDirectoryName(file);
			var depsFilePath = fileSystem.Path.Join(dirPath, fileNameWithoutExtension + ".deps.json");
			if (!fileSystem.File.Exists(depsFilePath)) throw ex;

			var parameters = new Dictionary<string, object?>();
			parameters.Add("depsFile", depsFilePath);
			parameters.Add("pathToSave", dirPath);

			List<string> libraries = new();

			var le = ex.LoaderExceptions.FirstOrDefault();
			if (le is FileNotFoundException fne && fne.FileName != null)
			{
				var library = fne.FileName.Substring(0, fne.FileName.IndexOf(','));
				parameters.Add("libraryName", library);

				logger.LogDebug($"Installing depency {library}, data is comding from {depsFilePath} and nuget package will be saved it to {dirPath}");

				var error = Executor.RunGoal("/external/plang/Runtime/InstallDependencies.goal", parameters).GetAwaiter().GetResult();
				if (error != null)
				{
					throw new Exception(error.ToString());
				}

				var dllFiles = fileSystem.Directory.GetFiles(dirPath, "*.dll", SearchOption.AllDirectories);
				foreach (var dll in dllFiles)
				{
					Assembly loadedAssembly = Assembly.LoadFile(dll);
				}
				return;
			}
			throw ex;


		}
	}
}
