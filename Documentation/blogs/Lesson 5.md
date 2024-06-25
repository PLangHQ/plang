# Errors, Retry, Caching and Events

## Errors

if you want to handle error for you code, it very simple

```plang
- read file.txt into %content%
    on error !HandleError
- write out %content%

HandleError
- write out %!error%
```

There you learn how to call another goal, and there we handle error that happen.

You have options, such as 

 ```plang
- read file.txt into %content%
    on error that contains 'not found', call !HandleNotFound
    on other errors !HandleError
- write out %content%
```

so you are flexible on what error you catch

## Events

You can bind events to application start & end, goal start & end and step start & end.

To create events, start by creating a folder `Events` (case senstive, one of few things in plang). Now create `Events.goal`, this file contains the events that will be bound.

```plang
Events
- on app start, call !AppStarted
- on goal start, for path '/api/*', call !Authenticate
- before step, call !BenchmarkTimer
```

You can even bind an event to a varible, even if it doesn't exist

```
- when var %content% is created, call !ContentCreated
- on %content% update, call !ContentUpdate
- on %content% remove, call !ContentRemoved
```

Lesson 6 - Next steps