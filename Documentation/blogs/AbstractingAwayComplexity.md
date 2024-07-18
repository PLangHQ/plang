# Plang: Abstracting Away the Complexity

Building software today is too complex. It takes us months to learn to code the basics and years to master it.

Depending on the language you use, just getting started on a project is a challenge.

I remember setting up my first TypeScript project. After a day or two of figuring out how to set it up, I just copy the `package.json` and `tsconfig.json` files when I created a new TypeScript project. I never really fully understood everything that was happening. 

Like this article if you do the same 🤷‍♂️

When we have our projects set up, we start a fight with our code. It is incredibly verbose (a lot of text we need to write) and it needs to be 100% correct, otherwise everything fails. Miss a comma, and you can spend hours looking for the reason why the code is failing.

Let’s look at some JavaScript code for starting a web server and serving content from our `api` folder.

We assume you have installed Node.js. Just [like Plang](https://github.com/PLangHQ/plang/blob/main/Documentation/blogs/Lesson%201.md), you need a runtime, something to run your code.

First, we need to install the right libraries:

```bash
npm init -y
npm install express
```

Let's stop here.

These two lines are interesting.

Let's pretend I am a completely new programmer. I would have some questions:

* What is npm?
* What is `npm init` and why `-y`?
* Why do I need to initialize something? What does that do? How do I know to do that?
* Install what? Express? What is that?
* How do I know what to install? Do I Google -> Blog post or use ChatGPT?

There are probably more questions. The point is, you need to gather quite a bit of knowledge, and we haven't even started to write code.

Ok, so let’s write some code.

> Note: I asked ChatGPT to generate all the JavaScript code for this article.

```javascript
const express = require('express');
const path = require('path');

const app = express();
const port = process.env.PORT || 3000;

// Serve static files from the 'api' directory
app.use(express.static(path.join(__dirname, 'api')));

app.listen(port, () => {
    console.log(`Server is running on http://localhost:${port}`);
});
```

More questions and more knowledge are needed now.

* `require`? Aha, I need to understand that I need to import libraries into my code.
* Where does `require('path')` come from? I just did `install` on `express`.
* `const`? I've seen `let` and `var`. More knowledge is needed.
* Port? I have heard about ports with web servers, but do we really need to define that?
* Then `app.use`? How does that work?
* `express.static`, huh?
* And suddenly something is `__dirname`.
* I think I understand `path.join`, that I am joining two paths. Finally, something I can understand. 😉

There is a lot of knowledge that needs to be learned, simply to start a web server.

## Why Is It So Complicated?

The reason it is so complex is because computers need very detailed and accurate information to run. If you don't spell it out for them exactly, heaven and earth will explode.

## Let's Play a Game

What we did above, how would you describe it?

I am not talking about installing libraries, defining variables, etc. I am talking about what we really want to happen.

What we really want is to `start a web server and serve content from the api folder`. That is it.

Simple enough.

So why not just say that to the computer?

## Simplifying with Plang

[Plang](https://plang.is) is an intent-based programming language designed to abstract away the complexity of traditional coding. Instead of focusing on the minutiae of syntax and setup, Plang allows you to express your intentions directly.

Let’s create a file called `Start.goal` in a folder, any folder you choose.

In that `Start.goal`, write this text:

```plang
Start
- start a web server and serve content from api folder
```

There is no init, libraries, require, const, or lines and lines of code. You just say what you want to happen.

Now open the terminal/console (yes, it's a bit complex), go to the folder where `Start.goal` is, and type in:

```bash
plang exec
```

Plang understands you and finds a way to do it.

You do [need to follow rules](https://github.com/PLangHQ/plang/blob/main/Documentation/blogs/Lesson%202.md); after all, Plang is a programming language.

## Getting Complexity Out of the Way

We have been abstracting away complexity for a long time.

We write SDKs to wrap API calls, we talk to a Redis server through a library because we don’t want to handle TCP/IP connections to it. We even create huge frameworks to minimize the code we need to write.

Even the languages we have today are abstractions on top of lower-level languages. Java compiles to byte code, C# to IL, Python is a high abstraction. Not many people want to write in Assembly.

## What Else?

Let's continue with JavaScript since we started.

Let's read a file, update some data in it, and save the data back.

```javascript
const fs = require('fs');
const path = require('path');

