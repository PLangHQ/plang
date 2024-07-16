# Security: Part 1 of X

> This is a working documentation. If you'd like to help out, check out the Issue for it.

This will be a series of posts, each tackling a part of security that we have problems with today.

> 80% of hacks can be attributed to passwords.

Plang does not use passwords. It's cool tech, the same that makes Bitcoin secure. Plang uses `%Identity%`.

## %Identity%

When a user communicates with other web services, Plang signs the request. This means that the server on the other end can use `%Identity%` as the password to your account.

Let's take a typical web service today; they ask you for an email and password. The number one reason for this is to find you in the database.

This is what a developer does in the backend:

```plang
LoadUser
- select user_id from users where email=%email% and password=%password%
```

This will give us the `user_id`. This means we can now go to the medical records table and just show you your records:

```plang
- select * from medical_records where user_id=%user_id%
```

As you can see, we filter by the `user_id` and only show you what you own.

With `%Identity%`, you can use that instead of an email and password. So instead of the `select` above, it would look like this:

```plang
LoadUser
- select user_id from users where Identity=%Identity%
```

And nothing changes in the medical_records query; it stays the same.

This means that you don't have to know any passwords, and you don't need to worry about them getting your email or other personal data.

The `%Identity%` is created the same way as Ethereum keys.

Plang creates a private key in the `Settings` table in the `./db/settings.sqlite`. It is not very protected now, but you can [override where settings are stored](../Services.md). So somebody will make a more secure version.

## [Lesson 8 - Identity 2](./Lesson%208.md)
