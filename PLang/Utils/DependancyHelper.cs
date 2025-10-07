using Microsoft.Extensions.Logging;
using NBitcoin.Protocol;
using PLang.Building.Parsers;
using PLang.Interfaces;
using PLang.SafeFileSystem;
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

			string modulesDirectory = fileSystem.Path.Join(goalPath, ".modules");
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

					var assembly = LoadDependencies(dll, ex, assignableFromType, goalPath);

				}
			}
			return modules;

		}

		private Assembly? LoadDependencies(string file, ReflectionTypeLoadException ex, Type assignableFromType, string goalPath)
		{
			if (AppContext.TryGetSwitch("InternalGoalRun", out bool isEnabled) && isEnabled) return null;

			var fileNameWithoutExtension = fileSystem.Path.GetFileNameWithoutExtension(file);
			var dirPath = fileSystem.Path.GetDirectoryName(file);
			var depsFilePath = fileSystem.Path.Join(dirPath, fileNameWithoutExtension + ".deps.json");
			if (!fileSystem.File.Exists(depsFilePath)) throw ex;



			List<string> libraries = new();

			var le = ex.LoaderExceptions.FirstOrDefault();
			if (le is FileNotFoundException fne && fne.FileName != null)
			{
				var library = fne.FileName.Substring(0, fne.FileName.IndexOf(','));

				return InstallDependancy(dirPath, depsFilePath, library);
			}
			throw ex;


		}

		public Assembly? InstallDependancy(string? dirPath, string depsFilePath, string library)
		{
			if (AppContext.TryGetSwitch("InternalGoalRun", out bool isEnabled) && isEnabled)
			{
				Console.WriteLine("InternalGoalRun - will stop");
				return null;
			}
			
			var parameters = new Dictionary<string, object?>();
			parameters.Add("depsFile", depsFilePath);
			parameters.Add("pathToSave", dirPath);
			parameters.Add("libraryName", library);

			logger.LogDebug($"Installing depency {library}, data is coming from {depsFilePath} and nuget package will be saved it to {dirPath}");
			Console.WriteLine($"Installing depency {library}, data is coming from {depsFilePath} and nuget package will be saved it to {dirPath}");
			var installerFolder = fileSystem.Path.Join(fileSystem.RootDirectory, "apps/Installer").AdjustPathToOs();
			if (!fileSystem.Directory.Exists(installerFolder))
			{
				var plangFolder = AppContext.BaseDirectory;
				string planInstallerFolder = fileSystem.Path.Join(plangFolder, "Goals/apps/Installer").AdjustPathToOs();
				/*
				var task = fileAccessHandler.ValidatePathResponse(fileSystem.RootDirectory, planInstallerFolder, "y");
				task.Wait();
				(var success, var error2) = task.Result;
				if (error2 != null) throw new Exception(error2.ToString());
				*/
				DirectoryHelper.Copy(planInstallerFolder, installerFolder);
				//prParser.ForceLoadAllGoals();
			}
			try
			{			

				(var engine, var vars, var error) = Executor.RunGoal("/apps/Installer/InstallDependencies.goal", parameters).GetAwaiter().GetResult();
				if (error != null)
				{
					throw new Exception(error.ToString());
				}
			}
			catch (Exception ex)
			{
				int i = 0;
				throw;
			}

			var dllFiles = fileSystem.Directory.GetFiles(dirPath, "*.dll", SearchOption.AllDirectories);
			foreach (var dll in dllFiles)
			{
				if (dll.Contains(library))
				{
					return Assembly.LoadFile(dll);
				}
			}
			return null;
		}
	}
}
