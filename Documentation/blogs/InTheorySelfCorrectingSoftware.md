# In Theory: Self Correcting software

In plang you can bind events to goals and steps. This is very powerfull. 

Let's create a scenario. You want to make sure somebody is logged in to be able to call a function in /admin

No problem in plang
```plang
Events
- before any goal in /admin, call AuthenticateUser

AuthenticateUser
- select id as userId from users where %Identity% and role='admin', return 1 row
- if %userId% is empty then
    - show error "You don't have permission"
```

There you have authentication solved. In 4 lines of code.

## Next step: Events on Modules

I want to be able to say

```
Events
- before any HttpModule.Post and Get, call AnalyzeRequest
```

Now, you should let your mind create what AnalyzeRequest is.

So, how does this translate to self correcting software.

## Self correcting software

Lets say I am doing a POST request to a service.

```plang
CreateUserAsExample
- post https://example.org/api/createuser
    {name:"%name%", }
```

Now we bind and event to all modules or just specific ones
```
Events
- on error on all modules
- on error on HttpModule.Post and Get, call SelfCorrect

SelfCorrect
- [llm] system: fix this plang code...
    user:%!error%
    write to %code%
- write to %!error.Goal.RelativePath%
- build plang code
- retry step
```

Now we asked the LLM to fix that plang code, if the createuser service requires now more then name, it will return error message
"{error:"You need to provide Email (%email%)"}

