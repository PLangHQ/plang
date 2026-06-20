# Types

Every value in PLang has a type — it tells PLang what kind of thing it is.

```plang
Start
- set %name% = "Alice"          <-- text
- set %age% = 30                <-- number
- read file.jpg into %photo%    <-- image
- read data.csv into %rows%     <-- table
```

PLang figures out the type automatically. You rarely need to say it yourself.

## Kind

Some types have a *kind* — a refinement that says more about the format.

- An image can be a `jpg`, a `png`, or a `gif`
- A number can be a whole number (`int`) or a decimal (`decimal`)
- Code can be `python`, `csharp`, or `js`

The type tells PLang *what* it is. The kind tells PLang *how it is stored*.

## Content from files and the web

When you read a file or fetch something from the internet, PLang starts with what it knows for certain: raw bytes. It labels them `binary` and notes the format (`jpg`, `json`, `csv`, …) as the kind.

The moment you *use* the value — navigate into it, read a property, loop over it — PLang decodes it into the real type automatically.

```plang
Start
- get http://example.com/data.json into %data%   <-- arrives as binary/json
- write out %data.name%                           <-- decoded to dict on first use
```

You don't have to think about this. It just works.

## Subtopics

- [list/](list/start.md) — an ordered collection of values
- [dict/](dict/start.md) — a set of named values
