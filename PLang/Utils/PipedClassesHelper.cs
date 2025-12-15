using PLang.Attributes;
using System.Collections.Generic;
using System.Reflection;

namespace PLang.Utils
{
	public class PipedClassesHelper
	{
		public List<Type>? GetPipedClasses()
		{
			var pipedClasses = new List<Type>();

			// Get all loaded assemblies
			var assemblies = AppDomain.CurrentDomain.GetAssemblies();

			foreach (var assembly in assemblies)
			{
				try
				{
					// Get all types from the assembly that have the [Piped] attribute
					var types = assembly.GetTypes()
						.Where(t => t.GetCustomAttribute<PipedAttribute>() != null)
						.ToList();

					pipedClasses.AddRange(types);
				}
				catch (ReflectionTypeLoadException)
				{
					// Skip assemblies that can't be loaded
					continue;
				}
			}

			return pipedClasses;
		}

		// If you only want to search specific assemblies (more efficient)
		public List<Type> GetPipedClassesFromAssembly(Assembly assembly)
		{
			return assembly.GetTypes()
				.Where(t => t.GetCustomAttribute<PipedAttribute>() != null)
				.ToList();
		}

		// If you want to search in the current assembly and referenced PLang assemblies
		public List<Type> GetPipedClassesFromPlang()
		{
			var pipedClasses = new List<Type>();
			var assemblies = AppDomain.CurrentDomain.GetAssemblies()
				.Where(a => a.FullName.StartsWith("PLang", StringComparison.OrdinalIgnoreCase) ||
						   a == Assembly.GetExecutingAssembly());

			foreach (var assembly in assemblies)
			{
				try
				{
					var types = assembly.GetTypes()
						.Where(t => t.GetCustomAttribute<PipedAttribute>() != null)
						.ToList();

					pipedClasses.AddRange(types);
				}
				catch (ReflectionTypeLoadException)
				{
					continue;
				}
			}

			return pipedClasses;
		}
	}
}
