# Jwt Token And Plang

So you already have clients out there that are using your service, they are sending JWT, they can't be asked to change their system.

So how can you move to plang but still support those that use JWT.

When they ask for a new token, you do in plang

```plang
- generate jwt token, write to %token%
- write out %token%
```

The user will then send the next request with that bearer token.

And on your end, you do nothing. Just start using %identity% to lookup the user

```plang
- select * from users where identity=%Identity%, return 1, write to %user%
- ... // do more stuff
```

You want to see it in action? Here it is.

Did you notice how short this post was? Thats how plang is, everything is short.