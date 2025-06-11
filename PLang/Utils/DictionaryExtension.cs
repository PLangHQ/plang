
using System.Collections.Concurrent;

namespace PLang.Utils
{
	public static class DictionaryExtension
	{
		private static readonly Lock _lock = new();

		public static void AddOrReplaceDict<TKey, TValue>(this Dictionary<TKey, TValue?>? dict, Dictionary<TKey, TValue?>? dict2)
		{
			if (dict2 is null || dict is null) return;

			foreach (var item in dict2)
			{
				dict.AddOrReplace(item.Key, item.Value);
			}
		}
		public static void AddOrReplace<TKey, TValue>(this Dictionary<TKey, TValue?>? dict, TKey key, TValue? value)
		{
			if (dict is null) return;


			try
			{
				lock (_lock)
				{
					if (dict.ContainsKey(key))
					{
						dict[key] = value;
					}
					else
					{
						dict.TryAdd(key, value);
					}
				}
			} catch 
			{
				throw;
			}
		}

		public static void AddOrReplace<TKey, TValue>(this Dictionary<TKey, TValue?>? dict, Dictionary<TKey, TValue?>? addDict)
		{
			if (dict == null || addDict == null) return;
			foreach (var item in addDict)
			{

				if (dict.ContainsKey(item.Key))
				{
					dict[item.Key] = item.Value;
				}
				else
				{
					dict.Add(item.Key, item.Value);
				}
			}
		}

	}
}