// Define the file path
const filePath = path.join(__dirname, 'file.json');

// Read the file
fs.readFile(filePath, 'utf8', (err, data) => {
    if (err) {
        console.error('Error reading file:', err);
        return;
    }

    // Parse the JSON data
    let jsonData;
    try {
        jsonData = JSON.parse(data);
    } catch (parseErr) {
        console.error('Error parsing JSON:', parseErr);
        return;
    }

    // Add the new property
    jsonData.NewProperty = true;

    // Convert JSON object to string
    const updatedData = JSON.stringify(jsonData, null, 2);

    // Write the updated JSON back to the file
    fs.writeFile(filePath, updatedData, 'utf8', (writeErr) => {
        if (writeErr) {
            console.error('Error writing file:', writeErr);
            return;
        }
        console.log('File successfully updated');
    });
});
```

WOW, that is a lot. (Thank you, ChatGPT, for making it so large to make my point even more 🤣)

Let's try that in Plang, let's [read a file](https://github.com/PLangHQ/plang/blob/main/Documentation/modules/PLang.Modules.FileModule.md#read-a-text-file) and update it:

```plang
ReadAndUpdateFile
- read file.json, into %data%
- set %data.NewProperty% = true
- save %data% to file.json
```

Hold up, we are not [handling errors](https://github.com/PLangHQ/plang/blob/main/Documentation/modules/handlers/ErrorHandler.md) like the JavaScript code.

Let's do that:

```plang
ReadAndUpdateFile
- read file.json, into %data%, 
    on error call HandleError
- set %data.NewProperty% = true
- save %data% to file.json
    on error call HandleError

HandleError
- write out %!error%
```

If you are a seasoned developer, you might have questions such as:

* You are not handling encoding.
* You are not checking if the file exists.
* And more.

All valid.

### Encoding

```plang
ReadAndUpdateFile
- read file.txt, it's ascii, into %data%
```

There you have ASCII, just tell it.

By default, Plang doesn't throw an error if the file doesn't exist, but just returns an empty object. You can tell it to throw an error if you must:

```plang
ReadAndUpdateFile
- read file.txt, into %data%, throw error if file is not found.
```

## HTTP Request

Let's do GET and POST in JavaScript:

```javascript
const https = require('https');

// Perform a GET request
https.get('https://jsonplaceholder.typicode.com/posts/1', (resp) => {
    let data = '';

    // A chunk of data has been received.
    resp.on('data', (chunk) => {
        data += chunk;
    });

    // The whole response has been received.
    resp.on('end', () => {
        console.log('GET request response:');
        console.log(JSON.parse(data));
    });

}).on('error', (err) => {
    console.error('Error performing GET request:', err.message);
});

// Data to be sent in the POST request
const postData = JSON.stringify({
    title: 'foo',
    body: 'bar',
    userId: 1,
});

// Options for the POST request
const options = {
    hostname: 'jsonplaceholder.typicode.com',
    path: '/posts',
    method: 'POST',
    headers: {
        'Content-Type': 'application/json',
        'Content-Length': Buffer.byteLength(postData),
    },
};

// Perform a POST request
const req = https.request(options, (res) => {
    let data = '';

    // A chunk of data has been received.
    res.on('data', (chunk) => {
        data += chunk;
    });

    // The whole response has been received.
    res.on('end', () => {
        console.log('POST request response:');
        console.log(JSON.parse(data));
    });
});

req.on('error', (err) => {
    console.error('Error performing POST request:', err.message);
});

// Write data to request body
req.write(postData);
req.end();
```

> There is an library that abstracts away this complexity and reduces this code.

Here is Plang code for [GET](https://github.com/PLangHQ/plang/blob/main/Documentation/modules/PLang.Modules.HttpModule.md#get-request) and [POST](https://github.com/PLangHQ/plang/blob/main/Documentation/modules/PLang.Modules.HttpModule.md#post-request):

```plang
GetAndPost
- GET https://jsonplaceholder.typicode.com/posts/
    write to %getResponse%
- POST https://jsonplaceholder.typicode.com/posts/
    data: 
        title  = 'foo',
        body   = 'bar',
        userId = 1
    write to %postResponse%
