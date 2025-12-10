# Web Scraping with LLM in Plang

> This article explores Plang, an intent-based programming language designed to interpret natural language. For more information, visit [plang.is](https://plang.is).

The price of LLM processing is going down fast. In the time that Plang has been out (March 2024) and today (July 19th, 2024), it has gone down 20x.

That is just crazy in such a short time.

This opens up new opportunities.

One of which is easy web scraping.

Instead of a programmer needing to understand complex DOM elements to parse, almost anybody can now scrape a website and get it into a structured format (it's called a scheme in the code below).

## Basic Web Scraping Example

Here is one example:

Create `WebScraper.goal` in a folder of your choosing. Not you must have [Plang installed](https://github.com/PLangHQ/plang/blob/main/Documentation/Install.md) to build and run this code.

```plang
WebScraper
- open https://en.wikipedia.org/wiki/Iceland
- load all content 'body' into %content%
- close browser
- [llm]: system: Extract data from the user content, according to the scheme
    user: %content%
    model: 'gpt-4o-mini'
    scheme: {flagUrl: string, capital: string, language: string, totalArea: number}
    write out %result%
- write out '
    Flag: %result.flagUrl%
    Capital: %result.capital%
    Language: %result.language%
    Total Area: %result.totalArea%
    '
```

lets now build this code
```bash
plang build
```

and then we wan't to run it
```bash
plang Webscraper
```

That costs $0.01184 to run through the LLM. 

We can even take the cost further down if you have the knowledge.

## Optimizing Web Scraping

Let's change the second step (line 3):

```plang
- load all content '.ib-country vcard' into %content%
```

Making the code like this:

```plang
WebScraper
- open https://en.wikipedia.org/wiki/Iceland
- load all content '.ib-country vcard' into %content%
- close browser
- [llm]: system: Extract data from the user content, according to the scheme
    user: %content%
    model: 'gpt-4o-mini'
    scheme: {flagUrl: string, capital: string, language: string, totalArea: number}
    write out %result%
- write out '
    Flag: %result.flagUrl%
    Capital: %result.capital%
    Language: %result.language%
    Total Area: %result.totalArea%
    '
```

Build and run it
```plang
plang exec WebScraper
```

That cost is now $0.00036.

## Scraping table for rows of data

You can also get lists of data from the LLM. 

In this example we download all the content of a page, send that to the LLM and ask it to exstract only the rows from the first table after specific headline and only those rows that have date.

Notice we are not using a browser here, but using `get` instead. Browser is usefull when website is using javascript, but if it's not doing that, you can simply use the `get`

```plang
WebScraber2
- get https://afd.calpoly.edu/web/sample-tables, write to %result%
- [llm] system: extract the rows in the table that comes after the headline "Basic Data Table with Column Headings", only include rows with date
        user: %result%
        model: 'gpt-4o-mini'
        scheme: [{Description:string, Date:date, Location:string}]
        write to %llmResult%
- foreach %llmResult%, call PrintItem

PrintItem
- write out %item.Description% - %item.Date% - %item.Location%
```

Total cost of scraping: $0.00267. 

## Decision Time

This becomes a decision to make: should I pay more time for the developer, or should I just let the LLM handle this?

If you pay the developer, you can be 100% sure that the data is going to be what is extracted from the website.

If you pay the LLM, just like a human, it can make mistakes, so you can't be 100% sure, but it might be enough to be 99.99% sure (I am just guessing that percentage).

Another benefit with the LLM is that parsing the DOM is fragile. If the website changes just a bit, there is a decent chance it might fail completely. LLM will, in most cases, handle those changes just fine.

## Taking it to the next step
Now that you have that data structure, you can start to use it.

You can start [inserting them into database](https://github.com/PLangHQ/plang/blob/main/Documentation/blogs/Database.md)
You can send a [message to somebody](https://github.com/PLangHQ/plang/blob/main/Documentation/blogs/Messaging.md)

Or you can use LLM to make decitions on your behalf

### Scheme

In the code above, you saw some strange code:

```json
{flagUrl: string, capital: string, language: string, totalArea: string}
```

For a programmer, it looks like what is called JSON, and it defines the structure of data that you would like to be returned.

Structured data is great because we can save it into a database.

Let's make this a bit nicer and start going through this:

```json
{
    flagUrl: string, 
    capital: string, 
    language: string,
    totalArea: number
}
```

Now it is a bit easier to read.

If we look at the first `flagUrl: string`, `flagUrl` is the name of the item we want back, and `string` tells us it should be text. You could also say `flagUrl: text`; that would work fine.

The other items are all `string`, so the same applies, except for `totalArea: number`. We are asking the LLM to give us a number back.

The LLM will now structure that data for us in this format.

Next line is this:

```plang
   write to %result%
```

We can then access those items that we listed through the `%result%` variable, like this: `%result.flagUrl%`.

## Accessing Structured Data

The LLM structures the data according to the scheme, which can be accessed like this:

```plang
write out %result%
```

To access individual items:

```plang
%result.flagUrl%
%result.capital%
%result.language%
%result.totalArea%
```

## Conclusion

Using Plang and LLM for web scraping is cost-effective and efficient, especially for beginners. It abstracts the complexity of DOM parsing and provides a flexible approach to data extraction. Explore more about Plang and its capabilities to leverage its full potential.