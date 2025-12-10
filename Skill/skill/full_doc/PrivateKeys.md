# Private Keys in Plang

In Plang, private keys are crucial for various operations, including encryption, blockchain activities, and messaging. This document provides a comprehensive guide on how private keys are managed, stored, and exported within the Plang environment.

## Storage of Private Keys

All private keys in Plang are stored in a SQLite database located at `.db/system.sqlite`. It is important to note that these keys are not encrypted, so securing access to this file is critical.

### Default Storage Paths

The default path for the `system.sqlite` file varies depending on the operating system:

- **Windows**: `C:\Users\[Username]\AppData\Roaming\plang\.db\system.sqlite`
- **Linux**: `/home/[Username]/plang/.db/system.sqlite`
- **macOS**: `/Users/[Username]/plang/.db/system.sqlite`

If Plang does not have permission to write to these locations, it will default to writing in the directory from which Plang is executed.

## Types of Private Keys

Plang can create up to four types of private keys, depending on the modules in use:

1. **Identity Keys**: Always created and used to encrypt and decrypt data. [Learn more about Identity Keys](./Identity.md)
2. **Encryption Keys**: Created upon first use for data encryption and decryption.
3. **Blockchain Keys**: Created upon first use for blockchain-related actions.
4. **Nostr Keys**: Created upon first use for sending and receiving messages.

## Exporting Private Keys

When attempting to export private keys using the `ExportPrivate` method, Plang will prompt the user with three questions. The responses are analyzed by a language model to assess the likelihood of the user being deceived. If the risk is deemed high, Plang will block the export for 24 hours. This feature is a proof of concept aimed at preventing social engineering attacks on unsuspecting users. [Read more about this feature](https://ingig.substack.com/p/exporting-private-keys-in-plang).

## Backup of Private Keys

Backing up critical private keys is essential, especially in the early versions of Plang (v.0.1), as the language does not provide any automated backup solutions. Users must manually back up their keys to ensure data security and continuity.

## Additional Reading

For those interested in the topic of private keys, consider reading this blog post about sharing private keys between two computers: [Plang and Local-First](https://ingig.substack.com/p/plang-and-local-first). Note that sharing Identity Keys between devices is not recommended. Each device should maintain its own identity, and services should support multiple identities per user.