- write out %getResponse%
- write out %postResponse%
```

> Formatting of data in POST is just so it looks better and is easier to read. It doesn't have to be setup this way, the LLM figures things out.

## Databases

In JavaScript, let's SELECT, INSERT, and UPDATE an SQLite database. 

First, install the SQLite library:

```bash
npm install sqlite3
```

And the JavaScript code:

```javascript
const sqlite3 = require('sqlite3').verbose();
const path = require('path');

// Open the database
const dbPath = path.join(__dirname, '.db', 'data.sqlite');
const db = new sqlite3.Database(dbPath, (err) => {
    if (err) {
        console.error('Error opening database:', err.message);
    } else {
        console.log('Connected to the SQLite database.');
    }
});

// Create a table
db.serialize(() => {
    db.run(`CREATE TABLE IF NOT EXISTS users (
        id INTEGER PRIMARY KEY AUTOINCREMENT,
        name TEXT NOT NULL,
        email TEXT NOT NULL UNIQUE
    )`);

    // Insert a row into the table
    const insert = `INSERT INTO users (name, email) VALUES (?, ?)`;
    db.run(insert, ['John Doe', 'john.doe@example.com'], function (err) {
        if (err) {
            return console.error('Error inserting row:', err.message);
        }
        console.log(`Row inserted with id: ${this.lastID}`);
    });

    // Select all rows from the table
    db.all(`SELECT * FROM users`, [], (err, rows) => {
        if (err) {
            throw err;
        }
        console.log('Select all rows:');
        rows.forEach((row) => {
            console.log(row);
        });
    });

    // Update a row in the table
    const update = `UPDATE users SET name = ? WHERE email = ?`;
    db.run(update, ['Jane Doe', 'john.doe@example.com'], function (err) {
        if (err) {
            return console.error('Error updating row:', err.message);
        }
        console.log(`Row(s) updated: ${this.changes}`);
    });

    // Select all rows from the table to verify the update
    db.all(`SELECT * FROM users`, [], (err, rows) => {
        if (err) {
            throw err;
        }
        console.log('Select all rows after update:');
        rows.forEach((row) => {
            console.log(row);
        });
    });
});

// Close the database connection
db.close((err) => {
    if (err) {
        return console.error('Error closing database:', err.message);
    }
    console.log('Database connection closed.');
});
```

And in Plang:

We start by defining the table in the `Setup.goal` file:

```plang
Setup
- create table users, columns: name(not null), email(not null, unique)
```

And then the code for SELECT, INSERT, and UPDATE. Create `DbWork.goal` file:

```plang
DbWork
- insert into users, 'John Doe', 'john.doe@example.com'
- select * from users, write to %users%
- write out %users%
- update users set name='Johnny Doe' where email='john.doe@example.com'
- select * from users, write to %users%
- write out %users%
```

## Embracing Simplicity

Programming should be about solving problems and implementing ideas, not getting bogged down by complexity. 

Plang’s approach allows you to describe what you want to achieve in natural language, making coding more intuitive and less error-prone.

## How Do I Know That Each Build Doesn't Change My Code?
This is a question I get often because people don't trust the LLM.

When [Plang builds](https://github.com/PLangHQ/plang/blob/main/Documentation/Builder.md) a step (a step is a line that starts with -), it generates a JSON instruction file and saves it into the `.build` folder. This file describes how to execute your Plang code at runtime.

This JSON instruction file is never built again, unless you modify the step.

Developers should commit their `.build` folder into a code repository such as Git, so that the next developer doesn't build the code when they pull it.

## More Information

If Plang is interesting to you, you should dig a bit deeper:

* [Basic concepts and lessons](https://github.com/PLangHQ/plang/blob/main/Documentation/blogs/Lesson%201.md)
* [Simple Todo example](https://github.com/PLangHQ/plang/blob/main/Documentation/Todo_webservice.md) is a good start
* Check out the [GitHub repo](https://github.com/PLangHQ/)
* [Meet up on Discord](https://discord.gg/A8kYUymsDD) or [GitHub discussions](https://github.com/orgs/PLangHQ/discussions) to get help and for general chat
* And [plang.is](https://plang.is), the official Plang website
