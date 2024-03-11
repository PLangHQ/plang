
# Cryptographic Module in PLang

## Introduction
The Cryptographic module in PLang is a powerful set of tools designed to provide developers with the ability to perform various cryptographic operations such as encryption, decryption, hashing, and token management. This module is essential for ensuring data security and integrity in applications.

## For Beginners
Cryptographic operations are like secret codes. They help keep information safe by transforming it into a form that only someone with the right key can understand. This is important for things like passwords, personal messages, or any data that needs to be kept private. In programming, we use these operations to protect data from being read or tampered with by unauthorized people.

## Best Practices for Cryptographic
When using cryptographic functions in PLang, it's important to follow best practices to ensure the security of your data:

1. **Use Strong Keys**: Always use strong, complex keys for encryption and hashing. Avoid common words or simple patterns.
2. **Keep Keys Secret**: Never expose your keys in your code or anywhere they could be accessed by others.
3. **Use Salts for Hashing**: When hashing data like passwords, use a salt to make it harder for attackers to guess the original value.
4. **Validate Inputs**: Always validate and sanitize inputs to cryptographic functions to prevent attacks.
5. **Stay Updated**: Keep your cryptographic algorithms up to date to protect against new vulnerabilities.

Here's an example of using a salt for hashing a password in PLang:

```plang
- set var %password% as 'MySecurePassword!'
- create salt, write to %salt%
- hash %password% using salt %salt%, write to %hashedPassword%
- write out 'Your hashed password is: %hashedPassword%'
```

In this example, we create a unique salt and use it to hash the password, which enhances security.

## Examples

# Cryptographic Module Examples

The Cryptographic module provides a range of functions for encryption, decryption, hashing, and token management. Below are examples of how to use these functions in the PLang language, sorted by their popularity and typical use cases.

## Encrypting and Decrypting Content

```plang
- set var %text% to 'Hello PLang world'
- encrypt %text%, write to %encryptedText%
- write out %encryptedText%
- decrypt %encryptedText%, write to %decryptedText%
- write out %decryptedText%
```

## Hashing and Verifying Passwords

```plang
- set var %password% as 'MySuperPassword123.'
- create salt, write to %salt%
- hash %password% using salt %salt%, write to %hashedPassword%
- write out 'Hashed Password: %hashedPassword%'
- validate hashed password 'MySuperPassword123.' with %hashedPassword%, write to %isValid%
- if %isValid% then
    - write out 'Password hash is valid'
```

## Managing Bearer Tokens

```plang
- set var %uniqueString% to 'user123'
- generate bearer token for %uniqueString%, expires in 2 weeks, write to %bearerToken%
- write out 'Bearer Token: %bearerToken%'
- validate bearer token %bearerToken%, write to %isValidBearer%
- if %isValidBearer% then
    - write out 'Bearer token is valid'
```

## HMAC SHA Hashing

```plang
- set var %input% to 'Sensitive data'
- set var %secretKey% to 'Secret123'
- hash HMAC SHA input %input% with key %secretKey%, write to %hmacShaHash%
- write out 'HMAC SHA Hash: %hmacShaHash%'
```

## Generating and Using Salts

```plang
- create salt with work factor 10, write to %salt%
- write out 'Generated Salt: %salt%'
```

## Retrieving Bearer Secret

```plang
- get bearer secret, write to %bearerSecret%
- write out 'Bearer Secret: %bearerSecret%'
```

## Setting Current Bearer Token

```plang
- set var %tokenName% to 'CurrentToken'
- set current bearer token %tokenName%
- write out 'Current bearer token set'
```

These examples demonstrate the versatility of the Cryptographic module in handling various cryptographic operations within the PLang language.


For a full list of examples, visit [PLang Cryptographic Examples](https://github.com/PLangHQ/plang/tree/main/Tests/Cryptographic).

## Step Options
When writing your PLang code, you can enhance your steps with these options for better control and error handling:

- [CacheHandler](/modules/handlers/CachingHandler.md): Manage caching to improve performance.
- [ErrorHandler](/modules/handlers/ErrorHandler.md): Handle errors gracefully.
- [RetryHandler](/modules/handlers/RetryHandler.md): Automatically retry steps on failure.
: Manage the cancellation of long-running steps.
: Execute steps without waiting for completion.

## Advanced
For those interested in diving deeper into the Cryptographic module and understanding how it interfaces with C#, check out the [advanced documentation](./PLang.Modules.CryptographicModule_advanced.md).

## Created
This documentation was created on 2024-01-02T21:42:14.
