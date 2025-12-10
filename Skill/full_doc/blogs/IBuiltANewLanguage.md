# I Built a New Programming Language – Plang

Around March or April 2023, I asked ChatGPT for a way to convert a video file to an audio file (I think), and it replied with a command line response:

```bash
ffmpeg -i input_video.mp4 -vn -acodec mp3 output_audio.mp3
```

FFmpeg is a really powerful tool, and I've known about it for a while—most video services use it. But this response sparked something in me. An idea. Oh boy, I had no clue where this would lead.

## The Idea Strikes

Seeing that command gave me this thought: What if I could create a tool that structures communication between the user and ChatGPT? A video editor you could just *talk* to.

So, I started with a little proof of concept. The tool would take text from a user, something like:

```
cut video.mp4, from 1 min to 3 min
```

It worked. The tool gave back a shorter version of the video. But to be truly useful, you’d need to reference the video name dynamically:

```
cut %videoName%, from 1 min to 3 min
```

That worked too. My proof of concept was functional.

But it wasn’t enough. There’s always some condition you need—an `if` statement. At this point, I realized: I was staring at the first step of a programming language. Variables, conditionals, next would be loops.

I parked the project. I thought, *don’t be crazy—you’re not going to create a programming language, that’s insane.*

## The Nagging Idea

But this idea kept nagging me for months. I couldn’t stop thinking about it. I started seeing potential—*massive* potential that other languages simply couldn’t touch.

I’ve been programming for over 30 years. I can usually think of solutions pretty quickly, and in my head, it’s like I can visualize problems and solutions in some kind of 3D structure. It’s hard to explain. But once I see the solution, implementing it is just technical work.

A colleague once pointed out that I often say, "In theory, it can be done." That’s my way of saying, *Yes, it’s possible.*

