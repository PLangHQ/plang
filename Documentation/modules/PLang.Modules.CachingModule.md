# Caching

## What is Caching (Module)
Caching is a technique used to store data temporarily in a fast-access layer, improving performance and reducing the need to retrieve or compute the same information repeatedly.

### Another way to explain Caching (Module)

#### ELI5
Imagine you have a toy box at home. Instead of going to the store every time you want to play, you keep your favorite toys in the box. Caching is like that toy box, keeping things you use a lot close by so you can get them quickly.

#### Business Perspective
From a business standpoint, caching is a strategy to enhance efficiency and user experience. It saves time and resources by storing frequently accessed data or computations, reducing the load on systems and speeding up response times for users.

# Best Practices for Caching

- **Set Appropriate Expiration Times**: Determine the right expiration time for each type of cached data. For instance, cache user session data for the duration of the session, but cache static content like CSS files for longer periods since they change infrequently.

- **Implement Cache Invalidation**: Develop a method to invalidate cache entries when the original data changes. For example, if a product price is updated in the database, ensure that the cached product page reflects this change immediately.

- **Monitor Cache Size**: Keep an eye on the cache size to prevent it from using too much memory. You might set a maximum size for the cache and use a least-recently-used (LRU) algorithm to remove old entries.

- **Selective Caching**: Only cache data that is costly to compute or retrieve and that is read frequently. Avoid caching data that is rarely accessed or quick to generate, like simple database queries.

- **Secure Sensitive Data**: If you must cache sensitive information, encrypt this data in the cache and ensure that only authorized users can access it. For example, user passwords should never be cached in plain text.

- **Cache Dependencies**: Be aware of dependencies between cached items and invalidate related caches as needed. For example, if you cache a user's profile page and their order history separately, updating the order history should invalidate the cache for the profile page if it displays the latest order.


# Examples

### Example 1: Basic Caching and Retrieval
```
UserSessionCache
- cache %userSessionData% to 'sessionKey'
- get cache 'sessionKey', write to %retrievedSession%
- if %retrievedSession% is not empty
  - write out 'Session data retrieved successfully!'
  - write out %retrievedSession%
- else
  - write out 'No session data found.'
```

### Example 2: Conditional Caching with Expiration
```
WeatherDataCache
- if %weatherUpdateRequired% equals true
  - cache %currentWeatherData% for 2 hours, to 'weatherKey'
  - write out 'Weather data has been updated in cache.'
- get cache 'weatherKey', write to %cachedWeather%
- write out 'Current weather:'
- write out %cachedWeather%
```

### Example 3: Removing Cached Data
```
ClearUserCache
- get cache 'userProfileKey', write to %cachedProfile%
- if %cachedProfile% is not empty
  - remove cache 'userProfileKey'
  - write out 'User profile cache cleared.'
- else
  - write out 'No user profile cache to clear.'
```

### Example 4: Updating Cached Data with Sliding Expiration
```
UpdateProductCache
- get cache 'productList', write to %cachedProducts%
- if %productsChanged% equals true
  - cache %updatedProductList% for sliding expiration of 30 minutes, to 'productList'
  - write out 'Product list cache has been updated.'
- else
  - write out 'No changes detected. Product list cache remains the same.'
```

### Example 5: Caching with Absolute Expiration
```
CacheEventDetails
- cache %eventDetails% until %eventEndTime%, to 'eventKey'
- write out 'Event details cached with absolute expiration set to event end time.'
```

# CSharp

## CachingModule

Source code: [CachingModule.cs](https://github.com/PLangHQ/Plang/modules/CachingModule.cs)

### Methods in the CachingModule class

- `Get(string key)`
  Retrieves an object from the cache using the specified key.

- `SetForSlidingExpiration(string key, object value, TimeSpan? slidingExpiration = null)`
  Adds an object to the cache with a sliding expiration policy, which resets each time the item is accessed.

- `SetForAbsoluteExpiration(string key, object value, DateTimeOffset absoluteExpiration)`
  Adds an object to the cache with a fixed expiration time.

- `RemoveCache(string key)`
  Removes an object from the cache using the specified key.
