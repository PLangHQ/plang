# CachingHandler Documentation

The `CachingHandler` in plang is designed to manage caching mechanisms efficiently. This document provides a comprehensive guide on how to implement caching in your plang scripts, covering both absolute and sliding time caching strategies. Additionally, it explains how to customize the caching backend by injecting a custom caching handler.

## Absolute Time Caching

In absolute time caching, an item is cached for a fixed duration from the time it is stored. Below is an example of how to implement absolute time caching in plang:

```plang
Start
- http://example.org
    write to %response%
    cache 'example.org', for 10 minutes
```

In this example, the response from `http://example.org` is cached under the key `'example.org'` for 10 minutes. It is crucial to specify a unique cache key to avoid overwriting existing cached data.

## Sliding Time Caching

Sliding time caching extends the cache duration each time the cached item is accessed. Here is how you can implement sliding time caching:

```plang
Start
- http://example.org
    write to %response%
    cache for 5 minutes from last usage
```

In this scenario, the cache duration resets to 5 minutes every time the cached item is accessed. Note that if the cache key is omitted, plang generates one automatically. However, this might lead to collisions with other cached items, so it is advisable to specify a unique cache key whenever possible.

## Custom Caching Handlers

By default, plang uses an in-memory caching mechanism. However, you can inject your own caching handler to utilize a different caching strategy, such as distributed caching. Below is an example of how to inject a Redis caching handler:

```plang
Start
- inject caching, /redis/redis.dll, use globally

LoadUrl
- http://example.org
    write to %response%
    cache 4 minutes
```

After injecting the Redis caching handler at the start of the application, all subsequent caching operations will utilize Redis.

For more details on how to inject custom caching handlers, refer to the [Services documentation](/Services.md).

## Conclusion

The `CachingHandler` in plang provides flexible options for caching, including absolute and sliding time strategies. It also supports the injection of custom caching handlers to suit different application needs. By following the examples and guidelines provided in this document, you can effectively implement and customize caching in your plang applications.