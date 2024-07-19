# Messaging

Plang has built-in messaging. It uses the [Nostr protocol](https://nostr.com/) by default to do this.

## Before We Start

Make sure to [install Plang](https://github.com/PLangHQ/plang/blob/main/Documentation/Install.md).

Note that building code in Plang costs money as it uses LLM. If you have an OpenAI key, you can use that. Instructions are available on your first build.

Plang is a programming language where you write the code in natural language. I call it intent programming because you just need to write your intent and the LLM will figure out what you would like to do.

To understand it, make sure to go through some of the [lessons we have](https://github.com/PLangHQ/plang/blob/main/Documentation/blogs/Lesson%202.md).

## Prepare

There are some Nostr clients out there. I recommend [Damus (iOS)](https://apps.apple.com/us/app/damus/id1628663131) and [Amethyst (Android)](https://play.google.com/store/apps/details?id=com.vitorpamplona.amethyst&hl=en).

Download either of these clients to your phone. This allows us to communicate with the Plang app we are building in this tutorial.

After you have set it up, create a new account and find the public address by clicking your profile picture and going into your profile.

The public address will start with `'npub.....'`.

Copy this address, so you have `'npub.....'` in your clipboard.

## Set Up Code

Let's get started. You should have [Plang installed](https://github.com/PLangHQ/plang/blob/main/Documentation/Install.md) and create a new folder on your computer (you choose the location).

Let's create the folder `MessageTest`. Inside the folder, create a file called `Start.goal`. This is the default entry point into your app.

Now write the code:

```plang
Start
- get the public address for message, write to %publicAddress%
- write out 'Your address is: %publicAddress%'
- listen to message from 'npub...', 
    on new message call ProcessMessage, content of goes into %message%

ProcessMessage
- write out 'Message from phone: %message%'
```

Now build and run it:

```bash
plang exec
```

It should now print out the Nostr public address and listen for messages:

```bash
Your address is: npub....
```

Try sending a message from your phone to the `npub` address that was printed out.

## Send a Message

Now we want to send a message to our phone.

Create a new file `SendMessage.goal`:
> Make sure to put in you npub address from your phone instead of 'npub....'
```plang
SendMessage
- send a message to 'npub....',
    content=%content%
```

Build the code:

```bash
plang build
```

And run the code with some content:

```bash
plang SendMessage content="Hello"
```

Your phone should receive a message with the content "Hello."

## More Information

If Plang is interesting to you, you should dig a bit deeper:

* [Basic concepts and lessons](https://github.com/PLangHQ/plang/blob/main/Documentation/blogs/Lesson%202.md)
* [Simple Todo example](https://github.com/PLangHQ/plang/blob/main/Documentation/Todo_webservice.md) is a good start
* [Documentation](https://github.com/PLangHQ/plang/blob/main/Documentation/modules/PLang.Modules.MessageModule.md) on the message module
* Check out the [GitHub repo](https://github.com/PLangHQ/)
* [Meet up on Discord](https://discord.gg/A8kYUymsDD) to discuss or get help
