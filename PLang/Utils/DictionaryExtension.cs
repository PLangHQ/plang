namespace PLang.Utils;

public static class DictionaryExtension
{
    public static void AddOrReplace<TKey, TValue>(this Dictionary<TKey, TValue?>? dict, TKey key, TValue? value)
    {
        if (dict is null) return;

        if (dict.ContainsKey(key))
            dict[key] = value;
        else
            dict.Add(key, value);
    }

    public static void AddOrReplace<TKey, TValue>(this Dictionary<TKey, TValue?>? dict,
        Dictionary<TKey, TValue?>? addDict)
    {
        if (dict == null || addDict == null) return;
        foreach (var item in addDict)
            if (dict.ContainsKey(item.Key))
                dict[item.Key] = item.Value;
            else
                dict.Add(item.Key, item.Value);
    }
}