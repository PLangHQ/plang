# Exporting private keys in plang
_**How plang tries minimizes the risk of you being scammed**_

Getting access to the private keys is the holy grail for any malicious actor. 

With it you can do anything. It’s the **one weak*** point in plang.

A popular way to scam people is to get them to export their private keys and send it to the malicious actor. 

They trick grandma to do it, because they promise a solution or an award for it.

In plang it is like you have a highly technical person always next to you to help you out.

So let’s use this highly technical person to minimize the likelihood of this happening

If you build and run this code, you will get 3 questions. 

First you need to [set up plang](https://github.com/PLangHQ/plang/blob/main/Documentation/GetStarted.md)

```plang
Start
- [crypt] export the private key for encryptions to %privateKey%
- write out %privateKey%
```

You will get this prompt:

    Before we export your private keys I would like to ask you 3 questions. Remember never share your private keys with people you don't know or trust.
    Question

    1. Why are you sharing your private key?
    2. Who specifically requested your private key, and how did they contact you?
    3. Were you promised any benefits, rewards, or solutions in return for your private key?

The user needs to answer each question.

LLM will analyze the answer and give the likelihood of it being a scam. 

If the LLM determines the risk to be high, the export will be blocked for 24 hours.

*Will this prevent users from exporting their keys to malicious actors?*

No, but hopefully it will decrease it. This is a working idea. 

*What else can be done?*

No clue. Maybe it will give good results and maybe not. 

*What if I lock the private key export but I need it?*

If you are technical savvy, you go into .db/system.sqlite and delete that “PrivateKeyLocked” setting. 

If you are not tech savvy, wait 24 hours.

*Should time lockout be shorter or long?*

Don't know, 24 hours is just a number chosen, maybe the user has cooled down and has thought about it.

*How is the private key protected?*

If you have access to the computer, in current version (0.1) it is not protected, it’s located in .db/system.sqlite, without any encryption

*Will that change?*

Yes, the reason for this unprotected key is to ease the development of plang and I haven’t implemented it. 

Anybody can write their [own implementation](https://github.com/PLangHQ/plang/blob/main/Documentation/Services.md#settings-service), so I expect FIDO2 or similiar to be implemented when needed

*Where can I see the code for this?*

Check out the [AskUserPrivateKeyExport.cs](https://github.com/PLangHQ/plang/blob/main/PLang/Errors/AskUser/AskUserPrivateKeyExport.cs)

## Other private keys in plang

There are 3 other private keys in plang, Identity, Message and Blockchain. Same can be done for them. 

The idea here is that you have 2 or more devices, and you can safely share the private key between them. 

Good example is the Identity private key, if you use a web service that you have created account with, you most likely want to have the same account with that web service on you laptop and on your phone.

## Tests

You can check out the [Test for extracting the private keys](https://github.com/PLangHQ/plang/blob/main/Tests/ExportPrivateKeys/ExportPrivateKeys.goal) in the Test folder in the repo

---
**One weak point**: of course that can be zero days and plang has not had security audit so there are probably other weak points, but in theory, there should only be one weak point in a system that runs on public/private key. Plang will get there.