namespace PLang.Variables;

using PLang.Attributes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

public class PipedClassDiscovery
{
	public List<Type> GetPipedClasses()
	{
		var pipedClasses = new List<Type>();
		var assemblies = AppDomain.CurrentDomain.GetAssemblies();

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