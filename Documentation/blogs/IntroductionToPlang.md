# Introduction to Plang: Master Coding Quickly and Easily!

## What is Plang?

Imagine telling a computer what to do as easily as giving instructions to a friend. That’s exactly what **Plang** allows you to do! With Plang, you can write out tasks in simple steps, and the computer follows your instructions without the need for complicated coding. Just use plain language to command the computer!

Ready to dive into a fun project that’ll get you hooked on programming? Let’s get started!


## Step 1: Prepare to Write Your First Program

Before we begin coding in **Plang**, you'll need to install it on your computer. But don’t worry, it’s a breeze! Just follow these steps:

1. **Download Plang:** Head to [Plang's GitHub page](https://github.com/PLangHQ/plang/blob/main/Documentation/Install.md) and follow the simple installation guide.
2. **Create a Project Folder:** Set up a folder on your computer for all your Plang programs. Give it a fun name like "MyFirstPlang."

## Step 2: Writing Your First Program

Let’s start by creating a program that asks for your name and age, and then responds with a message. You’ll save this in a file called **Start.goal**.

Here’s what the code looks like:

```plang
Start
- ask "What is your name?", write to %name%
- ask "How old are you?", write to %age%
- write out "Hello, %name%! You are %age% years old."
```

## What does this code do?
- First, the program **asks** for your name and age.
- Then, it **prints** a message like: "Hello, Alex! You are 10 years old."

It’s that simple! When you run the program, the computer will interact with you and display the answers you provide.


## Step 3: Saving and Running Your Program

Now let’s run your program:

1. **Save your file** as `Start.goal`.
2. **Open your terminal (or command prompt)** and navigate to your project folder.
3. Type `plang build` to build your program.
4. Run it by typing `plang`.

Once you do this, the program will ask for your name and age, and display the message you’ve written.


## Step 4: Interacting with Files

What if you want the program to interact with a file, like updating today’s date in a document? Let’s see how that works.

```plang
Start
- read file 'diary.txt', into %content%
- set %content% = "Hello diary"
- save %content% to 'diary.txt', overwrite file
```

This code:
- **Reads** a file called `diary.txt`.
- **Adds 'Hello diary'** to the content.
- **Saves** the updated content back to the file.


## Step 5: Build a To-Do List App

Let’s create something useful: a simple To-Do list app!

1. **Set up a database** for your tasks:
```plang
Setup
- create table tasks, columns: task(string, not null), due_date(datetime)
```

2. Now let’s **add tasks** and **display them**:
```plang
Start
- insert into tasks, task='Do homework', due_date=%Now+2hours%
- insert into tasks, task='Feed the dog', due_date=%Now+30minutes%
- select * from tasks, write to %allTasks%
- write out %allTasks%
```

When you run this, the program will save tasks like "Do homework" and "Feed the dog" and display them on the screen. 


## Step 6: Connecting Your Program to the Web

Let’s make things more interesting by getting data from the internet! Here’s an example that pulls posts from a website:

```plang
Start
- get https://jsonplaceholder.typicode.com/posts, write to %posts%
- write out %posts%
```

This will grab **posts** from the web and **display** them in your program, making it feel like a mini web browser!

## Step 7: Understanding Goals and Steps

In Plang, everything revolves around **goals** and **steps**. A **goal** is like the main task you want to accomplish, and the **steps** are the smaller actions needed to achieve it. For example:

- Goal: "Create a task."
- Steps: "Ask for task name, ask for due date, save task to list."

It’s like following a recipe, breaking things down into clear instructions that are easy to follow.


## Conclusion

With **Plang**, programming becomes simple, fun, and approachable. By breaking tasks down into clear steps, you can accomplish a lot without needing to learn complex commands.

## What’s Next?

1. **Create your own project:** Maybe a program to track your favorite games or books!
2. **Explore Plang’s capabilities:** Try adding more functionality, like [sending a message](https://github.com/PLangHQ/plang/blob/main/Documentation/modules/PLang.Modules.MessageModule.md) or [setting reminders](https://github.com/PLangHQ/plang/blob/main/Documentation/modules/PLang.Modules.ScheduleModule.md).
3. **Share your project:** Show your friends how easy and exciting coding can be!


**Questions to Think About:**

1. What other projects could you build using Plang’s goals and steps?
2. How could you expand your [To-Do list app](https://github.com/PLangHQ/plang/blob/main/Documentation/Todo_webservice.md), like adding task deletion or reminders?
3. Can you write a program that helps you with daily tasks like homework or scheduling?

## More Information

Interested in learning more about Plang? Here are some useful resources to get started:

- [Basic Concepts and Lessons](https://github.com/PLangHQ/plang/blob/main/Documentation/blogs/Lesson%202.md)
- [Todo Example](https://github.com/PLangHQ/plang/blob/main/Documentation/Todo_webservice.md) to build a simple web service
- Explore the [GitHub repo](https://github.com/PLangHQ/) for source code
- Join our [Discord Community](https://discord.gg/A8kYUymsDD) for discussions and support
- Or chat with the [Plang Assistant](https://chatgpt.com/g/g-Av6oopRtu-plang-help-code-generator) to get help with code generation