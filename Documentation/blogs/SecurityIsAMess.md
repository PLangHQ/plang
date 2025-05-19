# Security is a Mess – How Plang Helps Fix It

Security is hard. As developers, we’re not exactly great at it. The issue is that security is binary—it either works 100% or it doesn’t at all. If there’s a flaw in your code, an attacker could gain full access to your system. Worse, you can never truly prove that something is 100% secure. All you can do is try to prevent every possible way in, which requires a lot of experience and a lot of eyes on the code. The only thing that gets you closer to 100% is time and scrutiny.

## Scrutiny & Time

That’s the key to secure code: experienced developers looking at it, lots of them, and enough time passing without a breach. We never *know* it’s secure. We can only hope.

## The State of Development

If I asked 10 developers to write code that reads a file into a variable, I’d get 10 different implementations. Some would be buggy. Some would be insecure. That’s where we are today.

## LLM Coding

If you use an LLM to write the same thing, it’s the same story. Ask 10 times, and you’ll get 10 different versions—some with security flaws, some with bugs.

## Plang

Plang is intent-based programming. That means the developer just describes what they want to happen, and Plang handles it. For example, reading a file looks like this:

```plang
- read file.txt into %content%
```

The intent here is simple: read the contents of `file.txt` into the variable `%content%`.

Now here’s the key part: no matter how many developers write this in Plang, or how they phrase it, it will *always* result in the same underlying code. You can [view it yourself here](https://github.com/PLangHQ/plang/blob/3c1d89c9148a063bd6e916b802950626d5143fed/PLang/Modules/FileModule/Program.cs#L204) (if you’re technical enough to follow it).

That’s huge. No matter how many billions of lines are written to read files in Plang, they’ll all run through the same \~40 lines of code. (It’s 40 lines because the file module does more than just reading—there’s more to discover.)

## Higher Risk Examples

Reading a file is simple. Not typically a big security concern. So let’s take a more sensitive example: password hashing.

Hashing passwords isn’t trivial. You need to *really* know what you’re doing. Most people don’t. And here are some ways you can get completely SCREWED:

* Use the wrong algorithm? You’re screwed.
* Don’t use a salt? Screwed.
* Use the same salt every time? Screwed.
* Use a bad salt? Screwed.
* Too few iterations? Screwed.

I’m not even an expert in hashing, so this list is probably missing a few traps.

Ask 10 developers or an LLM 10 times to write a password hashing function, and you’ll get 20 different versions. LLMs might actually outperform humans here—but there’s still variance and risk.

In Plang, here’s how you do it:

```plang
- hash %password%, write to %hashedPassword%
```

You just want the password hashed. If you're curious about the details, [you can read the source code](https://github.com/PLangHQ/plang/blob/3c1d89c9148a063bd6e916b802950626d5143fed/PLang/Modules/CryptographicModule/Program.cs#L122).

Now, I’m no specialist, so maybe it’s wrong. But the good news? It’s open source. Anyone can scrutinize it. Experts can review and improve it. I’m not one of them—but plenty of people are.

## Usernames and Passwords = BAD

Oh boy. Usernames and passwords. What a nightmare.

They’re a pain for developers to manage, and they’re the weakest link in most systems. The solution? Public/private key cryptography. PGP (Pretty Good Privacy) has been around since the '90s and was basically that. But it was never made easy for developers to implement, so usernames and passwords stuck around.

Here’s where Plang shines again: all developers use the *same* code when writing functionality. That includes HTTP requests.

For example:

```plang
- get https://jsonplaceholder.typicode.com/posts/, write to %posts%
```

[More on HTTP here](HttpRequests.md)

This uniformity opens up new possibilities. Plang can automatically sign every request. The developer doesn’t have to think about it. On the server side, that request includes the [identity of the sender](https://github.com/PLangHQ/plang/blob/main/Documentation/Identity.md). No username or password needed.

Since every HTTP request in Plang runs through [the same code](https://github.com/PLangHQ/plang/blob/3c1d89c9148a063bd6e916b802950626d5143fed/PLang/Modules/HttpModule/Program.cs#L19), it can be reviewed, audited, and made secure by experienced eyes.

Removing usernames and passwords removes a *massive* security hole from your system.

## More Security, Fewer Bugs

All of this leads to one simple conclusion:

Plang apps have fewer bugs and are more secure.