So, Plang was still in my head. I had [written down ideas](https://docs.google.com/document/d/1ZEEq2WyXJlm9vLSxoA7iI_jJ0FKtNOmw2RtfrBh447E/edit?usp=sharing). I hadn’t written any code for months.

Then, in late August 2023, I picked it up again. The potential hasn’t disappointed me, and more has come to light as I’ve pushed forward.

## Turning Potential Into Reality

Now it was time to code—both the [builder](https://github.com/PLangHQ/plang/blob/main/Documentation/Builder.md) and the [runtime](https://github.com/PLangHQ/plang/blob/main/Documentation/RuntimeLifecycle.md).

I chose to build it in C# because I know it well and it’s a great language. Usually, programming languages aren’t built on top of another language. But doing it this way has huge benefits. Building a programming language from scratch is hard. You have to deal with all sorts of problems—garbage collection, encoding, virtual machines, security, and a ton of other stuff. I didn’t have to do any of that.

The development of Plang was more like app development than traditional language development. I had a huge leap forward. I could just build on top of what already existed.

C# created a new layer. Plang’s runtime is this thin layer—when you write code in Plang, it passes through this layer. It’s almost like a firewall, stopping stupid mistakes that developers typically make. This gives Plang the potential to be incredibly secure.

### Solving Security Problems

Usernames and passwords are *stupid*—they’re the cause of most security breaches. The tech to solve this already exists. Look at Bitcoin—we don’t use usernames or passwords there. So why use them in Plang? I didn’t. Here come [%Identity%](https://github.com/PLangHQ/plang/blob/main/Documentation/Identity.md). No usernames or passwords in Plang. You *can* implement them if you want, but it’s 10 times more work and way, way less secure.

The other big security problem? Storing all our data in centralized cloud services. That’s where the value is, and that’s what gets hacked. We use the cloud because we want to work on our computer, then continue on our phone. But what if you could do that without the cloud? [Plang solves that](https://ingig.substack.com/p/plang-and-local-first).

You can work on your computer, store your data locally, and still use that data on your phone. Plang allows this.

With Plang, the two biggest reasons for security breaches are eliminated: passwords and centralized cloud data. The two biggest issues in tech today—solved.

## Then the Cool Stuff

Did you know you can bind an [event to a variable](https://github.com/PLangHQ/plang/blob/main/Documentation/Events.md#bind-events-to-variables) in Plang? That’s something other languages simply can’t do.

You can [bind an event](https://github.com/PLangHQ/plang/blob/main/Documentation/Events.md) to every single line of code in Plang. Other languages can’t even dream of that.

When you compile Plang, it’s not some random binary code that nobody can read. [It’s clear text](https://github.com/PLangHQ/plang/blob/main/Documentation/.build/Start/00.%20Goal.pr). You can’t hide any bad intentions in Plang code.

And the code itself? You [write about 90%+ less in Plang](https://ingig.substack.com/p/plang-abstracting-away-the-complexity) than in other languages. For basic projects, that’s already a massive reduction. For larger projects, that number goes even higher.

Fewer lines of code = fewer bugs, better stability, fewer security issues.

It’s [crazy simple to handle the busy work](./AutomatingTheWork.md) that bogs us down every day. 

Here’s how you [cache something](https://github.com/PLangHQ/plang/blob/main/Documentation/CachingHandler.md) in Plang:

```plang
- select * from table, cache for 10 minutes
```

Need to [retry an API](./Lesson%205.md) call?

```plang
- get https://example.org/getinfo, retry 3 times over 3 minutes
```

Want to [send money](https://github.com/PLangHQ/plang/blob/main/Documentation/modules/PLang.Modules.BlockchainModule.md)?

```plang
- send 5 USDC to %address%
```

[Receive](https://github.com/PLangHQ/plang/blob/main/Documentation/modules/PLang.Modules.BlockchainModule.md#listen-to-transfer-events-on-a-smart-contract) money?

```plang
- listen to %address%, call ProcessPayment
```

Want to [send a report every Monday](https://github.com/PLangHQ/plang/blob/main/Documentation/modules/PLang.Modules.ScheduleModule.md) at 9 am?

```plang
- on mondays at 9am, call SendReport
```

It's clear. It's simple.

## The "WTF" Moments

There have been so many WTF moments while developing Plang. I’m writing code and suddenly realize, *Holy shit, if I can do this, that means...*

You can have [**self-correcting software**](https://ingig.substack.com/p/in-theory-self-correcting-software). Since Plang code is so simple, an LLM (like ChatGPT) can handle it with no problem.

You can have [**automatic unit testing**](https://ingig.substack.com/p/in-theory-automatic-unit-testing), and it’s truly a unit—each individual line of your code, not an entire method.

[Sub-1-second compile](./InTheoryInstantCompile.md) times. Even on massive projects.

The UI? [It’s running locally](https://github.com/PLangHQ/plang/blob/main/Documentation/Todo_UI.md), in clear text, so anyone can modify it. The user can make his own UI for any service. Services don’t need to put as much effort into the UI anymore—that responsibility can shift to the users. The services can focus on the core business.

LLMs can actually build you an [almost finished product](https://github.com/PLangHQ/apps/tree/main/DraftIdea), just from a description. Companies are spending *millions* getting LLMs to write Python, JavaScript, or other languages. They’re not even close to a bug-free compile. I did it *by myself* with Plang, and with a high success rate. The first working version? Took me about 15 minutes to write.

Using `%Identity%`. The impact of this [change is bigger](./AnonymousKYC.md) than I could’ve ever imagined.

In terms of security, we could virtually [eliminate hacking and data leaks](./FutureOfSecuritPlangApproach.md) affecting millions of people.

Imagine teaching kids Plang. It would teach logical reasoning, problem-solving, and creativity. 10 year old could easily do it.

Because you’re not getting bogged down by syntax, you can see the bigger picture. You can solve problems in a whole new way. Your context isn’t hundreds of lines of code—it’s just a few. You need to use plang to understand this.

Honestly, I believe everyone who uses a computer in the future will be programming in a language like Plang, even if they don’t realize it.

## Conclusion

I could go on and on. I truly believe Plang is a [new kind of programming language](https://docs.google.com/document/d/1ZEEq2WyXJlm9vLSxoA7iI_jJ0FKtNOmw2RtfrBh447E/edit?usp=sharing) that will completely change how we approach problems.

But it’s not [perfect](https://github.com/PLangHQ/plang/issues). The language is still buggy. There’s a lot of work to be done. Some things aren’t implemented as well as they should be, and there are areas I haven’t tested enough.

Like many developers, I do things just because they’re cool.

But coding continues. Every day, I do something new.
