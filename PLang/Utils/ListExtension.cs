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

}
