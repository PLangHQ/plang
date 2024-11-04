# How Plang Made SQL Accessible to Me

Plang makes programming more approachable by letting users express their intentions in natural language, making it ideal for those who aren't deeply familiar with coding syntax.

I’ve been around SQL for a long time, so I know my way around a query. But each database has its own syntax quirks, and even a simple task like finding records older than 24 hours can mean a lot of Googling or trying to remember specific commands.

This happened to me recently: I needed to grab records from a "statuses" table, but only the ones updated over 24 hours ago. Pre-Plang, I’d probably hit the web, hunting down the right SQL syntax, or maybe I’d ask ChatGPT to generate it, then adapt it to fit.

With Plang, it’s way simpler. I just tell it what I want to do in straightforward terms. Here’s exactly what I typed:

```plang
- select * from statuses where updated is older than 24 hours, write to %statuses%
```

For context, the `*` in SQL means “grab everything,” so alternatively, I could have written it out like this:

```plang
- select everything from statuses where updated is older than 24 hours, write to %statuses%
```

Now, if you know SQL, you can already spot that the phrase `is older than 24 hours` isn’t exactly valid syntax. Normally, this is where I’d be checking my database’s specific language rules. But with Plang, I just write out my intention, and it handles the rest. 

And the best part? I can check the exact SQL Plang generated to make sure it’s spot-on. Here’s what showed up in the `.pr` file:

```json
"Action": {
    "FunctionName": "Select",
    "Parameters": [
      {
        "Type": "string",
        "Name": "sql",
        "Value": "SELECT * FROM statuses WHERE updated > datetime('now', '-24 hours')"
      }
    ]
}
```

It’s clear, and it’s SQL-ready. Plang even adapts this syntax to different engines automatically, like swapping out for MySQL or SQLite as needed.

In short, Plang lets me get right to what I want to accomplish without having to fuss over syntax details. It’s like SQL, but with a much easier learning curve.