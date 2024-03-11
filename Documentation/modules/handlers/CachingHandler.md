# CachingHandler Guide for plang

## Introduction to CachingHandler

The `CachingHandler` in plang is a powerful tool that allows you to cache the results of specific steps in your code. This can significantly improve the performance of your applications by reducing the need to repeatedly fetch the same data. There are two primary caching strategies available: Absolute Time Caching and Sliding Time Caching.

## Absolute Time Caching

This strategy is used when you want to cache data for a fixed duration. The cached data will expire after the specified time has elapsed, regardless of how often it is accessed.

### Example: Absolute Time Caching

```plang
Start
- http://example.org
    write to %response%
    cache 'example.org', for 10 minutes
```

In this example, the data fetched from `http://example.org` is stored in the cache under the key `'example.org'` for a duration of 10 minutes.

**Cache Key:** It is crucial to provide a unique cache key, in this case, `'example.org'`, to avoid overwriting different cached data.

## Sliding Time Caching

This caching strategy extends the lifetime of the cached data each time it is accessed. It is ideal for scenarios where data should expire after a certain period of inactivity.

### Example: Sliding Time Caching

```plang
Start
- http://example.org
    write to %response%
    cache for 5 minutes from last usage
```

Here, the data is cached without an explicit key, and plang will manage the key internally. The cache duration is reset to 5 minutes with every access to the cached item.

**Note:** Omitting the cache key allows plang to generate one, but this can lead to potential cache collisions. Use this approach with caution.

## Custom Caching with IAppCache

By default, `CachingHandler` uses an in-memory cache. However, you can customize the caching mechanism by injecting your own caching handler, such as a distributed cache like Redis.

### Example: Injecting a Custom Caching Handler

```plang
Start
- inject caching, /redis/redis.dll, use globally

LoadUrl
- http://example.org
    write to %response%
    cache 4 minutes
```

After injecting the Redis caching handler at the start, all subsequent caching in the application will be managed by Redis.

**Further Reading:** To learn more about injecting custom services, refer to the [Services documentation](/Services.md).

## Best Practices

- Always use unique and descriptive cache keys.
- Select the caching strategy that best fits your application's needs.
- Be mindful of memory usage when caching data, especially with in-memory caches.

By incorporating these caching techniques into your plang applications, you can optimize performance and reduce the load on external systems.