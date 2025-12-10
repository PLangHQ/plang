# Lesson 5: Errors, Retry, Caching and Events

## Errors

If you want to handle errors in your code, it's very simple:

```plang
- read file.txt into %content%
    on error !HandleError
- write out %content%

HandleError
- write out %!error%
```

Here you learn how to call another goal and handle errors that occur.

You have options, such as:

```plang
- read file.txt into %content%
    on error that contains 'not found', call !HandleNotFound
    on other errors call !HandleError
- write out %content%
```

So you are flexible on which errors you catch.

## Retry

If a step fails, you can make the program retry that step again.

```plang
GetHttp
- https://jsonplaceholder.typicode.com/users/1
    retry 5 times over a 5 minute period
    write to %response%
- write out %response%
```

Here your program will retry 5 times over a 5-minute period.

## Caching

It's easy to cache:

```plang
GetHttp
- run expensiveCalculations.py, %number1%, %number2%
    cache for 10 minutes
    write to %results%
- write out %results%
```

The program here calls an external Python script that is expensive to run and caches the result for 10 minutes.

This is also beneficial for the external script as it doesn't need to handle caching.

## Events

You can bind events to application start & end, goal start & end, and step start & end.

To create events, start by creating a folder named `Events` (case sensitive, one of the few things in Plang). Now create `Events.goal`. This file contains the events that will be bound.

```plang
Events
- on app start, call !AppStarted
- on goal start, for path '/api/*', call !Authenticate
- before step, call !BenchmarkTimer
```

You can even bind an event to a variable, even if it doesn't exist:

```plang
- when var %content% is created, call !ContentCreated
- on %content% update, call !ContentUpdate
- on %content% remove, call !ContentRemoved
```

## Go to [Next Steps >>](./Lesson%206.md)
