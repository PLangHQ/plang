# Understanding Identity in plang

## What is Identity?

Identity in plang offers a secure and private way of handling user identification, replacing traditional methods like email and password with a unique digital key.

> Building a web app? Read **[Identity in the browser](./IdentityInTheBrowser.md)** as well. The
> browser holds the identity and only requests made by the plang js client carry it, which decides
> how the first page load, sign in, images and webhooks have to work.

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

## Data Mining
%Identity% doesn’t stop services from mining your data, but it ensures that they can only analyze how you interact with their platform—strictly behavior data. Because no personal information is ever transmitted, this behavioral data remains entirely anonymous. Plus, with Plang's client-side code being open source, you can always verify that no personal data is being shared.

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
- before each goal in /api/*, call ValidateIdentity

ValidateIdentity
- if %Identity% is empty then
	- write out error "You need to sign your requests"
```

#### Access Control

Determine user access levels based on their unique identity.

```plang
ShowPanel
- select accessLevel from userPermissions where Identity=%Identity%
- if %accessLevel% equals 'admin', call ShowAdminPanel, else call ShowUserPanel
```

#### Personalized User Experience
Tailor content based on user preferences linked to their unique identity.

```plang
Preferences
- select preferences from userSettings where Identity=%Identity%
- call CustomizeContent %preferences%
```

### What is `%MyIdentity%`

The `%MyIdentity%` variable is your go-to for viewing your system’s identity. Unlike `%Identity%`, which reflects the identity of the computer or person sending you a message or request, `%MyIdentity%` specifically represents your computer’s identity. In short, if you need to know what identity your system will share during a request, you’ll refer to `%MyIdentity%`.

```plang
Start
- write out 'My identity is %MyIdentity%'
```

This identity is automatically included in every HTTP request, any file access outside your app’s scope, and every message you send.


### Signed requests: what the runtime checks for you

| variable | what it is | when it is empty |
|---|---|---|
| `%Identity%` | the identity that signed the request being handled (the caller). On a run of the app itself, it is `%MyIdentity%` | the request was not signed |
| `%MyIdentity%` | this app's own identity, as text | never |

When a request carries a signature, the web server checks it before the route's goal runs:

| check | answer when it fails |
|---|---|
| the signature is valid for the identity it names | 401 |
| it is younger than 5 minutes, or before its own `expires` | 401, `X-Signature-Error: expired` |
| its nonce has not been used before (a replay) | 403, `X-Signature-Error: nonce` |
| it was made for this url (path and query) and method | 401 |
| the signature is well formed (`created`, `identity`, a supported type) | 400 or 401 |

A refused request does not run the route's goal, but it does reach the app's error events (`on app error`).

So a goal never reads the signature, checks the time or keeps nonces. It only decides whether `%Identity%` is allowed:

```plang
DeployRoute
- if %Identity% != %Settings.DeployerIdentity% then
    - throw error "Not allowed", status code 401
- call goal Deploy
```

Compare `%Identity%` with an identity you trust, never a token in the url, a cookie you made up or an ip address.

Used nonces are kept in the memory of the running process. They are forgotten when the app restarts and are not shared between instances of the app. A nonce is only kept after a valid signature, so a refused request does not use it up.

#### Every request is signed

```plang
- get https://example.com/report, write to %report%
```

This request is signed, and a plang server sees your identity in `%Identity%`. To send a request without a signature:

```plang
- get https://feeds.example.org/feed.xml, do not sign the request, write to %feed%
```

Use `do not sign the request` only for a server that cannot take the extra headers. Do not use it towards your own plang servers: the server then gets an empty `%Identity%`. A multipart form post, `download file` and sending binary are not signed.

#### Calling your own app

- **The same running app:** use `call goal`, not http.
- **Another running instance of the app** (a test instance, a second server): a signed request to its route is right. When both share the app's identity, the receiving route checks `%Identity% == %MyIdentity%`.

### C# - Advanced Programming

For developers interested in the technical workings of plang's Identity, especially in C#, the following will provide insight into how user requests are signed and verified.

Plang uses Ed25519 for signing.
 
#### Signing Requests & Verifying Signatures

When message is sent using http, message(Nostr), file system, etc. I series of headers are set so that the recieving party can validate the signature and trust that the `%Identity%` is valid.

The properties are as follows in order:
- **X-Signature-Method**: The method being used, "GET", "POST", etc.
- **X-Signature-Url**: The URL being requested.
- **X-Signature-Created**: The time the signature is created. This is valid for a maximum of 5 minutes if expires is not defined.
- **X-Signature-Nonce**: A random GUID string, unique for each request.
- **X-Signature-Body**: Hash of the content being submited using keccak256
- **X-Signature-Contract**: Default is C0. This is to define contracts between the user and the service such as Terms of Service, etc.
- **X-Signature-Expires**: (Optional) When the signature expires. Time in milliseconds since 1. jan 1970.
- **X-Signature-Public-Key**: The public key of the signing key.
- **X-Signature**: Is the signature of the content sent. Only the user owning the private key can create this signature. It is created by merging the other properties with the content being sent.

**Source code:**
1. Signing service:
https://github.com/PLangHQ/plang/blob/main/PLang/Services/SigningService/PLangSigningService.cs
2. Identity service: 
https://github.com/PLangHQ/plang/blob/main/PLang/Services/IdentityService/PLangIdentityService.cs



