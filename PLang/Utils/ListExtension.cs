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
	}
}
