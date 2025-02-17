# LLMs Are Analog – We Need an Analog Programming Language  

The title states that LLMs are analog. Let me start by explaining what I mean by that.  

If you ask an LLM a simple question—even with a typo—you still get the correct answer. Ask the same question the next day, with or without the typo, and you'll get the same answer, but it might be phrased differently.  

That’s what I mean by "analog." Compare that to digital systems: one typo, and everything fails. You also always get the exact same answer, formatted identically.  

Plang is an analog language. Don’t get me wrong—it’s still deterministic. But it allows flexibility in how you write statements.  

For example, let’s say you want to read the contents of a file. In Plang, you could write:  

```plang
- read file.txt, write to %content%
```  

Or, you could phrase it differently:  

```plang
- can u read the file.txt, put the stuff into %content%
```  

Now, put yourself in the shoes of a computer. If you received these two requests, would you be able to understand them? Yes, because we communicate at an analog level. Would you say it's deterministic? Also yes—you’ll always get the contents of `file.txt` into `%content%`.  

You can’t do that with traditional programming languages. In JavaScript, reading a file must be written in a strict, precise way:  

```javascript
const content = fs.readFileSync('file.txt', 'utf8');
```  

Make one typo, and it won’t work.  

---

## LLMs Doing Programming  

There have been some really interesting attempts at getting LLMs to program for us. Projects like [Vercel v0](https://v0.dev/), [TLDraw](https://makereal.tldraw.com/) (especially [this](https://computer.tldraw.com/), my favorite), and [Lovable](https://lovable.dev/) are pushing the boundaries. I'm sure there are more out there.  

But there’s a problem with letting LLMs handle all programming. They do relatively well with individual pieces but struggle to build a fully cohesive app—tying all those pieces together into something functional. It’s like giving them Lego bricks and expecting them to assemble a well-structured car.  

It is "claimed" that OpenAI’s o3 ranks among the top 250 programmers in the world. Maybe, for specific tasks like debugging or solving algorithmic problems. But ask it to build an app with a web service, database, caching, and external API integrations—everything a real-world app needs—and it won’t even come close. A task that a almost any developer (one of the 25 million programmers worldwide) can do. My argument, o3 is in top 25 million of programmer in that sense.

Lovable is probably the closest thing to a fully functional app builder using LLMs, that I know. But that’s not just because of GPT-4o—it’s because of the structured processes around it. They’ve built logic that knows how to set up Supabase, structure a web platform, and handle integrations. It’s not just the AI; it’s the framework around it.  


## Fundamental Problems  

I believe there are a few fundamental problems with getting LLMs to program for us using current languages:  

- **Programming languages are digital.** One tiny syntax error, and everything breaks. The LLM has to reread the code, debug it, and attempt a fix—possibly introducing a new mistake. This gets expensive fast.  
- **Verbosity.** Digital languages are incredibly verbose. They require strict structure and excessive syntax, making it hard to work efficiently.  
- **LLMs have been trained on trillions of lines of code** and still struggle to build fully cohesive applications. They can generate parts of an app but fail at orchestrating everything into a functional system.  
- **Context limitations.** Yes, context windows are expanding, but digital languages are inherently complex and verbose. Keeping the entire app in memory and ensuring every component stays in sync is incredibly difficult, if not impossible—or at least prohibitively expensive.  
- **Security.** Security is often binary: it either works 100% or fails entirely. If an LLM is encrypting a file, can we trust that it’s truly secure? I would not.  
- **API keys.** If an LLM needs to interact with an external service, how does it get API keys? A human still has to step in to manage authentication. Unless we build autonomous agents capable of registering credit cards and navigating service sign-ups, this remains a roadblock.  

This isn’t to say LLMs aren’t great at programming. They can assist, improve workflows, and boost productivity. But there are some fundamental challenges they simply can't overcome—because of the way our current programming languages are designed.  


## Plang – An Analog Language  

At [UT Messan](http://utmessan.is/) (Iceland’s largest IT conference), I did a quick demo showing how Plang works. I sketched out a UI, wrote a short description, and Plang generated an app.  

![Make your own app](https://i.ytimg.com/vi/zYhUsyOp4uw/hqdefault.jpg)

You can check [out the video](https://youtu.be/zYhUsyOp4uw) — it’s about DIY apps. Apps you can build at home for yourself, your family, or your local community. In the demo, I created an app for my own community.  

The GUI builder I used is still an early prototype. I built it the week before my talk because I knew it was possible and wanted to show Plang’s power visually—not just through command-line interactions.  

Here’s what the LLM was able to do:  

- **Generate code without syntax errors** (since Plang has no syntax errors).  
- **Keep it concise**—just 101 lines of code.  
- **Learn Plang on the fly.** LLMs don’t know Plang natively, so I had to teach it the language in about 350 lines of "training" (not perfect, but enough).  
- **Handle context efficiently.** With just three files and 101 lines of code, keeping track of everything was easy.  
- **Ensure security.** Plang interacts with pre-built, vetted modules. The language isn’t fully mature yet, but this approach will help make it more secure.  
- **API keys** Plang apps communicate securely using Ed25519, meaning API keys aren’t a issue—though some external services, like Discord, still require them since they don't support signing.  

The demo gives the impression that I just sketched a UI, wrote a description, and ran the app instantly. It’s not *that* simple in reality, but it does showcase how an analog language allows an LLM to build a cohesive app in record time.  

If you want to see the entire process, check out this video where I go step by step, from drawing the UI to running the app.  

## How You Can Run It  

It’s still early, so if you decide to play around with it, expect some rough edges. But the best part? The GUI builder itself is written in Plang, so you can tweak and improve it easily.  

### Steps to get started:  

1. **Install Plang** – Follow the [Get Started guide](https://github.com/PLangHQ/plang/blob/main/Documentation/GetStarted.md).  You need to buy some plang credits or use OpenAI API key when using plang.

2. **Clone the Builder repo** – Run:  
   ```sh
   git clone <repo_url>
   ```  
   
3. **Run Plang** – Open the command line in the Builder folder and type:  
   ```sh
   plang
   ```  

Now, you should see the canvas appear, where you can draw, describe the app, and click "Make."  

## What’s Next  

The Builder is still a prototype—a teaser, really. Right now, you only get one shot at drawing and building. Once the app is generated, there's no way to go back, tweak the design, and rebuild without starting from scratch. That’s a big limitation. 

Ideally, you should be able to take the generated app, bring it back onto the canvas, refine the UI, add new descriptions, and build on top of what already exists. That’s the long-term vision.

But for now, it doesn’t do that—and it won’t for a while. The goal of this prototype was simple: to visually demonstrate the power of Plang to a live audience.

My focus remains on core features of the language itself. The GUI builder isn’t a priority right now, but I hope to revisit it this fall/winter(2025).

Plang itself still has a lot of work ahead. It’s a new way of thinking about programming—more flexible, more human-friendly, and designed to work well with LLMs.  

If you're interested in experimenting with it or contributing, feel free to dive in. There’s plenty of room to shape what this  programming language becomes.  

## More Information

If Plang is interesting to you, you should dig a bit deeper:

* [Basic concepts and lessons](https://github.com/PLangHQ/plang/blob/main/Documentation/blogs/Lesson%202.md)
* [Simple Todo example](https://github.com/PLangHQ/plang/blob/main/Documentation/Todo_webservice.md) is a good start
* Check out the [GitHub repo](https://github.com/PLangHQ/)
* [Meet up on Discord](https://discord.gg/A8kYUymsDD) to discuss or get help