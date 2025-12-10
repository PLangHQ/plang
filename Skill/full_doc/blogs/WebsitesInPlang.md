# Building Webpages with Plang

**Until version 0.1.15.7, Plang only supported JSON responses.** With the latest update, Plang can handle any content type, including HTML. This means you can now create fully functional websites using Plang.

## A Glimpse into the Future

The current approach to building websites in version 0.1.15.7 isn’t exactly what I had envisioned long-term, but it’s a solid starting point. My vision for Plang is much grander, but this version already lets you create complete, functional web applications. It's every bit as capable as other platforms for web development.


## Your First Web App in Plang

To get started, you'll need a working knowledge of Plang as well as HTML, CSS, and JavaScript. Let’s dive in and create your first web app.


### Setting Up the Server and Displaying the Front Page

First, we’ll create the server and set up a route to serve the front page. Start by creating a file named `Start.goal`:

```plang
Start
- start webserver
- add route '/', call Frontpage
```

Here’s what’s happening:
1. The web server is initialized.
2. We set up a route so that when someone visits the root (`/`) of the website, the server calls the `Frontpage` goal.

Next, define the `Frontpage` goal in a file called `Frontpage.goal`:

```plang
Frontpage
- set %message% = "Hello Plang world!"
- render template "frontpage.html", write to %content%
- write out %content%
```

Here’s how this works:
- The `%message%` variable is defined to demonstrate how you can pass data to your HTML template.
- Plang renders the `frontpage.html` template, storing the result in the `%content%` variable.
- Finally, the content is sent to the user’s browser.

### Creating the HTML Template

Now, let’s create the HTML file that will serve as the front page. Save it as `frontpage.html` in your project folder:

```html
<html>
    <body>This is an HTML page that will say: {{ message }}</body>
</html>
```

This template uses `{{ message }}` as a placeholder for the `%message%` variable defined earlier.


### Running Your Web App

With everything in place, start your server and navigate to [http://localhost:8080](http://localhost:8080) in your browser. You should see:

```
This is an HTML page that will say: Hello Plang world!
```


## Wrapping Up

Congratulations! You’ve just built your first web app in Plang. While this is only the beginning, it’s a strong foundation for building dynamic and fully functional web applications. Stay tuned for more updates as Plang evolves!