using System.Reflection;

namespace PLang.Utils
{
	public static class RecordInitializer
	{
		public static T FromDictionary<T>(T instance, Dictionary<string, object?>? dict) where T : class
		{
			if (dict == null) return instance;

			var type = typeof(T);

			foreach (var kvp in dict)
			{
				var prop = type.GetProperty(kvp.Key, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
				if (prop == null) continue;

				var convertedValue = kvp.Value == null ? null : Convert.ChangeType(kvp.Value, prop.PropertyType);


				prop.SetValue(instance, convertedValue);

			}

			return instance;
		}

		private static bool IsInitOnly(this MethodInfo method)
			   => method.ReturnParameter.GetRequiredCustomModifiers().Contains(typeof(System.Runtime.CompilerServices.IsExternalInit));
	}
}
