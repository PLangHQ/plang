# In Theory: The Future of Security—A New Approach with Plang

> This "In Theory" article explores what’s possible with Plang. There are no technical barriers—just engineering time to bring these ideas to life.

I’ve always believed in aiming for 100% security because without it, achieving 100% privacy is impossible. 

Now, reaching that perfect 100%? That’s a different story—nearly unattainable. But the goal is to reduce risks until you're left with just one crucial point to protect. That singular point? It’s the private key.

### Enter Plang

Let’s talk about [Plang](https://plang.is), a platform that introduces something special: signatures.

Every bit of data you send through Plang is signed. This signature ensures that the data remains secure, and it all ties back to one thing—the private key. 

Plang boils the problem of security down to this one key. So, how do you protect it?

Right now, we rely on methods like fingerprints, PINs, face scans, and other authentication technologies. These tools provide robust security for safeguarding the private key. Hacking a private key from a modern smartphone is a tall order. Even desktop environments are improving, especially with WebAuthn. And if you’re really serious about security, external hardware like dongles can store your private key, offering an extra layer of protection.

### Granular Permissions: Tailoring Security Levels

For something as simple as an HTTP request, you might not need the full-on security of a PIN or fingerprint scan. Plang supports low-priority signatures—perfect for sending basic data while still validating your identity.

But when it comes to higher-stakes operations, like transferring money, more rigorous security is needed. You could be prompted for a face scan or a PIN. You can even define specific rules:

“Any transaction over $30 needs my thumbprint or face scan. Anything over $100? I want my face scan, thumbprint, and PIN.”

When an application asks for a signature, it’s calling a C# `Sign` function. This function interacts with your hardware, determining whether a simple face scan suffices or if you need to provide additional verification. 

The beauty here? The code remains clean, straightforward, and highly resistant to manipulation. And since Plang code is signed, you know it will only do what it’s supposed to do.

### So, Why Plang?

Plang isn’t just another programming language like Kotlin or any of the other dozens out there. It’s a layer *on top* of those languages. 

What does that mean? It simplifies programming, drastically. It takes care of much of the security heavy lifting, making it less likely for you to make mistakes. Essentially, Plang sits between you and your operational languages (like C#), limiting what you can do to only what’s safe and allowed.

And it’s not just a black box. The execution layer is in plain text, not binary, so both security experts and tools like large language models can easily validate it, preventing potential bad behavior.

The gap between Plang and C# is razor-thin—usually just a few lines of code. This thin layer creates a buffer zone. If a bug exists in the C# code, it’s easier to catch and correct. And when a vulnerability is discovered, like the infamous Log4j issue, Plang could address it with an update, meaning you wouldn’t have to modify your code at all.

### SaaS Integration: Simplicity and Security in Minutes

Plang can be integrated into any SaaS service with an API in just a few minutes, offering a level of security that’s unparalleled.

Here’s how it works: 

The user submits their username and password, and all requests are signed. This signature is now tied to their credentials. From that point on, the API can interact securely with their identity. It’s all about the user ID and its associated identity.

On the server side, your login code might look something like this:

```plang
Login
- hash %request.password% using XXX algorithm, with YYY as salt, write to %hashedPassword%
- select id from users where %request.username% and %hashedPassword%, return 1 row
- if %id% is not empty
    - update users set %Identity% where %id%
```

And on the client side, the login process is equally simple:

```plang
Login
- https://myservice.com/login
    { username: %Settings.Username%, password: %Settings.Password% }
```

Once logged in, the client can retrieve data securely—without needing to resend the username and password over and over again.

By focusing security around one critical point—the private key—Plang makes the entire process more secure and much simpler. In an ever-changing digital landscape filled with threats, innovations like Plang are exactly what we need to protect both our data and our privacy.

### Private Keys for Every Service

When interacting with two different services, Plang assigns a different private key for each one. This means your identity isn’t trackable across services. Even better, if one private key is compromised, it won’t affect the others. 

Even if a service is compromised, your private key is safe. Those who compromise the service are not able to execute command on that service in your name.

### Backing Up the Private Key

What happens if you lose your device? Are your private keys gone for good?

The solution lies in backup systems. The idea is to store keys in a secure way so you can recover them later. But storing them sounds risky, right?

That’s where trust comes in. You can designate people you trust—family, friends, even a lawyer—to help. Using Plang, it’s easy to encrypt and divide your private key among trusted individuals through a technique called *Shamir’s Secret Sharing*. You could, for instance, require three out of five trusted contacts to unlock your key. 

If you lose your device, you simply set up a new identity, reach out to those trusted contacts, and in no time, you’ll have your private key—and all your associated data—restored. You can find more about this concept in [this GitHub issue](https://github.com/PLangHQ/plang/issues/16).

Shamir’s Secret Sharing is just the technology to do this, the user would never know about it as it would be abstracted away in the UX.

### A Few Final Thoughts

As the title suggests, this is all “in theory.” None of this is science fiction—the tech is out there. It’s been implemented in other systems and languages, particularly in C#. What’s needed is to bring it all together within Plang.

While some of the ideas discussed here haven’t been implemented yet, they’re entirely feasible. There’s still work to be done—figuring out backup storage, UX considerations, and additional security concerns. But while it’s not easy, it’s certainly within reach.