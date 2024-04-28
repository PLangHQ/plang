# Understanding Identity in plang

## What is Identity?

Identity in plang offers a secure and private way of handling user identification, replacing traditional methods like email and password with a unique digital key.

## Identity Explained
In the world of web development, when a web service asks you to log in using your email and password, the main reason is to identify you in order to show you your data and not someone else's.

What happens in the background is that the developer takes your email and password and tries to match them with a row in the users' table in the database. If a match is found with a record in the database, it will provide the developer with your `userId`.

The developer will then use this `userId` to retrieve and show you your data. This is how developers determine which data belongs to you and what to display. The `userId` is your identity in the system.

So as you can see, Identity has nothing to do with what your name and email is. It is all about finding your data in the system.

In plang, this `userId` is built into the programming language. It is not called `userId`; instead, it is referred to as `%Identity%`.

plang creates this `%Identity%` on your computer and only your computer can create this specific `%Identity%`, so when we send it to a server, we can be sure that it is you and nobody else. 

## The Users' Advantage

Users no longer need to remember a username and password to access services. They can enjoy a totally friction-free user experience along with great security.

## Privacy for Users

Web services don't need to store your email or password. They don't need your name. You can use their service without ever giving up private information.

## The Developer's Advantage

For developers, plang's built-in `%Identity%` simplifies user authentication.

There's no need to manage keys or complex processes; plang handles everything, allowing developers to focus on building their application's core functionalities.

You no longer need to think about:
- Registration
- Login
- Forgot passwords
- User management such as changing name, email, passwords
- Payment methods (although not Identity, your payment comes for free with it)

This simplifies development, reduces risk, and makes laws like GDPR less cumbersome.

## Privacy for Services

There is no reason to store users private data in your database. It is better for the customer and better for you as a service provider. It reduces risk, as you don't store any private information about your customers.

## Security

Having a very strong `%Identity%` built into the programming language prevents many of the security issues we have today. 

- No more passwords, eliminating one of the most common reasons for hacks to occur. 
- No more outsourcing your login to 3rd parties such as Facebook, Google, Microsoft, Twitter, therefore reducing risk.
- Removes Man in the Middle attacks (MITM), as the signature not only proves the `%Identity%` but also that the content hasn't been manipulated.

As you dig deeper and use plang more, you will be surprised by all the benefits of having the `%Identity%` created by the user.

## Weakness
There is only one weakness. That is if somebody can access your private key, then that person can act as you. 

Since it is only one weakness, it is possible to implement extreme measures to protect it. 

Mobile phones have for example extremly high security to store you private key, where you need the device, bio and pin code to get data signed with it. This can be taken even further with dongles, multiple signature, and other offline devices.

> It should be noted that plang does not implement any security measure as of version 0.1 and the private key is stored in clear text on the computer. This will of course be fixed in future versions.

## Want to Dig Deeper, Want to Use `%Identity%`?

These examples show how you can use `%Identity%` in your plang goal files to manage user interactions and data securely and efficiently.

### Examples

`%Identity%` is a reserved keyword in the plang programming language. 

Anyone who creates an HTTP request to your service using the plang language automatically sends `%Identity%`, unless defined specifically not to send it.

`%Identity%` is a long string that your service can use as proof that a specific user is making the request and the content has not been manipulated.
#### Create user

Let's say you have a users table where you want maintain your users. In this example, the users table contains a column called `Identity`. To create a user you simply do

```plang
CreateUser
- insert into users, %Identity%, write to %userId%
- write out 'New user id: %userId%
```

#### Load user

To load a user from your database, you retrieve him by the `%Identity%`

```plang
LoadUser
- select id from users where %Identity%
- write out 'This is user id: %id%'
```


#### Managing User Data
This is an example of a GET service that would be located at http://myservice.com/api/GetBalance
```plang
GetBalance
- select balance from users where Identity=%Identity%, return 1 row
- write out %balance%
```
Securely fetch and manage user-specific data using the `%Identity%`. You don't need to load the `%Identity%`, it simply exists.

#### Validate user is logged in
You can add an event that runs before each goal to check if a user is logged in.

```plang
Events
- before each goal in /api/*, call !ValidateIdentity

ValidateIdentity
- if %Identity% is empty then
	- write out error "You need to sign your requests"
```

#### Access Control

Determine user access levels based on their unique identity.

```plang
ShowPanel
- select accessLevel from userPermissions where Identity=%Identity%
- if %accessLevel% equals 'admin', call !ShowAdminPanel, else !ShowUserPanel
```

#### Personalized User Experience
Tailor content based on user preferences linked to their unique identity.

```plang
Preferences
- select preferences from userSettings where Identity=%Identity%
- call !CustomizeContent %preferences%
```

### C# - Advanced Programming

For developers interested in the technical workings of plang's Identity, especially in C#, the following C# code snippets provide insight into how user requests are signed and verified. 
 
#### Signing Requests & Verifying Signatures

The properties are as follows:
- **X-Signature**: Is the signature of the content sent. Only the user owning the private key can create this signature. It is created by merging the other properties with the content being sent.
- **X-Signature-Created**: The time the signature is created. This is valid for a maximum of 5 minutes.
- **X-Signature-Nonce**: A random GUID string, unique for each request.
- **X-Signature-Address**: The public address of the blockchain key.
- **X-Signature-Contract**: Default is C0. This is to define contracts between the user and the service such as Terms of Service, etc.

Check out the source code at 
https://github.com/PLangHQ/plang/blob/main/PLang/Services/IdentityService/PLangIdentityService.cs



