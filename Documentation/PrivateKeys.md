# Plang Private Keys Management Guide

Welcome to the comprehensive guide for managing private keys in the Plang programming environment. This document is crafted to assist developers in securely handling private keys, ensuring the integrity and confidentiality of their applications. Plang is compatible with Windows, Linux, and macOS, and this guide will cover the nuances of working with private keys across these platforms.

## Storing Private Keys

In Plang, private keys are stored within a SQLite database located at `.db/system.sqlite`. It is important to emphasize that these keys are stored in plaintext and are not encrypted within the database.

### Root Private Key Storage

The root private key is stored in a global `system.sqlite` database. The path to this database is dependent on the operating system you are using:

- **Windows**: `C:\Users\[Username]\AppData\Roaming\plang\.db\system.sqlite`
- **Linux**: `/home/[Username]/plang/.db/system.sqlite`
- **macOS**: `/Users/[Username]/plang/.db/system.sqlite`

Please replace `[Username]` with your actual username on the system.

### Private Key Varieties

Plang manages three distinct types of private keys:

1. **Encryption Keys**: These keys are used to encrypt and decrypt data, ensuring that sensitive information remains secure.
2. **Blockchain Keys**: These keys are essential for signing requests, enabling `%Identity%` (refer to [Identity](./Identity.md) for more details), and facilitating other blockchain-related actions.
3. **Nostr Keys**: These keys are utilized for sending and receiving messages within the Nostr protocol.

## Exporting Private Keys

The `ExportPrivateKey` method in ModuleSettings.cs in Plang allows for the exportation of private keys. 

During the export process, Plang will prompt the user with three security questions. The responses are then analyzed by a Language Learning Model (LLM) to assess the likelihood of the user being a target of social engineering. If the risk is determined to be high, Plang will proactively block the export for a 24-hour period. 

This feature serves as a proof of concept to enhance user protection against social engineering attacks.


### Security Considerations

Developers must handle private keys with extreme caution due to their sensitive nature. While Plang includes security features to aid developers, it is imperative to adhere to key management and security best practices within your applications.

## Backup Recommendations

It is advisable to regularly back up critical private keys to prevent loss of access or data. Ensure that backups are stored securely and are accessible only to authorized personnel.

## Conclusion

This guide has outlined the key aspects of private key management within the Plang environment. By familiarizing yourself with the storage paths, key types, and the exportation process, you can confidently secure private keys in your development practices. Remember, prioritizing security is paramount when dealing with cryptographic materials.