using Nethereum.Contracts.Standards.ERC721.ContractDefinition;
using PLang.Errors;
using PLang.Interfaces;
using PLang.Models;
using System.Reflection;

namespace PLang.Utils
{
	public class AssemblyLoader
	{
		public (ObjectValue<List<T>?>, IError?) LoadImplementations<T>(IPLangFileSystem fileSystem, string absolutePath) where T : class
		{
			if (!fileSystem.File.Exists(absolutePath))
			{
				return (null, new Error($"DLL not found: {absolutePath}"));
			}
			
			try
			{
				var fullPath = fileSystem.Path.GetFullPath(absolutePath);
				var assembly = Assembly.LoadFrom(fullPath);

				var implementations = GetImplementations<T>(assembly);
				var ov = new ObjectValue<List<T>>(implementations);

				return (ov, null);
			}
			catch (Exception ex)
			{
				return (null, new Error($"Failed to load implementations from {absolutePath}: {ex.Message}", Exception: ex));
			}
		}

		private List<T> GetImplementations<T>(Assembly assembly) where T : class
		{
			var targetInterface = typeof(T);
			var results = new List<T>();

			var types = assembly.GetTypes()
				.Where(t => targetInterface.IsAssignableFrom(t)
						 && !t.IsInterface
						 && !t.IsAbstract);

			foreach (var type in types)
			{
				var instance = Activator.CreateInstance(type) as T;
				if (instance != null)
				{
					results.Add(instance);
				}
			}

			return results;
		}
	}
}
