# Security : Part 1 of X

This will be series of posts. Each tackling part of security that we have problems with to day.

> 80% of hacks can be contributed to passwords

Plang does not use passwords. It's cool tech, same that make Bitcoin secure. Plang uses %Identity%

## %Identity%

When user commicates with other web services, plang signes the request. This means that the server on the other end can use %Identity% as the password to you account.

Let's take typical web service today, they ask you for email and password. The nr. 1 reason for this is to find you in the database.

This is what a developer does in the backend

```plang
LoadUser
- select user_id from users where email=%email% and password=%password%
```

This will give us the `id` of the user, the `user_id`. This means we can now, go to the medical records table and just show you your records

```plang
- select * from medical_records where user_id=%user_id%
```
As you can see, we filter after the `user_id` and only show you what you own.

With %Identity% you can use that instead of email and password. So instead the `select` above, it would look like this

```plang
LoadUser
- select user_id from users where Identity=%Identity%
```

and nothing changes to the medical_records query, it stays the same.

This means that you don't have to know any passwords, and you don't need to worry about them getting your email or other personal data.


The `%Identity%` is created the same way as Ethereum keys

Plang creates a private key in the `Settings` table in the `./db/settings.sqlite`. It is not very protected now, but you can [overwrite where settings are store](../Services.md). So somebody will make more secure version.


Lesson 8 - Identity 2