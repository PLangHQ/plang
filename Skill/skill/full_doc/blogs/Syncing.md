# Local-First and Plang

Plang programming language encourages local-first development.

[Local-First is a movement](https://www.localfirstconf.com/) that I stumbled upon not long ago and Plang fits it perfectly.

## What is Local-First?

Local-first means your data stays on your device, and when shared between devices, it's encrypted before being transferred.

Once it reaches the other device, it's decrypted.

For more details, [watch this video.](https://youtu.be/qo5m92-9_QI?si=hGvepCeXax2bV5nN&t=85)

## Benefits of Local-First

Just to name a few:

* Your data is yours. Data is not yours when you use a service; Google (or any other service) can [close your account at any time](https://www.jefftk.com/p/how-likely-is-losing-a-google-account) without ever giving you an explanation.
* Speed. Apps should become faster as they don’t need to retrieve data from the internet.
* Privacy and Security by default. Data lives on your computer, and you will not sell it (at least it’s your choice); no service can look at your data. It is also expensive to hack individual computers rather then services with millions of users.
* No “End of Service.” Services often close their doors. When your app & data live on your computer, you will never have an end of service.
* [Backend development is a fraction](https://youtu.be/VLgmjzERT08?si=CxgzZQtis3wDv1hC&t=1194) of what is needed with the "regular" way, which means lower overhead and costs.

## Plang and Local-First

Although I only discovered the movement about a month ago, the language fits it like a glove.

I have always been rather privacy and security concerned, so when I designed the language, it was one of the principles that I wanted to solve.

## How Plang Does It

Plang has a built-in database, so when you run a Plang application, it uses a database on your computer, not on some cloud service.

Any change that happens in your database is encrypted, allowing you to send that change over the internet securely.

Sharing the private key between two devices is easy as Plang has a built-in messaging protocol, allowing you to send the private key between them securely and privately. You just need the public key of the other device to send it.

## Example

I want to show an example of how this is done in Plang code. You should have [Plang already set up](https://github.com/PLangHQ/plang/blob/main/Documentation/Install.md) if you want to run this.

You can find the [repository here](https://github.com/PLangHQ/apps/tree/main/SimpleSyncDemo). You dont need to build it. When exporting private key you will need to have access to LLM. If you are not using OpenAI key, you will be asked to register.

First, we set up the database:

```plang
Setup
- create table tasks, columns: text, due_date(datetime)
```

Now run this in your console/terminal:

```bash
plang exec
```

We now have a table, `tasks`, located in the folder `.db/data.sqlite`.

If you open the `data.sqlite`, you will see a table called `__Events__`. These events are the encrypted changes to the database.

These lines simply need to be synced to some cloud storage.

What that cloud storage is, is up to you to select. At the moment, none have been developed.

Let's make a “bad” syncing service, just to prove it works.

I have two devices running the same task list app. This is the code for it:

```plang
Start
- if %Settings.OtherDevicePublicKey% is not empty then, 
    call ListenForMessage, else PrintOutMyPubAddress    
- ask user, "What would you like to do?\n1. List out tasks\n2.Add task\3. Set address to other device", 
    must be a number between 1-3
    write to %choice%
- if %choice% == 1 then call ShowList
- if %choice% == 2 then call AskForTask
- if %choice% == 3 then call SetOtherDevicePublicKey
- call goal Start

ListenForMessage
- List for new message from %Settings.OtherDevicePublicKey%, 
        on new message call ProcessMessage, load message into %content% 

PrintOutMyPubAddress
- get public address for message, %pubAddress%
- write out 'My message address is: %pubAddress%'

ShowList
- select * from tasks, write out %tasks%
- write out %tasks%

AskForTask
- ask user 'What is the task', write to %text%
- call goal AddTask

Sync
- if %Settings.OtherDevicePublicKey% is not empty then
    - select id, data, keyHash from __Events__ 
        where id > %Settings.LastSyncId% order by id, write to %events%
    - for each %events%, call SendSync

ProcessMessage
- write out 'Received message: %content.type%'
- if %content.type% == "private_key", call StorePrivateKey, else WriteToEventsTable eventData=%content%

StorePrivateKey
- add %content.value% as private key to encryption, 

SendSync
- send a message to %Settings.OtherDevicePublicKey%
    content={"type":"event", id:"%item.id%", data:"%item.data%", key_hash:"%item.key_hash%"}
- set value %Settings.LastSyncId% = %item.id%    

WriteToEventsTable
- insert event source data into db, %eventData.id%, %eventData.data%, %eventData.key_hash%
- write out 'Data added'
- call goal Start

AddTask
- insert into tasks, text=%text%, due_date=%Now+1day%
- call goal Sync

SetOtherDevicePublicKey
- ask user 'What is the message address', 
    cannot be empty, must start with 'npub'
    write to %npubAddress%
- set %Settings.OtherDevicePublicKey% = %npubAddress%
- [crypt] get private key for encryption,  write to %pk%
- send message to %Settings.OtherDevicePublicKey%, 
        content={"type":"private_key", value:"%pk%"}
```

There you have syncing between two devices, in about 60 lines of code. Completely secure and private* 🤯

I call it a "bad" syncing service because messages aren’t really ideal for sending this type of data en masse, but it should be fine for small apps, something that we as individuals usually have. Just think about your task list; how much do you change that each day? Not that much.

There are probably some bugs in that code, but for the purpose of this example, it is fine.

## *Completely Secure and Private:

There still needs to be a comprehensive security audit on Plang; there might be something that I have missed, and security is in the details.

Those details are underlying core code that should not affect the Plang code that a user writes, so any code written today only becomes more secure with time.

This is actually opposite to what you have in today's programming languages.

## Status of Plang Syncing

Plang is a young programming language, and many things are still being implemented.

Syncing data is a complex topic, and my strength is not really there. I hope that the event sourcing setup that Plang provides is a good enough start for data structure, but nothing has been done regarding files living outside the database.

There are many good solutions out there, and I hope they will be integrated into Plang in some way.

## Running the Code

You need to run in a specific order because you need to set the public keys at the right time.

The UX is not so nice, but hey, this is just a simple demo. It’s also a console, and [GUI is still in its proof of concept phase](https://github.com/PLangHQ/plang/blob/main/Documentation/Todo_UI.md).

## Messaging

The messaging uses the [Nostr protocol](https://nostr.com/) to send messages. It is built into the Plang language. Having messaging built into the language is really powerful, as you can see. It also means that I am not creating a custom protocol or implementing custom encryption.

The data is actually encrypted twice. The data sent using Nostr is encrypted on the computer before creating the Nostr message, then the Nostr library sends an encrypted message using the public key of the receiver.

## Exporting Private Keys

If you run the code, the app will ask you three questions regarding your export of your private key.

This is an attempt to prevent people from getting scammed. You can [read more about it here](https://github.com/PLangHQ/plang/blob/main/Documentation/blogs/ExportPrivateKeys.md).

## Cost of Building

It costs to build code in Plang, so how much did this cost me?

It cost me between $2-3 to build this.

If you were to build this type of service in any other language, it would be weeks of development and thousands of dollars.

It took me about 2 hours from when I started to write this article until I had a functional version.

That is the power of Plang.🤯🤯
