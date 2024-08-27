# Message

## Introduction
The Message module in the PLang programming language provides a robust framework for handling messaging operations within applications. It allows developers to send and receive messages, manage user accounts, and interact with message relays, making it an essential tool for applications requiring communication capabilities.

## For Beginners
For those new to programming or unfamiliar with technical jargon, the Message module in PLang can be thought of as a post office for your application. It helps your application to send out messages (like sending a letter), receive messages (like getting mail), and manage information about where messages should go (like an address book). It simplifies the process of communication in your software, allowing you to focus on what messages to send and receive without worrying about the underlying complexities of network communication.

## Best Practices for Message
When using the Message module in your PLang code, it's important to follow certain best practices to ensure efficient and error-free operations:

1. **Always Handle Errors**: Ensure that your message operations are wrapped with error handling to manage any issues that might occur during message transmission or reception.
2. **Use Variables Wisely**: Store frequently used data like public keys in variables to make your code cleaner and more efficient.
3. **Keep Security in Mind**: Be cautious with sensitive information such as private keys. Ensure they are securely handled and not exposed in logs or to unauthorized users.

### Example
Here's a simple example to illustrate these best practices:
```plang
- get public key for messages, write to %pubKey%
- try
    - send message to %pubKey%, 'Hello, secure world!'
  catch
    - write out 'Failed to send message.'
```
In this example, we first retrieve and store the public key in a variable `%pubKey%`. We then attempt to send a message using this public key, and if an error occurs, we handle it gracefully by logging an error message.

## Examples
For practical applications and more detailed examples of how to use the Message module, please refer to the following code snippets and resources:

# Message Module Examples

The Message module in PLang allows for sending and receiving private messages, managing accounts, and interacting with message relays. Below are some practical examples of how to use the Message module in PLang, sorted by their expected frequency of use in real-world applications.

## Sending and Receiving Messages

### Send a Private Message to Myself
This example demonstrates how to send a private message to oneself. It can be useful for testing or reminders.
```plang
- send my self message, 'Hi, how are you on %Now%?'
```

### Send a Private Message to Another User
To send a message to another user, you need their public key. This example assumes the public key is stored in `%pubKey%`.
```plang
- send message to %pubKey%, 'Hello, this is a message sent on %Now%!'
```

### Listen for New Messages
This example sets up a listener for new messages. When a new message is received, it triggers a goal called `!NewMessage`.
```plang
- listen for new message, call !NewMessage, write content to %content%, %sender% for sender address
```

### Handle Received Message
This goal is triggered by the listener when a new message is received. It outputs the content of the message and the sender's address.
```plang
NewMessage
- write out "Received message: %content% from %sender%"
```

## Managing Accounts

### Get Public Key
Retrieving the public key of the current account can be essential for sharing with other users to receive messages.
```plang
- get public key for messages, write to %pubKey%
- write out "My public key is: %pubKey%"
```

### Set Current Account
This example demonstrates how to switch the current account by specifying a public key or account name.
```plang
- set current account for messaging, 'examplePublicKeyOrName'
```

## Advanced Usage

### Get Private Key
In scenarios where secure operations are needed, retrieving the private key might be necessary.
```plang
- get private key, write to %privateKey%
- write out "My private key is: %privateKey%" /Note: Be cautious with private key usage
```

### Get Relays
Retrieving a list of relays can be useful for understanding the network topology or debugging connection issues.
```plang
- get relays, write to %relayList%
- write out "Available relays: %relayList%"
```

These examples provide a basic understanding of how to interact with the Message module in PLang, covering common tasks like message handling and account management.

For a comprehensive list of examples, visit the [Message module examples](https://github.com/PLangHQ/plang/tree/main/Tests/Message).

## Step Options
Each step in your PLang code can be enhanced with various options to handle different scenarios effectively:
- [CacheHandler](/CachingHandler.md): Helps in storing and retrieving data efficiently.
- [ErrorHandler](/ErrorHandler.md): Manages errors gracefully during the execution of steps.
: Attempts to execute a step multiple times in case of failure.
- [CancellationHandler](/modules/CancelationHandler.md): Allows steps to be cancelled if they take too long or conditions change.
- [Run and Forget](/modules/RunAndForget.md): Executes a step without waiting for its completion, useful for background tasks.

## Advanced
For developers looking for more in-depth information on how the Message module interfaces with underlying C# implementations, please refer to the [advanced documentation](./PLang.Modules.MessageModule_advanced.md).

## Created
This documentation was created on 2024-07-18T10:50:31.