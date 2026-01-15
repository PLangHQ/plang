using Newtonsoft.Json;

namespace PLang.Utils
{
	public static class ListExtension
	{

		public static string ToJson<T>(this List<T> list)
		{
			return JsonConvert.SerializeObject(list);
		}
	}

	public static class ObjectExtension
	{

		public static string ToJson(this object obj)
		{
			return JsonConvert.SerializeObject(obj);
		}

		public static object? GetValueOnProperty(this object obj, string propertyName)
		{
			if (propertyName.StartsWith("!"))
			{
				if (propertyName.Equals("!data")) return JsonConvert.SerializeObject(obj);
			}
			var property = obj.GetType().GetProperty(propertyName);
			if (property == null) return null;
			return property.GetValue(obj, null);
		}
	}
}
