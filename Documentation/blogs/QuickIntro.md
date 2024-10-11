# Quick Intro

In this post, I’ll walk you through running the quick intro code that’s also available on our [GitHub page](link-to-github). Here’s what the code looks like:

```plang
LoadPosts
- get https://jsonplaceholder.typicode.com/posts/, write to %posts%
- go through %posts%, call InsertPost 

InsertPost
- insert into posts, %item.title%, %item.body%
```

This is actual Plang code. You can run it as-is, and it will do exactly what it describes.

However, there’s one small piece missing that I’ll explain shortly.

But before we get into that, you'll need to install Plang on your computer if you want to follow along.

Head over to the [Install page](../Install.md) and grab the version that works for your setup.

Also, you’ll want to install the [IDE](../IDE.md). The Plang IDE is a handy tool that helps you write and manage your Plang code.

---

# Let’s Get Started

Now, let’s go step-by-step.

1. **Create a folder** on your computer—wherever you like. Name it anything you prefer. For this example, I’ll name mine `c:\apps\QuickIntro`.

2. **Create a new file** in that folder and name it `Start.goal`. 

   This is just a text file where you’ll write your Plang code.

3. Open `Start.goal` and add the following code:

```plang
LoadPosts
- get https://jsonplaceholder.typicode.com/posts/, write to %posts%
- go through %posts%, call InsertPost 
- select everything from posts, %postFromDb%
- write out %postFromDb%

InsertPost
- insert into posts, %item.title%, %item.body%
```

We added there 2 lines to get the inserted data from database and showing it.

## The Missing Part

Remember I mentioned there’s one part missing?

The code you wrote is trying to insert data into a `posts` table, but we haven’t told Plang what that table looks like yet. We need to define it.

To do this, create another file in the same folder as your `Start.goal` file. Let’s name this new file `Setup.goal`.

In `Setup.goal`, you’ll describe the structure of the `posts` table, like this:

```plang
Setup
- create table posts, columns: title, body
```

Add this code to `Setup.goal`, and now we’re ready to build and run.

---

# Build and Run

In the Plang IDE, press the **F5** key. A window will pop up—just press Enter, and your code will build and run. You’ll see the output of the data printed right there.

Alternatively, if you prefer using the terminal/console, navigate to your folder and run the following command:

```bash
plang exec
```

And that’s it! You’ve just run your first Plang app. Pretty simple, right?

---

Let me know if you want to expand on any sections or make additional changes!