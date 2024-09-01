# In Theory: Self-Correcting Software

In Plang, you can bind events to goals and steps, a feature that proves incredibly powerful.

Let's consider a scenario: you want to ensure that a user is logged in before they can call a function in the `/admin` route. 

No problem in Plang:

```plang
Events
- before any goal in /admin, call AuthenticateUser

AuthenticateUser
- select id as userId from users where %Identity% and role='admin', return 1 row
- if %userId% is empty then
    - show error "You don't have permission"
```

And there you have it—authentication is solved in just four lines of code.

## Next Step: Events on Modules

I'll start by saying, you cannot bind events to module yet in plang, that why the title "In Theory"

Now, imagine this:

```plang
Events
- before any HttpModule.Post and Get, call AnalyzeRequest
```

Take a moment to consider what `AnalyzeRequest` could be. 

How does this translate to self-correcting software?

## Self-Correcting Software

Let's say I'm making a POST request to a service:

```plang
CreateUserAsExample
- post https://example.org/api/createuser
    {name:"%name%", }
```

Now, let's bind an event to all modules or just specific ones:

```plang
Events
- on error on all modules
- on error on HttpModule.Post and Get, call SelfCorrect

SelfCorrect
- [llm] system: fix this plang code...
    user:%!error.Message%
    write to %code%
- write to %!error.Goal.RelativePath%
- build plang code
- retry step
```

Here, we've instructed the LLM to fix the Plang code. 

If the `/api/createuser` service now requires more than just a name, it will return an error message such as `"{error:"You need to provide Email (%email%)"}`.

Asking the [current Plang assistant](https://chatgpt.com/share/78637171-19bd-40d5-9c16-e53bd64c12b1) to handle this scenario would give you an updated response:

```plang
CreateUserAsExample
- post https://example.org/api/createuser
    {name:"%name%", email:%email%}
```

Now, plang code can save the new code to its path, build and run the code.

But What if the Email is Still Empty?

Yes, the %email% field might still be empty. 

In this case, you need to traverse up the call stack and fix the previous goal. Once one issue is resolved, the same principle applies to the next one. It's just a matter of engineering.
