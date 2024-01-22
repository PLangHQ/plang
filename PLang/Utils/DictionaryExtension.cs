
namespace PLang.Utils
{
	public static class DictionaryExtension
	{
		public static void AddOrReplace<TKey, TValue>(this Dictionary<TKey, TValue>? dict, TKey key, TValue value)
		{
			if (dict is null) return;

			try
			{
				if (dict.ContainsKey(key))
				{
					dict[key] = value;
				}
				else
				{
					dict.Add(key, value);
				}
			} catch 
			{
				throw;
			}
		}

	}
}
