﻿namespace PLang.Interfaces;

public interface IAppCache
{
    Task<object?> Get(string key);
    Task Remove(string key);
    Task Set(string key, object value, DateTimeOffset absoluteExpiration);
    Task Set(string key, object value, TimeSpan slidingExpiration);
}