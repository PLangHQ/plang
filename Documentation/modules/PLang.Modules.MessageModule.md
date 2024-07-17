
# Message Module Documentation

## Introduction
The Message Module in plang is a powerful feature that allows users to send and receive messages within the context of the plang scripting language. It leverages the Nostr protocol to facilitate private and secure communication between users.

## For Beginners
If you're new to programming or unfamiliar with technical jargon, don't worry! The Message Module is essentially a set of instructions that you can use to create, send, and manage messages. Think of it as writing a letter, but instead of using pen and paper, you're using simple commands that tell the computer what to do. These commands can help you send a note to a friend, check if you've received any messages, or even listen for new messages as they arrive.

## Best Practices for Message
When writing plang code for the Message Module, it's important to follow some best practices to ensure your code is clear, maintainable, and efficient. Here's an example to illustrate:

```plang
Message
- set current account with public key or name 'Alice'
- listen for new message, call !NewMessage, write content to %messageContent%
- if %messageContent% contains 'urgent' then call !HandleUrgentMessage
```

In this example, we first set the current account to 'Alice'. Then, we start listening for new messages. If a message contains the word 'urgent', we call a special goal to handle it. This shows how to organize your steps logically and use conditional statements to make decisions.

## Examples

# Message Module Documentation

The Message Module allows you to send and receive private messages using the Nostr protocol. Below are examples of how to use the Message Module in plang.

## Examples

### Get Public Key
Retrieve your public key for messaging.
```plang
Message
- get public key for messages, write to %pubKey%
- write out %pubKey%
```

### Send a Message to Yourself
Send a private message to yourself.
```plang
Message
- send my self message, 'Hi how are you, 2.1.2024 22:05:54'
```

### Send a Message to Another User
Send a private message to another user using their public key.
```plang
Message
- send message to %pubKey%, 'Another message that I will receive, %now%'
```

### Listen for New Messages
Listen for new messages and handle them with a specified goal.
```plang
Message
- listen for new message, call !NewMessage, write content to %messageContent%
```

### Set Current Account
Set the current account by providing a public key or a name.
```plang
Message
- set current account with name 'Alice'
```

### Additional Examples
If a method does not have an example, here are some created using natural language.

#### Listen for Messages from a Specific Date
Listen for new messages starting from a specific date and time.
```plang
Message
- listen for new message from '1st January 2024', call !NewMessage, write content to %messageContent%
```

## Notes
- The `%variableName%` syntax is used to store and reference values within the plang script.
- The `!GoalName` syntax is used to reference a goal that should be called when a certain event occurs.
- The `write to %variableName%` step is used to store the result of a method call into a variable for later use.
- The `write out %variableName%` step is used to output the value of a variable.
- The `listen for new message` step starts a listener that will trigger a goal when a new message is received.
- The `send my self message` and `send message to %pubKey%` steps are used to send messages either to yourself or to another user.
- The `set current account with public key or name` step is used to switch the active account in the Message Module.


For a full list of examples, visit [Message Module Examples on GitHub](https://github.com/PLangHQ/plang/tree/main/Tests/Message).

## Step Options
Each step in your plang script can be enhanced with additional options for better control and error handling. Click on the links below for more details on how to use each option:

- [CacheHandler](/modules/cacheHandler.md)
- [ErrorHandler](/modules/ErrorHandler.md)
- [RetryHandler](/modules/RetryHandler.md)
- [CancellationHandler](/modules/CancelationHandler.md)
- [Run and Forget](/modules/RunAndForget.md)

## Advanced
For those who are interested in diving deeper into the Message Module and understanding how it maps to underlying C# functionality, please refer to the [advanced documentation](./PLang.Modules.MessageModule_advanced.md).

## Created
This documentation was created on 2024-01-02T22:07:23.
