namespace PLang.Models;

public interface IReturnDictionary
{
}

public class ReturnDictionary<TKey, TValue> : Dictionary<TKey, TValue>, IReturnDictionary
{
}