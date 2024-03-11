
# Caching

## Introduction
Caching is a crucial aspect of modern software development, allowing for the temporary storage of data to improve the efficiency and performance of applications. By storing frequently accessed data in a cache, programs can reduce the time and resources required to retrieve this data from slower storage mechanisms, such as databases or external services.

## For Beginners
Imagine you're at a library full of books, and you often need to reference a particular book. Instead of walking to the bookshelf every time to fetch it, you decide to keep it on your desk. This way, you can quickly grab it whenever you need it. Caching in programming works similarly; it keeps certain data readily available, so your program doesn't have to "walk to the bookshelf" every time it needs that data.

Caching helps speed up your program by storing pieces of information that are expensive (time-consuming) to get. For example, if your program needs to show the weather, it might take time to get that information from the internet. Instead, you can save the weather information in a cache and use it for a while before updating it again.

## Best Practices for Caching
When using caching in plang, it's important to consider the following best practices:

1. **Choose the Right Expiration Strategy**: Decide whether to use sliding expiration (cache expires after a period of inactivity) or absolute expiration (cache expires at a specific time).
2. **Cache Only What You Need**: Avoid caching unnecessary data. It's better to cache data that doesn't change often and is expensive to fetch.
3. **Handle Cache Misses Gracefully**: Be prepared for scenarios where the data is not in the cache and needs to be fetched again.
4. **Keep Cache Consistent**: Ensure that the cached data is updated or invalidated when the underlying data changes.

Let's look at an example:

```plang
- get 'http://api.example.com/data', write to %latestData%
- if %latestData% is not empty then call !CacheData, else !HandleError

  - CacheData:
    - cache %latestData% for 30 minutes, to 'dataKey'
    - write out 'Data cached successfully.'

  - HandleError:
    - write out 'Failed to fetch data. Please try again later.'
```

In this example, we attempt to fetch data from an external API. If successful, we cache it with a sliding expiration of 30 minutes. If the fetch fails, we handle the error by displaying a message.

## Examples

# Caching Module Examples

The Caching module provides methods for interacting with the application's cache. Below are examples of how to use the Caching module in plang, sorted by the most popular usage scenarios.

## Set Cache with Sliding Expiration

This is commonly used to cache items that should expire after a certain period of inactivity.

```plang
- cache 'userSessionData' for 10 minutes, to 'sessionKey'
```

Maps to C# method:
```csharp
SetForSlidingExpiration("sessionKey", userSessionData, TimeSpan.FromMinutes(10));
```

## Get Cached Item

Retrieving a cached item is a frequent operation to avoid unnecessary computations or data fetching.

```plang
- get cache 'sessionKey', write to %cachedSession%
- write out %cachedSession%
```

Maps to C# method:
```csharp
var cachedSession = Get("sessionKey");
```

## Set Cache with Absolute Expiration

This method is used when you want to cache an item until a specific point in time.

```plang
- cache 'dailyNews' until 2.1.2023 21:27:19, to 'newsKey'
```

Maps to C# method:
```csharp
SetForAbsoluteExpiration("newsKey", dailyNews, new DateTimeOffset(2023, 1, 2, 21, 27, 19, TimeSpan.Zero));
```

## Remove Cached Item

Removing a cached item is necessary when the data is no longer needed or has become stale.

```plang
- remove cache 'obsoleteDataKey'
```

Maps to C# method:
```csharp
RemoveCache("obsoleteDataKey");
```

## Cache External Data with Sliding Expiration

Caching data fetched from external sources can significantly reduce load times and bandwidth usage.

```plang
- get https://goweather.herokuapp.com/weather/, write to %weatherData%
- cache %weatherData% for 10 minutes, to 'weatherKey'
```

Maps to C# method:
```csharp
var weatherData = // Code to fetch weather data
SetForSlidingExpiration("weatherKey", weatherData, TimeSpan.FromMinutes(10));
```

## Retrieve and Display Cached External Data

After caching external data, it can be retrieved and displayed as needed.

```plang
- get cache 'weatherKey', write to %cachedWeather%
- write out %cachedWeather%
```

Maps to C# method:
```csharp
var cachedWeather = Get("weatherKey");
// Code to display weather data
```

Note: If a method returns a value, the step should end with "write to %variableName%", and the %variableName% can then be used in the next step for demonstration.


For a full list of caching examples in plang, please visit [PLang Caching Examples](https://github.com/PLangHQ/plang/tree/main/Tests/Caching).

## Step Options
When writing your plang code, you can enhance your steps with the following options. Click on the links for detailed usage instructions:

- [CacheHandler](/modules/handlers/CachingHandler.md)
- [ErrorHandler](/modules/handlers/ErrorHandler.md)
- [RetryHandler](/modules/handlers/RetryHandler.md)



## Advanced
For more advanced information on caching in plang and its underlying mapping with C#, refer to the [Advanced Caching Documentation](./PLang.Modules.CachingModule_advanced.md).

## Created
This documentation was created on 2024-01-02T21:28:07.
