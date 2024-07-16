# Security - Authentication

Having `%Identity%` for web service is incredable valiable. think about it, you don't need any registration, login, or forgotten password. 

Huge security whole just closed and you just saved a lot of work.

## Authentication

So how do you authenticate the user on your server. There are two ways to go about.

You can use the `%Identity%` everywhere, this variable is available on request and if the user does not sign his request, it is empty.

This is how you validate that `%Identity%` is not empty if you need to make sure that user is logged in

We start by binding an event to all goals coming into the /api path. 
```plang
Events
- before goal in 'api/*', call CheckIdentity
```

Then create `CheckIdentity.goal` file
```plang
CheckIdentity
- if %Identity% is empty, then
    - write out error 'You must sign request to use this service'
```

Now you will always be sure that you have `%Identity%` when you are executing your gols in the `api` folder.


Personally, I like to use `%userId%` in my application. But where does that `%userId%` come from?

`%userId%` comes because I create have a users table in my database. So in my `CheckIdentity` I have it like this. Leave the `Events.goal` as is.

```plang
CheckIdentity
- if %Identity% is empty, then call !NotSigned, LoadUser

NotSigned
- write out error 'You must sign request to use this service'

LoadUser
- select id as userId from users where %Identity%, return 1 row
- if %userId% is null then
    - insert into users %Identity%, write to %userId%
```

Now I have access to the variable `%userId%` in my goals. This is practical because you might want to keep behaviour data about your users, such as when the account was created.

> Note: The sql query automatically loads the columns as `%variables%` from the sql query when you return 1 row

## More documentation

That is it for now, help me build, come and checkout the [issue for the documentation](https://github.com/PLangHQ/plang/issues/42)

See also possible your [next steps](./Lesson%206.md)